using MergeBullet.Components;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace MergeBullet.Authoring
{
    /// <summary>
    /// SubScene 안의 Arena 오브젝트에 붙인다. 아레나 크기, 초기 총알 스폰, 레벨 규칙, 최대 레벨 효과, 방치 소멸 규칙,
    /// 적 스폰, 게임 상태를 ArenaBounds / BulletSpawner / BulletLevelSettings / MaxLevelEffect / BulletIdleSettings /
    /// EnemySpawner / GameState / GameStats / 레벨 진행(PlayerProgress, LevelUpOffer, BaseStats) 싱글톤으로 베이킹한다.
    /// </summary>
    public class ArenaAuthoring : MonoBehaviour
    {
        public Vector2 ArenaSize = new Vector2(16f, 9f);
        public GameObject BulletPrefab;
        [Min(1)] public int BulletCount = 100;
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
            new Color(1f, 0.15f, 0.2f), // 3 빨강
            new Color(0.75f, 0.3f, 1f), // 4 보라
        };

        [Header("Max Level Effect")]
        public GameObject MaxLevelEffectPrefab;
        [Min(0.01f)] public float EffectRadius = 1.2f;
        [Min(0.01f)] public float EffectDuration = 0.4f;
        [Min(0f)] public float EffectDamage = 5f;

        [Header("Idle Despawn")]
        [Min(0.1f)] public float IdleLifetime = 8f;
        [Min(0f)] public float IdleWarningTime = 1.5f;

        [Header("Enemies")]
        public GameObject EnemyPrefab;
        [Min(0.05f)] public float EnemySpawnInterval = 1f;
        [Min(0)] public int MaxEnemies = 30;

        [Header("Progression")]
        [Min(1)] public int FirstLevelUpXp = 5;
        [Min(0)] public int LevelUpXpGrowth = 3;

        class Baker : Baker<ArenaAuthoring>
        {
            public override void Bake(ArenaAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new ArenaBounds { HalfSize = authoring.ArenaSize * 0.5f });
                AddComponent(entity, new GameState());
                AddComponent(entity, new GameStats());
                AddComponent(entity, new PlayerProgress
                {
                    Level = 1,
                    NextLevelXp = authoring.FirstLevelUpXp,
                    FirstStep = authoring.FirstLevelUpXp,
                    StepGrowth = authoring.LevelUpXpGrowth,
                    // 총알·적 스폰과 다른 난수열을 쓰도록 시드를 비튼다
                    Random = Unity.Mathematics.Random.CreateFromIndex(authoring.Seed + 2),
                });
                AddComponent(entity, new LevelUpOffer { Selected = -1 });
                AddComponent(entity, new BaseStats());
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
                });

                AddComponent(entity, new BulletIdleSettings
                {
                    Lifetime = authoring.IdleLifetime,
                    WarningTime = authoring.IdleWarningTime,
                });

                if (authoring.EnemyPrefab != null)
                {
                    AddComponent(entity, new EnemySpawner
                    {
                        Prefab = GetEntity(authoring.EnemyPrefab, TransformUsageFlags.Dynamic),
                        Interval = authoring.EnemySpawnInterval,
                        MaxAlive = authoring.MaxEnemies,
                        Cooldown = authoring.EnemySpawnInterval,
                        // 총알 스폰과 다른 난수열을 쓰도록 시드를 비튼다
                        Random = Unity.Mathematics.Random.CreateFromIndex(authoring.Seed + 1),
                    });
                }

                if (authoring.MaxLevelEffectPrefab != null)
                {
                    AddComponent(entity, new MaxLevelEffect
                    {
                        Prefab = GetEntity(authoring.MaxLevelEffectPrefab, TransformUsageFlags.Dynamic),
                        Radius = authoring.EffectRadius,
                        Duration = authoring.EffectDuration,
                        Damage = authoring.EffectDamage,
                    });
                }

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
            static float4 LevelColor(ArenaAuthoring authoring, int index)
            {
                Color c = index < authoring.LevelColors.Length ? authoring.LevelColors[index] : Color.white;
                Color linear = c.linear;
                return new float4(linear.r, linear.g, linear.b, linear.a);
            }
        }
    }
}
