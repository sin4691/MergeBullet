using MergeBullet.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace MergeBullet.Systems
{
    /// <summary>모든 총알을 이동시키고 아레나 벽에서 반사시킨다. 워커 스레드에서 병렬 실행.</summary>
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
            new BulletMoveJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                ArenaHalfSize = SystemAPI.GetSingleton<ArenaBounds>().HalfSize,
            }.ScheduleParallel();
        }
    }

    [BurstCompile]
    public partial struct BulletMoveJob : IJobEntity
    {
        public float DeltaTime;
        public float2 ArenaHalfSize;

        void Execute(ref LocalTransform transform, ref BulletVelocity velocity, in Bullet bullet)
        {
            // 총알 중심이 갈 수 있는 범위 = 아레나 - 반지름
            float2 max = ArenaHalfSize - bullet.Radius;
            float2 min = -max;

            float2 p = transform.Position.xy + velocity.Value * DeltaTime;
            float2 v = velocity.Value;

            // 벽을 넘으면 넘은 만큼 안쪽으로 접어 넣고(거울 반사) 그 축의 속도 방향을 벽 안쪽으로 고정한다
            if (p.x < min.x) { p.x = 2f * min.x - p.x; v.x = math.abs(v.x); }
            else if (p.x > max.x) { p.x = 2f * max.x - p.x; v.x = -math.abs(v.x); }

            if (p.y < min.y) { p.y = 2f * min.y - p.y; v.y = math.abs(v.y); }
            else if (p.y > max.y) { p.y = 2f * max.y - p.y; v.y = -math.abs(v.y); }

            transform.Position = new float3(p, transform.Position.z);
            velocity.Value = v;
        }
    }
}
