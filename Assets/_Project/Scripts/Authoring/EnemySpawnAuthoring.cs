using System;
using System.Collections.Generic;
using MergeBullet.Components;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace MergeBullet.Authoring
{
    /// <summary>
    /// 적 스폰 규칙: 스폰 테이블(EnemySpawnEntry), 스폰 간격·최대 수·엘리트(EnemySpawner), 난이도 진행(DifficultySettings), 보스(BossSpawner).
    /// 보스도 여기 두는 이유: 적 공간 해시의 칸 크기(MaxEnemyRadius)가 보스까지 포함한 가장 큰 적 반지름이어야 해서.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemySpawnAuthoring : MonoBehaviour
    {
        // 기본 적. 처음부터 끝까지 가중치 1로 나온다
        public GameObject EnemyPrefab;
        // 추가 적 종류. 난이도 진행도에 따라 가중치가 Start → End로 바뀐다
        public List<EnemyVariant> EnemyVariants = new List<EnemyVariant>();
        [Min(0.05f)] public float EnemySpawnInterval = 1f;
        [Min(0)] public int MaxEnemies = 30;
        [Min(0f)] public float EnemySafeSpawnDistance = 4f;
        public uint Seed = 12346;

        [Header("Elite")]
        // 엘리트(중간 보스): 이 간격마다 일반 적 하나를 크고 단단하게. 잡으면 경험치를 많이 준다
        [Min(0f)] public float EliteInterval = 45f;
        [Min(1f)] public float EliteSize = 1.7f;
        [Min(1f)] public float EliteHealth = 6f;
        [Min(1)] public int EliteXp = 5;

        [Header("Boss")]
        public GameObject BossPrefab;
        [Min(10f)] public float BossInterval = 180f;
        [Min(0f)] public float BossHealthGrowth = 0.5f;

        [Header("Difficulty (위 스폰 값에서 시작해 RampSeconds 동안 아래 값까지 오른다)")]
        [Min(1f)] public float DifficultyRampSeconds = 300f;
        [Min(0.05f)] public float LateSpawnInterval = 0.15f;
        [Min(0)] public int LateMaxEnemies = 200;
        [Min(1f)] public float LateHealthMultiplier = 4f;
        [Min(1f)] public float LateSpeedMultiplier = 1.5f;

        class Baker : Baker<EnemySpawnAuthoring>
        {
            public override void Bake(EnemySpawnAuthoring authoring)
            {
                if (authoring.EnemyPrefab == null) return;

                Entity entity = GetEntity(TransformUsageFlags.None);
                var spawnTable = new List<EnemySpawnEntry>();
                float maxRadius = AddSpawnEntry(spawnTable, authoring.EnemyPrefab, 1f, 1f);
                foreach (EnemyVariant variant in authoring.EnemyVariants)
                {
                    if (variant.Prefab == null) continue;
                    maxRadius = math.max(maxRadius, AddSpawnEntry(spawnTable, variant.Prefab, variant.StartWeight, variant.EndWeight));
                }
                // 엘리트는 같은 적을 키워서 나오므로 공간 해시 기준 반지름에도 그 배율을 넣는다
                maxRadius *= math.max(authoring.EliteSize, 1f);

                // 보스도 같은 공간 해시로 충돌하므로 가장 큰 반지름 계산에 넣어야 한다 (보스가 가장 크다)
                if (authoring.BossPrefab != null)
                {
                    EnemyAuthoring boss = GetComponent<EnemyAuthoring>(authoring.BossPrefab);
                    if (boss != null) maxRadius = math.max(maxRadius, boss.Radius);

                    AddComponent(entity, new BossSpawner
                    {
                        Prefab = GetEntity(authoring.BossPrefab, TransformUsageFlags.Dynamic),
                        Interval = authoring.BossInterval,
                        HealthGrowthPerBoss = authoring.BossHealthGrowth,
                    });
                }

                AddComponent(entity, new EnemySpawner
                {
                    MaxEnemyRadius = maxRadius,
                    EliteInterval = authoring.EliteInterval,
                    EliteCooldown = authoring.EliteInterval,
                    EliteSize = authoring.EliteSize,
                    EliteHealth = authoring.EliteHealth,
                    EliteXp = authoring.EliteXp,
                    Interval = authoring.EnemySpawnInterval,
                    MaxAlive = authoring.MaxEnemies,
                    Cooldown = authoring.EnemySpawnInterval,
                    HealthMultiplier = 1f,
                    SpeedMultiplier = 1f,
                    SafeDistance = authoring.EnemySafeSpawnDistance,
                    Random = Unity.Mathematics.Random.CreateFromIndex(authoring.Seed),
                });
                AddComponent(entity, new DifficultySettings
                {
                    RampSeconds = authoring.DifficultyRampSeconds,
                    StartSpawnInterval = authoring.EnemySpawnInterval,
                    EndSpawnInterval = authoring.LateSpawnInterval,
                    StartMaxEnemies = authoring.MaxEnemies,
                    EndMaxEnemies = authoring.LateMaxEnemies,
                    EndHealthMultiplier = authoring.LateHealthMultiplier,
                    EndSpeedMultiplier = authoring.LateSpeedMultiplier,
                });

                // 버퍼는 다른 AddComponent가 끝난 뒤에 채운다
                DynamicBuffer<EnemySpawnEntry> buffer = AddBuffer<EnemySpawnEntry>(entity);
                foreach (EnemySpawnEntry entry in spawnTable) buffer.Add(entry);
            }

            // 스폰 테이블에 한 줄 추가하고, 그 적의 반지름을 돌려준다 (가장 큰 적 반지름 계산용).
            // 프리팹의 EnemyAuthoring을 읽으므로 그 값이 바뀌면 다시 베이킹된다
            float AddSpawnEntry(List<EnemySpawnEntry> table, GameObject prefab, float startWeight, float endWeight)
            {
                table.Add(new EnemySpawnEntry
                {
                    Prefab = GetEntity(prefab, TransformUsageFlags.Dynamic),
                    StartWeight = startWeight,
                    EndWeight = endWeight,
                });
                EnemyAuthoring enemy = GetComponent<EnemyAuthoring>(prefab);
                return enemy != null ? enemy.Radius : 0f;
            }
        }
    }

    /// <summary>추가 적 종류 한 줄 (Inspector 편집용). 난이도 진행도 0 → 1에 따라 가중치가 Start → End.</summary>
    [Serializable]
    public class EnemyVariant
    {
        public GameObject Prefab;
        [Min(0f)] public float StartWeight;
        [Min(0f)] public float EndWeight = 1f;
    }
}
