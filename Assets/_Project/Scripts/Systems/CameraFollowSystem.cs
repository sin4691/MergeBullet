using MergeBullet.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace MergeBullet.Systems
{
    /// <summary>
    /// 카메라 범위(CameraView)를 플레이어 쪽으로 부드럽게 옮긴다. 아레나 밖이 보이지 않게 가장자리에서 멈추고,
    /// 아레나가 화면보다 작으면 가운데에 고정된다. 총알이 이 범위에서 튕기므로 총알 이동보다 먼저 돈다.
    /// 플레이어 위치는 잡(PlayerMove)이 쓰므로 메인 스레드에서 읽지 않고, 이 계산도 잡으로 이어 붙인다 (기다림 없음).
    /// </summary>
    [UpdateAfter(typeof(PlayerMoveSystem))]
    [UpdateBefore(typeof(BulletMoveSystem))]
    [BurstCompile]
    public partial struct CameraFollowSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CameraView>();
            state.RequireForUpdate<ArenaBounds>();
            state.RequireForUpdate<PlayerPosition>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new CameraFollowJob
            {
                PlayerPositions = SystemAPI.GetComponentLookup<PlayerPosition>(true),
                Player = SystemAPI.GetSingletonEntity<PlayerPosition>(),
                ArenaHalfSize = SystemAPI.GetSingleton<ArenaBounds>().HalfSize,
                DeltaTime = SystemAPI.Time.DeltaTime,
            }.Schedule();
        }
    }

    [BurstCompile]
    partial struct CameraFollowJob : IJobEntity
    {
        [ReadOnly] public ComponentLookup<PlayerPosition> PlayerPositions;
        public Entity Player;
        public float2 ArenaHalfSize;
        public float DeltaTime;

        void Execute(ref CameraView view)
        {
            float2 target = PlayerPositions[Player].Value;
            if (!view.Snapped)
            {
                view.Center = target;
                view.Snapped = true;
            }
            else
            {
                // 지수 감쇠: 프레임 속도와 상관없이 같은 빠르기로 따라간다 (lerp 비율을 고정하면 FPS에 따라 달라짐)
                view.Center = math.lerp(view.Center, target, 1f - math.exp(-view.FollowSharpness * DeltaTime));
            }

            float2 limit = math.max(ArenaHalfSize - view.HalfSize, 0f);
            view.Center = math.clamp(view.Center, -limit, limit);
        }
    }

    /// <summary>총알이 튕기는 범위. 화면 모드면 카메라 범위, 아니면 아레나 전체.</summary>
    public static class BulletArea
    {
        public static void Get(float2 arenaHalfSize, bool hasView, in CameraView view, out float2 center, out float2 halfSize)
        {
            if (hasView && view.BulletsBounceOnView)
            {
                center = view.Center;
                halfSize = view.HalfSize;
            }
            else
            {
                center = float2.zero;
                halfSize = arenaHalfSize;
            }
        }
    }
}
