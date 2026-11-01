using Unity.Entities;

namespace MergeBullet.Components
{
    /// <summary>
    /// 로비 상점에서 산 영구 강화가 이번 판에 주는 보너스. 게임 씬에 들어올 때 PermanentBonusSystem이 저장 데이터로 만들고,
    /// ProgressionSystem이 기본 능력치를 기록할 때 곱한다. 값이 0/1이면 보너스 없음.
    /// </summary>
    public struct PermanentBonus : IComponentData
    {
        public float FireIntervalMultiplier;
        public float BonusMaxHealth;
        public float DamageMultiplier;
        public float MoveSpeedMultiplier;
        public float MagnetRadiusMultiplier;

        public static PermanentBonus None => new PermanentBonus
        {
            FireIntervalMultiplier = 1f,
            DamageMultiplier = 1f,
            MoveSpeedMultiplier = 1f,
            MagnetRadiusMultiplier = 1f,
        };
    }
}
