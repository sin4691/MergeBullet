using Unity.Entities;

namespace MergeBullet.Components
{
    /// <summary>
    /// 벤치마크 모드 싱글톤. 있으면 같은 부하를 유지하는 측정용 판이 된다:
    /// 총알을 TargetBullets개로 계속 채우고, 적과 플레이어는 죽지 않고, 레벨업으로 멈추지 않는다.
    /// WarmupSeconds 뒤 SampleSeconds 동안 프레임 시간을 재서 Console에 요약을 남긴다.
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
}
