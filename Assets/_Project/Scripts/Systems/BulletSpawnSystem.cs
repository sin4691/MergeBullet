using MergeBullet.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace MergeBullet.Systems
{
    /// <summary>BulletSpawner가 있으면 총알을 한 번에 생성하고, 다시 돌지 않도록 스포너를 제거한다.</summary>
    [BurstCompile]
    public partial struct BulletSpawnSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BulletSpawner>();
            state.RequireForUpdate<ArenaBounds>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            Entity spawnerEntity = SystemAPI.GetSingletonEntity<BulletSpawner>();
            BulletSpawner spawner = SystemAPI.GetSingleton<BulletSpawner>();
            float2 spawnHalf = SystemAPI.GetSingleton<ArenaBounds>().HalfSize - spawner.Radius;

            // 프리팹 복제는 구조 변경이라 메인 스레드에서 한 번에 처리한다 (시작 시 1회뿐이라 비용 무시 가능)
            NativeArray<Entity> bullets = state.EntityManager.Instantiate(spawner.Prefab, spawner.Count, Allocator.Temp);
            var random = Random.CreateFromIndex(spawner.Seed);

            foreach (Entity bullet in bullets)
            {
                float2 position = random.NextFloat2(-spawnHalf, spawnHalf);
                math.sincos(random.NextFloat(0f, 2f * math.PI), out float sin, out float cos);

                SystemAPI.SetComponent(bullet, LocalTransform.FromPositionRotationScale(
                    new float3(position, 0f), quaternion.identity, spawner.Radius * 2f));
                SystemAPI.SetComponent(bullet, new Bullet { Radius = spawner.Radius });
                SystemAPI.SetComponent(bullet, new BulletVelocity { Value = new float2(cos, sin) * spawner.Speed });
            }

            state.EntityManager.RemoveComponent<BulletSpawner>(spawnerEntity);
        }
    }
}
