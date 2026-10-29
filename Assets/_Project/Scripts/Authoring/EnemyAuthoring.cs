using MergeBullet.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace MergeBullet.Authoring
{
    /// <summary>적 프리팹에 붙인다. 적 능력치는 프리팹이 갖고, 스포너는 언제·어디에 만들지만 정한다.</summary>
    public class EnemyAuthoring : MonoBehaviour
    {
        [Min(0f)] public float MoveSpeed = 2f;
        [Min(0.01f)] public float Radius = 0.35f;
        [Min(0.1f)] public float MaxHealth = 3f;
        [Min(0f)] public float ContactDamage = 1f;
        [Min(0.01f)] public float HitFlashDuration = 0.12f;

        class Baker : Baker<EnemyAuthoring>
        {
            public override void Bake(EnemyAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Enemy
                {
                    MoveSpeed = authoring.MoveSpeed,
                    Radius = authoring.Radius,
                    ContactDamage = authoring.ContactDamage,
                });
                AddComponent(entity, new EnemyHealth { Value = authoring.MaxHealth });

                // 피격 번쩍임: 원래 색을 기억해 두고 엔티티별 색 오버라이드로 하얗게 바꿨다 되돌린다
                var renderer = GetComponent<MeshRenderer>();
                Material material = renderer != null ? renderer.sharedMaterial : null;
                DependsOn(material);
                float4 baseColor = AuthoringColor.LinearBaseColor(material);
                AddComponent(entity, new BaseColor { Value = baseColor });
                AddComponent(entity, new URPMaterialPropertyBaseColor { Value = baseColor });
                AddComponent(entity, new HitFlash { Duration = authoring.HitFlashDuration });
            }
        }
    }
}
