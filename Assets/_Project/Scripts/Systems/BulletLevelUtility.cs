using MergeBullet.Components;
using Unity.Mathematics;

namespace MergeBullet.Systems
{
    /// <summary>레벨 → 반지름/색/피해 계산. 스폰·합성·충돌이 같은 규칙을 쓰도록 한곳에 둔다.</summary>
    public static class BulletLevelUtility
    {
        public static float RadiusOf(int level, in BulletLevelSettings settings)
            => settings.BaseRadius * (1f + settings.RadiusGrowth * (level - 1));

        public static float DamageOf(int level, in BulletLevelSettings settings)
            => settings.BaseDamage * level;

        public static float4 ColorOf(int level, in BulletLevelSettings settings)
        {
            switch (level)
            {
                case 1: return settings.Level1Color;
                case 2: return settings.Level2Color;
                case 3: return settings.Level3Color;
                default: return settings.Level4Color;
            }
        }
    }
}
