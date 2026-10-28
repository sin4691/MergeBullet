using MergeBullet.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace MergeBullet.Systems
{
    /// <summary>
    /// 레벨 변화 없이 오래 남은 총알을 소멸시킨다. 소멸 직전 WarningTime 동안은 점점 작아진다.
    /// 합성보다 먼저 돌면서 만료된 총알에 BulletExpired를 켜 두면, 같은 프레임의 합성 대상에서 빠진다.
    /// (합성 ECB와 소멸 ECB가 같은 총알을 동시에 건드리는 충돌 방지)
    /// </summary>
    [UpdateAfter(typeof(BulletMoveSystem))]
    [UpdateBefore(typeof(BulletMergeSystem))]
    [BurstCompile]
    public partial struct BulletIdleSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BulletIdleSettings>();
            state.RequireForUpdate<BulletLevelSettings>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new BulletIdleJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                MaxLevel = SystemAPI.GetSingleton<BulletLevelSettings>().MaxLevel,
                Settings = SystemAPI.GetSingleton<BulletIdleSettings>(),
                Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
            }.ScheduleParallel();
        }
    }

    // EnabledRefRW<T> 인자만 쓰면 쿼리가 "T가 켜진 엔티티"로 잡혀서 아무것도 돌지 않는다.
    // 아직 만료되지 않은(꺼진) 총알을 대상으로 하도록 명시한다.
    [WithDisabled(typeof(BulletExpired))]
    [BurstCompile]
    partial struct BulletIdleJob : IJobEntity
    {
        public float DeltaTime;
        public int MaxLevel;
        public BulletIdleSettings Settings;
        public EntityCommandBuffer.ParallelWriter Ecb;

        void Execute(Entity entity, [ChunkIndexInQuery] int sortKey, ref BulletIdle idle, ref LocalTransform transform,
            EnabledRefRW<BulletExpired> expired, in Bullet bullet)
        {
            if (bullet.Level >= MaxLevel) return; // 최대 레벨은 MaxLevelBulletSystem이 처리한다

            // 합성으로 레벨이 올랐으면 타이머를 처음부터 다시 잰다
            if (bullet.Level != idle.LastLevel)
            {
                idle.LastLevel = bullet.Level;
                idle.Time = 0f;
                return;
            }

            idle.Time += DeltaTime;
            float remaining = Settings.Lifetime - idle.Time;

            if (remaining <= 0f)
            {
                expired.ValueRW = true;
                Ecb.DestroyEntity(sortKey, entity);
                return;
            }

            // 경고 구간: 남은 시간에 비례해 작아진다 (충돌 반지름은 그대로)
            if (remaining < Settings.WarningTime)
                transform.Scale = bullet.Radius * 2f * (remaining / Settings.WarningTime);
        }
    }
}
