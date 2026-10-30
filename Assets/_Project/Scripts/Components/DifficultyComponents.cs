using Unity.Entities;

namespace MergeBullet.Components
{
    /// <summary>
    /// 난이도 곡선 싱글톤. 생존 시간 0초의 Start 값에서 RampSeconds초의 End 값까지 선형으로 오르고, 이후엔 End 값으로 고정된다.
    /// 생존 시간이 기준이라 재시작하면 난이도도 자동으로 처음으로 돌아간다.
    /// </summary>
    public struct DifficultySettings : IComponentData
    {
        public float RampSeconds;
        public float StartSpawnInterval;
        public float EndSpawnInterval;
        public int StartMaxEnemies;
        public int EndMaxEnemies;
        /// <summary>RampSeconds 시점의 적 체력 배율 (시작은 1배).</summary>
        public float EndHealthMultiplier;
        /// <summary>RampSeconds 시점의 적 이동 속도 배율 (시작은 1배).</summary>
        public float EndSpeedMultiplier;
    }
}
