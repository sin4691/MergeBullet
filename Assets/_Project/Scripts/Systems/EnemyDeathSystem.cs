using MergeBullet.Components;
using Unity.Burst;
using Unity.Entities;

namespace MergeBullet.Systems
{
    /// <summary>
    /// 체력이 0 이하가 된 적을 제거한다.
    /// 피해를 주는 곳(총알, 폭발)이 여럿이어도 제거는 여기 한 곳에서만 해서, 같은 프레임에
    /// 같은 적이 두 번 삭제 명령을 받는 일이 없다.
    /// </summary>
    [UpdateAfter(typeof(BulletEnemyCollisionSystem))]
    [BurstCompile]
    public partial struct EnemyDeathSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EnemyHealth>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new EnemyDeathJob
            {
                Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
            }.ScheduleParallel();
        }
    }

    [BurstCompile]
    partial struct EnemyDeathJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter Ecb;

        void Execute(Entity entity, [ChunkIndexInQuery] int sortKey, in EnemyHealth health)
        {
            if (health.Value <= 0f)
                Ecb.DestroyEntity(sortKey, entity);
        }
    }
}
