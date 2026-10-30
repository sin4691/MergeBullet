using System;
using MergeBullet.Components;
using Unity.Entities;

namespace MergeBullet.Systems
{
    /// <summary>실행 인자 확인. 빌드를 `MergeBullet.exe -benchmark`로 실행하면 벤치마크로 돈다.</summary>
    public static class BenchmarkArgs
    {
        public const string Flag = "-benchmark";

        public static bool Requested() => Array.IndexOf(Environment.GetCommandLineArgs(), Flag) >= 0;
    }

    /// <summary>
    /// 시작할 때 한 번, 벤치마크를 켤지 정한다. Inspector 체크박스나 실행 인자 -benchmark가 있으면
    /// BenchmarkSettings를 붙이고(다른 시스템들이 이걸 보고 측정용으로 동작), 적 스폰을 측정 조건으로 바꾸고,
    /// 프레임 상한(VSync)을 꺼서 모니터 주사율에 막히지 않는 실제 프레임 시간을 재게 한다.
    /// 실행 인자·Application·QualitySettings는 관리 API라 Burst를 쓰지 않는다 (한 번만 돈다).
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(ProgressionSystem))]
    public partial struct BenchmarkActivationSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BenchmarkConfig>();
        }

        public void OnUpdate(ref SystemState state)
        {
            state.Enabled = false; // 한 번만 확인한다

            BenchmarkConfig config = SystemAPI.GetSingleton<BenchmarkConfig>();
            if (!config.StartEnabled && !BenchmarkArgs.Requested()) return;

            Entity entity = SystemAPI.GetSingletonEntity<BenchmarkConfig>();
            state.EntityManager.AddComponentData(entity, config.Settings);

            // 적을 빠르게 최대 수까지 채운다 (워밍업 안에 다 차도록)
            if (SystemAPI.HasSingleton<EnemySpawner>())
            {
                RefRW<EnemySpawner> spawner = SystemAPI.GetSingletonRW<EnemySpawner>();
                spawner.ValueRW.MaxAlive = config.MaxEnemies;
                spawner.ValueRW.Interval = 0.02f;
            }

            UnityEngine.QualitySettings.vSyncCount = 0;
            UnityEngine.Application.targetFrameRate = -1;
            UnityEngine.Debug.Log($"[Benchmark] started: bullets {config.Settings.TargetBullets}, enemies {config.MaxEnemies}");
        }
    }
}
