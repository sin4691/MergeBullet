using MergeBullet.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace MergeBullet.Systems
{
    /// <summary>
    /// 벤치마크 모드에서 워밍업 뒤 정해진 시간 동안 프레임 시간을 모아, 끝나면 Console에 요약을 한 번 남긴다.
    /// 평균만 보면 가끔 튀는 프레임이 묻히므로 p95(느린 쪽 5% 경계)와 최대값도 같이 기록한다.
    /// UnityEngine.Time·Debug는 관리 API라 Burst를 쓰지 않는다 (프레임당 숫자 몇 개라 비용은 무시 가능).
    /// </summary>
    public partial struct BenchmarkRecorderSystem : ISystem
    {
        NativeList<float> _frameMs;
        EntityQuery _bulletQuery;
        EntityQuery _enemyQuery;
        double _elapsed;
        long _bulletSum;
        long _enemySum;
        bool _done;
        int _targetBullets;
        int _maxEnemies;

        public void OnCreate(ref SystemState state)
        {
            _frameMs = new NativeList<float>(2048, Allocator.Persistent);
            _bulletQuery = SystemAPI.QueryBuilder().WithAll<Bullet>().Build();
            _enemyQuery = SystemAPI.QueryBuilder().WithAll<Enemy>().Build();
            state.RequireForUpdate<BenchmarkSettings>();
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_frameMs.IsCreated) _frameMs.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_done) return;

            BenchmarkSettings settings = SystemAPI.GetSingleton<BenchmarkSettings>();
            // timeScale(일시정지)와 무관한 실제 프레임 간격
            float frameSeconds = UnityEngine.Time.unscaledDeltaTime;
            _elapsed += frameSeconds;
            if (_elapsed < settings.WarmupSeconds) return;

            _frameMs.Add(frameSeconds * 1000f);
            _bulletSum += _bulletQuery.CalculateEntityCount();
            _enemySum += _enemyQuery.CalculateEntityCount();
            if (_elapsed < settings.WarmupSeconds + settings.SampleSeconds) return;

            _done = true;
            _targetBullets = settings.TargetBullets;
            _maxEnemies = SystemAPI.TryGetSingleton(out EnemySpawner spawner) ? spawner.MaxAlive : 0;
            LogSummary();
        }

        void LogSummary()
        {
            int frames = _frameMs.Length;
            float total = 0f;
            float max = 0f;
            for (int i = 0; i < frames; i++)
            {
                total += _frameMs[i];
                max = math.max(max, _frameMs[i]);
            }

            _frameMs.Sort();
            float average = total / frames;
            float p95 = _frameMs[math.min((int)(frames * 0.95f), frames - 1)];

            // 목표 수를 함께 적어서, 부하가 설계대로 유지됐는지 결과만 보고 확인할 수 있게 한다
            UnityEngine.Debug.Log(
                $"[Benchmark] {frames} frames | avg {average:0.00} ms ({1000f / average:0} FPS) | " +
                $"p95 {p95:0.00} ms | max {max:0.00} ms | " +
                $"bullets ~{_bulletSum / frames} (target {_targetBullets}) | enemies ~{_enemySum / frames} (max {_maxEnemies})");
        }
    }
}
