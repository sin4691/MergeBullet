using MergeBullet.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace MergeBullet.Systems
{
    /// <summary>
    /// 모든 총알을 이동시키고 벽에서 반사시킨다. 벽은 화면 가장자리(CameraView) 또는 아레나 경계 (BulletArea).
    /// 워커 스레드에서 병렬 실행. 카메라 범위는 잡(CameraFollow)이 쓰므로 메인 스레드에서 읽지 않고 잡 안에서 조회한다.
    /// </summary>
    [BurstCompile]
    public partial struct BulletMoveSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ArenaBounds>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            bool hasView = SystemAPI.TryGetSingletonEntity<CameraView>(out Entity viewEntity);
            new BulletMoveJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                ArenaHalfSize = SystemAPI.GetSingleton<ArenaBounds>().HalfSize,
                Views = SystemAPI.GetComponentLookup<CameraView>(true),
                ViewEntity = viewEntity,
                HasView = hasView,
            }.ScheduleParallel();
        }
    }

    [BurstCompile]
    public partial struct BulletMoveJob : IJobEntity
    {
        public float DeltaTime;
        public float2 ArenaHalfSize;
        [ReadOnly] public ComponentLookup<CameraView> Views;
        public Entity ViewEntity;
        public bool HasView;

        void Execute(ref LocalTransform transform, ref BulletVelocity velocity, in Bullet bullet)
        {
            CameraView view = HasView ? Views[ViewEntity] : default;
            BulletArea.Get(ArenaHalfSize, HasView, view, out float2 center, out float2 halfSize);

            // 총알 중심이 갈 수 있는 범위 = 벽 - 반지름
            float2 max = center + halfSize - bullet.Radius;
            float2 min = center - halfSize + bullet.Radius;

            float2 p = transform.Position.xy + velocity.Value * DeltaTime;
            float2 v = velocity.Value;

            // 벽을 넘으면 넘은 만큼 안쪽으로 접어 넣고(거울 반사) 그 축의 속도 방향을 벽 안쪽으로 고정한다
            if (p.x < min.x) { p.x = 2f * min.x - p.x; v.x = math.abs(v.x); }
            else if (p.x > max.x) { p.x = 2f * max.x - p.x; v.x = -math.abs(v.x); }

            if (p.y < min.y) { p.y = 2f * min.y - p.y; v.y = math.abs(v.y); }
            else if (p.y > max.y) { p.y = 2f * max.y - p.y; v.y = -math.abs(v.y); }

            // 화면 벽은 카메라와 함께 움직여서 총알이 벽 밖에 한참 남겨질 수 있다. 접어 넣어도 밖이면 벽에 붙인다
            p = math.clamp(p, min, max);

            transform.Position = new float3(p, transform.Position.z);
            velocity.Value = v;
        }
    }
}
