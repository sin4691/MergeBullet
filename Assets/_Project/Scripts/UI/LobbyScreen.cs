using MergeBullet.Meta;
using MergeBullet.Systems;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MergeBullet.UI
{
    /// <summary>
    /// 로비 씬 화면. 저장 데이터(코인·최고 기록)를 보여 주고, 시작하면 게임 씬을 불러온다.
    /// 로비에는 ECS 게임 로직이 없다 (게임 시스템들은 게임 씬의 싱글톤이 없으면 돌지 않는다).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class LobbyScreen : MonoBehaviour
    {
        Button _startButton;

        void Awake()
        {
            // 게임 오버(timeScale 0) 상태에서 로비로 왔을 수 있다
            Time.timeScale = 1f;

            // 빌드를 측정용(-benchmark)으로 실행했으면 로비를 건너뛰고 바로 게임으로
            if (BenchmarkArgs.Requested()) StartGame();
        }

        void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            _startButton = root.Q<Button>("start-button");
            _startButton.clicked += StartGame;

            SaveData save = SaveSystem.Current;
            root.Q<Label>("coins").text = $"{save.Coins} coins";
            root.Q<Label>("best").text = save.RunsPlayed == 0
                ? ""
                : $"Best  {GameHud.FormatTime(Mathf.FloorToInt(save.BestSurvivalTime))}   ·   {save.BestKills} kills";
        }

        void OnDisable()
        {
            if (_startButton != null) _startButton.clicked -= StartGame;
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame))
                StartGame();
        }

        static void StartGame() => SceneManager.LoadScene(SceneNames.Game);
    }
}
