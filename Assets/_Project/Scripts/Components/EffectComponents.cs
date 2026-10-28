using Unity.Entities;

namespace MergeBullet.Components
{
    /// <summary>
    /// 최대 레벨 총알이 소멸하면서 남기는 특수효과 설정 싱글톤.
    /// 지금은 폭발 하나뿐이고, 캐릭터가 생기면 캐릭터별로 다른 효과 프리팹을 갖게 된다.
    /// </summary>
    public struct MaxLevelEffect : IComponentData
    {
        public Entity Prefab;
        public float Radius;
        public float Duration;
    }

    /// <summary>폭발 효과 엔티티. Duration 동안 커졌다 줄어든 뒤 사라진다.</summary>
    public struct Explosion : IComponentData
    {
        public float Age;
        public float Duration;
        public float MaxRadius;
    }
}
