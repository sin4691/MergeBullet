using Unity.Entities;
using Unity.Mathematics;

namespace MergeBullet.Components
{
    public enum BossPhase : byte
    {
        /// <summary>일반 적처럼 플레이어를 쫓는다 (EnemyChaseSystem이 움직임).</summary>
        Chase,
        /// <summary>멈춰서 플레이어를 노려보며 깜빡인다 (돌진 예고).</summary>
        Windup,
        /// <summary>예고 끝에 정한 방향으로 빠르게 돌진한다.</summary>
        Dash,
    }

    /// <summary>예고(Windup) 끝에 하는 공격. 돌진 → 소환 → 돌진 → 충격파 순서로 돌아가며 쓴다.</summary>
    public enum BossAttack : byte
    {
        Dash,
        /// <summary>자기 주위에 부하(삼각형)를 동그랗게 소환한다. 부하가 벽이 되어 총알이 잘 튕긴다.</summary>
        Summon,
        /// <summary>보스를 중심으로 퍼지는 고리. 고리에 닿으면 피해.</summary>
        Shockwave,
    }

    /// <summary>
    /// 보스 패턴 상태. 보스도 Enemy라서 충돌·피해·밀어내기·처치·드롭은 일반 적과 같고, 움직임 패턴만 BossBehaviorSystem이 더한다.
    /// </summary>
    public struct Boss : IComponentData
    {
        public BossPhase Phase;
        /// <summary>현재 단계에 들어온 뒤 지난 시간.</summary>
        public float Timer;
        public float2 DashDirection;
        public float ChaseSeconds;
        public float WindupSeconds;
        public float DashSeconds;
        public float DashSpeed;

        /// <summary>이번 예고 끝에 할 공격과, 공격 순서에서 다음 차례.</summary>
        public BossAttack Attack;
        public int AttackIndex;
        /// <summary>이번 예고의 길이 (공격마다 다르다: 충격파는 피할 시간을 더 준다).</summary>
        public float WindupDuration;

        // 소환
        public Entity SummonPrefab;
        public int SummonCount;
        /// <summary>보스 몸에서 이만큼 떨어진 원 위에 소환한다.</summary>
        public float SummonDistance;

        // 충격파
        public float ShockwaveMaxRadius;
        public float ShockwaveSpeed;
        public float ShockwaveWidth;
        public float ShockwaveDamage;
    }

    /// <summary>
    /// 퍼지는 충격파 고리. 중심에서 Radius만큼 떨어진 곳에 두께 Width의 고리가 있고, 플레이어가 고리에 닿으면
    /// 한 번 피해를 준다 (PlayerDamageSystem). MaxRadius까지 퍼지면 사라진다 (ShockwaveSystem). 그림은 BossTelegraph.
    /// </summary>
    public struct Shockwave : IComponentData
    {
        public float2 Center;
        public float Radius;
        public float MaxRadius;
        public float Speed;
        public float Width;
        public float Damage;
        public bool HitPlayer;
    }

    /// <summary>
    /// 켜져 있으면 일반 추적(EnemyChaseSystem)을 멈추고 보스 패턴이 움직임을 맡는다.
    /// enableable이라 켜고 끌 때 구조 변경이 없다.
    /// </summary>
    public struct BossOverride : IComponentData, IEnableableComponent
    {
    }

    /// <summary>생존 시간 Interval초마다 보스를 하나씩 만드는 스포너 싱글톤.</summary>
    public struct BossSpawner : IComponentData
    {
        public Entity Prefab;
        public float Interval;
        /// <summary>이번 판에 나온 보스 수. 재시작하면 0.</summary>
        public int Spawned;
        /// <summary>보스가 하나 더 나올 때마다 체력에 더해지는 배율 (0.5면 1배, 1.5배, 2배 …).</summary>
        public float HealthGrowthPerBoss;
    }
}
