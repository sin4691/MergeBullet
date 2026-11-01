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
    /// 최대 레벨에 도달한 총알을 캐릭터의 특수효과로 바꾸고 총알은 제거한다.
    /// - 폭발(Blaster): 그 자리에 폭발 엔티티를 만든다. 피해·연출은 ExplosionSystem이 이어서 처리한다
    /// - 연쇄 번개(Storm): 그 자리에서 가까운 적부터 차례로 번개가 튀며 바로 피해를 준다
    /// 합성 결과는 프레임 끝에 반영되므로, 최대 레벨 총알은 한 프레임 보인 뒤 다음 프레임에 터진다.
    /// </summary>
    [UpdateAfter(typeof(BulletMergeSystem))]
    [BurstCompile]
    public partial struct MaxLevelBulletSystem : ISystem
    {
        EntityQuery _enemyQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _enemyQuery = SystemAPI.QueryBuilder().WithAll<Enemy, LocalTransform, EnemyHealth>().Build();
            state.RequireForUpdate<MaxLevelEffect>();
            state.RequireForUpdate<BulletLevelSettings>();
            state.RequireForUpdate<EffectQueue>();
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            MaxLevelEffect effect = SystemAPI.GetSingleton<MaxLevelEffect>();
            int maxLevel = SystemAPI.GetSingleton<BulletLevelSettings>().MaxLevel;
            Entity effectQueue = SystemAPI.GetSingletonEntity<EffectQueue>();

            if (effect.Kind == MaxLevelEffectKind.Explosion)
            {
                // 생성은 다음 프레임 시작(BeginSimulation)에 재생한다. 프레임 끝(EndSimulation)에 만들면 이번 프레임의
                // 위치 계산(TransformSystemGroup)이 이미 끝난 뒤라, 새 엔티티가 한 프레임 동안 프리팹 위치(원점)에 그려진다.
                new TriggerExplosionJob
                {
                    MaxLevel = maxLevel,
                    Effect = effect,
                    EffectQueueEntity = effectQueue,
                    Ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                        .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
                }.ScheduleParallel();
                return;
            }

            ScheduleChainLightning(ref state, effect, maxLevel, effectQueue);
        }

        // 1) 최대 레벨 총알 자리를 모으고 총알을 지운다 → 2) 적 목록을 받아 자리마다 연쇄 번개를 친다.
        // 레벨 4는 드물게 나오고 적은 수백 단위라, 번개 한 마디마다 적 전체에서 가장 가까운 적을 찾는 단순한 방식으로 충분하다
        void ScheduleChainLightning(ref SystemState state, MaxLevelEffect effect, int maxLevel, Entity effectQueue)
        {
            EndSimulationEntityCommandBufferSystem.Singleton endEcb =
                SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var strikes = new NativeList<float2>(Allocator.TempJob);

            JobHandle collect = new CollectStrikesJob
            {
                MaxLevel = maxLevel,
                Strikes = strikes,
                Ecb = endEcb.CreateCommandBuffer(state.WorldUnmanaged),
            }.Schedule(state.Dependency);

            NativeList<Entity> enemies = _enemyQuery.ToEntityListAsync(Allocator.TempJob, collect, out JobHandle enemiesReady);
            NativeList<LocalTransform> transforms =
                _enemyQuery.ToComponentDataListAsync<LocalTransform>(Allocator.TempJob, collect, out JobHandle transformsReady);

            JobHandle chain = new ChainLightningJob
            {
                Effect = effect,
                Strikes = strikes,
                Enemies = enemies,
                Transforms = transforms,
                Health = SystemAPI.GetComponentLookup<EnemyHealth>(),
                Flashes = SystemAPI.GetComponentLookup<HitFlash>(),
                EffectQueueEntity = effectQueue,
                Ecb = endEcb.CreateCommandBuffer(state.WorldUnmanaged),
            }.Schedule(JobHandle.CombineDependencies(collect, enemiesReady, transformsReady));

            strikes.Dispose(chain);
            enemies.Dispose(chain);
            transforms.Dispose(chain);
            state.Dependency = chain;
        }
    }

    [BurstCompile]
    partial struct TriggerExplosionJob : IJobEntity
    {
        public int MaxLevel;
        public MaxLevelEffect Effect;
        public Entity EffectQueueEntity;
        public EntityCommandBuffer.ParallelWriter Ecb;

        // sortKey: 병렬로 기록된 명령을 재생할 때 순서를 결정적으로 만들기 위한 키
        void Execute(Entity entity, [ChunkIndexInQuery] int sortKey, in Bullet bullet, in LocalTransform transform)
        {
            if (bullet.Level < MaxLevel) return;

            Entity effect = Ecb.Instantiate(sortKey, Effect.Prefab);
            Ecb.SetComponent(sortKey, effect, LocalTransform.FromPositionRotationScale(
                transform.Position, quaternion.identity, 0f));
            Ecb.SetComponent(sortKey, effect, new Explosion
            {
                Duration = Effect.Duration,
                MaxRadius = Effect.Radius,
                Damage = Effect.Damage,
            });
            Ecb.DestroyEntity(sortKey, entity);

            // 폭발 구 위에 파편·소리를 더한다 (병렬 잡이라 sortKey로 순서를 고정)
            Ecb.AppendToBuffer(sortKey, EffectQueueEntity, new EffectEvent
            {
                Type = EffectType.Explosion,
                Position = transform.Position.xy,
                Size = Effect.Radius,
                Color = new float4(1f, 0.95f, 0.8f, 1f),
            });
        }
    }

    [BurstCompile]
    partial struct CollectStrikesJob : IJobEntity
    {
        public int MaxLevel;
        public NativeList<float2> Strikes;
        public EntityCommandBuffer Ecb;

        void Execute(Entity entity, in Bullet bullet, in LocalTransform transform)
        {
            if (bullet.Level < MaxLevel) return;
            Strikes.Add(transform.Position.xy);
            Ecb.DestroyEntity(entity);
        }
    }

    [BurstCompile]
    struct ChainLightningJob : IJob
    {
        // 번개 색 (선형 색공간): 밝은 하늘색
        static readonly float4 BoltColor = new float4(0.55f, 0.85f, 1f, 1f);

        public MaxLevelEffect Effect;
        [ReadOnly] public NativeList<float2> Strikes;
        [ReadOnly] public NativeList<Entity> Enemies;
        [ReadOnly] public NativeList<LocalTransform> Transforms;
        public ComponentLookup<EnemyHealth> Health;
        public ComponentLookup<HitFlash> Flashes;
        public Entity EffectQueueEntity;
        public EntityCommandBuffer Ecb;

        public void Execute()
        {
            float rangeSq = Effect.ChainRange * Effect.ChainRange;
            int chainCount = math.min(Effect.ChainCount, 32); // 맞은 적 기록(FixedList) 크기 한도

            foreach (float2 strike in Strikes)
            {
                // 한 번의 연쇄에서 같은 적을 두 번 맞히지 않는다
                var hit = new FixedList128Bytes<int>();
                float2 from = strike;

                for (int link = 0; link < chainCount; link++)
                {
                    int target = FindNearest(from, rangeSq, ref hit);
                    if (target < 0) break;
                    hit.Add(target);

                    Entity enemy = Enemies[target];
                    Health.GetRefRW(enemy).ValueRW.Value -= Effect.ChainDamage;
                    RefRW<HitFlash> flash = Flashes.GetRefRW(enemy);
                    flash.ValueRW.Timer = flash.ValueRO.Duration;

                    float2 to = Transforms[target].Position.xy;
                    Ecb.AppendToBuffer(EffectQueueEntity, new EffectEvent
                    {
                        Type = EffectType.Lightning,
                        Position = from,
                        Target = to,
                        Size = 1f,
                        Color = BoltColor,
                    });
                    from = to;
                }
            }
        }

        int FindNearest(float2 from, float rangeSq, ref FixedList128Bytes<int> hit)
        {
            int best = -1;
            float bestDistanceSq = rangeSq;
            for (int i = 0; i < Enemies.Length; i++)
            {
                if (hit.Contains(i)) continue;
                float distanceSq = math.distancesq(from, Transforms[i].Position.xy);
                if (distanceSq >= bestDistanceSq) continue;
                bestDistanceSq = distanceSq;
                best = i;
            }
            return best;
        }
    }
}
