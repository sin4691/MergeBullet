using MergeBullet.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace MergeBullet.Systems
{
    /// <summary>
    /// 같은 레벨 총알 2개가 겹치면 다음 레벨 1개로 합친다.
    /// 1) 수집: 총알 데이터를 배열로 모으고 공간 해시에 넣는다 (병렬)
    /// 2) 탐색: 각 총알이 주변 9칸에서 가장 가까운 합성 상대를 찾는다 (병렬)
    /// 3) 확정: 한 총알이 두 번 합성되지 않게 순서대로 짝을 확정하고 ECB에 기록한다 (단일 스레드, 결정적)
    /// 메인 스레드는 잡을 예약만 하고 기다리지 않는다.
    /// </summary>
    [UpdateAfter(typeof(BulletMoveSystem))]
    [BurstCompile]
    public partial struct BulletMergeSystem : ISystem
    {
        EntityQuery _bulletQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // 방치 소멸 예정(BulletExpired 켜짐)인 총알은 합성하지 않는다
            _bulletQuery = SystemAPI.QueryBuilder()
                .WithAll<Bullet, BulletVelocity, LocalTransform>()
                .WithDisabled<BulletExpired>()
                .Build();
            state.RequireForUpdate(_bulletQuery);
            state.RequireForUpdate<BulletLevelSettings>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<EffectQueue>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            BulletLevelSettings settings = SystemAPI.GetSingleton<BulletLevelSettings>();
            // 정확한 개수(CalculateEntityCount)는 enableable 필터 때문에 BulletIdleSystem 잡이 끝나길
            // 메인 스레드에서 기다려야 한다. 대신 필터 전 개수로 넉넉히 잡고, 남는 칸은 Level 0인 빈 슬롯으로 둔다.
            int count = _bulletQuery.CalculateEntityCountWithoutFiltering();

            // 가장 큰 총알 지름 = 셀 크기 → 겹칠 수 있는 상대는 항상 주변 9칸 안에 있다
            float cellSize = 2f * BulletLevelUtility.RadiusOf(settings.MaxLevel, settings);

            var entities = new NativeArray<Entity>(count, Allocator.TempJob);
            var positions = new NativeArray<float2>(count, Allocator.TempJob);
            var velocities = new NativeArray<float2>(count, Allocator.TempJob);
            var bullets = new NativeArray<Bullet>(count, Allocator.TempJob);
            var partners = new NativeArray<int>(count, Allocator.TempJob);
            var grid = new NativeParallelMultiHashMap<int, int>(count, Allocator.TempJob);

            JobHandle collect = new CollectBulletsJob
            {
                CellSize = cellSize,
                Entities = entities,
                Positions = positions,
                Velocities = velocities,
                Bullets = bullets,
                Grid = grid.AsParallelWriter(),
            }.ScheduleParallel(_bulletQuery, state.Dependency);

            JobHandle find = new FindMergePartnerJob
            {
                CellSize = cellSize,
                MaxLevel = settings.MaxLevel,
                Positions = positions,
                Bullets = bullets,
                Grid = grid,
                Partners = partners,
            }.Schedule(count, 64, collect);

            JobHandle resolve = new ResolveMergesJob
            {
                Settings = settings,
                Entities = entities,
                Positions = positions,
                Velocities = velocities,
                Bullets = bullets,
                Partners = partners,
                Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged),
                EffectQueueEntity = SystemAPI.GetSingletonEntity<EffectQueue>(),
            }.Schedule(find);

            // 잡이 끝난 뒤 해제되도록 의존성에 묶는다
            entities.Dispose(resolve);
            positions.Dispose(resolve);
            velocities.Dispose(resolve);
            bullets.Dispose(resolve);
            partners.Dispose(resolve);
            grid.Dispose(resolve);

            state.Dependency = resolve;
        }
    }

    [BurstCompile]
    partial struct CollectBulletsJob : IJobEntity
    {
        public float CellSize;

        // 각 엔티티는 자기 인덱스에만 쓰므로 병렬 쓰기가 안전하다
        [NativeDisableParallelForRestriction] public NativeArray<Entity> Entities;
        [NativeDisableParallelForRestriction] public NativeArray<float2> Positions;
        [NativeDisableParallelForRestriction] public NativeArray<float2> Velocities;
        [NativeDisableParallelForRestriction] public NativeArray<Bullet> Bullets;
        public NativeParallelMultiHashMap<int, int>.ParallelWriter Grid;

        void Execute(Entity entity, [EntityIndexInQuery] int index,
            in LocalTransform transform, in BulletVelocity velocity, in Bullet bullet)
        {
            float2 position = transform.Position.xy;
            Entities[index] = entity;
            Positions[index] = position;
            Velocities[index] = velocity.Value;
            Bullets[index] = bullet;
            Grid.Add(SpatialHash.KeyOf(SpatialHash.CellOf(position, CellSize)), index);
        }
    }

    [BurstCompile]
    struct FindMergePartnerJob : IJobParallelFor
    {
        public float CellSize;
        public int MaxLevel;
        [ReadOnly] public NativeArray<float2> Positions;
        [ReadOnly] public NativeArray<Bullet> Bullets;
        [ReadOnly] public NativeParallelMultiHashMap<int, int> Grid;

        /// <summary>i의 합성 상대 인덱스. 없으면 -1.</summary>
        public NativeArray<int> Partners;

        public void Execute(int i)
        {
            Partners[i] = -1;
            Bullet self = Bullets[i];
            if (self.Level == 0) return; // 빈 슬롯 (필터에 걸려 수집되지 않은 칸)
            if (self.Level >= MaxLevel) return; // 최대 레벨은 더 합성하지 않는다

            float2 position = Positions[i];
            int2 cell = SpatialHash.CellOf(position, CellSize);
            float bestDistSq = float.MaxValue;

            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int key = SpatialHash.KeyOf(cell + new int2(dx, dy));
                if (!Grid.TryGetFirstValue(key, out int j, out var it)) continue;
                do
                {
                    if (j == i || Bullets[j].Level != self.Level) continue;

                    float distSq = math.distancesq(position, Positions[j]);
                    float touch = self.Radius + Bullets[j].Radius;
                    if (distSq < touch * touch && distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        Partners[i] = j;
                    }
                } while (Grid.TryGetNextValue(out j, ref it));
            }
        }
    }

    [BurstCompile]
    struct ResolveMergesJob : IJob
    {
        public BulletLevelSettings Settings;
        [ReadOnly] public NativeArray<Entity> Entities;
        [ReadOnly] public NativeArray<float2> Positions;
        [ReadOnly] public NativeArray<float2> Velocities;
        [ReadOnly] public NativeArray<Bullet> Bullets;
        [ReadOnly] public NativeArray<int> Partners;
        public EntityCommandBuffer Ecb;
        public Entity EffectQueueEntity;

        public void Execute()
        {
            // 탐색 단계에선 A→B, C→B처럼 한 총알이 여러 짝에 걸릴 수 있다.
            // 인덱스 순서대로 먼저 온 짝만 확정하고, 밀린 총알은 다음 프레임에 다시 시도한다.
            var consumed = new NativeArray<bool>(Entities.Length, Allocator.Temp);

            for (int i = 0; i < Entities.Length; i++)
            {
                int j = Partners[i];
                if (j < 0 || consumed[i] || consumed[j]) continue;
                consumed[i] = true;
                consumed[j] = true;

                int level = Bullets[i].Level + 1;
                float radius = BulletLevelUtility.RadiusOf(level, Settings);
                float2 position = (Positions[i] + Positions[j]) * 0.5f;

                // 두 진행 방향의 평균으로 날아간다. 정면충돌로 상쇄되면 i의 진행 방향에 수직으로 튕겨 나간다
                float2 vi = Velocities[i];
                float2 direction = vi + Velocities[j];
                if (math.lengthsq(direction) < 1e-6f) direction = new float2(-vi.y, vi.x);
                float2 velocity = math.normalizesafe(direction) * math.length(vi);

                Entity survivor = Entities[i];
                Ecb.SetComponent(survivor, LocalTransform.FromPositionRotationScale(
                    new float3(position, 0f), quaternion.identity, radius * 2f));
                Ecb.SetComponent(survivor, new Bullet { Level = level, Radius = radius });
                Ecb.SetComponent(survivor, new BulletVelocity { Value = velocity });
                Ecb.SetComponent(survivor, new URPMaterialPropertyBaseColor
                {
                    Value = BulletLevelUtility.ColorOf(level, Settings),
                });
                Ecb.DestroyEntity(Entities[j]);

                // 연출은 이벤트만 남기고, 재생은 EffectPresenter가 한다
                Ecb.AppendToBuffer(EffectQueueEntity, new EffectEvent
                {
                    Type = EffectType.Merge,
                    Position = position,
                    Size = radius,
                    Color = BulletLevelUtility.ColorOf(level, Settings),
                });
            }
        }
    }
}
