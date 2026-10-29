using Unity.Entities;

namespace MergeBullet.Components
{
    /// <summary>바닥에 떨어진 경험치 젬. 플레이어 자석 범위에 들어오면 끌려가고, 닿으면 Value만큼 경험치가 오른다.</summary>
    public struct XpGem : IComponentData
    {
        public int Value;
        /// <summary>한 번 끌리기 시작하면 범위를 벗어나도 끝까지 따라간다.</summary>
        public bool Attracted;
    }

    /// <summary>적이 죽을 때 떨어뜨리는 젬. 드롭은 적마다 다를 수 있어서 적 프리팹이 갖는다.</summary>
    public struct XpDrop : IComponentData
    {
        public Entity GemPrefab;
        public int Value;
    }

    /// <summary>플레이어의 젬 줍기 능력치. 자석 범위는 업그레이드로 늘어난다.</summary>
    public struct GemCollector : IComponentData
    {
        public float MagnetRadius;
        public float PickupRadius;
        public float PullSpeed;
    }
}
