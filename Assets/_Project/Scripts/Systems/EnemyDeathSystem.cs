using MergeBullet.Components;
using Unity.Burst;
using Unity.Entities;

namespace MergeBullet.Systems
{
    /// <summary>
    /// 체력이 0 이하가 된 적을 제거하고 처치 수를 센다.
    /// 피해를 주는 곳(총알, 폭발)이 여럿이어도 제거는 여기 한 곳에서만 해서, 같은 프레임에
    /// 같은 적이 두 번 삭제 명령을 받는 일이 없다.
    /// 처치 수 하나를 여러 스레드가 동시에 올리면 충돌하므로 단일 스레드로 돈다 (적은 수백 단위라 충분).
    /// </summary>
    [UpdateAfter(typeof(BulletEnemyCollisionSystem))]
    [BurstCompile]
    public partial struct EnemyDeathSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EnemyHealth>();
            state.RequireForUpdate<GameStats>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new EnemyDeathJob
            {
                Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged),
                StatsEntity = SystemAPI.GetSingletonEntity<GameStats>(),
                Stats = SystemAPI.GetComponentLookup<GameStats>(),
            }.Schedule();
        }
    }

    [BurstCompile]
    partial struct EnemyDeathJob : IJobEntity
    {
        public EntityCommandBuffer Ecb;
        public Entity StatsEntity;
        public ComponentLookup<GameStats> Stats;

        void Execute(Entity entity, in EnemyHealth health)
        {
            if (health.Value > 0f) return;

            Ecb.DestroyEntity(entity);
            Stats.GetRefRW(StatsEntity).ValueRW.Kills++;
        }
    }
}
