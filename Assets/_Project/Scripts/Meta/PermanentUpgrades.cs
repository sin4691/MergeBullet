using MergeBullet.Components;
using UnityEngine;

namespace MergeBullet.Meta
{
    /// <summary>영구 강화 하나의 정의 (단계당 효과, 최대 단계, 1단계 가격). 이름·설명은 판 안 강화와 같은 문구(upgrade.{종류}.*)를 쓴다.</summary>
    public readonly struct PermanentUpgradeInfo
    {
        public readonly float PerLevel;
        public readonly int MaxLevel;
        public readonly int BaseCost;

        public PermanentUpgradeInfo(float perLevel, int maxLevel, int baseCost)
        {
            PerLevel = perLevel;
            MaxLevel = maxLevel;
            BaseCost = baseCost;
        }
    }

    /// <summary>
    /// 영구 강화 표와 계산. 판 안 레벨업 강화와 같은 목록(UpgradeType)을 쓰고, 한 단계의 효과만 작다
    /// (판 안 강화는 한 판 동안 크게, 영구 강화는 판을 시작할 때의 기본 능력치를 조금씩).
    /// 상점 UI(표시·가격)와 게임 시작 시 보너스 계산이 같은 표를 쓴다.
    /// </summary>
    public static class PermanentUpgrades
    {
        public const int Count = (int)UpgradeType.Duration + 1;

        // 단계가 하나 오를 때마다 가격이 이 배수로 오른다
        const float CostGrowth = 1.5f;

        // 인덱스 = UpgradeType (저장 데이터의 단계 배열 인덱스이기도 해서 순서를 바꾸지 않는다)
        static readonly PermanentUpgradeInfo[] Table =
        {
            new PermanentUpgradeInfo(perLevel: 0.05f, maxLevel: 10, baseCost: 20), // FireRate: 발사 간격 -5%
            new PermanentUpgradeInfo(perLevel: 0.10f, maxLevel: 10, baseCost: 25), // Damage: 피해 +10%
            new PermanentUpgradeInfo(perLevel: 0.05f, maxLevel: 5, baseCost: 20),  // MoveSpeed: 이동 속도 +5%
            new PermanentUpgradeInfo(perLevel: 0.05f, maxLevel: 5, baseCost: 15),  // BulletSpeed: 총알 속도 +5%
            new PermanentUpgradeInfo(perLevel: 20f, maxLevel: 10, baseCost: 15),   // MaxHealth: 최대 체력 +20
            new PermanentUpgradeInfo(perLevel: 0.08f, maxLevel: 5, baseCost: 25),  // ExplosionRadius: 최대 레벨 효과 범위 +8%
            new PermanentUpgradeInfo(perLevel: 0.15f, maxLevel: 5, baseCost: 10),  // Magnet: 줍기 범위 +15%
            new PermanentUpgradeInfo(perLevel: 0.10f, maxLevel: 5, baseCost: 20),  // Duration: 총알 지속 시간 +10%
        };

        public static PermanentUpgradeInfo Get(UpgradeType type) => Table[(int)type];

        /// <summary>현재 단계에서 다음 단계로 올리는 가격.</summary>
        public static int CostOf(UpgradeType type, int currentLevel)
            => Mathf.RoundToInt(Get(type).BaseCost * Mathf.Pow(CostGrowth, currentLevel));

        /// <summary>해당 단계까지 산 총 효과 (체력은 수치, 나머지는 비율. 0.15 = 15%).</summary>
        public static float TotalEffect(UpgradeType type, int level) => Get(type).PerLevel * level;

        /// <summary>저장된 단계로 이번 판의 보너스를 계산한다.</summary>
        public static PermanentBonus BonusFor(SaveData save)
        {
            float Total(UpgradeType type) => TotalEffect(type, save.GetUpgradeLevel(type));

            return new PermanentBonus
            {
                FireIntervalMultiplier = 1f - Total(UpgradeType.FireRate),
                DamageMultiplier = 1f + Total(UpgradeType.Damage),
                MoveSpeedMultiplier = 1f + Total(UpgradeType.MoveSpeed),
                BulletSpeedMultiplier = 1f + Total(UpgradeType.BulletSpeed),
                BonusMaxHealth = Total(UpgradeType.MaxHealth),
                EffectRadiusMultiplier = 1f + Total(UpgradeType.ExplosionRadius),
                MagnetRadiusMultiplier = 1f + Total(UpgradeType.Magnet),
                DurationMultiplier = 1f + Total(UpgradeType.Duration),
            };
        }
    }
}
