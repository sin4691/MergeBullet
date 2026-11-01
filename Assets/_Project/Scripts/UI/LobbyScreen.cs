using MergeBullet.Meta;
using MergeBullet.Systems;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MergeBullet.UI
{
    /// <summary>
    /// 로비 씬 화면. 저장 데이터(코인·최고 기록)를 보여 주고, 캐릭터를 고르거나 해금하고, 영구 강화를 사고,
    /// 시작하면 게임 씬을 불러온다. 고른 캐릭터는 게임 씬에서 CharacterSetupSystem이 적용한다.
    /// 로비에는 ECS 게임 로직이 없다 (게임 시스템들은 게임 씬의 싱글톤이 없으면 돌지 않는다).
    /// 언어를 고르면(L10n.Changed) 폰트와 화면 문구를 전부 다시 채운다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class LobbyScreen : MonoBehaviour
    {
        // UXML의 고정 문구: 요소 이름 → 문자열 키
        static readonly (string Element, string Key)[] StaticTexts =
        {
            ("characters-title", "lobby.characters"),
            ("upgrades-title", "lobby.upgrades"),
            ("start-button", "lobby.start"),
        };

        VisualElement _root;
        Button _startButton;
        DropdownField _languageDropdown;
        Label _coinsText;
        Label _bestText;
        readonly ShopRow[] _rows = new ShopRow[PermanentUpgrades.Count];
        readonly ShopRow[] _characterRows = new ShopRow[Characters.Count];

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
            _root = root;
            _startButton = root.Q<Button>("start-button");
            _startButton.focusable = false;
            _startButton.clicked += StartGame;
            _languageDropdown = root.Q<DropdownField>("language-dropdown");
            // 포커스를 가져가면 Space(시작 단축키)가 드롭다운을 여는 데 쓰여서 끈다
            _languageDropdown.focusable = false;
            _languageDropdown.choices = L10n.LanguageNames();
            _languageDropdown.RegisterValueChangedCallback(OnLanguagePicked);
            _coinsText = root.Q<Label>("coins");
            _bestText = root.Q<Label>("best");

            BuildCharacters(root.Q("character-list"));
            BuildShop(root.Q("shop-list"));
            L10n.Changed += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            L10n.Changed -= Refresh;
            if (_startButton != null) _startButton.clicked -= StartGame;
            _languageDropdown?.UnregisterValueChangedCallback(OnLanguagePicked);
            // 상점 버튼은 UIDocument와 함께 새로 만들어지므로 따로 해제하지 않는다
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
                StartGame();
        }

        void BuildCharacters(VisualElement list)
        {
            list.Clear();
            for (int i = 0; i < Characters.Count; i++)
            {
                var type = (CharacterType)i;
                var row = new VisualElement();
                row.AddToClassList("shop-row");

                var swatch = new VisualElement();
                swatch.AddToClassList("character-swatch");
                swatch.style.backgroundColor = Characters.Get(type).Color;
                var name = new Label();
                name.AddToClassList("shop-name");
                var description = new Label();
                description.AddToClassList("shop-effect");
                var button = new Button(() => ChooseCharacter(type)) { focusable = false };
                button.AddToClassList("shop-buy");

                row.Add(swatch);
                row.Add(name);
                row.Add(description);
                row.Add(button);
                list.Add(row);
                _characterRows[i] = new ShopRow { Name = name, Effect = description, Buy = button };
            }
        }

        // 해금된 캐릭터는 선택, 잠긴 캐릭터는 코인으로 해금(해금하면 바로 선택된다)
        void ChooseCharacter(CharacterType type)
        {
            if (SaveSystem.Current.IsUnlocked(type)) SaveSystem.SelectCharacter(type);
            else SaveSystem.TryUnlockCharacter(type);
            Refresh();
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

                var name = new Label();
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

        // 구매·선택·언어 변경 때만 다시 그린다 (매 프레임 갱신하지 않음)
        void Refresh()
        {
            L10n.ApplyFont(_root);
            foreach ((string element, string key) in StaticTexts)
                _root.Q<TextElement>(element).text = L10n.Get(key);
            int language = L10n.CurrentLanguageIndex;
            if (language >= 0 && language < _languageDropdown.choices.Count)
                _languageDropdown.SetValueWithoutNotify(_languageDropdown.choices[language]);

            SaveData save = SaveSystem.Current;
            _coinsText.text = L10n.Get("lobby.coins", save.Coins);
            _bestText.text = save.RunsPlayed == 0
                ? ""
                : L10n.Get("lobby.best", GameHud.FormatTime(Mathf.FloorToInt(save.BestSurvivalTime)), save.BestKills);

            for (int i = 0; i < _characterRows.Length; i++)
            {
                var type = (CharacterType)i;
                ShopRow row = _characterRows[i];
                row.Name.text = L10n.Get($"character.{type}.name");
                row.Effect.text = L10n.Get($"character.{type}.desc");
                bool selected = save.Selected == type;
                row.Buy.parent.EnableInClassList("selected", selected);

                if (save.IsUnlocked(type))
                {
                    row.Buy.text = L10n.Get(selected ? "lobby.selected" : "lobby.select");
                    row.Buy.SetEnabled(!selected);
                    continue;
                }

                int cost = Characters.Get(type).UnlockCost;
                row.Buy.text = L10n.Get("lobby.unlock", cost);
                row.Buy.SetEnabled(save.Coins >= cost);
            }

            for (int i = 0; i < _rows.Length; i++)
            {
                var type = (PermanentUpgradeType)i;
                int level = save.GetUpgradeLevel(type);
                int maxLevel = PermanentUpgrades.Get(type).MaxLevel;
                ShopRow row = _rows[i];

                row.Name.text = L10n.Get($"perm.{type}.name");
                string progress = L10n.Get("lobby.level", level, maxLevel);
                row.Effect.text = level == 0 ? progress : $"{progress}   {DescribePermanent(type, level)}";

                if (level >= maxLevel)
                {
                    row.Buy.text = L10n.Get("lobby.max");
                    row.Buy.SetEnabled(false);
                    continue;
                }

                int cost = PermanentUpgrades.CostOf(type, level);
                row.Buy.text = $"{cost}";
                row.Buy.SetEnabled(save.Coins >= cost);
            }
        }

        // 목록 순서가 언어 순서와 같아서 고른 위치로 언어를 바꾼다 (이름 문자열로 찾지 않음)
        void OnLanguagePicked(ChangeEvent<string> evt) => L10n.SetLanguage(_languageDropdown.index);

        // 체력은 수치(+4), 나머지는 비율(+15%)로 보여 준다
        static string DescribePermanent(PermanentUpgradeType type, int level)
        {
            float total = PermanentUpgrades.TotalEffect(type, level);
            string amount = type == PermanentUpgradeType.MaxHealth ? total.ToString("0") : L10n.Percent(total);
            return L10n.Get($"perm.{type}.desc", amount);
        }

        static void StartGame() => SceneManager.LoadScene(SceneNames.Game);
    }
}
