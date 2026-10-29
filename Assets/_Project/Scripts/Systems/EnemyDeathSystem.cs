using MergeBullet.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace MergeBullet.Systems
{
    /// <summary>
    /// 체력이 0 이하가 된 적을 제거하고, 처치 수를 세고, 경험치 젬을 떨어뜨린다.
    /// 피해를 주는 곳(총알, 폭발)이 여럿이어도 제거는 여기 한 곳에서만 해서, 같은 프레임에
    /// 같은 적이 두 번 삭제 명령을 받는 일이 없다.
    /// 처치 수 하나를 여러 스레드가 동시에 올리면 충돌하므로 단일 스레드로 돈다 (적은 수백 단위라 충분).
    /// </summary>
    [UpdateAfter(typeof(BulletEnemyCollisionSystem))]
    [BurstCompile]
    public partial struct EnemyDeathSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EnemyHealth>();
            state.RequireForUpdate<GameStats>();
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new EnemyDeathJob
            {
                DestroyEcb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged),
                // 젬 생성은 다음 프레임 시작에 재생해야 위치 계산 전에 만들어져 원점에 한 프레임 그려지지 않는다
                SpawnEcb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged),
                StatsEntity = SystemAPI.GetSingletonEntity<GameStats>(),
                Stats = SystemAPI.GetComponentLookup<GameStats>(),
                Drops = SystemAPI.GetComponentLookup<XpDrop>(true),
                Transforms = SystemAPI.GetComponentLookup<LocalTransform>(true),
            }.Schedule();
        }
    }

    [BurstCompile]
    partial struct EnemyDeathJob : IJobEntity
    {
        public EntityCommandBuffer DestroyEcb;
        public EntityCommandBuffer SpawnEcb;
        public Entity StatsEntity;
        public ComponentLookup<GameStats> Stats;
        [ReadOnly] public ComponentLookup<XpDrop> Drops;
        [ReadOnly] public ComponentLookup<LocalTransform> Transforms;

        void Execute(Entity entity, in EnemyHealth health)
        {
            if (health.Value > 0f) return;

            DestroyEcb.DestroyEntity(entity);
            Stats.GetRefRW(StatsEntity).ValueRW.Kills++;

            if (!Drops.TryGetComponent(entity, out XpDrop drop)) return;

            // 젬 프리팹의 크기는 그대로 쓰고, 위치는 죽은 자리, 모양은 45도 돌린 마름모
            LocalTransform gemTransform = Transforms[drop.GemPrefab];
            gemTransform.Position = Transforms[entity].Position;
            gemTransform.Rotation = quaternion.RotateZ(math.PI * 0.25f);

            Entity gem = SpawnEcb.Instantiate(drop.GemPrefab);
            SpawnEcb.SetComponent(gem, gemTransform);
            SpawnEcb.SetComponent(gem, new XpGem { Value = drop.Value });
        }
    }
}
