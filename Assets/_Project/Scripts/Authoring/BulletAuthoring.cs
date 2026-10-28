using MergeBullet.Components;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

namespace MergeBullet.Authoring
{
    /// <summary>총알 프리팹에 붙인다. 베이킹 시 총알 엔티티에 필요한 컴포넌트를 추가한다.</summary>
    public class BulletAuthoring : MonoBehaviour
    {
        class Baker : Baker<BulletAuthoring>
        {
            public override void Bake(BulletAuthoring authoring)
            {
                // Dynamic: 런타임에 LocalTransform을 매 프레임 바꾸므로
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Bullet { Level = 1 });
                AddComponent(entity, new BulletVelocity());
                // 머티리얼을 레벨마다 따로 만들지 않고 엔티티별로 _BaseColor만 덮어쓴다 (배칭 유지)
                AddComponent(entity, new URPMaterialPropertyBaseColor { Value = 1f });
            }
        }
    }
}
