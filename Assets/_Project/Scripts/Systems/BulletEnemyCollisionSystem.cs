using MergeBullet.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace MergeBullet.Systems
{
    /// <summary>
    /// 총알이 적에 닿으면 적 표면에서 반사되고 적에게 피해를 준다.
    /// 1) 수집: 적 위치를 배열로 모으고 공간 해시에 넣는다 (병렬)
    /// 2) 반사: 총알마다 주변 9칸의 적과 겹침 검사 → 표면 밖으로 밀어내고 속도 반사, 피해 기록 (병렬)
    /// 3) 피해: 기록된 피해를 적 체력에 반영한다 (단일 스레드, 같은 적을 여러 총알이 맞혀도 안전)
    /// 체력이 0 이하가 된 적의 제거는 EnemyDeathSystem이 맡는다.
    /// </summary>
    [UpdateAfter(typeof(BulletMoveSystem))]
    [UpdateAfter(typeof(EnemyChaseSystem))]
    [UpdateBefore(typeof(BulletMergeSystem))]
    [BurstCompile]
    public partial struct BulletEnemyCollisionSystem : ISystem
    {
        EntityQuery _enemyQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _enemyQuery = SystemAPI.QueryBuilder().WithAll<Enemy, LocalTransform, EnemyHealth>().Build();
            state.RequireForUpdate(_enemyQuery);
            state.RequireForUpdate<BulletLevelSettings>();
            state.RequireForUpdate<EnemySpawner>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            BulletLevelSettings levels = SystemAPI.GetSingleton<BulletLevelSettings>();
            float enemyRadius = SystemAPI.GetComponent<Enemy>(SystemAPI.GetSingleton<EnemySpawner>().Prefab).Radius;

            // 셀 크기 ≥ 가장 큰 총알 반지름 + 적 반지름의 합 → 겹칠 수 있는 적은 항상 주변 9칸 안에 있다
            float cellSize = 2f * (enemyRadius + BulletLevelUtility.RadiusOf(levels.MaxLevel, levels));
            int count = _enemyQuery.CalculateEntityCount();

            var enemies = new NativeArray<Entity>(count, Allocator.TempJob);
            var positions = new NativeArray<float2>(count, Allocator.TempJob);
            var radii = new NativeArray<float>(count, Allocator.TempJob);
            var grid = new NativeParallelMultiHashMap<int, int>(count, Allocator.TempJob);
            var hits = new NativeQueue<EnemyHit>(Allocator.TempJob);

            JobHandle collect = new CollectEnemiesJob
            {
                CellSize = cellSize,
                Entities = enemies,
                Positions = positions,
                Radii = radii,
                Grid = grid.AsParallelWriter(),
            }.ScheduleParallel(_enemyQuery, state.Dependency);

            JobHandle bounce = new BulletEnemyBounceJob
            {
                CellSize = cellSize,
                Levels = levels,
                EnemyPositions = positions,
                EnemyRadii = radii,
                Grid = grid,
                Hits = hits.AsParallelWriter(),
            }.ScheduleParallel(collect);

            JobHandle damage = new ApplyBulletDamageJob
            {
                Enemies = enemies,
                Hits = hits,
                Health = SystemAPI.GetComponentLookup<EnemyHealth>(),
                Flashes = SystemAPI.GetComponentLookup<HitFlash>(),
            }.Schedule(bounce);

            enemies.Dispose(damage);
            positions.Dispose(damage);
            radii.Dispose(damage);
            grid.Dispose(damage);
            hits.Dispose(damage);

            state.Dependency = damage;
        }
    }

    struct EnemyHit
    {
        public int EnemyIndex;
        public float Damage;
    }

    [BurstCompile]
    partial struct CollectEnemiesJob : IJobEntity
    {
        public float CellSize;

        // 각 엔티티는 자기 인덱스에만 쓰므로 병렬 쓰기가 안전하다
        [NativeDisableParallelForRestriction] public NativeArray<Entity> Entities;
        [NativeDisableParallelForRestriction] public NativeArray<float2> Positions;
        [NativeDisableParallelForRestriction] public NativeArray<float> Radii;
        public NativeParallelMultiHashMap<int, int>.ParallelWriter Grid;

        void Execute(Entity entity, [EntityIndexInQuery] int index, in LocalTransform transform, in Enemy enemy)
        {
            float2 position = transform.Position.xy;
            Entities[index] = entity;
            Positions[index] = position;
            Radii[index] = enemy.Radius;
            Grid.Add(SpatialHash.KeyOf(SpatialHash.CellOf(position, CellSize)), index);
        }
    }

    [BurstCompile]
    partial struct BulletEnemyBounceJob : IJobEntity
    {
        public float CellSize;
        public BulletLevelSettings Levels;
        [ReadOnly] public NativeArray<float2> EnemyPositions;
        [ReadOnly] public NativeArray<float> EnemyRadii;
        [ReadOnly] public NativeParallelMultiHashMap<int, int> Grid;
        public NativeQueue<EnemyHit>.ParallelWriter Hits;

        void Execute(ref LocalTransform transform, ref BulletVelocity velocity, in Bullet bullet)
        {
            float2 position = transform.Position.xy;
            int2 cell = SpatialHash.CellOf(position, CellSize);

            // 가장 깊이 겹친(가장 가까운) 적 하나와만 충돌 처리한다
            int hit = -1;
            float bestDistSq = float.MaxValue;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int key = SpatialHash.KeyOf(cell + new int2(dx, dy));
                if (!Grid.TryGetFirstValue(key, out int j, out var it)) continue;
                do
                {
                    float distSq = math.distancesq(position, EnemyPositions[j]);
                    float touch = bullet.Radius + EnemyRadii[j];
                    if (distSq < touch * touch && distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        hit = j;
                    }
                } while (Grid.TryGetNextValue(out j, ref it));
            }
            if (hit < 0) return;

            // 적 중심 → 총알 방향이 충돌면의 법선. 중심이 겹치면 진행 반대 방향을 법선으로 쓴다
            float2 v = velocity.Value;
            float2 normal = math.normalizesafe(position - EnemyPositions[hit], -math.normalizesafe(v));

            // 다음 프레임에 같은 적과 또 겹치지 않게 표면 밖으로 밀어낸다
            position = EnemyPositions[hit] + normal * (bullet.Radius + EnemyRadii[hit]);
            transform.Position = new float3(position, transform.Position.z);

            // 적 쪽으로 날아가던 경우에만 반사 + 피해 (적이 다가와 겹친 경우는 밀어내기만)
            float approach = math.dot(v, normal);
            if (approach >= 0f) return;

            velocity.Value = v - 2f * approach * normal;
            Hits.Enqueue(new EnemyHit { EnemyIndex = hit, Damage = BulletLevelUtility.DamageOf(bullet.Level, Levels) });
        }
    }

    [BurstCompile]
    struct ApplyBulletDamageJob : IJob
    {
        [ReadOnly] public NativeArray<Entity> Enemies;
        public NativeQueue<EnemyHit> Hits;
        public ComponentLookup<EnemyHealth> Health;
        public ComponentLookup<HitFlash> Flashes;

        public void Execute()
        {
            while (Hits.TryDequeue(out EnemyHit hit))
            {
                RefRW<EnemyHealth> health = Health.GetRefRW(Enemies[hit.EnemyIndex]);
                health.ValueRW.Value -= hit.Damage;

                // 맞을 때마다 번쩍임을 처음부터 다시 시작한다
                RefRW<HitFlash> flash = Flashes.GetRefRW(Enemies[hit.EnemyIndex]);
                flash.ValueRW.Timer = flash.ValueRO.Duration;
            }
        }
    }
}
