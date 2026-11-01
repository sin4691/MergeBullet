using MergeBullet.Meta;
using MergeBullet.Systems;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MergeBullet.UI
{
    /// <summary>
    /// 로비 씬 화면. 저장 데이터(코인·최고 기록)를 보여 주고, 영구 강화를 사고, 시작하면 게임 씬을 불러온다.
    /// 로비에는 ECS 게임 로직이 없다 (게임 시스템들은 게임 씬의 싱글톤이 없으면 돌지 않는다).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class LobbyScreen : MonoBehaviour
    {
        Button _startButton;
        Label _coinsText;
        Label _bestText;
        readonly ShopRow[] _rows = new ShopRow[PermanentUpgrades.Count];

        // 상점 한 줄: 이름 · 현재 단계와 효과 · 구매 버튼
        struct ShopRow
        {
            public Label Name;
            public Label Effect;
            public Button Buy;
        }

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
            _startButton.focusable = false;
            _startButton.clicked += StartGame;
            _coinsText = root.Q<Label>("coins");
            _bestText = root.Q<Label>("best");

            BuildShop(root.Q("shop-list"));
            Refresh();
        }

        void OnDisable()
        {
            if (_startButton != null) _startButton.clicked -= StartGame;
            // 상점 버튼은 UIDocument와 함께 새로 만들어지므로 따로 해제하지 않는다
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
                StartGame();
        }

        // 강화 종류만큼 줄을 코드로 만든다 (종류가 늘어도 UXML을 고칠 필요가 없게)
        void BuildShop(VisualElement list)
        {
            list.Clear();
            for (int i = 0; i < PermanentUpgrades.Count; i++)
            {
                var type = (PermanentUpgradeType)i;
                var row = new VisualElement();
                row.AddToClassList("shop-row");

                var name = new Label(PermanentUpgrades.Get(type).Name);
                name.AddToClassList("shop-name");
                var effect = new Label();
                effect.AddToClassList("shop-effect");
                // 버튼이 포커스를 가져가면 Space(시작 단축키)가 구매로도 눌려서 끈다
                var buy = new Button(() => Buy(type)) { focusable = false };
                buy.AddToClassList("shop-buy");

                row.Add(name);
                row.Add(effect);
                row.Add(buy);
                list.Add(row);
                _rows[i] = new ShopRow { Name = name, Effect = effect, Buy = buy };
            }
        }

        void Buy(PermanentUpgradeType type)
        {
            if (SaveSystem.TryBuyUpgrade(type)) Refresh();
        }

        // 구매할 때만 다시 그린다 (매 프레임 갱신하지 않음)
        void Refresh()
        {
            SaveData save = SaveSystem.Current;
            _coinsText.text = $"{save.Coins} coins";
            _bestText.text = save.RunsPlayed == 0
                ? ""
                : $"Best  {GameHud.FormatTime(Mathf.FloorToInt(save.BestSurvivalTime))}   ·   {save.BestKills} kills";

            for (int i = 0; i < _rows.Length; i++)
            {
                var type = (PermanentUpgradeType)i;
                int level = save.GetUpgradeLevel(type);
                int maxLevel = PermanentUpgrades.Get(type).MaxLevel;
                ShopRow row = _rows[i];

                row.Effect.text = level == 0
                    ? $"Lv 0/{maxLevel}"
                    : $"Lv {level}/{maxLevel}   {PermanentUpgrades.Describe(type, level)}";

                if (level >= maxLevel)
                {
                    row.Buy.text = "MAX";
                    row.Buy.SetEnabled(false);
                    continue;
                }

                int cost = PermanentUpgrades.CostOf(type, level);
                row.Buy.text = $"{cost}";
                row.Buy.SetEnabled(save.Coins >= cost);
            }
        }

        static void StartGame() => SceneManager.LoadScene(SceneNames.Game);
    }
}
