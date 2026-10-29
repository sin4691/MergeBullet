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
        // 마름모 옆폭을 줄여(로컬 Y) 앞뒤로 긴 모양 → 어느 쪽을 향하는지 보인다.
        // 앞뒤 길이는 그대로 2 * 반지름이라 진행 방향으로는 충돌 원과 크기가 맞는다
        const float BodyWidth = 0.55f;

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

            // 모양(PostTransformMatrix): 정육면체를 45도 돌려 마름모로 만든 뒤 옆폭(로컬 Y)을 줄인다.
            // 방향(LocalTransform.Rotation)은 이와 분리돼서, 추적 시스템은 회전값만 바꾸면 된다.
            ecb.AddComponent(enemy, new PostTransformMatrix
            {
                Value = math.mul(float4x4.Scale(1f, BodyWidth, 1f), float4x4.RotateZ(math.PI * 0.25f)),
            });

            // 대각선 절반이 반지름이 되도록 한 변을 radius * √2로. 첫 프레임은 아레나 중앙을 바라본다
            float facing = math.atan2(-position.y, -position.x);
            ecb.SetComponent(enemy, LocalTransform.FromPositionRotationScale(
                new float3(position, 0f), quaternion.RotateZ(facing), radius * math.SQRT2));
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
