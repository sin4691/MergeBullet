using Unity.Entities;

namespace MergeBullet.Components
{
    /// <summary>레벨 4 총알이 남기는 특수효과 종류. 캐릭터마다 다르다.</summary>
    public enum MaxLevelEffectKind : byte
    {
        /// <summary>그 자리에서 범위 폭발 (Blaster).</summary>
        Explosion,
        /// <summary>가까운 적부터 차례로 튀는 연쇄 번개 (Storm).</summary>
        Lightning,
    }

    /// <summary>
    /// 최대 레벨 총알이 소멸하면서 남기는 특수효과 설정 싱글톤.
    /// 효과 종류(Kind)는 게임 시작 시 선택한 캐릭터에 따라 CharacterSetupSystem이 정한다.
    /// </summary>
    public struct MaxLevelEffect : IComponentData
    {
        public MaxLevelEffectKind Kind;

        // 폭발
        public Entity Prefab;
        public float Radius;
        public float Duration;
        public float Damage;

        // 연쇄 번개: 처음 지점에서 ChainRange 안의 가장 가까운 적 → 그 적에서 다음 가장 가까운 적 … 최대 ChainCount번
        public int ChainCount;
        public float ChainRange;
        public float ChainDamage;
    }

    /// <summary>폭발 효과 엔티티. 생긴 다음 프레임에 범위 피해를 한 번 주고, Duration 동안 커졌다 줄어든 뒤 사라진다.</summary>
    public struct Explosion : IComponentData
    {
        public float Age;
        public float Duration;
        public float MaxRadius;
        public float Damage;
        public bool DamageApplied;
    }
}
