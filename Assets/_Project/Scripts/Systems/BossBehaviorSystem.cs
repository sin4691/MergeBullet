using MergeBullet.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace MergeBullet.Systems
{
    /// <summary>
    /// 보스 패턴: 추적(Chase) → 예고(Windup, 멈춰서 깜빡임) → 공격 → 다시 추적.
    /// 공격은 돌진 → 소환 → 돌진 → 충격파 순서로 돌아간다. 돌진은 Dash 단계에서 움직이고, 소환·충격파는 예고가 끝나는
    /// 순간 엔티티를 만들고 바로 추적으로 돌아간다.
    /// 추적 중에는 일반 적과 같이 EnemyChaseSystem이 움직이고, 예고·돌진 중에는 BossOverride를 켜서
    /// 추적 시스템이 손대지 않게 한 뒤 여기서 직접 움직인다.
    /// 보스는 몇 마리뿐이라 병렬로 나누지 않고 한 잡에서 처리한다 (생성 명령을 한 버퍼에 순서대로 쓰기 위해서도).
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
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
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
                Enemies = SystemAPI.GetComponentLookup<Enemy>(true),
                // 새 엔티티(부하·충격파)는 다음 프레임 시작에 만든다 (프레임 끝에 만들면 한 프레임 원점에 그려짐)
                Ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged),
            }.Schedule();
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
        // 소환할 부하 프리팹의 능력치를 읽는다 (보스 자신의 Enemy는 아래 Execute의 in 매개변수)
        [ReadOnly] public ComponentLookup<Enemy> Enemies;
        public EntityCommandBuffer Ecb;

        // 공격 순서: 돌진 사이에 소환·충격파를 번갈아 끼운다
        static readonly BossAttack[] AttackOrder = { BossAttack.Dash, BossAttack.Summon, BossAttack.Dash, BossAttack.Shockwave };
        // 예고 길이 배율: 충격파는 범위가 넓어 피할 시간을 더 준다
        const float SummonWindupScale = 1f;
        const float ShockwaveWindupScale = 1.5f;

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
                    boss.Attack = AttackOrder[boss.AttackIndex % AttackOrder.Length];
                    boss.AttackIndex++;
                    boss.WindupDuration = boss.WindupSeconds * (boss.Attack == BossAttack.Shockwave ? ShockwaveWindupScale
                        : boss.Attack == BossAttack.Summon ? SummonWindupScale : 1f);
                    boss.Phase = BossPhase.Windup;
                    boss.Timer = 0f;
                    overrideChase.ValueRW = true;
                    return;

                case BossPhase.Windup:
                    // 멈춘 채 플레이어를 노려보며 하얗게 깜빡여 공격을 예고한다 (피격 번쩍임을 재사용).
                    // 무슨 공격인지는 BossTelegraph가 보여 준다 (돌진 경로·소환 원·충격파 범위)
                    transform.Rotation = quaternion.RotateZ(math.atan2(toPlayer.y, toPlayer.x));
                    if (math.frac(boss.Timer * WindupBlinkRate) < 0.5f) flash.Timer = flash.Duration;

                    // 예고 동안 조준 방향을 계속 갱신한다 → BossTelegraph(연출)가 이 방향으로 돌진 경로를 그린다.
                    // 예고가 끝나면 그때의 방향으로 고정되므로, 플레이어는 선을 보고 옆으로 피할 수 있다
                    boss.DashDirection = math.normalizesafe(toPlayer, boss.DashDirection);
                    if (boss.Timer < boss.WindupDuration) return;

                    boss.Timer = 0f;
                    if (boss.Attack == BossAttack.Dash)
                    {
                        boss.Phase = BossPhase.Dash;
                        return;
                    }

                    if (boss.Attack == BossAttack.Summon) Summon(boss, position, enemy.Radius);
                    else SpawnShockwave(boss, position, enemy.Radius);
                    boss.Phase = BossPhase.Chase;
                    overrideChase.ValueRW = false;
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

        // 보스 몸에서 SummonDistance 떨어진 원 위에 같은 간격으로 부하를 놓는다 (아레나 밖이면 안으로 당김)
        void Summon(in Boss boss, float2 center, float bossRadius)
        {
            if (boss.SummonPrefab == Entity.Null || !Enemies.HasComponent(boss.SummonPrefab)) return;

            Enemy stats = Enemies[boss.SummonPrefab];
            float distance = bossRadius + boss.SummonDistance + stats.Radius;
            float2 limit = ArenaHalfSize - stats.Radius;
            for (int i = 0; i < boss.SummonCount; i++)
            {
                math.sincos(2f * math.PI * i / boss.SummonCount, out float sin, out float cos);
                float2 position = math.clamp(center + new float2(cos, sin) * distance, -limit, limit);
                EnemySpawnUtility.RecordSpawn(Ecb, boss.SummonPrefab, stats, position);
            }
        }

        // 고리는 보스 몸 가장자리에서 시작해 퍼진다
        void SpawnShockwave(in Boss boss, float2 center, float bossRadius)
        {
            Entity wave = Ecb.CreateEntity();
            Ecb.AddComponent(wave, new Shockwave
            {
                Center = center,
                Radius = bossRadius,
                MaxRadius = boss.ShockwaveMaxRadius,
                Speed = boss.ShockwaveSpeed,
                Width = boss.ShockwaveWidth,
                Damage = boss.ShockwaveDamage,
            });
        }
    }
}
