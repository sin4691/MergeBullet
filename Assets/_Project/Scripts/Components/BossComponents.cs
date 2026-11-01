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
