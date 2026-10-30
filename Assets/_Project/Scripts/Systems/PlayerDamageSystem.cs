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
    /// 적에게 닿으면 플레이어가 한 대 맞고, 무적 시간 동안은 다시 맞지 않는다. 체력이 0이 되면 게임 오버로 표시한다.
    /// 1) 탐색: 플레이어에 닿은 적 중 가장 센 접촉 피해를 찾는다 (단일 스레드, 플레이어 하나에 여러 적이 쓰는 충돌을 피함)
    /// 2) 반영: 무적 시간이 끝났으면 그 피해를 한 번 적용하고 무적 시간을 다시 건다
    /// </summary>
    [UpdateAfter(typeof(EnemyChaseSystem))]
    [BurstCompile]
    public partial struct PlayerDamageSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerHealth>();
            state.RequireForUpdate<GameState>();
            state.RequireForUpdate<EffectQueue>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 벤치마크 중에는 플레이어가 죽지 않아야 같은 부하로 끝까지 잴 수 있다
            if (SystemAPI.HasSingleton<BenchmarkSettings>()) return;

            Entity player = SystemAPI.GetSingletonEntity<PlayerHealth>();
            var strongestHit = new NativeReference<float>(0f, Allocator.TempJob);

            JobHandle find = new FindContactHitJob
            {
                PlayerEntity = player,
                Players = SystemAPI.GetComponentLookup<Player>(true),
                PlayerPositions = SystemAPI.GetComponentLookup<PlayerPosition>(true),
                StrongestHit = strongestHit,
            }.Schedule(state.Dependency);

            JobHandle apply = new ApplyPlayerDamageJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                StrongestHit = strongestHit,
                PlayerEntity = player,
                GameStateEntity = SystemAPI.GetSingletonEntity<GameState>(),
                Health = SystemAPI.GetComponentLookup<PlayerHealth>(),
                GameStates = SystemAPI.GetComponentLookup<GameState>(),
                PlayerPositions = SystemAPI.GetComponentLookup<PlayerPosition>(true),
                Players = SystemAPI.GetComponentLookup<Player>(true),
                EffectQueueEntity = SystemAPI.GetSingletonEntity<EffectQueue>(),
                Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged),
            }.Schedule(find);

            strongestHit.Dispose(apply);
            state.Dependency = apply;
        }
    }

    [BurstCompile]
    partial struct FindContactHitJob : IJobEntity
    {
        // 추적 시스템이 적을 딱 접촉 거리에 세우므로 부동소수 오차만큼 여유를 둔다
        const float ContactSlack = 0.01f;

        public Entity PlayerEntity;
        [ReadOnly] public ComponentLookup<Player> Players;
        [ReadOnly] public ComponentLookup<PlayerPosition> PlayerPositions;
        public NativeReference<float> StrongestHit;

        void Execute(in LocalTransform transform, in Enemy enemy)
        {
            float reach = enemy.Radius + Players[PlayerEntity].Radius + ContactSlack;
            if (math.distancesq(transform.Position.xy, PlayerPositions[PlayerEntity].Value) > reach * reach) return;

            // 여러 적에게 동시에 닿아도 한 대만 맞는다 (가장 센 피해 기준)
            StrongestHit.Value = math.max(StrongestHit.Value, enemy.ContactDamage);
        }
    }

    [BurstCompile]
    struct ApplyPlayerDamageJob : IJob
    {
        public float DeltaTime;
        [ReadOnly] public NativeReference<float> StrongestHit;
        public Entity PlayerEntity;
        public Entity GameStateEntity;
        public ComponentLookup<PlayerHealth> Health;
        public ComponentLookup<GameState> GameStates;
        [ReadOnly] public ComponentLookup<PlayerPosition> PlayerPositions;
        [ReadOnly] public ComponentLookup<Player> Players;
        public Entity EffectQueueEntity;
        public EntityCommandBuffer Ecb;

        public void Execute()
        {
            RefRW<PlayerHealth> health = Health.GetRefRW(PlayerEntity);
            health.ValueRW.InvulnerableTimer = math.max(health.ValueRO.InvulnerableTimer - DeltaTime, 0f);

            if (StrongestHit.Value <= 0f || health.ValueRO.InvulnerableTimer > 0f) return;

            health.ValueRW.Value = math.max(health.ValueRO.Value - StrongestHit.Value, 0f);
            health.ValueRW.InvulnerableTimer = health.ValueRO.InvulnerableDuration;

            Ecb.AppendToBuffer(EffectQueueEntity, new EffectEvent
            {
                Type = EffectType.PlayerHit,
                Position = PlayerPositions[PlayerEntity].Value,
                Size = Players[PlayerEntity].Radius,
                Color = new float4(1f, 0.25f, 0.25f, 1f),
            });

            if (health.ValueRO.Value <= 0f)
                GameStates.GetRefRW(GameStateEntity).ValueRW.IsGameOver = true;
        }
    }
}
