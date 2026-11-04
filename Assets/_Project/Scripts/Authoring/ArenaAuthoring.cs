using System;
using System.Collections.Generic;
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
        // 맵 전체 크기. 화면(ViewSize)보다 크면 카메라가 플레이어를 따라간다.
        // 48x27은 총알이 멀리 흩어져 합성이 너무 드물어서, 총알이 금방 돌아오는 1.5배로 둔다
        public Vector2 ArenaSize = new Vector2(24f, 13.5f);
        public GameObject BulletPrefab;
        [Min(1)] public int BulletCount = 100;
        [Min(0.01f)] public float BulletRadius = 0.12f;
        public float BulletSpeed = 5f;
        public uint Seed = 12345;

        [Header("Camera")]
        // 어떤 기기에서도 이만큼은 보인다 (화면비가 다르면 한쪽이 더 보임). 화면 모드일 때 총알 벽도 이 크기.
        // 시작은 가깝게, 난이도가 오를수록(Difficulty RampSeconds) LateViewSize까지 넓어진다
        public Vector2 ViewSize = new Vector2(14f, 7.875f);
        // 맵(24x13.5) 안에 들어오는 선에서 멀리 (16:9 비율 유지)
        public Vector2 LateViewSize = new Vector2(21f, 11.8125f);
        [Min(0.1f)] public float CameraFollowSharpness = 8f;
        // 켜면 총알이 화면 가장자리에서, 끄면 아레나 벽에서 튕긴다.
        // 화면 벽은 합성·폭발이 화면 끝에 몰려서, 적이 벽이 되는 느낌이 안 나 기본은 끈다
        public bool BulletsBounceOnView;

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

        [Header("Max Level Effect")]
        public GameObject MaxLevelEffectPrefab;
        [Min(0.01f)] public float EffectRadius = 1.2f;
        [Min(0.01f)] public float EffectDuration = 0.4f;
        [Min(0f)] public float EffectDamage = 5f;
        // Storm 캐릭터의 연쇄 번개 (어느 효과를 쓸지는 게임 시작 시 캐릭터로 정해진다)
        [Min(1)] public int LightningChainCount = 5;
        [Min(0.1f)] public float LightningChainRange = 4f;
        [Min(0f)] public float LightningDamage = 4f;

        [Header("Idle Despawn")]
        // 8초면 화면에 총알이 오래 남아 합성이 쉬웠다
        [Min(0.1f)] public float IdleLifetime = 5f;
        [Min(0f)] public float IdleWarningTime = 1.5f;

        [Header("Enemies")]
        // 기본 적. 처음부터 끝까지 가중치 1로 나온다
        public GameObject EnemyPrefab;
        // 추가 적 종류. 난이도 진행도에 따라 가중치가 Start → End로 바뀐다
        public List<EnemyVariant> EnemyVariants = new List<EnemyVariant>();

        [Header("Boss")]
        public GameObject BossPrefab;
        [Min(10f)] public float BossInterval = 180f;
        [Min(0f)] public float BossHealthGrowth = 0.5f;
        [Min(0.05f)] public float EnemySpawnInterval = 1f;
        [Min(0)] public int MaxEnemies = 30;
        [Min(0f)] public float EnemySafeSpawnDistance = 4f;
        // 엘리트(중간 보스): 이 간격마다 일반 적 하나를 크고 단단하게. 잡으면 경험치를 많이 준다
        [Min(0f)] public float EliteInterval = 45f;
        [Min(1f)] public float EliteSize = 1.7f;
        [Min(1f)] public float EliteHealth = 6f;
        [Min(1)] public int EliteXp = 5;

        [Header("Difficulty (위 스폰 값에서 시작해 RampSeconds 동안 아래 값까지 오른다)")]
        [Min(1f)] public float DifficultyRampSeconds = 300f;
        [Min(0.05f)] public float LateSpawnInterval = 0.15f;
        [Min(0)] public int LateMaxEnemies = 200;
        [Min(1f)] public float LateHealthMultiplier = 4f;
        [Min(1f)] public float LateSpeedMultiplier = 1.5f;

        [Header("Benchmark (에디터에서 측정할 때만 켠다. 빌드는 실행 인자 -benchmark)")]
        public bool BenchmarkMode;
        // 레벨 1 총알 1000개 ≈ 아레나 면적의 31%. 5000개는 면적의 157%라 겹쳐서 바로 합성돼 유지가 안 된다
        [Min(0)] public int BenchmarkBullets = 1000;
        [Min(0)] public int BenchmarkEnemies = 300;
        [Min(0f)] public float BenchmarkWarmupSeconds = 8f;
        [Min(1f)] public float BenchmarkSampleSeconds = 10f;

        [Header("Run")]
        // 30분 버티면 클리어. 끝이 있어야 판마다 보상을 받고 다음 판으로 넘어간다
        [Min(0f)] public float RunDurationSeconds = 1800f;

        [Header("Progression")]
        [Min(1)] public int FirstLevelUpXp = 5;
        [Min(0)] public int LevelUpXpGrowth = 3;

        class Baker : Baker<ArenaAuthoring>
        {
            public override void Bake(ArenaAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new ArenaBounds { HalfSize = authoring.ArenaSize * 0.5f });
                AddComponent(entity, new CameraView
                {
                    HalfSize = authoring.ViewSize * 0.5f,
                    StartHalfSize = authoring.ViewSize * 0.5f,
                    LateHalfSize = authoring.LateViewSize * 0.5f,
                    FollowSharpness = authoring.CameraFollowSharpness,
                    BulletsBounceOnView = authoring.BulletsBounceOnView,
                });
                AddComponent(entity, new GameState());
                AddComponent(entity, new GameStats());
                AddComponent(entity, new Augments());
                AddComponent(entity, new RunRules { DurationSeconds = authoring.RunDurationSeconds });
                // 연출 이벤트 큐: 로직 시스템이 쓰고 EffectPresenter가 읽는다
                AddComponent(entity, new EffectQueue());
                AddBuffer<EffectEvent>(entity);
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
                    // 레벨 5 증강 전용 색: 흰색(레벨 4) 다음이라 눈에 띄는 청록
                    Level5Color = authoring.LevelColors.Length > 4 ? LevelColor(authoring, 4) : new float4(0.35f, 0.95f, 1f, 1f),
                });

                AddComponent(entity, new BulletIdleSettings
                {
                    Lifetime = authoring.IdleLifetime,
                    WarningTime = authoring.IdleWarningTime,
                });

                if (authoring.EnemyPrefab != null)
                {
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
                        // 총알 스폰과 다른 난수열을 쓰도록 시드를 비튼다
                        Random = Unity.Mathematics.Random.CreateFromIndex(authoring.Seed + 1),
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

                if (authoring.MaxLevelEffectPrefab != null)
                {
                    AddComponent(entity, new MaxLevelEffect
                    {
                        Prefab = GetEntity(authoring.MaxLevelEffectPrefab, TransformUsageFlags.Dynamic),
                        Radius = authoring.EffectRadius,
                        Duration = authoring.EffectDuration,
                        Damage = authoring.EffectDamage,
                        Kind = MaxLevelEffectKind.Explosion,
                        ChainCount = authoring.LightningChainCount,
                        ChainRange = authoring.LightningChainRange,
                        ChainDamage = authoring.LightningDamage,
                    });
                }

                if (authoring.BulletPrefab == null) return;

                // 벤치마크 조건은 항상 넣어 두고, 켜는 건 실행 시점에 정한다 (체크박스 또는 실행 인자 -benchmark)
                AddComponent(entity, new BenchmarkConfig
                {
                    StartEnabled = authoring.BenchmarkMode,
                    MaxEnemies = authoring.BenchmarkEnemies,
                    Settings = new BenchmarkSettings
                    {
                        BulletPrefab = GetEntity(authoring.BulletPrefab, TransformUsageFlags.Dynamic),
                        TargetBullets = authoring.BenchmarkBullets,
                        BulletSpeed = authoring.BulletSpeed,
                        WarmupSeconds = authoring.BenchmarkWarmupSeconds,
                        SampleSeconds = authoring.BenchmarkSampleSeconds,
                        Random = Unity.Mathematics.Random.CreateFromIndex(authoring.Seed + 3),
                    },
                });
                AddComponent(entity, new BulletSpawner
                {
                    Prefab = GetEntity(authoring.BulletPrefab, TransformUsageFlags.Dynamic),
                    Count = authoring.BulletCount,
                    Speed = authoring.BulletSpeed,
                    Seed = authoring.Seed,
                });
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

            // 머티리얼 프로퍼티 오버라이드는 셰이더에 그대로 들어가므로 선형 색공간 값으로 넘긴다
            static float4 LevelColor(ArenaAuthoring authoring, int index)
            {
                Color c = index < authoring.LevelColors.Length ? authoring.LevelColors[index] : Color.white;
                Color linear = c.linear;
                return new float4(linear.r, linear.g, linear.b, linear.a);
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
