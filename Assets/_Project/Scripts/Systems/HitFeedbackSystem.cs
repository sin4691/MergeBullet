using MergeBullet.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace MergeBullet.Systems
{
    /// <summary>
    /// 피격 연출. 머티리얼은 그대로 두고 엔티티별 색 오버라이드(URPMaterialPropertyBaseColor)만 바꾼다.
    /// - 적: 맞은 순간 하얀색 → 원래 색으로 빠르게 돌아온다 (HitFlash)
    /// - 플레이어: 무적 시간 동안 하얀색과 원래 색을 번갈아 깜빡인다 (PlayerHealth.InvulnerableTimer)
    /// </summary>
    [UpdateAfter(typeof(BulletEnemyCollisionSystem))]
    [UpdateAfter(typeof(ExplosionSystem))]
    [UpdateAfter(typeof(PlayerDamageSystem))]
    [BurstCompile]
    public partial struct HitFeedbackSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            new EnemyHitFlashJob { DeltaTime = deltaTime }.ScheduleParallel();
            new PlayerBlinkJob().Schedule();
        }
    }

    [BurstCompile]
    partial struct EnemyHitFlashJob : IJobEntity
    {
        static readonly float4 White = new float4(1f);

        public float DeltaTime;

        void Execute(ref HitFlash flash, ref URPMaterialPropertyBaseColor color, in BaseColor baseColor)
        {
            if (flash.Timer <= 0f) return;

            flash.Timer = math.max(flash.Timer - DeltaTime, 0f);
            // Timer가 0이 되는 프레임에 정확히 원래 색으로 돌아간다
            color.Value = math.lerp(baseColor.Value, White, flash.Timer / flash.Duration);
        }
    }

    [BurstCompile]
    partial struct PlayerBlinkJob : IJobEntity
    {
        // 초당 깜빡임 횟수
        const float BlinkRate = 10f;

        void Execute(ref URPMaterialPropertyBaseColor color, in PlayerHealth health, in BaseColor baseColor)
        {
            // 남은 무적 시간을 기준으로 깜빡여서, 게임이 멈추면(timeScale 0) 깜빡임도 멈춘다
            bool white = health.InvulnerableTimer > 0f && math.frac(health.InvulnerableTimer * BlinkRate) >= 0.5f;
            color.Value = white ? new float4(1f) : baseColor.Value;
        }
    }
}
