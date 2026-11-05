using MergeBullet.Components;
using Unity.Entities;
using UnityEngine;

namespace MergeBullet.Authoring
{
    /// <summary>
    /// 맵 모양: 가운데 맵 크기, 그 둘레의 벽·부서지는 문·통로. ArenaBounds, 벽 목록(WallBox·RoomState·WallDamage),
    /// 문 규칙(WallSettings)을 Arena 엔티티에 굽는다. 벽 상자 자체는 자식 오브젝트(WallBlockAuthoring)들이 굽는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class ArenaLayoutAuthoring : MonoBehaviour
    {
        // 맵 전체 크기. 화면(ViewSize)보다 크면 카메라가 플레이어를 따라간다.
        // 48x27은 총알이 멀리 흩어져 합성이 너무 드물어서, 총알이 금방 돌아오는 1.5배로 둔다
        public Vector2 ArenaSize = new Vector2(24f, 13.5f);

        [Header("Passage And Gates (바꾼 뒤 Update/Arena Walls And Floor 메뉴로 벽을 다시 만든다)")]
        // 가운데 맵(Arena Size) 네 벽 중앙에 부서지는 문이 있고, 벽 바깥을 한 바퀴 두르는 통로가 있다.
        // 문 하나를 부수면 통로로 나가 맵 둘레를 돌 수 있다
        [Min(1f)] public float SidePassageWidth = 5f;   // 왼쪽·오른쪽 통로 폭
        [Min(1f)] public float EndPassageWidth = 3.5f;  // 아래·위 통로 폭
        [Min(0.1f)] public float WallThickness = 0.5f;
        [Min(0.5f)] public float GateWidth = 3f;
        // 문은 총알이 튕길 때마다 그 총알의 피해만큼 깎인다
        [Min(1f)] public float GateHealth = 150f;
        // 문이 부서지면 그 뒤 통로에 뿌리는 경험치 젬
        public GameObject WallGemPrefab;
        [Min(0)] public int WallRewardGems = 12;
        [Min(1)] public int WallGemValue = 2;

        /// <summary>바깥 경계(가운데 맵 + 안쪽 벽 + 통로 + 바깥 테두리 벽)의 반 크기.</summary>
        public Vector2 OuterHalfSize => ArenaSize * 0.5f
            + new Vector2(WallThickness * 2f + SidePassageWidth, WallThickness * 2f + EndPassageWidth);

        class Baker : Baker<ArenaLayoutAuthoring>
        {
            public override void Bake(ArenaLayoutAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.None);
                // 바깥 경계는 통로와 바깥 테두리 벽까지 포함한 큰 사각형. 그 안의 벽은 벽 상자(WallBlock)들이 막는다
                AddComponent(entity, new ArenaBounds { HalfSize = authoring.OuterHalfSize });
                AddComponent(entity, new WallDamage { Dirty = true });
                AddBuffer<WallBox>(entity);
                AddBuffer<RoomState>(entity);
                AddComponent(entity, new WallSettings
                {
                    GateHealth = authoring.GateHealth,
                    GemPrefab = authoring.WallGemPrefab != null
                        ? GetEntity(authoring.WallGemPrefab, TransformUsageFlags.Dynamic)
                        : Entity.Null,
                    RewardGems = authoring.WallRewardGems,
                    GemValue = authoring.WallGemValue,
                });
            }
        }
    }
}
