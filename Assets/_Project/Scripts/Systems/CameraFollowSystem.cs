using MergeBullet.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace MergeBullet.Systems
{
    /// <summary>
    /// 카메라 범위(CameraView)를 플레이어 쪽으로 부드럽게 옮기고, 난이도 진행도만큼 넓힌다. 아레나 밖이 보이지 않게 가장자리에서 멈추고,
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
                Arena = SystemAPI.GetSingleton<ArenaBounds>(),
                DeltaTime = SystemAPI.Time.DeltaTime,
                // 난이도 진행도(0 → 1, RampSeconds 동안)는 DifficultySystem이 메인 스레드에서 쓰는 값이라 바로 읽어도 기다림이 없다
                Progress = SystemAPI.TryGetSingleton(out EnemySpawner spawner) ? spawner.Progress : 0f,
            }.Schedule();
        }
    }

    [BurstCompile]
    partial struct CameraFollowJob : IJobEntity
    {
        [ReadOnly] public ComponentLookup<PlayerPosition> PlayerPositions;
        public Entity Player;
        public ArenaBounds Arena;
        public float DeltaTime;
        public float Progress;

        void Execute(ref CameraView view)
        {
            // 진행도는 몇 분에 걸쳐 조금씩 오르므로 화면도 눈치채지 못할 만큼 천천히 넓어진다
            view.HalfSize = math.lerp(view.StartHalfSize, view.LateHalfSize, math.saturate(Progress));

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

            float2 limit = math.max(Arena.HalfSize - view.HalfSize, 0f);
            view.Center = math.clamp(view.Center, Arena.Center - limit, Arena.Center + limit);
        }
    }

    /// <summary>총알이 튕기는 범위. 화면 모드면 카메라 범위, 아니면 아레나 전체.</summary>
    public static class BulletArea
    {
        public static void Get(in ArenaBounds arena, bool hasView, in CameraView view, out float2 center, out float2 halfSize)
        {
            if (hasView && view.BulletsBounceOnView)
            {
                center = view.Center;
                halfSize = view.HalfSize;
            }
            else
            {
                center = arena.Center;
                halfSize = arena.HalfSize;
            }
        }
    }
}
