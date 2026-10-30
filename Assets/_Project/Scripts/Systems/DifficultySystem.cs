using MergeBullet.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace MergeBullet.Systems
{
    /// <summary>
    /// 생존 시간에 따라 적 스폰 조건(간격, 최대 수, 체력·속도 배율, 적 종류 비율)을 올린다.
    /// 스포너 값만 바꾸고 실제 적용은 EnemySpawnSystem이 하므로, 이미 나와 있는 적은 그대로고 새로 나오는 적부터 강해진다.
    /// </summary>
    [UpdateBefore(typeof(EnemySpawnSystem))]
    [BurstCompile]
    public partial struct DifficultySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DifficultySettings>();
            state.RequireForUpdate<EnemySpawner>();
            state.RequireForUpdate<GameStats>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 벤치마크는 고정된 조건으로 재야 하므로 난이도를 올리지 않는다
            if (SystemAPI.HasSingleton<BenchmarkSettings>()) return;

            DifficultySettings difficulty = SystemAPI.GetSingleton<DifficultySettings>();
            float t = math.saturate(SystemAPI.GetSingleton<GameStats>().SurvivalTime / difficulty.RampSeconds);

            RefRW<EnemySpawner> spawner = SystemAPI.GetSingletonRW<EnemySpawner>();
            spawner.ValueRW.Interval = math.lerp(difficulty.StartSpawnInterval, difficulty.EndSpawnInterval, t);
            spawner.ValueRW.MaxAlive = (int)math.round(math.lerp(difficulty.StartMaxEnemies, difficulty.EndMaxEnemies, t));
            spawner.ValueRW.HealthMultiplier = math.lerp(1f, difficulty.EndHealthMultiplier, t);
            spawner.ValueRW.SpeedMultiplier = math.lerp(1f, difficulty.EndSpeedMultiplier, t);
            spawner.ValueRW.Progress = t;
        }
    }
}
