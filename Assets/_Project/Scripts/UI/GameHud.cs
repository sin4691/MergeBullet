using MergeBullet.Components;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace MergeBullet.UI
{
    /// <summary>
    /// UI Toolkit HUD 컨트롤러. 레이아웃은 GameHud.uxml, 스타일은 GameHud.uss에 있고, 여기서는 값만 채운다.
    /// ECS에서 체력·게임 상태·기록을 읽어 그리고, ECS에는 재시작 요청만 쓴다. 메인 씬(SubScene 밖)에 둔다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class GameHud : MonoBehaviour
    {
        static readonly Color HealthLowColor = new Color(0.9f, 0.2f, 0.2f);
        static readonly Color HealthFullColor = new Color(0.3f, 0.85f, 0.4f);

        World _world;
        EntityQuery _playerQuery;
        EntityQuery _gameQuery;
        EntityQuery _statsQuery;

        VisualElement _hpFill;
        Label _hpText;
        Label _timeText;
        Label _killText;
        VisualElement _gameOver;
        Label _finalText;
        Button _restartButton;

        // 값이 바뀔 때만 문자열을 새로 만든다 (매 프레임 문자열 할당 → GC 방지)
        int _shownHealth = -1;
        int _shownMaxHealth = -1;
        int _shownSeconds = -1;
        int _shownKills = -1;
        bool _shownGameOver;

        void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            _hpFill = root.Q("hp-fill");
            _hpText = root.Q<Label>("hp-text");
            _timeText = root.Q<Label>("time-text");
            _killText = root.Q<Label>("kill-text");
            _gameOver = root.Q("game-over");
            _finalText = root.Q<Label>("final-text");
            _restartButton = root.Q<Button>("restart-button");
            _restartButton.clicked += RequestRestart;

            // UIDocument가 다시 켜지면 화면이 새로 만들어지므로 다음 Update에서 전부 다시 그리게 한다
            _shownHealth = _shownMaxHealth = _shownSeconds = _shownKills = -1;
            _shownGameOver = true;
        }

        void OnDisable()
        {
            if (_restartButton != null) _restartButton.clicked -= RequestRestart;
        }

        void Update()
        {
            if (!TryBindWorld()) return;

            // 이 값들을 쓰는 잡(PlayerDamage, EnemyDeath)이 아직 돌고 있을 수 있다.
            // EntityQuery로 직접 읽을 땐 그 잡을 먼저 끝내야 안전 검사 예외가 나지 않는다
            _playerQuery.CompleteDependency();
            _gameQuery.CompleteDependency();
            _statsQuery.CompleteDependency();

            if (_playerQuery.TryGetSingleton(out PlayerHealth health))
                ShowHealth(health);

            _statsQuery.TryGetSingleton(out GameStats stats);
            ShowStats(stats);

            _gameQuery.TryGetSingleton(out GameState game);
            ShowGameOver(game.IsGameOver, stats);

            Keyboard keyboard = Keyboard.current;
            if (game.IsGameOver && keyboard != null && keyboard.rKey.wasPressedThisFrame)
                RequestRestart();
        }

        // 기본 월드는 Play마다 새로 만들어지므로, 바뀌었으면 쿼리를 다시 만든다
        bool TryBindWorld()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return false;
            if (world == _world) return true;

            _world = world;
            _playerQuery = world.EntityManager.CreateEntityQuery(typeof(PlayerHealth));
            _gameQuery = world.EntityManager.CreateEntityQuery(typeof(GameState));
            _statsQuery = world.EntityManager.CreateEntityQuery(typeof(GameStats));
            return true;
        }

        void ShowHealth(PlayerHealth health)
        {
            float ratio = health.Max > 0f ? Mathf.Clamp01(health.Value / health.Max) : 0f;
            _hpFill.style.width = Length.Percent(ratio * 100f);
            _hpFill.style.backgroundColor = Color.Lerp(HealthLowColor, HealthFullColor, ratio);

            int value = Mathf.CeilToInt(health.Value);
            int max = Mathf.CeilToInt(health.Max);
            if (value == _shownHealth && max == _shownMaxHealth) return;
            _shownHealth = value;
            _shownMaxHealth = max;
            _hpText.text = $"HP {value} / {max}";
        }

        void ShowStats(GameStats stats)
        {
            int seconds = Mathf.FloorToInt(stats.SurvivalTime);
            if (seconds != _shownSeconds)
            {
                _shownSeconds = seconds;
                _timeText.text = FormatTime(seconds);
            }

            if (stats.Kills != _shownKills)
            {
                _shownKills = stats.Kills;
                _killText.text = $"KILLS {stats.Kills}";
            }
        }

        void ShowGameOver(bool isGameOver, GameStats stats)
        {
            if (isGameOver == _shownGameOver) return;
            _shownGameOver = isGameOver;

            _gameOver.style.display = isGameOver ? DisplayStyle.Flex : DisplayStyle.None;
            if (isGameOver)
                _finalText.text = $"Survived {FormatTime(Mathf.FloorToInt(stats.SurvivalTime))}   ·   {stats.Kills} kills";
        }

        static string FormatTime(int totalSeconds) => $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";

        void RequestRestart()
        {
            if (_world == null || !_world.IsCreated) return;

            // EntityManager 읽기/쓰기는 관련 잡을 알아서 끝낸 뒤 실행된다
            Entity entity = _gameQuery.GetSingletonEntity();
            GameState game = _world.EntityManager.GetComponentData<GameState>(entity);
            game.RestartRequested = true;
            _world.EntityManager.SetComponentData(entity, game);
        }
    }
}
