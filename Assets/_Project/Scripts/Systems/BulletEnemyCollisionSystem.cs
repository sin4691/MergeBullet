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
            // 적 종류마다 크기가 달라서, 가장 큰 적 기준으로 칸 크기를 정해야 충돌을 놓치지 않는다
            float enemyRadius = SystemAPI.GetSingleton<EnemySpawner>().MaxEnemyRadius;

            // 셀 크기 ≥ 가장 큰 총알 반지름 + 적 반지름의 합 → 겹칠 수 있는 적은 항상 주변 9칸 안에 있다
            float cellSize = 2f * (enemyRadius + BulletLevelUtility.RadiusOf(levels.MaxLevel, levels));
            int count = _enemyQuery.CalculateEntityCount();

            var enemies = new NativeArray<Entity>(count, Allocator.TempJob);
            var positions = new NativeArray<float2>(count, Allocator.TempJob);
            var radii = new NativeArray<float>(count, Allocator.TempJob);
            var axes = new NativeArray<float2>(count, Allocator.TempJob);
            var grid = new NativeParallelMultiHashMap<int, int>(count, Allocator.TempJob);
            var hits = new NativeQueue<EnemyHit>(Allocator.TempJob);

            JobHandle collect = new CollectEnemiesJob
            {
                CellSize = cellSize,
                Entities = enemies,
                Positions = positions,
                Radii = radii,
                Axes = axes,
                Grid = grid.AsParallelWriter(),
            }.ScheduleParallel(_enemyQuery, state.Dependency);

            JobHandle bounce = new BulletEnemyBounceJob
            {
                CellSize = cellSize,
                Levels = levels,
                EnemyPositions = positions,
                EnemyRadii = radii,
                EnemyAxes = axes,
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
            axes.Dispose(damage);
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
        // 적이 바라보는 방향(로컬 x축)의 단위 벡터. 회전한 상자 판정에 쓴다
        [NativeDisableParallelForRestriction] public NativeArray<float2> Axes;
        public NativeParallelMultiHashMap<int, int>.ParallelWriter Grid;

        void Execute(Entity entity, [EntityIndexInQuery] int index, in LocalTransform transform, in Enemy enemy)
        {
            float2 position = transform.Position.xy;
            Entities[index] = entity;
            Positions[index] = position;
            Radii[index] = enemy.Radius;
            Axes[index] = math.normalizesafe(math.mul(transform.Rotation, new float3(1f, 0f, 0f)).xy, new float2(1f, 0f));
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
        [ReadOnly] public NativeArray<float2> EnemyAxes;
        [ReadOnly] public NativeParallelMultiHashMap<int, int> Grid;
        public NativeQueue<EnemyHit>.ParallelWriter Hits;

        void Execute(ref LocalTransform transform, ref BulletVelocity velocity, in Bullet bullet)
        {
            float2 position = transform.Position.xy;
            int2 cell = SpatialHash.CellOf(position, CellSize);

            // 적은 반 변 = Radius인 축 정렬 정사각형. 총알(원)과 상자의 가장 가까운 점 거리로 겹침을 본다.
            // 가장 깊이 겹친(표면까지 가장 가까운) 적 하나와만 충돌 처리한다
            int hit = -1;
            float bestDistSq = float.MaxValue;
            float radiusSq = bullet.Radius * bullet.Radius;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int key = SpatialHash.KeyOf(cell + new int2(dx, dy));
                if (!Grid.TryGetFirstValue(key, out int j, out var it)) continue;
                do
                {
                    float2 relative = ToLocal(position - EnemyPositions[j], EnemyAxes[j]);
                    float2 closest = math.clamp(relative, -EnemyRadii[j], EnemyRadii[j]);
                    float distSq = math.distancesq(relative, closest);
                    if (distSq < radiusSq && distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        hit = j;
                    }
                } while (Grid.TryGetNextValue(out j, ref it));
            }
            if (hit < 0) return;

            // 적이 회전해 있으므로 총알 위치를 적 기준(로컬) 좌표로 돌려서 축 정렬 상자처럼 계산하고,
            // 구한 법선·위치를 다시 월드 좌표로 돌린다
            float2 v = velocity.Value;
            float2 center = EnemyPositions[hit];
            float half = EnemyRadii[hit];
            float2 axis = EnemyAxes[hit];
            float2 rel = ToLocal(position - center, axis);
            float2 nearest = math.clamp(rel, -half, half);
            float2 normal;
            if (math.distancesq(rel, nearest) > 1e-8f)
            {
                // 면에 닿으면 법선이 축 방향(벽처럼 반듯한 반사), 모서리에 닿으면 모서리 → 총알 방향
                normal = math.normalize(rel - nearest);
                rel = nearest + normal * bullet.Radius;
            }
            else
            {
                // 총알 중심이 상자 안까지 들어왔으면 덜 파고든 축 쪽 면으로 꺼낸다
                float2 depth = half - math.abs(rel);
                normal = depth.x < depth.y
                    ? new float2(rel.x >= 0f ? 1f : -1f, 0f)
                    : new float2(0f, rel.y >= 0f ? 1f : -1f);
                if (depth.x < depth.y) rel.x = normal.x * (half + bullet.Radius);
                else rel.y = normal.y * (half + bullet.Radius);
            }
            normal = ToWorld(normal, axis);
            position = center + ToWorld(rel, axis);

            // 다음 프레임에 같은 적과 또 겹치지 않게 표면 밖으로 밀어낸 위치
            transform.Position = new float3(position, transform.Position.z);

            // 적 쪽으로 날아가던 경우에만 반사 + 피해 (적이 다가와 겹친 경우는 밀어내기만)
            float approach = math.dot(v, normal);
            if (approach >= 0f) return;

            velocity.Value = v - 2f * approach * normal;
            Hits.Enqueue(new EnemyHit { EnemyIndex = hit, Damage = BulletLevelUtility.DamageOf(bullet.Level, Levels) });
        }

        // axis = 적의 로컬 x축(바라보는 방향). 로컬 y축은 그 왼쪽 수직
        static float2 ToLocal(float2 world, float2 axis) => new float2(math.dot(world, axis), world.y * axis.x - world.x * axis.y);
        static float2 ToWorld(float2 local, float2 axis) => local.x * axis + local.y * new float2(-axis.y, axis.x);
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
