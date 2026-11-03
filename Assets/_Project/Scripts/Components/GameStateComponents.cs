using Unity.Entities;

namespace MergeBullet.Components
{
    /// <summary>
    /// 게임 진행 상태 싱글톤. 플레이어 체력이 0이 되면 IsGameOver,
    /// HUD에서 재시작을 누르면 RestartRequested가 켜지고 GameFlowSystem이 처리한다.
    /// Paused는 HUD의 일시정지(Esc·버튼·앱이 백그라운드로 갈 때)가 켜고 끈다. 재시작하면 새 상태라 풀린다.
    /// Cleared는 제한 시간(RunRules)까지 살아남아 판이 끝났다는 뜻이다 (IsGameOver와 함께 켜진다).
    /// </summary>
    public struct GameState : IComponentData
    {
        public bool IsGameOver;
        public bool RestartRequested;
        public bool Paused;
        public bool Cleared;
    }

    /// <summary>판 규칙 싱글톤 (Arena에서 베이킹). 재시작해도 바뀌지 않는 설정이라 GameState와 따로 둔다.</summary>
    public struct RunRules : IComponentData
    {
        /// <summary>이 시간(초)까지 살아남으면 클리어. 0이면 끝없이 계속된다.</summary>
        public float DurationSeconds;
    }

    /// <summary>이번 판 기록 싱글톤. HUD가 표시하고, 재시작하면 0으로 돌아간다.</summary>
    public struct GameStats : IComponentData
    {
        /// <summary>생존 시간(초). 게임 오버면 timeScale 0이라 더 늘지 않는다.</summary>
        public float SurvivalTime;
        public int Kills;
        /// <summary>처치한 보스 수 (Kills에도 포함). 보스는 코인을 따로 더 준다.</summary>
        public int BossKills;
    }
}
