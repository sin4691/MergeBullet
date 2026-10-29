using MergeBullet.Components;
using Unity.Entities;
using UnityEngine;

namespace MergeBullet.Authoring
{
    /// <summary>적 프리팹에 붙인다. 적 능력치는 프리팹이 갖고, 스포너는 언제·어디에 만들지만 정한다.</summary>
    public class EnemyAuthoring : MonoBehaviour
    {
        [Min(0f)] public float MoveSpeed = 2f;
        [Min(0.01f)] public float Radius = 0.35f;

        class Baker : Baker<EnemyAuthoring>
        {
            public override void Bake(EnemyAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Enemy { MoveSpeed = authoring.MoveSpeed, Radius = authoring.Radius });
            }
        }
    }
}
