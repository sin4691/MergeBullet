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
    /// 폭발 생성·총알 제거는 그다음 프레임 시작에 재생된다.
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
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 생성은 다음 프레임 시작(BeginSimulation)에 재생한다. 프레임 끝(EndSimulation)에 만들면 이번 프레임의
            // 위치 계산(TransformSystemGroup)이 이미 끝난 뒤라, 새 엔티티가 한 프레임 동안 프리팹 위치(원점)에 그려진다.
            new TriggerMaxLevelEffectJob
            {
                MaxLevel = SystemAPI.GetSingleton<BulletLevelSettings>().MaxLevel,
                Effect = SystemAPI.GetSingleton<MaxLevelEffect>(),
                Ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
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
