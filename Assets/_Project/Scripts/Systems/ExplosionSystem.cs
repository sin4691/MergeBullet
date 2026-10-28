using MergeBullet.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace MergeBullet.Systems
{
    /// <summary>
    /// 폭발 효과를 재생한다: 수명 동안 0 → 최대 크기 → 0으로 부풀었다 줄어들고, 수명이 끝나면 제거된다.
    /// 지금은 연출만 있고, 적이 생기면 여기서 범위 피해를 준다.
    /// </summary>
    [BurstCompile]
    public partial struct ExplosionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Explosion>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new AnimateExplosionJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
            }.ScheduleParallel();
        }
    }

    [BurstCompile]
    partial struct AnimateExplosionJob : IJobEntity
    {
        public float DeltaTime;
        public EntityCommandBuffer.ParallelWriter Ecb;

        void Execute(Entity entity, [ChunkIndexInQuery] int sortKey, ref Explosion explosion, ref LocalTransform transform)
        {
            explosion.Age += DeltaTime;
            if (explosion.Age >= explosion.Duration)
            {
                Ecb.DestroyEntity(sortKey, entity);
                return;
            }

            // sin(0..π): 처음과 끝이 0, 중간이 1 → 팡 터졌다 사라지는 모양
            float t = explosion.Age / explosion.Duration;
            transform.Scale = explosion.MaxRadius * 2f * math.sin(math.PI * t);
        }
    }
}
