using MergeBullet.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace MergeBullet.Systems
{
    /// <summary>
    /// 스폰 간격마다, 살아 있는 적이 MaxAlive 미만이면 아레나 네 변 중 임의의 위치에 적을 하나 만든다.
    /// 적 종류는 스폰 테이블(EnemySpawnEntry)에서 난이도 진행도에 맞는 가중치로 고른다.
    /// 간격·최대 수·체력·속도 배율은 DifficultySystem이 생존 시간에 따라 바꿔 둔다.
    /// 생성은 ECB로 미뤄서 메인 스레드가 다른 잡을 기다리는 구조 변경(sync point)을 만들지 않는다.
    /// 따로 EliteInterval마다 엘리트(같은 종류를 크고 단단하게, 경험치 많이) 하나를 최대 수와 상관없이 만든다.
    /// 플레이어와 SafeDistance 이상 떨어진 곳에만 스폰한다. 플레이어 위치는 이번 프레임 이동 잡보다 먼저 읽어서
    /// (이전 프레임 잡은 이미 끝난 상태) 메인 스레드가 기다리지 않는다.
    /// </summary>
    [UpdateBefore(typeof(PlayerMoveSystem))]
    [BurstCompile]
    public partial struct EnemySpawnSystem : ISystem
    {
        EntityQuery _enemyQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _enemyQuery = SystemAPI.QueryBuilder().WithAll<Enemy>().Build();
            state.RequireForUpdate<EnemySpawner>();
            state.RequireForUpdate<EnemySpawnEntry>();
            state.RequireForUpdate<ArenaBounds>();
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            RefRW<EnemySpawner> spawner = SystemAPI.GetSingletonRW<EnemySpawner>();
            float deltaTime = SystemAPI.Time.DeltaTime;

            // 생성은 다음 프레임 시작(BeginSimulation)에 재생한다. 프레임 끝(EndSimulation)에 만들면 이번 프레임의
            // 위치 계산(TransformSystemGroup)이 이미 끝난 뒤라, 새 엔티티가 한 프레임 동안 프리팹 위치(원점)에 그려진다.
            EntityCommandBuffer ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            if (spawner.ValueRO.EliteInterval > 0f)
            {
                spawner.ValueRW.EliteCooldown -= deltaTime;
                if (spawner.ValueRO.EliteCooldown <= 0f)
                {
                    spawner.ValueRW.EliteCooldown += spawner.ValueRO.EliteInterval;
                    Spawn(ref state, spawner, ecb, elite: true);
                }
            }

            spawner.ValueRW.Cooldown -= deltaTime;
            if (spawner.ValueRO.Cooldown > 0f) return;
            spawner.ValueRW.Cooldown = math.max(spawner.ValueRO.Cooldown + spawner.ValueRO.Interval, 0f);

            if (_enemyQuery.CalculateEntityCount() >= spawner.ValueRO.MaxAlive) return;
            Spawn(ref state, spawner, ecb, elite: false);
        }

        // 엘리트 색: 원래 빨간 계열에 금색을 섞어 "특별한 적"으로 보이게
        static readonly float4 EliteTint = new float4(1f, 0.8f, 0.25f, 1f);
        const float EliteTintAmount = 0.45f;
        const float EliteContactDamage = 1.5f;
        const float EliteSpeed = 0.85f;

        void Spawn(ref SystemState state, RefRW<EnemySpawner> spawner, EntityCommandBuffer ecb, bool elite)
        {
            DynamicBuffer<EnemySpawnEntry> spawnTable = SystemAPI.GetSingletonBuffer<EnemySpawnEntry>(true);
            Entity prefab = PickPrefab(spawnTable, spawner.ValueRO.Progress, ref spawner.ValueRW.Random);
            // Enemy는 어떤 잡도 쓰지 않는 읽기 전용 데이터라, 메인 스레드에서 읽어도 잡을 기다리지 않는다
            Enemy stats = SystemAPI.GetComponent<Enemy>(prefab);

            // 난이도 배율은 스폰할 때 한 번만 곱한다 (이미 나온 적은 그대로)
            stats.MaxHealth *= spawner.ValueRO.HealthMultiplier;
            stats.MoveSpeed *= spawner.ValueRO.SpeedMultiplier;
            if (elite)
            {
                stats.Radius *= spawner.ValueRO.EliteSize;
                stats.MaxHealth *= spawner.ValueRO.EliteHealth;
                stats.ContactDamage *= EliteContactDamage;
                stats.MoveSpeed *= EliteSpeed;
            }

            float2 limit = SystemAPI.GetSingleton<ArenaBounds>().HalfSize - stats.Radius;
            float2 player = SystemAPI.TryGetSingleton(out PlayerPosition playerPosition) ? playerPosition.Value : float2.zero;
            bool hasView = SystemAPI.TryGetSingleton(out CameraView view);
            EnemySpawnUtility.SpawnRing(hasView, view, limit, stats.Radius, out float2 ringCenter, out float2 ringHalf);
            float2 position = EnemySpawnUtility.PickSpawnPoint(
                ref spawner.ValueRW.Random, limit, ringCenter, ringHalf, player, spawner.ValueRO.SafeDistance);

            Entity enemy = EnemySpawnUtility.RecordSpawn(ecb, prefab, stats, position);
            if (!elite) return;

            // 프리팹의 드롭·색은 어떤 잡도 쓰지 않아서 바로 읽어도 기다리지 않는다
            if (SystemAPI.HasComponent<XpDrop>(prefab))
            {
                XpDrop drop = SystemAPI.GetComponent<XpDrop>(prefab);
                drop.Value *= spawner.ValueRO.EliteXp;
                ecb.SetComponent(enemy, drop);
            }
            if (SystemAPI.HasComponent<BaseColor>(prefab))
            {
                // 피격 번쩍임이 끝나면 BaseColor로 돌아오므로 둘 다 바꾼다
                float4 color = math.lerp(SystemAPI.GetComponent<BaseColor>(prefab).Value, EliteTint, EliteTintAmount);
                ecb.SetComponent(enemy, new BaseColor { Value = color });
                ecb.SetComponent(enemy, new URPMaterialPropertyBaseColor { Value = color });
            }
        }

        // 가중치 비례로 하나를 고른다. 각 가중치는 진행도에 따라 Start → End로 보간된다
        static Entity PickPrefab(DynamicBuffer<EnemySpawnEntry> table, float progress, ref Random random)
        {
            float total = 0f;
            for (int i = 0; i < table.Length; i++)
                total += math.max(math.lerp(table[i].StartWeight, table[i].EndWeight, progress), 0f);
            if (total <= 0f) return table[0].Prefab;

            float roll = random.NextFloat(total);
            for (int i = 0; i < table.Length; i++)
            {
                roll -= math.max(math.lerp(table[i].StartWeight, table[i].EndWeight, progress), 0f);
                if (roll < 0f) return table[i].Prefab;
            }
            return table[table.Length - 1].Prefab; // 부동소수 오차로 끝까지 온 경우
        }
    }
}
