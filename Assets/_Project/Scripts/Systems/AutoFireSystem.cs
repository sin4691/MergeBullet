using MergeBullet.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace MergeBullet.Systems
{
    /// <summary>
    /// 쿨다운마다 레벨 1 총알을 플레이어 몸 바깥에서 발사한다.
    /// 가장 가까운 적을 조준하고, 적이 없으면 바라보는 방향으로 쏜다.
    /// 1) 가장 가까운 적 찾기 → 2) 발사, 두 잡을 이어서 예약하고 메인 스레드는 기다리지 않는다.
    /// </summary>
    [UpdateAfter(typeof(EnemyChaseSystem))]
    [BurstCompile]
    public partial struct AutoFireSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AutoFire>();
            state.RequireForUpdate<BulletLevelSettings>();
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var nearest = new NativeReference<NearestTarget>(
                new NearestTarget { DistanceSq = float.MaxValue }, Allocator.TempJob);

            JobHandle find = new FindNearestEnemyJob
            {
                PlayerEntity = SystemAPI.GetSingletonEntity<AutoFire>(),
                PlayerPositions = SystemAPI.GetComponentLookup<PlayerPosition>(true),
                Nearest = nearest,
            }.Schedule(state.Dependency);

            // 생성은 다음 프레임 시작(BeginSimulation)에 재생한다. 프레임 끝(EndSimulation)에 만들면 이번 프레임의
            // 위치 계산(TransformSystemGroup)이 이미 끝난 뒤라, 새 엔티티가 한 프레임 동안 프리팹 위치(원점)에 그려진다.
            JobHandle fire = new AutoFireJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Levels = SystemAPI.GetSingleton<BulletLevelSettings>(),
                Target = nearest,
                Ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged),
            }.Schedule(find);

            nearest.Dispose(fire);
            state.Dependency = fire;
        }
    }

    struct NearestTarget
    {
        public float2 Position;
        public float DistanceSq;
        public bool Found;
    }

    /// <summary>
    /// 적 전체를 한 번 훑어 가장 가까운 적을 찾는다 (단일 스레드).
    /// 적 수가 수백 단위라 전수 비교로 충분하다. 수천으로 늘면 공간 해시로 바꾼다.
    /// </summary>
    [BurstCompile]
    partial struct FindNearestEnemyJob : IJobEntity
    {
        public Entity PlayerEntity;
        [ReadOnly] public ComponentLookup<PlayerPosition> PlayerPositions;
        public NativeReference<NearestTarget> Nearest;

        void Execute(in LocalTransform transform, in Enemy enemy)
        {
            float2 position = transform.Position.xy;
            float distanceSq = math.distancesq(position, PlayerPositions[PlayerEntity].Value);
            if (distanceSq >= Nearest.Value.DistanceSq) return;

            Nearest.Value = new NearestTarget { Position = position, DistanceSq = distanceSq, Found = true };
        }
    }

    [BurstCompile]
    partial struct AutoFireJob : IJobEntity
    {
        public float DeltaTime;
        public BulletLevelSettings Levels;
        [ReadOnly] public NativeReference<NearestTarget> Target;
        public EntityCommandBuffer Ecb;

        void Execute(ref AutoFire weapon, in LocalTransform transform, in FacingDirection facing, in Player player)
        {
            weapon.Cooldown -= DeltaTime;
            if (weapon.Cooldown > 0f) return;

            // 프레임이 길어져도 발사 간격이 밀리지 않도록 남은 시간을 이어 붙인다 (한 프레임에 최대 1발)
            weapon.Cooldown = math.max(weapon.Cooldown + weapon.Interval, 0f);

            float2 origin = transform.Position.xy;
            NearestTarget target = Target.Value;
            float2 direction = target.Found
                ? math.normalizesafe(target.Position - origin, facing.Value)
                : facing.Value;

            // 대구경 증강이면 처음부터 높은 레벨. 최대 레벨 이상이면 바로 터져 버리므로 그 아래로 자른다
            int level = math.clamp(weapon.StartLevel, 1, Levels.MaxLevel - 1);
            float radius = BulletLevelUtility.RadiusOf(level, Levels);
            float4 color = BulletLevelUtility.ColorOf(level, Levels);

            // 쌍둥이 총구 증강이면 여러 발을 옆으로 나란히 띄우고 부채꼴로 벌려 쏜다.
            // 같은 자리에서 나가면 서로 닿아 바로 합성돼 레벨 2 한 발처럼 보이기 때문
            int shots = math.max(weapon.ShotsPerVolley, 1);
            for (int i = 0; i < shots; i++)
            {
                float angle = (i - (shots - 1) * 0.5f) * SpreadRadians;
                math.sincos(angle, out float sin, out float cos);
                float2 shotDirection = new float2(direction.x * cos - direction.y * sin, direction.x * sin + direction.y * cos);
                // 플레이어 몸 바깥에서 생성해야 바로 겹쳐 보이지 않는다. 옆 간격은 총알 지름 + 여유라 서로 닿지 않는다
                float2 side = new float2(-direction.y, direction.x) * ((i - (shots - 1) * 0.5f) * (radius * 2f + SideGap));
                float2 position = origin + shotDirection * (player.Radius + radius) + side;

                Entity bullet = Ecb.Instantiate(weapon.BulletPrefab);
                Ecb.SetComponent(bullet, LocalTransform.FromPositionRotationScale(
                    new float3(position, 0f), quaternion.identity, radius * 2f));
                Ecb.SetComponent(bullet, new Bullet { Level = level, Radius = radius });
                Ecb.SetComponent(bullet, new BulletVelocity { Value = shotDirection * weapon.BulletSpeed });
                Ecb.SetComponent(bullet, new URPMaterialPropertyBaseColor { Value = color });
            }
        }

        // 여러 발을 쏠 때 총알 사이 각도 (약 20도)와, 처음 나란히 놓을 때 총알 사이 빈틈
        const float SpreadRadians = 0.35f;
        const float SideGap = 0.15f;
    }
}
