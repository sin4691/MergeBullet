using MergeBullet.Components;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace MergeBullet.Authoring
{
    /// <summary>SubScene 안의 Player 오브젝트에 붙인다. 이동·무기 설정을 플레이어 엔티티로 베이킹한다.</summary>
    public class PlayerAuthoring : MonoBehaviour
    {
        [Min(0f)] public float MoveSpeed = 6f;
        [Min(0.01f)] public float Radius = 0.3f;

        [Header("Auto Fire")]
        public GameObject BulletPrefab;
        [Min(0.01f)] public float FireInterval = 0.25f;
        [Min(0f)] public float BulletSpeed = 7f;

        class Baker : Baker<PlayerAuthoring>
        {
            public override void Bake(PlayerAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Player { MoveSpeed = authoring.MoveSpeed, Radius = authoring.Radius });
                AddComponent(entity, new PlayerMoveInput());
                AddComponent(entity, new PlayerPosition { Value = ((float3)authoring.transform.position).xy });
                AddComponent(entity, new FacingDirection { Value = new float2(1f, 0f) });

                if (authoring.BulletPrefab == null) return;
                AddComponent(entity, new AutoFire
                {
                    BulletPrefab = GetEntity(authoring.BulletPrefab, TransformUsageFlags.Dynamic),
                    Interval = authoring.FireInterval,
                    BulletSpeed = authoring.BulletSpeed,
                });
            }
        }
    }
}
