using System;
using System.Collections.Generic;
using MergeBullet.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;

namespace MergeBullet.Systems
{
    /// <summary>
    /// 벤치마크 모드에서 워밍업 뒤 정해진 시간 동안 프레임 시간을 모아, 끝나면 Console과 파일에 요약을 한 번 남긴다.
    /// 빌드를 -benchmark로 실행했으면 기록 후 저절로 종료한다 (결과: persistentDataPath/benchmark.txt).
    /// - 프레임 전체: 평균만 보면 가끔 튀는 프레임이 묻히므로 p95(느린 쪽 5% 경계)와 최대값도 같이 기록한다.
    /// - 시스템·잡별: 이 프로젝트의 시스템·잡 이름과 엔진 주요 단계(PlayerLoop, EditorLoop, 렌더링 등)가 들어간
    ///   Profiler 마커를 찾아 프레임당 평균 시간을 비싼 순으로 남긴다.
    ///   잡은 여러 워커 스레드의 시간을 합친 값(CPU 사용량)이라 프레임 시간과 직접 더해지지는 않는다.
    /// UnityEngine.Time·Debug·Profiler 조회는 관리 API라 Burst를 쓰지 않는다 (프레임당 숫자 몇 개라 비용은 무시 가능).
    /// </summary>
    public partial struct BenchmarkRecorderSystem : ISystem
    {
        const int TopMarkerCount = 20;

        NativeList<float> _frameMs;
        NativeList<ProfilerRecorder> _recorders;
        NativeList<FixedString128Bytes> _markerNames;
        NativeList<double> _markerTotalMs;
        EntityQuery _bulletQuery;
        EntityQuery _enemyQuery;
        double _elapsed;
        double _sampleEnd;
        long _bulletSum;
        long _enemySum;
        bool _sampling;
        bool _done;

        public void OnCreate(ref SystemState state)
        {
            _frameMs = new NativeList<float>(2048, Allocator.Persistent);
            _recorders = new NativeList<ProfilerRecorder>(64, Allocator.Persistent);
            _markerNames = new NativeList<FixedString128Bytes>(64, Allocator.Persistent);
            _markerTotalMs = new NativeList<double>(64, Allocator.Persistent);
            _bulletQuery = SystemAPI.QueryBuilder().WithAll<Bullet>().Build();
            _enemyQuery = SystemAPI.QueryBuilder().WithAll<Enemy>().Build();
            state.RequireForUpdate<BenchmarkSettings>();
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_recorders.IsCreated)
            {
                for (int i = 0; i < _recorders.Length; i++) _recorders[i].Dispose();
                _recorders.Dispose();
            }
            if (_frameMs.IsCreated) _frameMs.Dispose();
            if (_markerNames.IsCreated) _markerNames.Dispose();
            if (_markerTotalMs.IsCreated) _markerTotalMs.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_done) return;

            BenchmarkSettings settings = SystemAPI.GetSingleton<BenchmarkSettings>();
            // timeScale(일시정지)와 무관한 실제 프레임 간격
            float frameSeconds = UnityEngine.Time.unscaledDeltaTime;
            _elapsed += frameSeconds;
            if (_elapsed < settings.WarmupSeconds) return;

            // 에디터 첫 실행은 Burst 컴파일 동안 느려서 워밍업 시간 안에 적이 다 안 찰 수 있다.
            // 적이 최대 수에 도달한 뒤에 측정을 시작한다 (너무 오래 걸리면 워밍업 3배 시점에 그냥 시작)
            int maxEnemies = SystemAPI.TryGetSingleton(out EnemySpawner spawner) ? spawner.MaxAlive : 0;
            if (!_sampling && _enemyQuery.CalculateEntityCount() < maxEnemies && _elapsed < settings.WarmupSeconds * 3f)
                return;

            // 마커 목록은 시스템·잡이 한 번씩 실행된 뒤(워밍업 이후)에 만들어야 빠짐없이 잡힌다.
            // 측정 끝 시각도 여기서 정한다 (워밍업이 길어져도 측정 시간은 항상 SampleSeconds)
            if (!_sampling)
            {
                StartMarkerRecorders();
                _sampling = true;
                _sampleEnd = _elapsed + settings.SampleSeconds;
                return;
            }

            _frameMs.Add(frameSeconds * 1000f);
            _bulletSum += _bulletQuery.CalculateEntityCount();
            _enemySum += _enemyQuery.CalculateEntityCount();
            for (int i = 0; i < _recorders.Length; i++)
                _markerTotalMs[i] += _recorders[i].LastValue / 1_000_000.0; // ns → ms

            if (_elapsed < _sampleEnd) return;

            _done = true;
            LogSummary(settings.TargetBullets, maxEnemies);
        }

        // 이 프로젝트의 시스템·잡 이름 + 엔진 쪽 주요 단계 이름이 들어간 마커만 기록한다
        void StartMarkerRecorders()
        {
            var keywords = new List<string>
            {
                "BeginSimulationEntityCommandBufferSystem",
                "EndSimulationEntityCommandBufferSystem",
                "TransformSystemGroup",
                "EntitiesGraphicsSystem",
                // 우리 코드 밖에서 프레임 시간이 어디에 쓰이는지 보기 위한 엔진·에디터 단계
                "PlayerLoop",
                "EditorLoop",
                "WaitForPresent",
                "WaitForTargetFPS",
                "DoRenderLoop",
                "UIElementsRuntime",
            };
            foreach (Type type in typeof(BenchmarkRecorderSystem).Assembly.GetTypes())
            {
                if (type.IsValueType && (type.Name.EndsWith("System") || type.Name.EndsWith("Job")))
                    keywords.Add(type.Name);
            }

            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            foreach (ProfilerRecorderHandle handle in handles)
            {
                string name = ProfilerRecorderHandle.GetDescription(handle).Name;
                if (name.Length >= FixedString128Bytes.UTF8MaxLengthInBytes || !ContainsAny(name, keywords)) continue;

                // 잡은 여러 워커에서 여러 번 실행되므로 한 프레임 안의 샘플을 모두 더한다.
                // Default에는 StartImmediately가 없어서 직접 넣어야 기록이 시작된다 (빠뜨리면 전부 0)
                _recorders.Add(new ProfilerRecorder(handle, 1,
                    ProfilerRecorderOptions.SumAllSamplesInFrame | ProfilerRecorderOptions.StartImmediately));
                _markerNames.Add(new FixedString128Bytes(name));
                _markerTotalMs.Add(0.0);
            }
        }

        static bool ContainsAny(string name, List<string> keywords)
        {
            foreach (string keyword in keywords)
                if (name.Contains(keyword)) return true;
            return false;
        }

        void LogSummary(int targetBullets, int maxEnemies)
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
            var log = new System.Text.StringBuilder();
            log.AppendLine($"[Benchmark] {DateTime.Now:yyyy-MM-dd HH:mm} | {BuildKind()} | {UnityEngine.Screen.width}x{UnityEngine.Screen.height}");
            log.Append($"[Benchmark] {frames} frames | avg {average:0.00} ms ({1000f / average:0} FPS) | ");
            log.Append($"p95 {p95:0.00} ms | max {max:0.00} ms | ");
            log.AppendLine($"bullets ~{_bulletSum / frames} (target {targetBullets}) | enemies ~{_enemySum / frames} (max {maxEnemies})");

            // 구조체 메서드 안의 람다는 필드(this)를 잡을 수 없어서 지역 변수로 받아 둔다
            NativeList<double> totals = _markerTotalMs;
            var order = new List<int>();
            for (int i = 0; i < _markerNames.Length; i++) order.Add(i);
            order.Sort((a, b) => totals[b].CompareTo(totals[a]));

            // 릴리스 빌드는 Profiler 마커 시간을 모으지 않아 전부 0이다 → 0인 항목은 빼고, 하나도 없으면 그렇다고 적는다
            log.AppendLine($"[Benchmark] top {TopMarkerCount} markers (ms per frame, jobs = sum of worker threads):");
            int shown = 0;
            for (int i = 0; i < order.Count && shown < TopMarkerCount; i++)
            {
                int index = order[i];
                if (_markerTotalMs[index] <= 0.0) break;
                log.AppendLine($"  {_markerTotalMs[index] / frames,7:0.000}  {_markerNames[index]}");
                shown++;
            }
            if (shown == 0) log.AppendLine("  (no marker data - release builds do not record Profiler markers)");

            string text = log.ToString();
            UnityEngine.Debug.Log(text);
            WriteResultFile(text);

            // 빌드를 측정용으로 실행했으면 결과만 남기고 끝낸다 (에디터 Play는 그대로 둔다)
            if (!UnityEngine.Application.isEditor && BenchmarkArgs.Requested())
                UnityEngine.Application.Quit();
        }

        static string BuildKind()
        {
            if (UnityEngine.Application.isEditor) return "Editor";
            return UnityEngine.Debug.isDebugBuild ? "Development build" : "Release build";
        }

        // 빌드에서는 Console이 없으니 파일로도 남긴다. 측정할 때마다 아래에 이어 붙인다
        static void WriteResultFile(string text)
        {
            string path = System.IO.Path.Combine(UnityEngine.Application.persistentDataPath, "benchmark.txt");
            try
            {
                System.IO.File.AppendAllText(path, text + Environment.NewLine);
                UnityEngine.Debug.Log($"[Benchmark] saved to {path}");
            }
            catch (System.IO.IOException e)
            {
                UnityEngine.Debug.LogWarning($"[Benchmark] could not write {path}: {e.Message}");
            }
        }
    }
}
