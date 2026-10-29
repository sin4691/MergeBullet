using MergeBullet.Components;
using MergeBullet.Systems;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace MergeBullet.UI
{
    /// <summary>
    /// UI Toolkit HUD 컨트롤러. 레이아웃은 GameHud.uxml, 스타일은 GameHud.uss에 있고, 여기서는 값만 채운다.
    /// ECS에서 체력·게임 상태·기록·레벨을 읽어 그리고, ECS에는 재시작 요청과 업그레이드 선택만 쓴다.
    /// 메인 씬(SubScene 밖)에 둔다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class GameHud : MonoBehaviour
    {
        const int ChoiceCount = 3;
        static readonly Color HealthLowColor = new Color(0.9f, 0.2f, 0.2f);
        static readonly Color HealthFullColor = new Color(0.3f, 0.85f, 0.4f);

        World _world;
        EntityQuery _playerQuery;
        EntityQuery _gameQuery;
        EntityQuery _statsQuery;
        EntityQuery _progressQuery;
        EntityQuery _offerQuery;

        VisualElement _hpFill;
        Label _hpText;
        Label _levelText;
        VisualElement _xpFill;
        Label _timeText;
        Label _killText;
        VisualElement _levelUp;
        readonly Button[] _choiceButtons = new Button[ChoiceCount];
        readonly Label[] _choiceTitles = new Label[ChoiceCount];
        readonly Label[] _choiceDescs = new Label[ChoiceCount];
        VisualElement _gameOver;
        Label _finalText;
        Button _restartButton;

        // 값이 바뀔 때만 문자열을 새로 만든다 (매 프레임 문자열 할당 → GC 방지)
        int _shownHealth = -1;
        int _shownMaxHealth = -1;
        int _shownSeconds = -1;
        int _shownKills = -1;
        int _shownLevel = -1;
        bool _shownGameOver;
        // 지금 보여 주는 선택지가 몇 레벨 것인지 (-1: 닫힘). 연속 레벨업이면 열린 채로 내용만 바뀐다
        int _shownOfferLevel;

        void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            _hpFill = root.Q("hp-fill");
            _hpText = root.Q<Label>("hp-text");
            _levelText = root.Q<Label>("level-text");
            _xpFill = root.Q("xp-fill");
            _timeText = root.Q<Label>("time-text");
            _killText = root.Q<Label>("kill-text");
            _levelUp = root.Q("level-up");
            _gameOver = root.Q("game-over");
            _finalText = root.Q<Label>("final-text");
            _restartButton = root.Q<Button>("restart-button");
            _restartButton.clicked += RequestRestart;

            for (int i = 0; i < ChoiceCount; i++)
            {
                int index = i; // 람다가 반복 변수 대신 이 복사본을 잡도록
                _choiceButtons[i] = root.Q<Button>($"choice-{i}");
                _choiceTitles[i] = root.Q<Label>($"choice-{i}-title");
                _choiceDescs[i] = root.Q<Label>($"choice-{i}-desc");
                _choiceButtons[i].clicked += () => SelectUpgrade(index);
            }

            // UIDocument가 다시 켜지면 화면이 새로 만들어지므로 다음 Update에서 전부 다시 그리게 한다
            _shownHealth = _shownMaxHealth = _shownSeconds = _shownKills = _shownLevel = -1;
            _shownGameOver = true;
            _shownOfferLevel = int.MinValue;
        }

        void OnDisable()
        {
            if (_restartButton != null) _restartButton.clicked -= RequestRestart;
            // 선택지 버튼은 UIDocument와 함께 새로 만들어지므로 따로 해제하지 않는다
        }

        void Update()
        {
            if (!TryBindWorld()) return;

            // 이 값들을 쓰는 잡(PlayerDamage, EnemyDeath)이 아직 돌고 있을 수 있다.
            // EntityQuery로 직접 읽을 땐 그 잡을 먼저 끝내야 안전 검사 예외가 나지 않는다
            _playerQuery.CompleteDependency();
            _gameQuery.CompleteDependency();
            _statsQuery.CompleteDependency();
            _progressQuery.CompleteDependency();
            _offerQuery.CompleteDependency();

            if (_playerQuery.TryGetSingleton(out PlayerHealth health))
                ShowHealth(health);

            _statsQuery.TryGetSingleton(out GameStats stats);
            ShowStats(stats);

            if (_progressQuery.TryGetSingleton(out PlayerProgress progress))
                ShowLevel(progress, stats.Kills);

            _offerQuery.TryGetSingleton(out LevelUpOffer offer);
            ShowLevelUp(offer, progress.Level);

            _gameQuery.TryGetSingleton(out GameState game);
            ShowGameOver(game.IsGameOver, stats);

            HandleKeys(game, offer);
        }

        void HandleKeys(GameState game, LevelUpOffer offer)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (game.IsGameOver && keyboard.rKey.wasPressedThisFrame)
                RequestRestart();

            if (!offer.Active || offer.Selected >= 0) return;
            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) SelectUpgrade(0);
            else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) SelectUpgrade(1);
            else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) SelectUpgrade(2);
        }

        // 기본 월드는 Play마다 새로 만들어지므로, 바뀌었으면 쿼리를 다시 만든다
        bool TryBindWorld()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return false;
            if (world == _world) return true;

            _world = world;
            EntityManager em = world.EntityManager;
            _playerQuery = em.CreateEntityQuery(typeof(PlayerHealth));
            _gameQuery = em.CreateEntityQuery(typeof(GameState));
            _statsQuery = em.CreateEntityQuery(typeof(GameStats));
            _progressQuery = em.CreateEntityQuery(typeof(PlayerProgress));
            _offerQuery = em.CreateEntityQuery(typeof(LevelUpOffer));
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

        void ShowLevel(PlayerProgress progress, int kills)
        {
            int span = progress.NextLevelKills - progress.PrevLevelKills;
            float ratio = span > 0 ? Mathf.Clamp01((float)(kills - progress.PrevLevelKills) / span) : 0f;
            _xpFill.style.width = Length.Percent(ratio * 100f);

            if (progress.Level == _shownLevel) return;
            _shownLevel = progress.Level;
            _levelText.text = $"LV {progress.Level}";
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

        void ShowLevelUp(LevelUpOffer offer, int level)
        {
            int offerLevel = offer.Active ? level : -1;
            if (offerLevel == _shownOfferLevel) return;
            _shownOfferLevel = offerLevel;

            _levelUp.style.display = offer.Active ? DisplayStyle.Flex : DisplayStyle.None;
            if (!offer.Active) return;

            SetChoice(0, offer.Choice0);
            SetChoice(1, offer.Choice1);
            SetChoice(2, offer.Choice2);
        }

        void SetChoice(int index, UpgradeType type)
        {
            (string title, string description) = Describe(type);
            _choiceTitles[index].text = title;
            _choiceDescs[index].text = description;
        }

        // 업그레이드 이름·설명은 화면 표시용이라 UI가 정한다. 수치는 실제 적용 값(UpgradeTuning)에서 가져온다
        static (string title, string description) Describe(UpgradeType type)
        {
            switch (type)
            {
                case UpgradeType.FireRate:
                    return ("Rapid Fire", $"Fire interval -{Percent(1f - UpgradeTuning.FireIntervalMultiplier)}");
                case UpgradeType.Damage:
                    return ("Heavy Rounds", $"Bullet damage +{Percent(UpgradeTuning.DamageBonus)}");
                case UpgradeType.MoveSpeed:
                    return ("Swift Feet", $"Move speed +{Percent(UpgradeTuning.MoveSpeedBonus)}");
                case UpgradeType.BulletSpeed:
                    return ("Velocity", $"Bullet speed +{Percent(UpgradeTuning.BulletSpeedBonus)}");
                case UpgradeType.MaxHealth:
                    return ("Vitality", $"Max HP +{UpgradeTuning.MaxHealthBonus:0} and heal {UpgradeTuning.MaxHealthBonus:0}");
                case UpgradeType.ExplosionRadius:
                    return ("Big Boom", $"Explosion radius +{Percent(UpgradeTuning.ExplosionRadiusBonus)}");
                default:
                    return (type.ToString(), "");
            }
        }

        static string Percent(float value) => $"{Mathf.RoundToInt(value * 100f)}%";

        void ShowGameOver(bool isGameOver, GameStats stats)
        {
            if (isGameOver == _shownGameOver) return;
            _shownGameOver = isGameOver;

            _gameOver.style.display = isGameOver ? DisplayStyle.Flex : DisplayStyle.None;
            if (isGameOver)
                _finalText.text = $"Survived {FormatTime(Mathf.FloorToInt(stats.SurvivalTime))}   ·   {stats.Kills} kills";
        }

        static string FormatTime(int totalSeconds) => $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";

        // EntityManager 읽기/쓰기는 관련 잡을 알아서 끝낸 뒤 실행된다
        void SelectUpgrade(int index)
        {
            if (_world == null || !_world.IsCreated || !_offerQuery.TryGetSingletonEntity<LevelUpOffer>(out Entity entity)) return;

            LevelUpOffer offer = _world.EntityManager.GetComponentData<LevelUpOffer>(entity);
            if (!offer.Active || offer.Selected >= 0) return;
            offer.Selected = index;
            _world.EntityManager.SetComponentData(entity, offer);
        }

        void RequestRestart()
        {
            if (_world == null || !_world.IsCreated) return;

            Entity entity = _gameQuery.GetSingletonEntity();
            GameState game = _world.EntityManager.GetComponentData<GameState>(entity);
            game.RestartRequested = true;
            _world.EntityManager.SetComponentData(entity, game);
        }
    }
}
