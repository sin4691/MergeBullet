using MergeBullet.Components;
using UnityEngine;

namespace MergeBullet.Meta
{
    /// <summary>로비 상점에서 코인으로 사는 영구 강화 종류. 저장 데이터의 단계 배열 인덱스로도 쓰므로 순서를 바꾸지 않는다.</summary>
    public enum PermanentUpgradeType
    {
        FireRate,
        MaxHealth,
        Damage,
        MoveSpeed,
        Magnet,
    }

    /// <summary>영구 강화 하나의 정의 (이름, 단계당 효과, 최대 단계, 1단계 가격).</summary>
    public readonly struct PermanentUpgradeInfo
    {
        public readonly string Name;
        public readonly float PerLevel;
        public readonly int MaxLevel;
        public readonly int BaseCost;

        public PermanentUpgradeInfo(string name, float perLevel, int maxLevel, int baseCost)
        {
            Name = name;
            PerLevel = perLevel;
            MaxLevel = maxLevel;
            BaseCost = baseCost;
        }
    }

    /// <summary>
    /// 영구 강화 표와 계산. 상점 UI(표시·가격)와 게임 시작 시 보너스 계산이 같은 표를 쓴다.
    /// 판 안의 레벨업 강화(UpgradeTuning)와 별개로, 판을 시작할 때의 기본 능력치를 올린다.
    /// </summary>
    public static class PermanentUpgrades
    {
        public const int Count = 5;

        // 단계가 하나 오를 때마다 가격이 이 배수로 오른다
        const float CostGrowth = 1.5f;

        static readonly PermanentUpgradeInfo[] Table =
        {
            new PermanentUpgradeInfo("Rapid Fire", perLevel: 0.05f, maxLevel: 10, baseCost: 20), // 발사 간격 -5%
            new PermanentUpgradeInfo("Vitality", perLevel: 2f, maxLevel: 10, baseCost: 15),      // 최대 체력 +2
            new PermanentUpgradeInfo("Power", perLevel: 0.10f, maxLevel: 10, baseCost: 25),      // 피해 +10%
            new PermanentUpgradeInfo("Agility", perLevel: 0.05f, maxLevel: 5, baseCost: 20),     // 이동 속도 +5%
            new PermanentUpgradeInfo("Magnet", perLevel: 0.15f, maxLevel: 5, baseCost: 10),      // 줍기 범위 +15%
        };

        public static PermanentUpgradeInfo Get(PermanentUpgradeType type) => Table[(int)type];

        /// <summary>현재 단계에서 다음 단계로 올리는 가격.</summary>
        public static int CostOf(PermanentUpgradeType type, int currentLevel)
            => Mathf.RoundToInt(Get(type).BaseCost * Mathf.Pow(CostGrowth, currentLevel));

        /// <summary>해당 단계까지 산 총 효과를 사람이 읽는 문장으로.</summary>
        public static string Describe(PermanentUpgradeType type, int level)
        {
            float total = Get(type).PerLevel * level;
            switch (type)
            {
                case PermanentUpgradeType.FireRate: return $"Fire interval -{Percent(total)}";
                case PermanentUpgradeType.MaxHealth: return $"Max HP +{total:0}";
                case PermanentUpgradeType.Damage: return $"Bullet damage +{Percent(total)}";
                case PermanentUpgradeType.MoveSpeed: return $"Move speed +{Percent(total)}";
                default: return $"Gem pickup range +{Percent(total)}";
            }
        }

        /// <summary>저장된 단계로 이번 판의 보너스를 계산한다.</summary>
        public static PermanentBonus BonusFor(SaveData save)
        {
            float Total(PermanentUpgradeType type) => Get(type).PerLevel * save.GetUpgradeLevel(type);

            return new PermanentBonus
            {
                FireIntervalMultiplier = 1f - Total(PermanentUpgradeType.FireRate),
                BonusMaxHealth = Total(PermanentUpgradeType.MaxHealth),
                DamageMultiplier = 1f + Total(PermanentUpgradeType.Damage),
                MoveSpeedMultiplier = 1f + Total(PermanentUpgradeType.MoveSpeed),
                MagnetRadiusMultiplier = 1f + Total(PermanentUpgradeType.Magnet),
            };
        }

        static string Percent(float value) => $"{Mathf.RoundToInt(value * 100f)}%";
    }
}
