using MergeBullet.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace MergeBullet.Systems
{
    /// <summary>모든 적이 플레이어를 향해 직선으로 다가온다. 몸이 닿는 거리에서 멈춘다. 워커 스레드에서 병렬 실행.</summary>
    [UpdateAfter(typeof(PlayerMoveSystem))]
    [BurstCompile]
    public partial struct EnemyChaseSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Player>();
            state.RequireForUpdate<Enemy>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new EnemyChaseJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                PlayerEntity = SystemAPI.GetSingletonEntity<Player>(),
                Players = SystemAPI.GetComponentLookup<Player>(true),
                PlayerPositions = SystemAPI.GetComponentLookup<PlayerPosition>(true),
            }.ScheduleParallel();
        }
    }

    [BurstCompile]
    partial struct EnemyChaseJob : IJobEntity
    {
        public float DeltaTime;
        public Entity PlayerEntity;
        [ReadOnly] public ComponentLookup<Player> Players;
        [ReadOnly] public ComponentLookup<PlayerPosition> PlayerPositions;

        void Execute(ref LocalTransform transform, in Enemy enemy)
        {
            float2 toPlayer = PlayerPositions[PlayerEntity].Value - transform.Position.xy;
            float distance = math.length(toPlayer);
            float gap = distance - (enemy.Radius + Players[PlayerEntity].Radius);
            if (gap <= 0f) return;

            float step = math.min(enemy.MoveSpeed * DeltaTime, gap);
            transform.Position += new float3(toPlayer / distance * step, 0f);
        }
    }
}
