using MergeBullet.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace MergeBullet.Systems
{
    /// <summary>업그레이드 1회당 증가량. ProgressionSystem이 적용하고, HUD가 같은 값으로 설명 문구를 만든다.</summary>
    public static class UpgradeTuning
    {
        public const float FireIntervalMultiplier = 0.85f; // 발사 간격 -15% (곱연산)
        public const float DamageBonus = 0.25f;            // 원래 피해의 +25%
        public const float MoveSpeedBonus = 0.10f;         // 원래 이동 속도의 +10%
        public const float BulletSpeedBonus = 0.15f;       // 원래 총알 속도의 +15%
        public const float MaxHealthBonus = 2f;            // 최대 체력 +2, 같은 양 회복
        public const float ExplosionRadiusBonus = 0.20f;   // 원래 폭발 반경의 +20%
        public const float MagnetRadiusBonus = 0.30f;      // 원래 젬 자석 범위의 +30%
    }

    /// <summary>
    /// 레벨업과 업그레이드를 관리한다.
    /// - 경험치가 다음 레벨 기준에 닿으면 업그레이드 3개를 뽑아 LevelUpOffer를 연다 (GameFlowSystem이 게임을 멈춤)
    /// - HUD가 선택지를 고르면 해당 능력치에 적용하고 닫는다
    /// - 재시작하면 능력치를 BaseStats로 되돌리고 레벨을 1로 초기화한다
    /// 프레임 맨 앞(이전 프레임 잡이 끝난 뒤)에 돌아서, 싱글톤을 메인 스레드에서 직접 읽고 써도 기다림이 거의 없다.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(GameFlowSystem))]
    [BurstCompile]
    public partial struct ProgressionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerProgress>();
            state.RequireForUpdate<LevelUpOffer>();
            state.RequireForUpdate<BaseStats>();
            state.RequireForUpdate<GameState>();
            state.RequireForUpdate<Player>();
            state.RequireForUpdate<PlayerHealth>();
            state.RequireForUpdate<AutoFire>();
            state.RequireForUpdate<BulletLevelSettings>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            RefRW<BaseStats> baseStats = SystemAPI.GetSingletonRW<BaseStats>();
            if (!baseStats.ValueRO.Captured) CaptureBaseStats(ref state, ref baseStats.ValueRW);

            RefRW<PlayerProgress> progress = SystemAPI.GetSingletonRW<PlayerProgress>();
            RefRW<LevelUpOffer> offer = SystemAPI.GetSingletonRW<LevelUpOffer>();

            // 재시작 처리는 GameFlowSystem(이 다음에 실행)이 플래그를 끄기 전에 여기서 레벨·업그레이드 쪽만 되돌린다
            GameState game = SystemAPI.GetSingleton<GameState>();
            if (game.RestartRequested)
            {
                RestoreBaseStats(ref state, baseStats.ValueRO);
                progress.ValueRW.Level = 1;
                progress.ValueRW.Experience = 0;
                progress.ValueRW.PrevLevelXp = 0;
                progress.ValueRW.NextLevelXp = progress.ValueRO.FirstStep;
                offer.ValueRW = new LevelUpOffer { Selected = -1 };
                return;
            }
            if (game.IsGameOver) return;

            if (offer.ValueRO.Active)
            {
                if (offer.ValueRO.Selected < 0) return; // 아직 고르는 중
                ApplyUpgrade(ref state, ChoiceAt(offer.ValueRO, offer.ValueRO.Selected), baseStats.ValueRO);
                offer.ValueRW = new LevelUpOffer { Selected = -1 };
            }

            // 한 번에 여러 레벨을 넘었으면 방금 고른 직후 다음 선택지를 바로 연다 (한 번에 하나씩)
            if (progress.ValueRO.Experience < progress.ValueRO.NextLevelXp) return;

            PlayerProgress p = progress.ValueRO;
            p.Level++;
            p.PrevLevelXp = p.NextLevelXp;
            p.NextLevelXp += p.FirstStep + p.StepGrowth * (p.Level - 1);
            offer.ValueRW = RollOffer(ref p.Random, SystemAPI.HasSingleton<MaxLevelEffect>());
            progress.ValueRW = p;
        }

        static UpgradeType ChoiceAt(in LevelUpOffer offer, int index)
        {
            switch (index)
            {
                case 0: return offer.Choice0;
                case 1: return offer.Choice1;
                default: return offer.Choice2;
            }
        }

        // 후보 중 서로 다른 3개를 뽑는다 (부분 Fisher-Yates 셔플)
        static LevelUpOffer RollOffer(ref Random random, bool hasExplosion)
        {
            var pool = new FixedList32Bytes<UpgradeType>
            {
                UpgradeType.FireRate,
                UpgradeType.Damage,
                UpgradeType.MoveSpeed,
                UpgradeType.BulletSpeed,
                UpgradeType.MaxHealth,
                UpgradeType.Magnet,
            };
            if (hasExplosion) pool.Add(UpgradeType.ExplosionRadius);

            for (int i = 0; i < 3; i++)
            {
                int j = random.NextInt(i, pool.Length);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }

            return new LevelUpOffer
            {
                Active = true,
                Selected = -1,
                Choice0 = pool[0],
                Choice1 = pool[1],
                Choice2 = pool[2],
            };
        }

        void CaptureBaseStats(ref SystemState state, ref BaseStats stats)
        {
            AutoFire weapon = SystemAPI.GetSingleton<AutoFire>();
            stats.MoveSpeed = SystemAPI.GetSingleton<Player>().MoveSpeed;
            stats.MaxHealth = SystemAPI.GetSingleton<PlayerHealth>().Max;
            stats.FireInterval = weapon.Interval;
            stats.BulletSpeed = weapon.BulletSpeed;
            stats.BulletDamage = SystemAPI.GetSingleton<BulletLevelSettings>().BaseDamage;
            stats.ExplosionRadius = SystemAPI.TryGetSingleton(out MaxLevelEffect effect) ? effect.Radius : 0f;
            stats.MagnetRadius = SystemAPI.TryGetSingleton(out GemCollector collector) ? collector.MagnetRadius : 0f;
            stats.Captured = true;
        }

        void RestoreBaseStats(ref SystemState state, in BaseStats stats)
        {
            RefRW<AutoFire> weapon = SystemAPI.GetSingletonRW<AutoFire>();
            weapon.ValueRW.Interval = stats.FireInterval;
            weapon.ValueRW.BulletSpeed = stats.BulletSpeed;
            SystemAPI.GetSingletonRW<Player>().ValueRW.MoveSpeed = stats.MoveSpeed;
            // 현재 체력은 GameFlowSystem이 Max로 채운다
            SystemAPI.GetSingletonRW<PlayerHealth>().ValueRW.Max = stats.MaxHealth;
            SystemAPI.GetSingletonRW<BulletLevelSettings>().ValueRW.BaseDamage = stats.BulletDamage;
            if (SystemAPI.HasSingleton<MaxLevelEffect>())
                SystemAPI.GetSingletonRW<MaxLevelEffect>().ValueRW.Radius = stats.ExplosionRadius;
            if (SystemAPI.HasSingleton<GemCollector>())
                SystemAPI.GetSingletonRW<GemCollector>().ValueRW.MagnetRadius = stats.MagnetRadius;
        }

        void ApplyUpgrade(ref SystemState state, UpgradeType type, in BaseStats stats)
        {
            switch (type)
            {
                case UpgradeType.FireRate:
                    SystemAPI.GetSingletonRW<AutoFire>().ValueRW.Interval *= UpgradeTuning.FireIntervalMultiplier;
                    break;
                case UpgradeType.Damage:
                    SystemAPI.GetSingletonRW<BulletLevelSettings>().ValueRW.BaseDamage +=
                        stats.BulletDamage * UpgradeTuning.DamageBonus;
                    break;
                case UpgradeType.MoveSpeed:
                    SystemAPI.GetSingletonRW<Player>().ValueRW.MoveSpeed += stats.MoveSpeed * UpgradeTuning.MoveSpeedBonus;
                    break;
                case UpgradeType.BulletSpeed:
                    SystemAPI.GetSingletonRW<AutoFire>().ValueRW.BulletSpeed +=
                        stats.BulletSpeed * UpgradeTuning.BulletSpeedBonus;
                    break;
                case UpgradeType.MaxHealth:
                    RefRW<PlayerHealth> health = SystemAPI.GetSingletonRW<PlayerHealth>();
                    health.ValueRW.Max += UpgradeTuning.MaxHealthBonus;
                    health.ValueRW.Value = math.min(health.ValueRO.Value + UpgradeTuning.MaxHealthBonus, health.ValueRO.Max);
                    break;
                case UpgradeType.ExplosionRadius:
                    if (SystemAPI.HasSingleton<MaxLevelEffect>())
                        SystemAPI.GetSingletonRW<MaxLevelEffect>().ValueRW.Radius +=
                            stats.ExplosionRadius * UpgradeTuning.ExplosionRadiusBonus;
                    break;
                case UpgradeType.Magnet:
                    if (SystemAPI.HasSingleton<GemCollector>())
                        SystemAPI.GetSingletonRW<GemCollector>().ValueRW.MagnetRadius +=
                            stats.MagnetRadius * UpgradeTuning.MagnetRadiusBonus;
                    break;
            }
        }
    }
}
