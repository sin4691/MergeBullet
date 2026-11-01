using MergeBullet.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace MergeBullet.Systems
{
    /// <summary>
    /// 보스 돌진 패턴: 추적(Chase) → 예고(Windup, 멈춰서 깜빡임) → 돌진(Dash) → 다시 추적.
    /// 추적 중에는 일반 적과 같이 EnemyChaseSystem이 움직이고, 예고·돌진 중에는 BossOverride를 켜서
    /// 추적 시스템이 손대지 않게 한 뒤 여기서 직접 움직인다.
    /// </summary>
    [UpdateAfter(typeof(EnemyChaseSystem))]
    [UpdateBefore(typeof(EnemySeparationSystem))]
    [BurstCompile]
    public partial struct BossBehaviorSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Boss>();
            state.RequireForUpdate<Player>();
            state.RequireForUpdate<ArenaBounds>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new BossBehaviorJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                ArenaHalfSize = SystemAPI.GetSingleton<ArenaBounds>().HalfSize,
                PlayerEntity = SystemAPI.GetSingletonEntity<Player>(),
                PlayerPositions = SystemAPI.GetComponentLookup<PlayerPosition>(true),
            }.ScheduleParallel();
        }
    }

    // BossOverride는 켜고 끄는 대상이라, 켜짐·꺼짐과 상관없이 모든 보스를 처리한다
    [WithPresent(typeof(BossOverride))]
    [BurstCompile]
    partial struct BossBehaviorJob : IJobEntity
    {
        // 예고 중 초당 깜빡임 횟수
        const float WindupBlinkRate = 6f;

        public float DeltaTime;
        public float2 ArenaHalfSize;
        public Entity PlayerEntity;
        [ReadOnly] public ComponentLookup<PlayerPosition> PlayerPositions;

        void Execute(ref Boss boss, ref LocalTransform transform, ref HitFlash flash,
            EnabledRefRW<BossOverride> overrideChase, in Enemy enemy)
        {
            boss.Timer += DeltaTime;
            float2 position = transform.Position.xy;
            float2 toPlayer = PlayerPositions[PlayerEntity].Value - position;

            switch (boss.Phase)
            {
                case BossPhase.Chase:
                    if (boss.Timer < boss.ChaseSeconds) return;
                    boss.Phase = BossPhase.Windup;
                    boss.Timer = 0f;
                    overrideChase.ValueRW = true;
                    return;

                case BossPhase.Windup:
                    // 멈춘 채 플레이어를 계속 노려보고, 하얗게 깜빡여 돌진을 예고한다 (피격 번쩍임을 재사용)
                    transform.Rotation = quaternion.RotateZ(math.atan2(toPlayer.y, toPlayer.x));
                    if (math.frac(boss.Timer * WindupBlinkRate) < 0.5f) flash.Timer = flash.Duration;
                    if (boss.Timer < boss.WindupSeconds) return;

                    // 예고가 끝나는 순간의 방향으로 고정한다 → 플레이어가 예고를 보고 옆으로 피할 수 있다
                    boss.DashDirection = math.normalizesafe(toPlayer, new float2(1f, 0f));
                    boss.Phase = BossPhase.Dash;
                    boss.Timer = 0f;
                    return;

                case BossPhase.Dash:
                    float2 limit = ArenaHalfSize - enemy.Radius;
                    position = math.clamp(position + boss.DashDirection * boss.DashSpeed * DeltaTime, -limit, limit);
                    transform.Position = new float3(position, transform.Position.z);
                    if (boss.Timer < boss.DashSeconds) return;

                    boss.Phase = BossPhase.Chase;
                    boss.Timer = 0f;
                    overrideChase.ValueRW = false;
                    return;
            }
        }
    }
}
