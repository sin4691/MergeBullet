using MergeBullet.Components;
using Unity.Entities;
using UnityEngine;

namespace MergeBullet.Authoring
{
    /// <summary>폭발 효과 프리팹에 붙인다. 크기·수명은 스폰할 때 MaxLevelEffect 값으로 채운다.</summary>
    public class ExplosionAuthoring : MonoBehaviour
    {
        class Baker : Baker<ExplosionAuthoring>
        {
            public override void Bake(ExplosionAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Explosion());
            }
        }
    }
}
