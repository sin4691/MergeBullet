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
    /// 경험치 젬을 플레이어에게 끌어당기고, 닿으면 주워서 경험치를 올린다.
    /// 1) 끌기·줍기: 젬마다 자석 범위면 플레이어 쪽으로 이동, 줍기 범위면 값을 기록하고 삭제 예약 (병렬)
    /// 2) 경험치 반영: 기록된 값을 합쳐 PlayerProgress에 더한다 (단일 스레드, 경험치 하나에 여러 젬이 쓰는 충돌 방지)
    /// </summary>
    [UpdateAfter(typeof(PlayerMoveSystem))]
    [BurstCompile]
    public partial struct GemCollectSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<XpGem>();
            state.RequireForUpdate<GemCollector>();
            state.RequireForUpdate<PlayerProgress>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var collected = new NativeQueue<int>(Allocator.TempJob);

            JobHandle attract = new GemAttractJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                PlayerEntity = SystemAPI.GetSingletonEntity<GemCollector>(),
                Collectors = SystemAPI.GetComponentLookup<GemCollector>(true),
                PlayerPositions = SystemAPI.GetComponentLookup<PlayerPosition>(true),
                Collected = collected.AsParallelWriter(),
                Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
            }.ScheduleParallel(state.Dependency);

            JobHandle add = new AddExperienceJob
            {
                Collected = collected,
                ProgressEntity = SystemAPI.GetSingletonEntity<PlayerProgress>(),
                Progress = SystemAPI.GetComponentLookup<PlayerProgress>(),
            }.Schedule(attract);

            collected.Dispose(add);
            state.Dependency = add;
        }
    }

    [BurstCompile]
    partial struct GemAttractJob : IJobEntity
    {
        public float DeltaTime;
        public Entity PlayerEntity;
        [ReadOnly] public ComponentLookup<GemCollector> Collectors;
        [ReadOnly] public ComponentLookup<PlayerPosition> PlayerPositions;
        public NativeQueue<int>.ParallelWriter Collected;
        public EntityCommandBuffer.ParallelWriter Ecb;

        void Execute(Entity entity, [ChunkIndexInQuery] int sortKey, ref LocalTransform transform, ref XpGem gem)
        {
            GemCollector collector = Collectors[PlayerEntity];
            float2 toPlayer = PlayerPositions[PlayerEntity].Value - transform.Position.xy;
            float distanceSq = math.lengthsq(toPlayer);

            if (!gem.Attracted && distanceSq > collector.MagnetRadius * collector.MagnetRadius) return;
            gem.Attracted = true;

            if (distanceSq <= collector.PickupRadius * collector.PickupRadius)
            {
                Collected.Enqueue(gem.Value);
                Ecb.DestroyEntity(sortKey, entity);
                return;
            }

            float distance = math.sqrt(distanceSq);
            float step = math.min(collector.PullSpeed * DeltaTime, distance);
            transform.Position += new float3(toPlayer / distance * step, 0f);
        }
    }

    [BurstCompile]
    struct AddExperienceJob : IJob
    {
        public NativeQueue<int> Collected;
        public Entity ProgressEntity;
        public ComponentLookup<PlayerProgress> Progress;

        public void Execute()
        {
            int total = 0;
            while (Collected.TryDequeue(out int value)) total += value;
            if (total == 0) return;

            Progress.GetRefRW(ProgressEntity).ValueRW.Experience += total;
        }
    }
}
