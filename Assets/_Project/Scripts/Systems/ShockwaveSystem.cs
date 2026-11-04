using MergeBullet.Components;
using Unity.Burst;
using Unity.Entities;

namespace MergeBullet.Systems
{
    /// <summary>보스 충격파 고리를 퍼뜨리고, 최대 반경까지 가면 지운다. 플레이어 피해는 PlayerDamageSystem이 같은 경로로 처리한다.</summary>
    [UpdateBefore(typeof(PlayerDamageSystem))]
    [BurstCompile]
    public partial struct ShockwaveSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Shockwave>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new SpreadShockwaveJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged),
            }.Schedule();
        }
    }

    [BurstCompile]
    partial struct SpreadShockwaveJob : IJobEntity
    {
        public float DeltaTime;
        public EntityCommandBuffer Ecb;

        void Execute(Entity entity, ref Shockwave wave)
        {
            wave.Radius += wave.Speed * DeltaTime;
            if (wave.Radius >= wave.MaxRadius) Ecb.DestroyEntity(entity);
        }
    }
}
