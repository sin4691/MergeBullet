using MergeBullet.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace MergeBullet.Systems
{
    /// <summary>
    /// 생존 시간 Interval초마다 보스를 하나씩 만든다 (3분, 6분 …). 나올 때마다 체력이 HealthGrowthPerBoss만큼 더 높고,
    /// 지금 난이도의 체력·속도 배율도 일반 적처럼 곱한다. 위치는 일반 적과 같은 규칙(플레이어에서 떨어진 가장자리)이다.
    /// 일반 적 스폰처럼 플레이어 이동 잡보다 먼저 돌아서 플레이어 위치를 읽어도 기다리지 않는다.
    /// </summary>
    [UpdateBefore(typeof(PlayerMoveSystem))]
    [UpdateAfter(typeof(DifficultySystem))]
    [BurstCompile]
    public partial struct BossSpawnSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BossSpawner>();
            state.RequireForUpdate<EnemySpawner>();
            state.RequireForUpdate<GameStats>();
            state.RequireForUpdate<ArenaBounds>();
            state.RequireForUpdate<WallBox>();
            state.RequireForUpdate<RoomState>();
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 벤치마크는 고정된 조건으로 재야 하므로 보스를 내지 않는다
            if (SystemAPI.HasSingleton<BenchmarkSettings>()) return;

            RefRW<BossSpawner> bossSpawner = SystemAPI.GetSingletonRW<BossSpawner>();
            int due = (int)(SystemAPI.GetSingleton<GameStats>().SurvivalTime / bossSpawner.ValueRO.Interval);
            if (due <= bossSpawner.ValueRO.Spawned) return;

            int bossIndex = bossSpawner.ValueRO.Spawned;
            bossSpawner.ValueRW.Spawned++;

            // 일반 적 스포너의 난이도 배율·난수·안전 거리를 같이 쓴다
            RefRW<EnemySpawner> enemySpawner = SystemAPI.GetSingletonRW<EnemySpawner>();
            Entity prefab = bossSpawner.ValueRO.Prefab;
            Enemy stats = SystemAPI.GetComponent<Enemy>(prefab);
            stats.MaxHealth *= (1f + bossSpawner.ValueRO.HealthGrowthPerBoss * bossIndex) * enemySpawner.ValueRO.HealthMultiplier;
            stats.MoveSpeed *= enemySpawner.ValueRO.SpeedMultiplier;

            ArenaBounds arena = SystemAPI.GetSingleton<ArenaBounds>();
            float2 limit = arena.HalfSize - stats.Radius;
            float2 player = SystemAPI.TryGetSingleton(out PlayerPosition playerPosition) ? playerPosition.Value : float2.zero;
            bool hasView = SystemAPI.TryGetSingleton(out CameraView view);
            EnemySpawnUtility.SpawnRing(hasView, view, arena.Center, limit, stats.Radius, out float2 ringCenter, out float2 ringHalf);
            float2 position = EnemySpawnUtility.PickSpawnPoint(
                ref enemySpawner.ValueRW.Random, arena.Center, limit, ringCenter, ringHalf, player, enemySpawner.ValueRO.SafeDistance,
                stats.Radius, SystemAPI.GetSingletonBuffer<WallBox>(true).AsNativeArray(),
                SystemAPI.GetSingletonBuffer<RoomState>(true).AsNativeArray());

            EntityCommandBuffer ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            EnemySpawnUtility.RecordSpawn(ecb, prefab, stats, position);
        }
    }
}
