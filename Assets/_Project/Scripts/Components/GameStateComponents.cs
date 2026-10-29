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
}
