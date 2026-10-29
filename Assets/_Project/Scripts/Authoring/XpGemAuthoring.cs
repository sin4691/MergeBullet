using MergeBullet.Components;
using Unity.Entities;
using UnityEngine;

namespace MergeBullet.Authoring
{
    /// <summary>경험치 젬 프리팹에 붙인다. 경험치 양은 떨어뜨리는 적(XpDrop)이 스폰할 때 정한다.</summary>
    public class XpGemAuthoring : MonoBehaviour
    {
        class Baker : Baker<XpGemAuthoring>
        {
            public override void Bake(XpGemAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new XpGem { Value = 1 });
            }
        }
    }
}
