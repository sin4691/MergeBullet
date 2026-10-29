using MergeBullet.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace MergeBullet.Systems
{
    /// <summary>
    /// 최대 레벨에 도달한 총알을 특수효과로 바꾼다: 그 자리에 효과 엔티티를 만들고 총알은 제거한다.
    /// 합성 결과는 프레임 끝에 반영되므로, 최대 레벨 총알은 한 프레임 보인 뒤 다음 프레임에 터진다.
    /// </summary>
    [UpdateAfter(typeof(BulletMergeSystem))]
    [BurstCompile]
    public partial struct MaxLevelBulletSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MaxLevelEffect>();
            state.RequireForUpdate<BulletLevelSettings>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new TriggerMaxLevelEffectJob
            {
                MaxLevel = SystemAPI.GetSingleton<BulletLevelSettings>().MaxLevel,
                Effect = SystemAPI.GetSingleton<MaxLevelEffect>(),
                Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
            }.ScheduleParallel();
        }
    }

    [BurstCompile]
    partial struct TriggerMaxLevelEffectJob : IJobEntity
    {
        public int MaxLevel;
        public MaxLevelEffect Effect;
        public EntityCommandBuffer.ParallelWriter Ecb;

        // sortKey: 병렬로 기록된 명령을 재생할 때 순서를 결정적으로 만들기 위한 키
        void Execute(Entity entity, [ChunkIndexInQuery] int sortKey, in Bullet bullet, in LocalTransform transform)
        {
            if (bullet.Level < MaxLevel) return;

            Entity effect = Ecb.Instantiate(sortKey, Effect.Prefab);
            Ecb.SetComponent(sortKey, effect, LocalTransform.FromPositionRotationScale(
                transform.Position, quaternion.identity, 0f));
            Ecb.SetComponent(sortKey, effect, new Explosion
            {
                Duration = Effect.Duration,
                MaxRadius = Effect.Radius,
                Damage = Effect.Damage,
            });
            Ecb.DestroyEntity(sortKey, entity);
        }
    }
}
