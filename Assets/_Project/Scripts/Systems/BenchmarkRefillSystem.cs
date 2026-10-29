using MergeBullet.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace MergeBullet.Systems
{
    /// <summary>
    /// 벤치마크 모드에서 총알 수를 TargetBullets로 유지한다. 합성·소멸로 줄어든 만큼 레벨 1 총알을 임의 위치에 보충해서,
    /// 측정하는 동안 합성·충돌 시스템이 계속 같은 부하를 받게 한다.
    /// 보충은 ECB로 미뤄 측정 중에 메인 스레드가 잡을 기다리는 구조 변경(sync point)을 만들지 않는다.
    /// </summary>
    [BurstCompile]
    public partial struct BenchmarkRefillSystem : ISystem
    {
        // 한 프레임에 너무 많이 만들면 그 프레임만 튀어서, 나눠서 채운다
        const int MaxSpawnPerFrame = 250;

        EntityQuery _bulletQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _bulletQuery = SystemAPI.QueryBuilder().WithAll<Bullet>().Build();
            state.RequireForUpdate<BenchmarkSettings>();
            state.RequireForUpdate<BulletLevelSettings>();
            state.RequireForUpdate<ArenaBounds>();
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            RefRW<BenchmarkSettings> settings = SystemAPI.GetSingletonRW<BenchmarkSettings>();
            int missing = settings.ValueRO.TargetBullets - _bulletQuery.CalculateEntityCount();
            if (missing <= 0) return;

            BulletLevelSettings levels = SystemAPI.GetSingleton<BulletLevelSettings>();
            const int level = 1;
            float radius = BulletLevelUtility.RadiusOf(level, levels);
            float4 color = BulletLevelUtility.ColorOf(level, levels);
            float2 spawnHalf = SystemAPI.GetSingleton<ArenaBounds>().HalfSize - radius;

            // 생성은 다음 프레임 시작(BeginSimulation)에 재생 → 위치 계산 전에 만들어져 원점에 한 프레임 그려지지 않는다
            EntityCommandBuffer ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            Random random = settings.ValueRO.Random;
            Entity prefab = settings.ValueRO.BulletPrefab;
            float speed = settings.ValueRO.BulletSpeed;

            int count = math.min(missing, MaxSpawnPerFrame);
            for (int i = 0; i < count; i++)
            {
                float2 position = random.NextFloat2(-spawnHalf, spawnHalf);
                math.sincos(random.NextFloat(0f, 2f * math.PI), out float sin, out float cos);

                Entity bullet = ecb.Instantiate(prefab);
                ecb.SetComponent(bullet, LocalTransform.FromPositionRotationScale(
                    new float3(position, 0f), quaternion.identity, radius * 2f));
                ecb.SetComponent(bullet, new Bullet { Level = level, Radius = radius });
                ecb.SetComponent(bullet, new BulletVelocity { Value = new float2(cos, sin) * speed });
                ecb.SetComponent(bullet, new URPMaterialPropertyBaseColor { Value = color });
            }

            settings.ValueRW.Random = random;
        }
    }
}
