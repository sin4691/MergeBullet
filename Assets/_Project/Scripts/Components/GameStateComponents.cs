using Unity.Entities;

namespace MergeBullet.Components
{
    /// <summary>
    /// 게임 진행 상태 싱글톤. 플레이어 체력이 0이 되면 IsGameOver,
    /// HUD에서 재시작을 누르면 RestartRequested가 켜지고 GameFlowSystem이 처리한다.
    /// </summary>
    public struct GameState : IComponentData
    {
        public bool IsGameOver;
        public bool RestartRequested;
    }

    /// <summary>이번 판 기록 싱글톤. HUD가 표시하고, 재시작하면 0으로 돌아간다.</summary>
    public struct GameStats : IComponentData
    {
        /// <summary>생존 시간(초). 게임 오버면 timeScale 0이라 더 늘지 않는다.</summary>
        public float SurvivalTime;
        public int Kills;
    }
}
