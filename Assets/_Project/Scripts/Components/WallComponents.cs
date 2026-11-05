using Unity.Entities;
using Unity.Mathematics;

namespace MergeBullet.Components
{
    /// <summary>
    /// 맵 안의 벽 덩어리 하나 (축 정렬 상자). 움직이지 않으므로 위치를 굽는 시점에 데이터로 저장해 둔다. 아레나 바깥 경계(ArenaBounds) 안을 이 상자들로 채워서
    /// 가운데 맵과 방들만 비워 둔다. 총알은 튕기고, 플레이어·적은 밀려난다. 게임 중에 쓰는 목록은 WallBox 버퍼.
    /// </summary>
    public struct WallBlock : IComponentData
    {
        public float2 Center;
        public float2 HalfSize;
    }

    /// <summary>
    /// 부서지는 문: 벽 덩어리 중 체력이 있는 것 (각 벽 중앙). 부서지면 그리지 않고 충돌 목록에서 빠지며, 뒤의 방(RoomIndex)이 열린다.
    /// 재시작하면 다시 닫힌다.
    /// </summary>
    public struct Gate : IComponentData
    {
        public int Index;
        public int RoomIndex;
        public float Health;
        public float MaxHealth;
        public bool Broken;
    }

    /// <summary>문 뒤의 방 하나 (바닥 영역). 닫혀 있는 동안은 적이 그 안에 나오지 않는다.</summary>
    public struct Room : IComponentData
    {
        public int Index;
        public float2 Center;
        public float2 HalfSize;
    }

    /// <summary>
    /// 지금 서 있는 벽 상자 목록 (Arena 싱글톤의 버퍼). WallSystem이 처음·문이 부서질 때·재시작 때 다시 만든다.
    /// 총알·플레이어·적·스폰이 이 목록 하나만 보고 충돌을 계산한다 (엔티티를 매번 찾지 않게).
    /// </summary>
    public struct WallBox : IBufferElementData
    {
        public float2 Center;
        public float2 HalfSize;
        /// <summary>문이면 문 번호, 아니면 -1. 총알이 문에 튕기면 그 문에 피해를 기록한다.</summary>
        public int Gate;
    }

    /// <summary>방 목록 (Arena 싱글톤의 버퍼). 닫힌 방 안에는 적을 스폰하지 않는다.</summary>
    public struct RoomState : IBufferElementData
    {
        public float2 Center;
        public float2 HalfSize;
        public bool Open;
    }

    /// <summary>부서지는 문 규칙 싱글톤 (Arena에서 베이킹).</summary>
    public struct WallSettings : IComponentData
    {
        public float GateHealth;
        // 보상: 방이 열리면 그 안에 경험치 젬
        public Entity GemPrefab;
        public int RewardGems;
        public int GemValue;
    }

    /// <summary>
    /// 이번 프레임 총알이 각 문(문 번호 순서, 최대 4개)에 준 피해 합. BulletMoveSystem이 쓰고 WallSystem이 다음 프레임 처음에 반영한다.
    /// </summary>
    public struct WallDamage : IComponentData
    {
        public float4 Amount;
        /// <summary>벽 목록을 다시 만들어야 함 (처음·재시작).</summary>
        public bool Dirty;
    }
}
