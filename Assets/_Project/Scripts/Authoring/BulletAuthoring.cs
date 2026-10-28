using MergeBullet.Components;
using Unity.Entities;
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
                AddComponent(entity, new Bullet());
                AddComponent(entity, new BulletVelocity());
            }
        }
    }
}
