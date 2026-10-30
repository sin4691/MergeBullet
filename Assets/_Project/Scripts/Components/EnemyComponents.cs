using Unity.Entities;

namespace MergeBullet.Components
{
    /// <summary>적 태그 겸 이동 속도·몸통 반지름·접촉 피해.</summary>
    public struct Enemy : IComponentData
    {
        public float MoveSpeed;
        public float Radius;
        /// <summary>플레이어에 닿았을 때 한 번에 주는 피해. 이후 플레이어 무적 시간 동안은 다시 맞지 않는다.</summary>
        public float ContactDamage;
        /// <summary>
        /// 스폰 시 체력. 현재 체력(EnemyHealth)은 잡들이 계속 쓰지만 이 값은 읽기만 해서,
        /// 스폰 시스템이 메인 스레드에서 읽어도 잡을 기다리지 않는다.
        /// </summary>
        public float MaxHealth;
    }

    /// <summary>
    /// 적 체력. 피해를 주는 시스템(총알·폭발)은 깎기만 하고, 0 이하가 된 적의 제거는 EnemyDeathSystem 한 곳에서 한다.
    /// </summary>
    public struct EnemyHealth : IComponentData
    {
        public float Value;
    }

    /// <summary>일정 간격으로 아레나 가장자리에 적을 만드는 스포너 싱글톤.</summary>
    public struct EnemySpawner : IComponentData
    {
        public Entity Prefab;
        public float Interval;
        public int MaxAlive;
        /// <summary>다음 스폰까지 남은 시간.</summary>
        public float Cooldown;
        /// <summary>난이도에 따른 배율. DifficultySystem이 갱신하고, 새로 스폰되는 적에만 적용된다.</summary>
        public float HealthMultiplier;
        public float SpeedMultiplier;
        /// <summary>스폰 위치용 난수 상태. 컴포넌트에 두어야 프레임마다 이어서 뽑힌다.</summary>
        public Unity.Mathematics.Random Random;
    }
}
