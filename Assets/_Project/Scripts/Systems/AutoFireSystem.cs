using MergeBullet.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace MergeBullet.Systems
{
    /// <summary>
    /// 쿨다운마다 레벨 1 총알을 플레이어 몸 바깥에서 발사한다.
    /// 발사 방향은 지금은 바라보는 방향이고, 적이 생기면 가장 가까운 적 조준으로 바뀐다.
    /// </summary>
    [UpdateAfter(typeof(PlayerMoveSystem))]
    [BurstCompile]
    public partial struct AutoFireSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AutoFire>();
            state.RequireForUpdate<BulletLevelSettings>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new AutoFireJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Levels = SystemAPI.GetSingleton<BulletLevelSettings>(),
                Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged),
            }.Schedule();
        }
    }

    [BurstCompile]
    partial struct AutoFireJob : IJobEntity
    {
        public float DeltaTime;
        public BulletLevelSettings Levels;
        public EntityCommandBuffer Ecb;

        void Execute(ref AutoFire weapon, in LocalTransform transform, in FacingDirection facing, in Player player)
        {
            weapon.Cooldown -= DeltaTime;
            if (weapon.Cooldown > 0f) return;

            // 프레임이 길어져도 발사 간격이 밀리지 않도록 남은 시간을 이어 붙인다 (한 프레임에 최대 1발)
            weapon.Cooldown = math.max(weapon.Cooldown + weapon.Interval, 0f);

            const int level = 1;
            float radius = BulletLevelUtility.RadiusOf(level, Levels);
            float2 direction = facing.Value;
            // 플레이어 몸 바깥에서 생성해야 바로 겹쳐 보이지 않는다
            float2 position = transform.Position.xy + direction * (player.Radius + radius);

            Entity bullet = Ecb.Instantiate(weapon.BulletPrefab);
            Ecb.SetComponent(bullet, LocalTransform.FromPositionRotationScale(
                new float3(position, 0f), quaternion.identity, radius * 2f));
            Ecb.SetComponent(bullet, new Bullet { Level = level, Radius = radius });
            Ecb.SetComponent(bullet, new BulletVelocity { Value = direction * weapon.BulletSpeed });
            Ecb.SetComponent(bullet, new URPMaterialPropertyBaseColor { Value = BulletLevelUtility.ColorOf(level, Levels) });
        }
    }
}
