using MergeBullet.Components;
using MergeBullet.Meta;
using MergeBullet.Systems;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MergeBullet.UI
{
    /// <summary>
    /// UI Toolkit HUD 컨트롤러. 레이아웃은 GameHud.uxml, 스타일은 GameHud.uss에 있고, 여기서는 값만 채운다.
    /// ECS에서 체력·게임 상태·기록·레벨을 읽어 그리고, ECS에는 재시작 요청과 업그레이드 선택만 쓴다.
    /// 로비로 가는 건 씬 이동이라 여기서 직접 하고, 남은 엔티티 정리는 GameSceneExitCleanupSystem이 한다.
    /// 판이 끝나면 코인 보상을 저장 데이터(SaveSystem)에 반영한다.
    /// 화면 문구는 전부 L10n(UI 문자열 표)에서 가져온다. 언어는 로비에서만 바꾸므로 켜질 때 한 번 채운다.
    /// 메인 씬(SubScene 밖)에 둔다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class GameHud : MonoBehaviour
    {
        const int ChoiceCount = 3;
        static readonly Color HealthLowColor = new Color(0.9f, 0.2f, 0.2f);
        static readonly Color HealthFullColor = new Color(0.3f, 0.85f, 0.4f);

        // UXML의 고정 문구: 요소 이름 → 문자열 키
        static readonly (string Element, string Key)[] StaticTexts =
        {
            ("boss-name", "hud.boss"),
            ("levelup-title", "levelup.title"),
            ("levelup-hint", "levelup.hint"),
            ("gameover-title", "gameover.title"),
            ("restart-button", "gameover.retry"),
            ("lobby-button", "gameover.lobby"),
            ("pause-title", "pause.title"),
            ("resume-button", "pause.resume"),
            ("pause-lobby-button", "pause.lobby"),
        };

        World _world;
        EntityQuery _playerQuery;
        EntityQuery _gameQuery;
        EntityQuery _statsQuery;
        EntityQuery _progressQuery;
        EntityQuery _offerQuery;
        EntityQuery _bossQuery;

        VisualElement _hpFill;
        Label _hpText;
        Label _levelText;
        VisualElement _xpFill;
        Label _timeText;
        Label _killText;
        VisualElement _bossBar;
        VisualElement _bossFill;
        VisualElement _levelUp;
        readonly Button[] _choiceButtons = new Button[ChoiceCount];
        readonly Label[] _choiceTitles = new Label[ChoiceCount];
        readonly Label[] _choiceDescs = new Label[ChoiceCount];
        VisualElement _gameOver;
        Label _finalText;
        Label _rewardText;
        Button _restartButton;
        Button _lobbyButton;
        VisualElement _pause;
        SettingsPanel _settings;

        // 값이 바뀔 때만 문자열을 새로 만든다 (매 프레임 문자열 할당 → GC 방지)
        int _shownHealth = -1;
        int _shownMaxHealth = -1;
        int _shownSeconds = -1;
        int _shownKills = -1;
        int _shownLevel = -1;
        bool _shownGameOver;
        bool _shownPaused;
        // 이번 판 보상을 이미 받았는지 (게임 오버 화면이 떠 있는 동안 한 번만 지급)
        bool _rewarded;
        RunReward _reward;
        // 지금 보여 주는 선택지가 몇 레벨 것인지 (-1: 닫힘). 연속 레벨업이면 열린 채로 내용만 바뀐다
        int _shownOfferLevel;

        void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            L10n.ApplyFont(root);
            foreach ((string element, string key) in StaticTexts)
                root.Q<TextElement>(element).text = L10n.ForInput(key);

            VisualElement hud = root.Q("hud");
            SafeArea.Fit(hud);
            // 휴대폰(과 에디터 시험)에서만 가상 조이스틱. 맨 앞(index 0)에 넣어야 다른 UI 뒤에 깔린다
            if (Application.isMobilePlatform || Application.isEditor)
                hud.Insert(0, new TouchJoystick());
            // 레벨업 카드의 1/2/3 키 표시는 키보드가 없는 휴대폰에서 숨긴다
            if (L10n.IsTouch)
                root.Query<Label>(className: "choice-key").ForEach(label => label.style.display = DisplayStyle.None);

            _hpFill = root.Q("hp-fill");
            _hpText = root.Q<Label>("hp-text");
            _levelText = root.Q<Label>("level-text");
            _xpFill = root.Q("xp-fill");
            _timeText = root.Q<Label>("time-text");
            _killText = root.Q<Label>("kill-text");
            _bossBar = root.Q("boss-bar");
            _bossFill = root.Q("boss-fill");
            _levelUp = root.Q("level-up");
            _gameOver = root.Q("game-over");
            _finalText = root.Q<Label>("final-text");
            _rewardText = root.Q<Label>("reward-text");
            _restartButton = root.Q<Button>("restart-button");
            _restartButton.clicked += RequestRestart;
            _lobbyButton = root.Q<Button>("lobby-button");
            _lobbyButton.clicked += GoToLobby;

            _pause = root.Q("pause");
            _settings = new SettingsPanel(root.Q("pause-settings"));
            root.Q<Button>("pause-button").clicked += () => SetPaused(true);
            root.Q<Button>("resume-button").clicked += () => SetPaused(false);
            root.Q<Button>("pause-lobby-button").clicked += GoToLobby;

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
            _shownPaused = true;
            _shownOfferLevel = int.MinValue;
        }

        void OnDisable()
        {
            if (_restartButton != null) _restartButton.clicked -= RequestRestart;
            if (_lobbyButton != null) _lobbyButton.clicked -= GoToLobby;
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
            _bossQuery.CompleteDependency();

            if (_playerQuery.TryGetSingleton(out PlayerHealth health))
                ShowHealth(health);

            _statsQuery.TryGetSingleton(out GameStats stats);
            ShowStats(stats);
            ShowBoss();

            if (_progressQuery.TryGetSingleton(out PlayerProgress progress))
                ShowLevel(progress);

            _offerQuery.TryGetSingleton(out LevelUpOffer offer);
            ShowLevelUp(offer, progress.Level);

            _gameQuery.TryGetSingleton(out GameState game);
            // 판이 끝난 순간 한 번만 코인을 지급·저장한다 (게임 오버 화면을 그리기 전에)
            if (game.IsGameOver && !_rewarded)
            {
                _reward = SaveSystem.AwardRun(stats);
                _rewarded = true;
            }
            else if (!game.IsGameOver)
            {
                _rewarded = false;
            }

            ShowGameOver(game.IsGameOver, stats);
            ShowPause(game.Paused && !game.IsGameOver);

            HandleKeys(game, offer);
        }

        void HandleKeys(GameState game, LevelUpOffer offer)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (game.IsGameOver)
            {
                if (keyboard.rKey.wasPressedThisFrame) RequestRestart();
                else if (keyboard.escapeKey.wasPressedThisFrame) GoToLobby();
                return;
            }

            // Esc(안드로이드 뒤로 가기도 Esc로 들어온다): 일시정지 켜고 끄기. 레벨업 선택 중에는 무시
            if (keyboard.escapeKey.wasPressedThisFrame && !offer.Active)
            {
                SetPaused(!game.Paused);
                return;
            }
            if (game.Paused) return;

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
            _bossQuery = em.CreateEntityQuery(typeof(Boss), typeof(Enemy), typeof(EnemyHealth));
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
            _hpText.text = L10n.Get("hud.hp", value, max);
        }

        void ShowLevel(PlayerProgress progress)
        {
            int span = progress.NextLevelXp - progress.PrevLevelXp;
            float ratio = span > 0 ? Mathf.Clamp01((float)(progress.Experience - progress.PrevLevelXp) / span) : 0f;
            _xpFill.style.width = Length.Percent(ratio * 100f);

            if (progress.Level == _shownLevel) return;
            _shownLevel = progress.Level;
            _levelText.text = L10n.Get("hud.level", progress.Level);
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
                _killText.text = L10n.Get("hud.kills", stats.Kills);
            }
        }

        // 보스가 있으면 첫 번째 보스의 체력 비율을 보여 준다 (Temp 할당이라 GC가 생기지 않는다)
        void ShowBoss()
        {
            bool hasBoss = !_bossQuery.IsEmpty;
            _bossBar.style.display = hasBoss ? DisplayStyle.Flex : DisplayStyle.None;
            if (!hasBoss) return;

            using var healths = _bossQuery.ToComponentDataArray<EnemyHealth>(Unity.Collections.Allocator.Temp);
            using var enemies = _bossQuery.ToComponentDataArray<Enemy>(Unity.Collections.Allocator.Temp);
            float ratio = enemies[0].MaxHealth > 0f ? Mathf.Clamp01(healths[0].Value / enemies[0].MaxHealth) : 0f;
            _bossFill.style.width = Length.Percent(ratio * 100f);
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

        // 이름·설명 문장은 문자열 표(upgrade.{종류}.name/desc)에, 수치는 실제 적용 값(UpgradeTuning)에서 가져온다
        static (string title, string description) Describe(UpgradeType type)
        {
            string amount = type switch
            {
                UpgradeType.FireRate => L10n.Percent(1f - UpgradeTuning.FireIntervalMultiplier),
                UpgradeType.Damage => L10n.Percent(UpgradeTuning.DamageBonus),
                UpgradeType.MoveSpeed => L10n.Percent(UpgradeTuning.MoveSpeedBonus),
                UpgradeType.BulletSpeed => L10n.Percent(UpgradeTuning.BulletSpeedBonus),
                UpgradeType.MaxHealth => UpgradeTuning.MaxHealthBonus.ToString("0"),
                UpgradeType.ExplosionRadius => L10n.Percent(UpgradeTuning.ExplosionRadiusBonus),
                _ => L10n.Percent(UpgradeTuning.MagnetRadiusBonus),
            };
            return (L10n.Get($"upgrade.{type}.name"), L10n.Get($"upgrade.{type}.desc", amount));
        }

        void ShowGameOver(bool isGameOver, GameStats stats)
        {
            if (isGameOver == _shownGameOver) return;
            _shownGameOver = isGameOver;

            _gameOver.style.display = isGameOver ? DisplayStyle.Flex : DisplayStyle.None;
            if (!isGameOver) return;

            _finalText.text = L10n.Get("gameover.summary", FormatTime(Mathf.FloorToInt(stats.SurvivalTime)), stats.Kills);
            string best = _reward.NewBestTime || _reward.NewBestKills ? "   " + L10n.Get("gameover.newbest") : "";
            _rewardText.text = L10n.Get("gameover.reward", _reward.Coins, SaveSystem.Current.Coins) + best;
        }

        public static string FormatTime(int totalSeconds) => $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";

        // EntityManager 읽기/쓰기는 관련 잡을 알아서 끝낸 뒤 실행된다
        void SelectUpgrade(int index)
        {
            if (_world == null || !_world.IsCreated || !_offerQuery.TryGetSingletonEntity<LevelUpOffer>(out Entity entity)) return;

            LevelUpOffer offer = _world.EntityManager.GetComponentData<LevelUpOffer>(entity);
            if (!offer.Active || offer.Selected >= 0) return;
            offer.Selected = index;
            _world.EntityManager.SetComponentData(entity, offer);
        }

        void ShowPause(bool paused)
        {
            if (paused == _shownPaused) return;
            _shownPaused = paused;
            _pause.style.display = paused ? DisplayStyle.Flex : DisplayStyle.None;
            if (paused) _settings.Refresh();
            // 닫을 때 설정을 저장한다 (슬라이더를 키보드 등으로 바꿔 손을 떼는 이벤트가 없었던 경우까지)
            else GameSettings.Save();
        }

        // 게임 오버·레벨업 선택 중에는 일시정지하지 않는다 (이미 멈춰 있고 각자의 화면이 떠 있음)
        void SetPaused(bool paused)
        {
            if (_world == null || !_world.IsCreated || !_gameQuery.TryGetSingletonEntity<GameState>(out Entity entity)) return;

            GameState game = _world.EntityManager.GetComponentData<GameState>(entity);
            if (game.IsGameOver || game.Paused == paused) return;
            if (paused && _offerQuery.TryGetSingleton(out LevelUpOffer offer) && offer.Active) return;
            game.Paused = paused;
            _world.EntityManager.SetComponentData(entity, game);
        }

        // 휴대폰에서 홈 버튼·전화 등으로 앱이 백그라운드로 가면 자동으로 일시정지 (돌아왔을 때 이미 죽어 있지 않게)
        void OnApplicationPause(bool pausing)
        {
            if (pausing) SetPaused(true);
        }

        void RequestRestart()
        {
            if (_world == null || !_world.IsCreated) return;

            Entity entity = _gameQuery.GetSingletonEntity();
            GameState game = _world.EntityManager.GetComponentData<GameState>(entity);
            game.RestartRequested = true;
            _world.EntityManager.SetComponentData(entity, game);
        }

        // 다시 하기는 씬 안에서 바로 초기화(빠름), 로비는 씬 이동
        static void GoToLobby() => SceneManager.LoadScene(SceneNames.Lobby);
    }
}
