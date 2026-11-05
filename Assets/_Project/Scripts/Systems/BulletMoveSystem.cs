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
    /// 모든 총알을 이동시키고 벽에서 반사시킨다. 벽은 화면 가장자리(CameraView) 또는 아레나 경계 (BulletArea).
    /// 워커 스레드에서 병렬 실행. 카메라 범위는 잡(CameraFollow)이 쓰므로 메인 스레드에서 읽지 않고 잡 안에서 조회한다.
    /// 맵 안의 벽 상자(WallBox)에서도 튕기고, 그게 부서지는 문이면 피해를 기록한다
    /// (병렬로 큐에 쌓고 → 한 잡에서 문별로 합쳐 WallDamage에 쓴다. 반영은 다음 프레임 처음에 WallSystem이).
    /// </summary>
    [BurstCompile]
    public partial struct BulletMoveSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ArenaBounds>();
            state.RequireForUpdate<BulletLevelSettings>();
            state.RequireForUpdate<WallBox>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            bool hasView = SystemAPI.TryGetSingletonEntity<CameraView>(out Entity viewEntity);
            // 벤치마크 중에는 벽이 부서져 맵이 바뀌면 같은 조건으로 잴 수 없다
            bool breakableWalls = SystemAPI.TryGetSingletonEntity<WallDamage>(out Entity damageEntity)
                && !SystemAPI.HasSingleton<BenchmarkSettings>();
            var wallHits = new NativeQueue<WallHit>(Allocator.TempJob);

            JobHandle move = new BulletMoveJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Arena = SystemAPI.GetSingleton<ArenaBounds>(),
                Levels = SystemAPI.GetSingleton<BulletLevelSettings>(),
                Views = SystemAPI.GetComponentLookup<CameraView>(true),
                ViewEntity = viewEntity,
                HasView = hasView,
                Walls = SystemAPI.GetSingletonBuffer<WallBox>(true).AsNativeArray(),
                RecordWallHits = breakableWalls,
                WallHits = wallHits.AsParallelWriter(),
            }.ScheduleParallel(state.Dependency);

            JobHandle sum = move;
            if (breakableWalls)
            {
                sum = new SumWallHitsJob
                {
                    Hits = wallHits,
                    Damage = SystemAPI.GetComponentLookup<WallDamage>(),
                    DamageEntity = damageEntity,
                }.Schedule(move);
            }

            wallHits.Dispose(sum);
            state.Dependency = sum;
        }
    }

    struct WallHit
    {
        public int Gate;
        public float Damage;
    }

    [BurstCompile]
    partial struct BulletMoveJob : IJobEntity
    {
        public float DeltaTime;
        public ArenaBounds Arena;
        public BulletLevelSettings Levels;
        [ReadOnly] public ComponentLookup<CameraView> Views;
        public Entity ViewEntity;
        public bool HasView;
        [ReadOnly] public NativeArray<WallBox> Walls;
        public bool RecordWallHits;
        public NativeQueue<WallHit>.ParallelWriter WallHits;

        void Execute(ref LocalTransform transform, ref BulletVelocity velocity, ref Bullet bullet)
        {
            CameraView view = HasView ? Views[ViewEntity] : default;
            BulletArea.Get(Arena, HasView, view, out float2 center, out float2 halfSize);

            // 총알 중심이 갈 수 있는 범위 = 벽 - 반지름
            float2 max = center + halfSize - bullet.Radius;
            float2 min = center - halfSize + bullet.Radius;

            float2 p = transform.Position.xy + velocity.Value * DeltaTime;
            float2 v = velocity.Value;
            bool bounced = false;

            // 벽을 넘으면 넘은 만큼 안쪽으로 접어 넣고(거울 반사) 그 축의 속도 방향을 벽 안쪽으로 고정한다
            if (p.x < min.x) { p.x = 2f * min.x - p.x; v.x = math.abs(v.x); bounced = true; }
            else if (p.x > max.x) { p.x = 2f * max.x - p.x; v.x = -math.abs(v.x); bounced = true; }

            if (p.y < min.y) { p.y = 2f * min.y - p.y; v.y = math.abs(v.y); bounced = true; }
            else if (p.y > max.y) { p.y = 2f * max.y - p.y; v.y = -math.abs(v.y); bounced = true; }

            // 화면 벽은 카메라와 함께 움직여서 총알이 벽 밖에 한참 남겨질 수 있다. 접어 넣어도 밖이면 벽에 붙인다
            p = math.clamp(p, min, max);

            // 맵 안의 벽 상자: 겹치면 밖으로 밀어내고, 벽 쪽으로 가던 중이면 반사 (적 상자 판정과 같은 방식)
            if (WallCollision.PushOut(ref p, bullet.Radius, Walls, out float2 normal, out int box))
            {
                float approach = math.dot(v, normal);
                if (approach < 0f)
                {
                    v -= 2f * approach * normal;
                    bounced = true;
                    // 부서지는 문이면 피해 기록 (이번 튕김 전 횟수로 계산: 탄성 증강)
                    if (RecordWallHits && Walls[box].Gate >= 0)
                        WallHits.Enqueue(new WallHit { Gate = Walls[box].Gate, Damage = BulletLevelUtility.DamageOf(bullet, Levels) });
                }
            }

            if (bounced) bullet.Bounces++;

            transform.Position = new float3(p, transform.Position.z);
            velocity.Value = v;
        }
    }

    // 문은 네 개라 병렬로 쓰면 같은 칸을 동시에 고치게 된다. 큐에 모은 걸 한 스레드에서 합친다
    [BurstCompile]
    struct SumWallHitsJob : IJob
    {
        public NativeQueue<WallHit> Hits;
        public ComponentLookup<WallDamage> Damage;
        public Entity DamageEntity;

        public void Execute()
        {
            RefRW<WallDamage> damage = Damage.GetRefRW(DamageEntity);
            while (Hits.TryDequeue(out WallHit hit))
                if (hit.Gate >= 0 && hit.Gate < 4) damage.ValueRW.Amount[hit.Gate] += hit.Damage;
        }
    }
}
