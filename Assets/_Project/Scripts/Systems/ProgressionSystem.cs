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
        public const float MaxHealthBonus = 20f;           // 최대 체력 +20, 같은 양 회복
        public const float ExplosionRadiusBonus = 0.20f;   // 원래 폭발 반경의 +20%
        public const float MagnetRadiusBonus = 0.30f;      // 원래 젬 자석 범위의 +30%
    }

    /// <summary>
    /// 증강 수치. ProgressionSystem이 고를 때 한 번 적용하고, HUD가 같은 값으로 설명 문구를 만든다.
    /// 배율은 고른 순간의 값에 곱한다 (그 뒤 일반 강화는 기본 능력치 기준으로 더해짐).
    /// </summary>
    public static class AugmentTuning
    {
        public const int EveryLevels = 5;                  // 5, 10, 15 … 레벨에 일반 강화 대신 증강

        public const int TwinShots = 2;                    // 쌍둥이 총구: 한 번에 2발
        public const float TwinDamage = 0.7f;              //   피해 -30%
        public const float OverheatInterval = 0.5f;        // 과열: 발사 간격 절반
        public const float OverheatDamage = 0.6f;          //   피해 -40%
        public const int HeavyStartLevel = 2;              // 대구경: 레벨 2 총알 발사
        public const float HeavyInterval = 2f;             //   발사 간격 2배
        public const float RicochetBonus = 0.2f;           // 탄성: 튕길 때마다 피해 +20%
        public const float RicochetSpeed = 0.8f;           //   총알 속도 -20%
        public const float OverloadEffect = 2.5f;          // 과부하: 최대 레벨 효과 2.5배
        public const float OverloadInterval = 3f;          //   발사 간격 3배
        public const float ChainMergeRadius = 0.9f;        // 연쇄 합성: 합성 폭발 반경
        public const float ChainMergeDamage = 2f;          //   합성 폭발 피해 = 기본 총알 피해 × 2
        public const float ChainMergeBulletDamage = 0.7f;  //   총알 피해 -30%
        public const int LevelFiveMaxLevel = 5;            // 레벨 5: 최대 레벨 5
        public const float LevelFiveEffect = 2.5f;         //   최대 레벨 효과 2.5배
        public const float EnduranceLifetime = 2f;         // 끈기: 총알 지속 시간 2배
        public const float EnduranceDamage = 0.8f;         //   피해 -20%
    }

    /// <summary>
    /// 레벨업과 업그레이드를 관리한다.
    /// - 5레벨마다는 일반 강화 대신 증강(규칙 바꾸기) 3개를 뽑는다
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
            // 영구 강화 보너스가 정해진 뒤에 기본 능력치를 기록해야 한다 (PermanentBonusSystem이 만든다)
            state.RequireForUpdate<PermanentBonus>();
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
                if (SystemAPI.HasSingleton<Augments>()) SystemAPI.SetSingleton(new Augments());
                progress.ValueRW.Level = 1;
                progress.ValueRW.Experience = 0;
                progress.ValueRW.PrevLevelXp = 0;
                progress.ValueRW.NextLevelXp = progress.ValueRO.FirstStep;
                offer.ValueRW = new LevelUpOffer { Selected = -1 };
                return;
            }
            // 벤치마크 중에는 레벨업 선택으로 게임이 멈추면 안 된다
            if (game.IsGameOver || SystemAPI.HasSingleton<BenchmarkSettings>()) return;

            if (offer.ValueRO.Active)
            {
                if (offer.ValueRO.Selected < 0) return; // 아직 고르는 중
                if (offer.ValueRO.IsAugment) ApplyAugment(ref state, AugmentAt(offer.ValueRO, offer.ValueRO.Selected));
                else ApplyUpgrade(ref state, ChoiceAt(offer.ValueRO, offer.ValueRO.Selected), baseStats.ValueRO);
                offer.ValueRW = new LevelUpOffer { Selected = -1 };
            }

            // 한 번에 여러 레벨을 넘었으면 방금 고른 직후 다음 선택지를 바로 연다 (한 번에 하나씩)
            if (progress.ValueRO.Experience < progress.ValueRO.NextLevelXp) return;

            PlayerProgress p = progress.ValueRO;
            p.Level++;
            p.PrevLevelXp = p.NextLevelXp;
            p.NextLevelXp += p.FirstStep + p.StepGrowth * (p.Level - 1);
            uint taken = SystemAPI.TryGetSingleton(out Augments augments) ? augments.Taken : uint.MaxValue;
            offer.ValueRW = p.Level % AugmentTuning.EveryLevels == 0 && TryRollAugments(ref p.Random, taken, out LevelUpOffer augmentOffer)
                ? augmentOffer
                : RollOffer(ref p.Random, SystemAPI.HasSingleton<MaxLevelEffect>());
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

        static AugmentType AugmentAt(in LevelUpOffer offer, int index)
        {
            switch (index)
            {
                case 0: return offer.Augment0;
                case 1: return offer.Augment1;
                default: return offer.Augment2;
            }
        }

        // 아직 안 고른 증강 중 최대 3개 (같은 증강은 한 판에 한 번). 남은 게 없으면 false → 일반 강화
        static bool TryRollAugments(ref Random random, uint taken, out LevelUpOffer offer)
        {
            var pool = new FixedList32Bytes<AugmentType>();
            for (int i = 0; i < (int)AugmentType.Count; i++)
                if ((taken & (1u << i)) == 0) pool.Add((AugmentType)i);

            offer = default;
            if (pool.Length == 0) return false;

            int count = math.min(3, pool.Length);
            for (int i = 0; i < count; i++)
            {
                int j = random.NextInt(i, pool.Length);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }

            offer = new LevelUpOffer
            {
                Active = true,
                Selected = -1,
                IsAugment = true,
                ChoiceCount = count,
                Augment0 = pool[0],
                Augment1 = count > 1 ? pool[1] : pool[0],
                Augment2 = count > 2 ? pool[2] : pool[0],
            };
            return true;
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
                ChoiceCount = 3,
                Choice0 = pool[0],
                Choice1 = pool[1],
                Choice2 = pool[2],
            };
        }

        // 베이킹된 능력치에 영구 강화 보너스를 곱한 값이 이번 판의 기본 능력치가 된다.
        // 기록한 뒤 바로 실제 컴포넌트에도 써서, 첫 판부터 보너스가 적용된 상태로 시작한다
        void CaptureBaseStats(ref SystemState state, ref BaseStats stats)
        {
            PermanentBonus bonus = SystemAPI.GetSingleton<PermanentBonus>();
            AutoFire weapon = SystemAPI.GetSingleton<AutoFire>();
            stats.MoveSpeed = SystemAPI.GetSingleton<Player>().MoveSpeed * bonus.MoveSpeedMultiplier;
            stats.MaxHealth = SystemAPI.GetSingleton<PlayerHealth>().Max + bonus.BonusMaxHealth;
            stats.FireInterval = weapon.Interval * bonus.FireIntervalMultiplier;
            stats.BulletSpeed = weapon.BulletSpeed;
            stats.BulletDamage = SystemAPI.GetSingleton<BulletLevelSettings>().BaseDamage * bonus.DamageMultiplier;
            stats.ExplosionRadius = SystemAPI.TryGetSingleton(out MaxLevelEffect effect) ? effect.Radius : 0f;
            stats.MagnetRadius = SystemAPI.TryGetSingleton(out GemCollector collector)
                ? collector.MagnetRadius * bonus.MagnetRadiusMultiplier
                : 0f;
            stats.MaxLevel = SystemAPI.GetSingleton<BulletLevelSettings>().MaxLevel;
            stats.EffectDamage = effect.Damage;
            stats.ChainRange = effect.ChainRange;
            stats.ChainDamage = effect.ChainDamage;
            stats.IdleLifetime = SystemAPI.TryGetSingleton(out BulletIdleSettings idle) ? idle.Lifetime : 0f;
            stats.Captured = true;

            RestoreBaseStats(ref state, stats);
            RefRW<PlayerHealth> health = SystemAPI.GetSingletonRW<PlayerHealth>();
            health.ValueRW.Value = health.ValueRO.Max;
        }

        void RestoreBaseStats(ref SystemState state, in BaseStats stats)
        {
            RefRW<AutoFire> weapon = SystemAPI.GetSingletonRW<AutoFire>();
            weapon.ValueRW.Interval = stats.FireInterval;
            weapon.ValueRW.BulletSpeed = stats.BulletSpeed;
            weapon.ValueRW.ShotsPerVolley = 1;
            weapon.ValueRW.StartLevel = 1;
            SystemAPI.GetSingletonRW<Player>().ValueRW.MoveSpeed = stats.MoveSpeed;
            // 현재 체력은 GameFlowSystem이 Max로 채운다
            SystemAPI.GetSingletonRW<PlayerHealth>().ValueRW.Max = stats.MaxHealth;
            RefRW<BulletLevelSettings> levels = SystemAPI.GetSingletonRW<BulletLevelSettings>();
            levels.ValueRW.BaseDamage = stats.BulletDamage;
            levels.ValueRW.MaxLevel = stats.MaxLevel;
            levels.ValueRW.BounceDamageBonus = 0f;
            if (SystemAPI.HasSingleton<MaxLevelEffect>())
            {
                RefRW<MaxLevelEffect> effect = SystemAPI.GetSingletonRW<MaxLevelEffect>();
                effect.ValueRW.Radius = stats.ExplosionRadius;
                effect.ValueRW.Damage = stats.EffectDamage;
                effect.ValueRW.ChainRange = stats.ChainRange;
                effect.ValueRW.ChainDamage = stats.ChainDamage;
                effect.ValueRW.MergeBlastRadius = 0f;
                effect.ValueRW.MergeBlastDamage = 0f;
            }
            if (SystemAPI.HasSingleton<BulletIdleSettings>())
                SystemAPI.GetSingletonRW<BulletIdleSettings>().ValueRW.Lifetime = stats.IdleLifetime;
            if (SystemAPI.HasSingleton<GemCollector>())
                SystemAPI.GetSingletonRW<GemCollector>().ValueRW.MagnetRadius = stats.MagnetRadius;
        }

        // 증강 하나를 적용한다. 대부분 "큰 이득 + 대가"를 한 번에 곱한다
        void ApplyAugment(ref SystemState state, AugmentType type)
        {
            if (SystemAPI.HasSingleton<Augments>())
                SystemAPI.GetSingletonRW<Augments>().ValueRW.Taken |= 1u << (int)type;

            RefRW<AutoFire> weapon = SystemAPI.GetSingletonRW<AutoFire>();
            RefRW<BulletLevelSettings> levels = SystemAPI.GetSingletonRW<BulletLevelSettings>();
            switch (type)
            {
                case AugmentType.TwinBarrel:
                    weapon.ValueRW.ShotsPerVolley = AugmentTuning.TwinShots;
                    levels.ValueRW.BaseDamage *= AugmentTuning.TwinDamage;
                    break;
                case AugmentType.Overheat:
                    weapon.ValueRW.Interval *= AugmentTuning.OverheatInterval;
                    levels.ValueRW.BaseDamage *= AugmentTuning.OverheatDamage;
                    break;
                case AugmentType.HeavyCaliber:
                    weapon.ValueRW.StartLevel = AugmentTuning.HeavyStartLevel;
                    weapon.ValueRW.Interval *= AugmentTuning.HeavyInterval;
                    break;
                case AugmentType.Ricochet:
                    levels.ValueRW.BounceDamageBonus = AugmentTuning.RicochetBonus;
                    weapon.ValueRW.BulletSpeed *= AugmentTuning.RicochetSpeed;
                    break;
                case AugmentType.Overload:
                    weapon.ValueRW.Interval *= AugmentTuning.OverloadInterval;
                    ScaleMaxLevelEffect(ref state, AugmentTuning.OverloadEffect);
                    break;
                case AugmentType.ChainMerge:
                    if (SystemAPI.HasSingleton<MaxLevelEffect>())
                    {
                        RefRW<MaxLevelEffect> effect = SystemAPI.GetSingletonRW<MaxLevelEffect>();
                        effect.ValueRW.MergeBlastRadius = AugmentTuning.ChainMergeRadius;
                        // 피해는 줄이기 전의 총알 피해 기준
                        effect.ValueRW.MergeBlastDamage = levels.ValueRO.BaseDamage * AugmentTuning.ChainMergeDamage;
                    }
                    levels.ValueRW.BaseDamage *= AugmentTuning.ChainMergeBulletDamage;
                    break;
                case AugmentType.LevelFive:
                    levels.ValueRW.MaxLevel = AugmentTuning.LevelFiveMaxLevel;
                    ScaleMaxLevelEffect(ref state, AugmentTuning.LevelFiveEffect);
                    break;
                case AugmentType.Endurance:
                    if (SystemAPI.HasSingleton<BulletIdleSettings>())
                        SystemAPI.GetSingletonRW<BulletIdleSettings>().ValueRW.Lifetime *= AugmentTuning.EnduranceLifetime;
                    levels.ValueRW.BaseDamage *= AugmentTuning.EnduranceDamage;
                    break;
            }
        }

        // 최대 레벨 효과를 키운다: 폭발은 범위·피해, 번개는 피해와 튀는 거리 (거리는 넓이가 아니라 길이라 제곱근만큼)
        void ScaleMaxLevelEffect(ref SystemState state, float factor)
        {
            if (!SystemAPI.HasSingleton<MaxLevelEffect>()) return;
            RefRW<MaxLevelEffect> effect = SystemAPI.GetSingletonRW<MaxLevelEffect>();
            effect.ValueRW.Radius *= factor;
            effect.ValueRW.Damage *= factor;
            effect.ValueRW.ChainDamage *= factor;
            effect.ValueRW.ChainRange *= math.sqrt(factor);
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
