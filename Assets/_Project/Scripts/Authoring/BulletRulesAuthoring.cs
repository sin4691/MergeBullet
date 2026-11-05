using MergeBullet.Components;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace MergeBullet.Authoring
{
    /// <summary>
    /// 총알 규칙: 레벨별 크기·피해·색(BulletLevelSettings), 방치 소멸(BulletIdleSettings), 시작 시 뿌리는 총알(BulletSpawner).
    /// 총알 프리팹 하나의 모양은 BulletAuthoring, 여기는 판 전체에 걸친 규칙.
    /// </summary>
    [DisallowMultipleComponent]
    public class BulletRulesAuthoring : MonoBehaviour
    {
        public GameObject BulletPrefab;
        // 시작 시 뿌리는 총알 수. 플레이어가 쏘게 된 뒤로는 0 (스트레스 테스트할 때만 올린다)
        [Min(0)] public int BulletCount = 100;
        [Min(0.01f)] public float BulletRadius = 0.12f;
        public float BulletSpeed = 5f;
        public uint Seed = 12345;

        [Header("Merge")]
        [Range(2, 4)] public int MaxLevel = 4;
        [Min(0f)] public float RadiusGrowthPerLevel = 0.5f;
        [Min(0f)] public float BulletBaseDamage = 1f;
        public Color[] LevelColors =
        {
            new Color(1f, 0.85f, 0.2f), // 1 노랑
            new Color(1f, 0.5f, 0.1f),  // 2 주황
            // 빨강은 적 색과 헷갈려서 쓰지 않는다. 가장 센 레벨 4는 흰색 (폭발 색과 이어짐)
            new Color(0.75f, 0.3f, 1f), // 3 보라
            new Color(1f, 1f, 1f),      // 4 흰색
        };

        [Header("Idle Despawn")]
        // 8초면 화면에 총알이 오래 남아 합성이 쉬웠다
        [Min(0.1f)] public float IdleLifetime = 5f;
        [Min(0f)] public float IdleWarningTime = 1.5f;

        class Baker : Baker<BulletRulesAuthoring>
        {
            public override void Bake(BulletRulesAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new BulletLevelSettings
                {
                    MaxLevel = authoring.MaxLevel,
                    BaseRadius = authoring.BulletRadius,
                    RadiusGrowth = authoring.RadiusGrowthPerLevel,
                    BaseDamage = authoring.BulletBaseDamage,
                    Level1Color = LevelColor(authoring, 0),
                    Level2Color = LevelColor(authoring, 1),
                    Level3Color = LevelColor(authoring, 2),
                    Level4Color = LevelColor(authoring, 3),
                    // 레벨 5 증강 전용 색: 흰색(레벨 4) 다음이라 눈에 띄는 청록
                    Level5Color = authoring.LevelColors.Length > 4 ? LevelColor(authoring, 4) : new float4(0.35f, 0.95f, 1f, 1f),
                });
                AddComponent(entity, new BulletIdleSettings
                {
                    Lifetime = authoring.IdleLifetime,
                    WarningTime = authoring.IdleWarningTime,
                });

                if (authoring.BulletPrefab == null) return;
                AddComponent(entity, new BulletSpawner
                {
                    Prefab = GetEntity(authoring.BulletPrefab, TransformUsageFlags.Dynamic),
                    Count = authoring.BulletCount,
                    Speed = authoring.BulletSpeed,
                    Seed = authoring.Seed,
                });
            }

            // 머티리얼 프로퍼티 오버라이드는 셰이더에 그대로 들어가므로 선형 색공간 값으로 넘긴다
            static float4 LevelColor(BulletRulesAuthoring authoring, int index)
            {
                Color c = index < authoring.LevelColors.Length ? authoring.LevelColors[index] : Color.white;
                Color linear = c.linear;
                return new float4(linear.r, linear.g, linear.b, linear.a);
            }
        }
    }
}
