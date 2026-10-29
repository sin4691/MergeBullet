using MergeBullet.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace MergeBullet.Systems
{
    /// <summary>
    /// 스폰 간격마다, 살아 있는 적이 MaxAlive 미만이면 아레나 네 변 중 임의의 위치에 적을 하나 만든다.
    /// 생성은 ECB로 미뤄서 메인 스레드가 다른 잡을 기다리는 구조 변경(sync point)을 만들지 않는다.
    /// </summary>
    [BurstCompile]
    public partial struct EnemySpawnSystem : ISystem
    {
        EntityQuery _enemyQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _enemyQuery = SystemAPI.QueryBuilder().WithAll<Enemy>().Build();
            state.RequireForUpdate<EnemySpawner>();
            state.RequireForUpdate<ArenaBounds>();
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            RefRW<EnemySpawner> spawner = SystemAPI.GetSingletonRW<EnemySpawner>();
            spawner.ValueRW.Cooldown -= SystemAPI.Time.DeltaTime;
            if (spawner.ValueRO.Cooldown > 0f) return;
            spawner.ValueRW.Cooldown = math.max(spawner.ValueRO.Cooldown + spawner.ValueRO.Interval, 0f);

            if (_enemyQuery.CalculateEntityCount() >= spawner.ValueRO.MaxAlive) return;

            Entity prefab = spawner.ValueRO.Prefab;
            float radius = SystemAPI.GetComponent<Enemy>(prefab).Radius;
            float2 limit = SystemAPI.GetSingleton<ArenaBounds>().HalfSize - radius;
            float2 position = RandomPointOnEdge(ref spawner.ValueRW.Random, limit);

            // 생성은 다음 프레임 시작(BeginSimulation)에 재생한다. 프레임 끝(EndSimulation)에 만들면 이번 프레임의
            // 위치 계산(TransformSystemGroup)이 이미 끝난 뒤라, 새 엔티티가 한 프레임 동안 프리팹 위치(원점)에 그려진다.
            EntityCommandBuffer ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            Entity enemy = ecb.Instantiate(prefab);
            // 정육면체를 45도 돌린 마름모 모양. 대각선 절반이 반지름이 되도록 한 변을 radius * √2로
            ecb.SetComponent(enemy, LocalTransform.FromPositionRotationScale(
                new float3(position, 0f), quaternion.RotateZ(math.PI * 0.25f), radius * math.SQRT2));
        }

        static float2 RandomPointOnEdge(ref Random random, float2 limit)
        {
            float t = random.NextFloat(-1f, 1f);
            switch (random.NextInt(4))
            {
                case 0: return new float2(t * limit.x, limit.y);   // 위
                case 1: return new float2(t * limit.x, -limit.y);  // 아래
                case 2: return new float2(-limit.x, t * limit.y);  // 왼쪽
                default: return new float2(limit.x, t * limit.y);  // 오른쪽
            }
        }
    }
}
