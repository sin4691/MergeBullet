using MergeBullet.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace MergeBullet.Systems
{
    /// <summary>입력대로 플레이어를 움직이고 아레나 밖으로 나가지 않게 막는다. 움직였으면 바라보는 방향을 갱신한다.</summary>
    [UpdateAfter(typeof(PlayerInputSystem))]
    [BurstCompile]
    public partial struct PlayerMoveSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ArenaBounds>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new PlayerMoveJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                ArenaHalfSize = SystemAPI.GetSingleton<ArenaBounds>().HalfSize,
            }.Schedule();
        }
    }

    [BurstCompile]
    partial struct PlayerMoveJob : IJobEntity
    {
        public float DeltaTime;
        public float2 ArenaHalfSize;

        void Execute(ref LocalTransform transform, ref PlayerPosition playerPosition, ref FacingDirection facing,
            in Player player, in PlayerMoveInput input)
        {
            float2 limit = ArenaHalfSize - player.Radius;
            float2 position = math.clamp(transform.Position.xy + input.Move * player.MoveSpeed * DeltaTime, -limit, limit);
            transform.Position = new float3(position, transform.Position.z);
            playerPosition.Value = position;

            if (math.lengthsq(input.Move) > 0.01f)
                facing.Value = math.normalize(input.Move);
        }
    }
}
