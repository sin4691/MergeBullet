using Unity.Entities;

namespace MergeBullet.Components
{
    /// <summary>
    /// 강화 종류. 판 안 레벨업 강화와 로비 상점의 영구 강화가 같은 목록을 쓴다 (한 단계 효과만 다름).
    /// 영구 강화 저장 데이터의 배열 인덱스이기도 해서 순서를 바꾸지 않고 끝에만 추가한다.
    /// 화면에 보일 이름·설명은 UI가 정한다.
    /// </summary>
    public enum UpgradeType : byte
    {
        FireRate,
        Damage,
        MoveSpeed,
        BulletSpeed,
        MaxHealth,
        /// <summary>최대 레벨 효과의 범위 (폭발 반경, 번개가 튀는 거리).</summary>
        ExplosionRadius,
        Magnet,
        /// <summary>총알이 레벨 변화 없이 버티는 시간 (방치 소멸까지).</summary>
        Duration,
    }

    /// <summary>
    /// 플레이어 레벨 진행 싱글톤. 경험치 젬을 주워 Experience가 NextLevelXp에 도달하면 레벨업한다.
    /// </summary>
    public struct PlayerProgress : IComponentData
    {
        public int Level;
        /// <summary>이번 판에 모은 경험치 합계.</summary>
        public int Experience;
        /// <summary>현재 레벨이 시작된 경험치 (경험치 바의 0% 지점).</summary>
        public int PrevLevelXp;
        /// <summary>다음 레벨업 경험치 (경험치 바의 100% 지점).</summary>
        public int NextLevelXp;
        /// <summary>레벨 1→2에 필요한 경험치.</summary>
        public int FirstStep;
        /// <summary>레벨이 오를 때마다 필요 경험치가 이만큼 늘어난다.</summary>
        public int StepGrowth;
        /// <summary>선택지 뽑기용 난수 상태.</summary>
        public Unity.Mathematics.Random Random;
    }

    /// <summary>
    /// 지금 화면에 띄운 레벨업 선택지. Active인 동안 게임이 멈추고,
    /// HUD가 Selected(0~2)를 쓰면 ProgressionSystem이 적용한 뒤 닫는다.
    /// </summary>
    public struct LevelUpOffer : IComponentData
    {
        public bool Active;
        /// <summary>고른 선택지 번호. 아직 안 골랐으면 -1.</summary>
        public int Selected;
        public UpgradeType Choice0;
        public UpgradeType Choice1;
        public UpgradeType Choice2;
        /// <summary>true면 일반 강화 대신 증강 선택지 (Augment0~2).</summary>
        public bool IsAugment;
        /// <summary>실제 선택지 수. 증강이 몇 개 안 남았으면 3보다 적다.</summary>
        public int ChoiceCount;
        public AugmentType Augment0;
        public AugmentType Augment1;
        public AugmentType Augment2;
    }

    /// <summary>
    /// 이번 판의 기본 능력치 (베이킹 값 × 로비에서 산 영구 강화). 첫 프레임에 한 번 기록해 두고, 재시작하면 이 값으로 되돌린다.
    /// 업그레이드 증가량도 이 값 기준이라 여러 번 골라도 일정하게 오른다.
    /// </summary>
    public struct BaseStats : IComponentData
    {
        public bool Captured;
        public float MoveSpeed;
        public float MaxHealth;
        public float FireInterval;
        public float BulletSpeed;
        public float BulletDamage;
        public float ExplosionRadius;
        public float MagnetRadius;
        // 증강이 바꾸는 값들. 재시작하면 이 값으로 되돌린다
        public int MaxLevel;
        public float EffectDamage;
        public float ChainRange;
        public float ChainDamage;
        public float IdleLifetime;
    }
}
