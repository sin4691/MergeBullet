using MergeBullet.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace MergeBullet.Authoring
{
    /// <summary>SubScene 안의 Player 오브젝트에 붙인다. 이동·무기 설정을 플레이어 엔티티로 베이킹한다.</summary>
    public class PlayerAuthoring : MonoBehaviour
    {
        [Min(0f)] public float MoveSpeed = 6f;
        [Min(0.01f)] public float Radius = 0.3f;
        [Min(1f)] public float MaxHealth = 10f;
        [Min(0f)] public float InvulnerableDuration = 0.8f;

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
                AddComponent(entity, new PlayerHealth
                {
                    Value = authoring.MaxHealth,
                    Max = authoring.MaxHealth,
                    InvulnerableDuration = authoring.InvulnerableDuration,
                });
                AddComponent(entity, new PlayerMoveInput());
                AddComponent(entity, new PlayerPosition { Value = ((float3)authoring.transform.position).xy });
                AddComponent(entity, new FacingDirection { Value = new float2(1f, 0f) });

                // 무적 시간 깜빡임: 원래 색을 기억해 두고 엔티티별 색 오버라이드로 하얀색과 번갈아 칠한다
                var renderer = GetComponent<MeshRenderer>();
                Material material = renderer != null ? renderer.sharedMaterial : null;
                DependsOn(material);
                float4 baseColor = AuthoringColor.LinearBaseColor(material);
                AddComponent(entity, new BaseColor { Value = baseColor });
                AddComponent(entity, new URPMaterialPropertyBaseColor { Value = baseColor });

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
