using Unity.Entities;

namespace MergeBullet.Components
{
    /// <summary>
    /// 적 태그 겸 이동 속도·몸통 크기·접촉 피해. 몸은 변이 Sides개인 정다각형이고, Radius는 중심에서 각 변까지의 거리
    /// (내접원 반지름)라 변의 수와 상관없이 "변까지 거리"가 같다. 변이 많을수록 센 적 (삼각형 → 사각형 → 오각형 → 육각형).
    /// </summary>
    public struct Enemy : IComponentData
    {
        public float MoveSpeed;
        public float Radius;
        /// <summary>정다각형 변의 수 (3~8). 총알은 이 다각형의 면에서 튕긴다.</summary>
        public int Sides;
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

    /// <summary>
    /// 일정 간격으로 아레나 가장자리에 적을 만드는 스포너 싱글톤.
    /// 어떤 적을 만들지는 같은 엔티티의 EnemySpawnEntry 버퍼(적 종류별 가중치)에서 고른다.
    /// </summary>
    public struct EnemySpawner : IComponentData
    {
        /// <summary>
        /// 모든 적 종류 중 가장 큰 반지름. 공간 해시 칸 크기의 기준이라, 큰 적이 있어도 충돌을 놓치지 않는다.
        /// </summary>
        public float MaxEnemyRadius;
        /// <summary>난이도 진행도 0~1. DifficultySystem이 갱신하고, 적 종류 가중치를 고를 때 쓴다.</summary>
        public float Progress;
        public float Interval;
        public int MaxAlive;
        /// <summary>다음 스폰까지 남은 시간.</summary>
        public float Cooldown;
        /// <summary>난이도에 따른 배율. DifficultySystem이 갱신하고, 새로 스폰되는 적에만 적용된다.</summary>
        public float HealthMultiplier;
        public float SpeedMultiplier;
        /// <summary>플레이어와 최소 이만큼 떨어진 가장자리에만 스폰한다 (바로 옆에 생겨 피할 수 없는 피격 방지).</summary>
        public float SafeDistance;
        /// <summary>스폰 위치용 난수 상태. 컴포넌트에 두어야 프레임마다 이어서 뽑힌다.</summary>
        public Unity.Mathematics.Random Random;

        // 엘리트: EliteInterval초마다 스폰 테이블의 적 하나를 크고 단단하게 (최대 수와 상관없이). 0이면 없음
        public float EliteInterval;
        public float EliteCooldown;
        public float EliteSize;
        public float EliteHealth;
        public int EliteXp;
    }

    /// <summary>
    /// 스폰 테이블 한 줄. 가중치는 난이도 진행도에 따라 StartWeight → EndWeight로 바뀐다.
    /// 예: 탱커는 0 → 0.4라서 처음엔 안 나오다가 점점 섞여 나온다.
    /// </summary>
    public struct EnemySpawnEntry : IBufferElementData
    {
        public Entity Prefab;
        public float StartWeight;
        public float EndWeight;
    }
}
