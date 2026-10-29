using Unity.Entities;

namespace MergeBullet.Components
{
    /// <summary>레벨업 때 고를 수 있는 업그레이드 종류. 화면에 보일 이름·설명은 UI가 정한다.</summary>
    public enum UpgradeType : byte
    {
        FireRate,
        Damage,
        MoveSpeed,
        BulletSpeed,
        MaxHealth,
        ExplosionRadius,
        Magnet,
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
    }

    /// <summary>
    /// 업그레이드 전 원래 능력치. 첫 프레임에 한 번 기록해 두고, 재시작하면 이 값으로 되돌린다.
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
    }
}
