using MergeBullet.Components;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace MergeBullet.Authoring
{
    /// <summary>문 뒤 방의 바닥 영역에 붙인다 (빈 오브젝트, 위치 = 방 중심). 크기는 Size.</summary>
    public class RoomAuthoring : MonoBehaviour
    {
        public int Index;
        public Vector2 Size;

        class Baker : Baker<RoomAuthoring>
        {
            public override void Bake(RoomAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.None);
                Vector3 position = authoring.transform.position;
                AddComponent(entity, new Room
                {
                    Index = authoring.Index,
                    Center = new float2(position.x, position.y),
                    HalfSize = new float2(authoring.Size.x, authoring.Size.y) * 0.5f,
                });
            }
        }
    }
}
