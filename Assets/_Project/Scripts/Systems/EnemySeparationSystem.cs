using MergeBullet.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace MergeBullet.Systems
{
    /// <summary>
    /// 겹친 적끼리 서로 밀어내서 한 덩어리로 뭉치지 않게 한다.
    /// 1) 수집: 이동이 끝난 적 위치를 배열(스냅샷)과 공간 해시에 넣는다 (병렬, 충돌 시스템과 같은 잡 재사용)
    /// 2) 분리: 각 적이 주변 9칸의 적과 겹친 만큼 바깥으로 밀린다 (병렬)
    /// 분리 단계는 스냅샷만 읽고 자기 위치만 쓰므로, 처리 순서와 상관없이 결과가 같다.
    /// </summary>
    [UpdateAfter(typeof(EnemyChaseSystem))]
    [UpdateBefore(typeof(BulletEnemyCollisionSystem))]
    [BurstCompile]
    public partial struct EnemySeparationSystem : ISystem
    {
        EntityQuery _enemyQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _enemyQuery = SystemAPI.QueryBuilder().WithAll<Enemy, LocalTransform>().Build();
            state.RequireForUpdate(_enemyQuery);
            state.RequireForUpdate<EnemySpawner>();
            state.RequireForUpdate<ArenaBounds>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float enemyRadius = SystemAPI.GetComponent<Enemy>(SystemAPI.GetSingleton<EnemySpawner>().Prefab).Radius;

            // 셀 크기 = 적 지름 → 겹칠 수 있는 이웃은 항상 주변 9칸 안에 있다
            float cellSize = 2f * enemyRadius;
            int count = _enemyQuery.CalculateEntityCount();

            var entities = new NativeArray<Entity>(count, Allocator.TempJob);
            var positions = new NativeArray<float2>(count, Allocator.TempJob);
            var radii = new NativeArray<float>(count, Allocator.TempJob);
            var grid = new NativeParallelMultiHashMap<int, int>(count, Allocator.TempJob);

            JobHandle collect = new CollectEnemiesJob
            {
                CellSize = cellSize,
                Entities = entities,
                Positions = positions,
                Radii = radii,
                Grid = grid.AsParallelWriter(),
            }.ScheduleParallel(_enemyQuery, state.Dependency);

            JobHandle separate = new SeparateEnemiesJob
            {
                CellSize = cellSize,
                ArenaHalfSize = SystemAPI.GetSingleton<ArenaBounds>().HalfSize,
                Positions = positions,
                Radii = radii,
                Grid = grid,
            }.ScheduleParallel(_enemyQuery, collect);

            entities.Dispose(separate);
            positions.Dispose(separate);
            radii.Dispose(separate);
            grid.Dispose(separate);

            state.Dependency = separate;
        }
    }

    [BurstCompile]
    partial struct SeparateEnemiesJob : IJobEntity
    {
        // 겹친 거리를 한 프레임에 전부 풀면 여럿이 엉켰을 때 떨린다 → 일부만 풀고 다음 프레임에 이어서 푼다
        const float Stiffness = 0.5f;

        public float CellSize;
        public float2 ArenaHalfSize;
        [ReadOnly] public NativeArray<float2> Positions;
        [ReadOnly] public NativeArray<float> Radii;
        [ReadOnly] public NativeParallelMultiHashMap<int, int> Grid;

        void Execute([EntityIndexInQuery] int index, ref LocalTransform transform, in Enemy enemy)
        {
            float2 self = Positions[index];
            int2 cell = SpatialHash.CellOf(self, CellSize);
            float2 push = float2.zero;

            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int key = SpatialHash.KeyOf(cell + new int2(dx, dy));
                if (!Grid.TryGetFirstValue(key, out int j, out var it)) continue;
                do
                {
                    if (j == index) continue;

                    float2 away = self - Positions[j];
                    float minDistance = enemy.Radius + Radii[j];
                    float distanceSq = math.lengthsq(away);
                    if (distanceSq >= minDistance * minDistance) continue;

                    float distance = math.sqrt(distanceSq);
                    // 완전히 같은 자리면 방향이 없으므로 인덱스로 정한 각도로 밀어 둘이 서로 반대로 갈라지게 한다
                    float2 direction = distance > 1e-5f
                        ? away / distance
                        : AngleDirection(index < j ? 0f : math.PI);

                    // 겹친 거리의 절반씩 서로 반대로 밀리면 둘이 딱 맞닿는다
                    push += direction * ((minDistance - distance) * 0.5f);
                } while (Grid.TryGetNextValue(out j, ref it));
            }

            if (math.all(push == float2.zero)) return;

            float2 limit = ArenaHalfSize - enemy.Radius;
            float2 position = math.clamp(self + push * Stiffness, -limit, limit);
            transform.Position = new float3(position, transform.Position.z);
        }

        static float2 AngleDirection(float angle)
        {
            math.sincos(angle, out float sin, out float cos);
            return new float2(cos, sin);
        }
    }
}
