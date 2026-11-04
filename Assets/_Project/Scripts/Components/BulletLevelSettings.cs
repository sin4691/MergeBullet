using Unity.Entities;
using Unity.Mathematics;

namespace MergeBullet.Components
{
    /// <summary>레벨별 크기·색·피해 규칙 싱글톤. 계산은 Systems/BulletLevelUtility에서 한다.</summary>
    public struct BulletLevelSettings : IComponentData
    {
        public int MaxLevel;
        public float BaseRadius;
        /// <summary>레벨이 1 오를 때마다 BaseRadius 대비 늘어나는 비율.</summary>
        public float RadiusGrowth;
        /// <summary>레벨 1 총알이 적에게 주는 피해. 레벨 n은 BaseDamage * n.</summary>
        public float BaseDamage;
        public float4 Level1Color;
        public float4 Level2Color;
        public float4 Level3Color;
        public float4 Level4Color;
        /// <summary>레벨 5 증강을 골랐을 때만 쓰인다.</summary>
        public float4 Level5Color;
        /// <summary>튕길 때마다 더해지는 피해 비율 (탄성 증강). 0이면 없음.</summary>
        public float BounceDamageBonus;
    }
}
