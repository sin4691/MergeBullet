using MergeBullet.Components;
using Unity.Burst;
using Unity.Collections;
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
            state.RequireForUpdate<WallBox>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new PlayerMoveJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Arena = SystemAPI.GetSingleton<ArenaBounds>(),
                Walls = SystemAPI.GetSingletonBuffer<WallBox>(true).AsNativeArray(),
            }.Schedule();
        }
    }

    [BurstCompile]
    partial struct PlayerMoveJob : IJobEntity
    {
        public float DeltaTime;
        public ArenaBounds Arena;
        [ReadOnly] public NativeArray<WallBox> Walls;

        void Execute(ref LocalTransform transform, ref PlayerPosition playerPosition, ref FacingDirection facing,
            in Player player, in PlayerMoveInput input)
        {
            float2 limit = Arena.HalfSize - player.Radius;
            float2 position = math.clamp(transform.Position.xy + input.Move * player.MoveSpeed * DeltaTime,
                Arena.Center - limit, Arena.Center + limit);
            // 벽 덩어리·닫힌 문은 지나가지 못한다 (밀려난다)
            WallCollision.PushOut(ref position, player.Radius, Walls);
            transform.Position = new float3(position, transform.Position.z);
            playerPosition.Value = position;

            if (math.lengthsq(input.Move) > 0.01f)
                facing.Value = math.normalize(input.Move);
        }
    }
}
