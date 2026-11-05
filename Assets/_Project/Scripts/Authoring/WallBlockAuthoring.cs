using MergeBullet.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace MergeBullet.Authoring
{
    /// <summary>
    /// 벽 덩어리(상자)에 붙인다. 크기는 오브젝트의 크기(localScale x·y)에서 가져온다 (메뉴가 큐브를 늘려서 만든다).
    /// IsGate면 부서지는 문이고, 부서지면 RoomIndex의 방이 열린다.
    /// </summary>
    public class WallBlockAuthoring : MonoBehaviour
    {
        public bool IsGate;
        public int GateIndex;
        public int RoomIndex;

        class Baker : Baker<WallBlockAuthoring>
        {
            public override void Bake(WallBlockAuthoring authoring)
            {
                Vector3 scale = authoring.transform.lossyScale;
                Vector3 position = authoring.transform.position;
                // 문은 부서지면 그리지 않도록 켜고 끌 수 있어야 해서 움직이는 엔티티로, 나머지 벽은 고정
                Entity entity = GetEntity(authoring.IsGate ? TransformUsageFlags.Dynamic : TransformUsageFlags.Renderable);
                AddComponent(entity, new WallBlock
                {
                    Center = new float2(position.x, position.y),
                    HalfSize = new float2(scale.x, scale.y) * 0.5f,
                });
                if (!authoring.IsGate) return;

                AddComponent(entity, new Gate { Index = authoring.GateIndex, RoomIndex = authoring.RoomIndex });
                // 체력이 줄수록 붉게 (원래 색은 BaseColor로 기억)
                var renderer = GetComponent<MeshRenderer>();
                Material material = renderer != null ? renderer.sharedMaterial : null;
                DependsOn(material);
                float4 baseColor = AuthoringColor.LinearBaseColor(material);
                AddComponent(entity, new BaseColor { Value = baseColor });
                AddComponent(entity, new URPMaterialPropertyBaseColor { Value = baseColor });
            }
        }
    }
}
