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
    /// 폭발 효과를 처리한다.
    /// 1) 새 폭발 수집: 아직 피해를 안 준 폭발을 목록에 모으고 표시한다 (단일 스레드)
    /// 2) 범위 피해: 적마다 새 폭발 범위 안이면 체력을 깎는다 (병렬)
    /// 3) 연출: 수명 동안 0 → 최대 크기 → 0으로 부풀었다 줄어들고, 수명이 끝나면 제거된다 (병렬)
    /// </summary>
    [UpdateBefore(typeof(EnemyDeathSystem))]
    [BurstCompile]
    public partial struct ExplosionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Explosion>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var blasts = new NativeList<Blast>(Allocator.TempJob);

            JobHandle collect = new CollectNewBlastsJob { Blasts = blasts }.Schedule(state.Dependency);

            // 폭발은 드물고 적은 수백 단위라, 적마다 이번 프레임의 새 폭발 전체와 거리 비교로 충분하다
            JobHandle damage = new ApplyBlastDamageJob { Blasts = blasts.AsDeferredJobArray() }.ScheduleParallel(collect);

            JobHandle animate = new AnimateExplosionJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
            }.ScheduleParallel(damage);

            blasts.Dispose(animate);
            state.Dependency = animate;
        }
    }

    struct Blast
    {
        public float2 Position;
        public float Radius;
        public float Damage;
    }

    [BurstCompile]
    partial struct CollectNewBlastsJob : IJobEntity
    {
        public NativeList<Blast> Blasts;

        void Execute(ref Explosion explosion, in LocalTransform transform)
        {
            if (explosion.DamageApplied) return;
            explosion.DamageApplied = true;
            Blasts.Add(new Blast
            {
                Position = transform.Position.xy,
                Radius = explosion.MaxRadius,
                Damage = explosion.Damage,
            });
        }
    }

    [BurstCompile]
    partial struct ApplyBlastDamageJob : IJobEntity
    {
        [ReadOnly] public NativeArray<Blast> Blasts;

        void Execute(ref EnemyHealth health, in LocalTransform transform, in Enemy enemy)
        {
            float2 position = transform.Position.xy;
            for (int i = 0; i < Blasts.Length; i++)
            {
                // 적 몸의 일부라도 폭발 반경에 들어오면 맞은 것으로 본다
                float reach = Blasts[i].Radius + enemy.Radius;
                if (math.distancesq(position, Blasts[i].Position) < reach * reach)
                    health.Value -= Blasts[i].Damage;
            }
        }
    }

    [BurstCompile]
    partial struct AnimateExplosionJob : IJobEntity
    {
        public float DeltaTime;
        public EntityCommandBuffer.ParallelWriter Ecb;

        void Execute(Entity entity, [ChunkIndexInQuery] int sortKey, ref Explosion explosion, ref LocalTransform transform)
        {
            explosion.Age += DeltaTime;
            if (explosion.Age >= explosion.Duration)
            {
                Ecb.DestroyEntity(sortKey, entity);
                return;
            }

            // sin(0..π): 처음과 끝이 0, 중간이 1 → 팡 터졌다 사라지는 모양
            float t = explosion.Age / explosion.Duration;
            transform.Scale = explosion.MaxRadius * 2f * math.sin(math.PI * t);
        }
    }
}
