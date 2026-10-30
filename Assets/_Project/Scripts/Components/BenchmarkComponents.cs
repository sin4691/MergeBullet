using Unity.Entities;

namespace MergeBullet.Components
{
    /// <summary>
    /// 벤치마크 모드 싱글톤. 있으면 같은 부하를 유지하는 측정용 판이 된다:
    /// 총알을 TargetBullets개로 계속 채우고, 적과 플레이어는 죽지 않고, 레벨업으로 멈추지 않는다.
    /// WarmupSeconds 뒤 SampleSeconds 동안 프레임 시간을 재서 Console에 요약을 남긴다.
    /// 베이킹으로 붙지 않고, BenchmarkActivationSystem이 시작할 때 붙인다.
    /// </summary>
    public struct BenchmarkSettings : IComponentData
    {
        public Entity BulletPrefab;
        public int TargetBullets;
        public float BulletSpeed;
        public float WarmupSeconds;
        public float SampleSeconds;
        /// <summary>보충 총알 위치·방향용 난수 상태.</summary>
        public Unity.Mathematics.Random Random;
    }

    /// <summary>
    /// 벤치마크 준비 데이터. 항상 베이킹되고, Inspector 체크박스가 켜져 있거나 실행 인자 -benchmark가 있으면
    /// BenchmarkActivationSystem이 BenchmarkSettings를 붙여 측정을 시작한다.
    /// 그래서 빌드 하나로 평소 게임과 측정을 모두 돌릴 수 있다.
    /// </summary>
    public struct BenchmarkConfig : IComponentData
    {
        public bool StartEnabled;
        public int MaxEnemies;
        public BenchmarkSettings Settings;
    }
}
