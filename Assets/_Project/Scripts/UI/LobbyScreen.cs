using System;
using MergeBullet.Meta;
using MergeBullet.Systems;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MergeBullet.UI
{
    /// <summary>
    /// 로비 씬 화면. 타이틀 → 메인 메뉴 → (시작: 캐릭터 고르기 / 강화: 상점 / 설정: 언어) 화면을 오간다.
    /// 화면들은 Lobby.uxml 한 파일에 다 있고, 한 번에 하나만 보이게 display를 바꾼다 (씬을 나누지 않아 전환이 즉시).
    /// 키보드(방향키·Enter·Esc)와 마우스·터치 둘 다 되게, 메뉴 선택은 버튼 포커스 대신 직접 관리한다
    /// (포커스를 쓰면 Space·Enter가 엉뚱한 버튼을 누르는 문제가 있었다).
    /// 고른 캐릭터는 게임 씬에서 CharacterSetupSystem이 적용한다. 로비에는 ECS 게임 로직이 없다.
    /// 언어를 고르면(L10n.Changed) 폰트와 화면 문구를 전부 다시 채운다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class LobbyScreen : MonoBehaviour
    {
        enum Page { Title, Menu, Play, Upgrades, Settings }

        // UXML의 고정 문구: 요소 이름 → 문자열 키
        static readonly (string Element, string Key)[] StaticTexts =
        {
            ("menu-play", "menu.play"),
            ("menu-upgrades", "menu.upgrades"),
            ("menu-settings", "menu.settings"),
            ("menu-quit", "menu.quit"),
            ("characters-title", "lobby.characters"),
            ("upgrades-title", "lobby.upgrades"),
            ("settings-title", "settings.title"),
            ("language-label", "settings.language"),
            ("start-button", "lobby.start"),
            ("play-back", "common.back"),
            ("upgrades-back", "common.back"),
            ("settings-back", "common.back"),
        };

        // 타이틀은 게임을 켰을 때 한 번만. 판을 끝내고 로비로 돌아오면 바로 메뉴 (씬이 바뀌어도 남도록 static)
        static bool s_titleShown;

        VisualElement _root;
        TitleBackground _background;
        VisualElement _wallet;
        Label _titleLogo;
        Label _titlePress;
        Label _coinsText;
        Label _bestText;
        DropdownField _languageDropdown;
        SettingsPanel _settings;
        Page _page;
        VisualElement[] _pages;

        Button[] _menuItems;
        Action[] _menuActions;
        int _menuIndex;

        readonly ShopRow[] _rows = new ShopRow[PermanentUpgrades.Count];
        readonly ShopRow[] _characterCards = new ShopRow[Characters.Count];

        // 상점 줄·캐릭터 카드 공통: 이름 · 설명 · 버튼
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

            _background = new TitleBackground();
            _background.AddToClassList("title-background");
            root.Q("lobby").Insert(0, _background);
            SafeArea.Fit(root.Q("lobby"));

            _wallet = root.Q("wallet");
            _titleLogo = root.Q<Label>("title-logo");
            _titlePress = root.Q<Label>("title-press");
            _coinsText = root.Q<Label>("coins");
            _bestText = root.Q<Label>("best");

            // Page 순서와 같게
            _pages = new[]
            {
                root.Q("title-page"), root.Q("menu-page"), root.Q("play-page"), root.Q("upgrades-page"), root.Q("settings-page"),
            };
            // 타이틀은 아무 곳이나 누르면 넘어간다 (마우스·터치)
            _pages[(int)Page.Title].RegisterCallback<PointerDownEvent>(_ => ShowPage(Page.Menu));

            BuildMenu(root);
            BindButton(root, "start-button", StartGame);
            BindButton(root, "play-back", () => ShowPage(Page.Menu));
            BindButton(root, "upgrades-back", () => ShowPage(Page.Menu));
            BindButton(root, "settings-back", () => ShowPage(Page.Menu));

            _languageDropdown = root.Q<DropdownField>("language-dropdown");
            // 포커스를 가져가면 Space·Enter가 드롭다운을 여는 데 쓰여서 끈다
            _languageDropdown.focusable = false;
            _languageDropdown.choices = L10n.LanguageNames();
            _languageDropdown.RegisterValueChangedCallback(OnLanguagePicked);

            BuildCharacters(root.Q("character-list"));
            BuildShop(root.Q("shop-list"));
            _settings = new SettingsPanel(root.Q("settings-panel"));
            L10n.Changed += Refresh;
            Refresh();

            ShowPage(s_titleShown ? Page.Menu : Page.Title);
            s_titleShown = true;
        }

        void OnDisable()
        {
            L10n.Changed -= Refresh;
            _languageDropdown?.UnregisterValueChangedCallback(OnLanguagePicked);
            // 나머지 버튼은 UIDocument와 함께 새로 만들어지므로 따로 해제하지 않는다
        }

        void Update()
        {
            // 일시정지와 상관없는 연출이라 unscaled
            float time = Time.unscaledTime;
            _background.Tick(Time.unscaledDeltaTime);

            if (_page == Page.Title)
            {
                // 로고는 살짝 숨 쉬듯 커졌다 작아지고, 안내 문구는 깜빡인다
                float pulse = 1f + 0.02f * Mathf.Sin(time * 2f);
                _titleLogo.style.scale = new Scale(new Vector3(pulse, pulse, 1f));
                _titlePress.style.opacity = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(time * 2.2f));
            }

            HandleKeys();
        }

        void HandleKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            bool back = keyboard.escapeKey.wasPressedThisFrame;
            bool confirm = keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame
                || keyboard.spaceKey.wasPressedThisFrame;
            bool up = keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame;
            bool down = keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame;
            bool left = keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame;
            bool right = keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame;

            switch (_page)
            {
                case Page.Title:
                    if (keyboard.anyKey.wasPressedThisFrame) ShowPage(Page.Menu);
                    break;
                case Page.Menu:
                    if (up) SelectMenu(_menuIndex - 1);
                    else if (down) SelectMenu(_menuIndex + 1);
                    else if (confirm) _menuActions[_menuIndex]();
                    else if (back) ShowPage(Page.Title);
                    break;
                case Page.Play:
                    if (left) StepCharacter(-1);
                    else if (right) StepCharacter(1);
                    else if (confirm) StartGame();
                    else if (back) ShowPage(Page.Menu);
                    break;
                default:
                    if (back) ShowPage(Page.Menu);
                    break;
            }
        }

        // ---------- 화면 전환 ----------

        void ShowPage(Page page)
        {
            _page = page;
            for (int i = 0; i < _pages.Length; i++)
                _pages[i].style.display = i == (int)page ? DisplayStyle.Flex : DisplayStyle.None;
            _wallet.style.display = page == Page.Title ? DisplayStyle.None : DisplayStyle.Flex;

            // 새 화면은 투명에서 시작해 USS transition(opacity)으로 나타난다. 같은 프레임에 1로 바꾸면 전환이 안 보여서 다음 프레임에
            VisualElement shown = _pages[(int)page];
            shown.style.opacity = 0f;
            shown.schedule.Execute(() => shown.style.opacity = 1f);

            if (page == Page.Menu) SelectMenu(_menuIndex);
        }

        void BuildMenu(VisualElement root)
        {
            _menuItems = new[]
            {
                root.Q<Button>("menu-play"), root.Q<Button>("menu-upgrades"),
                root.Q<Button>("menu-settings"), root.Q<Button>("menu-quit"),
            };
            _menuActions = new Action[]
            {
                () => ShowPage(Page.Play), () => ShowPage(Page.Upgrades), () => ShowPage(Page.Settings), Quit,
            };

            for (int i = 0; i < _menuItems.Length; i++)
            {
                int index = i; // 람다가 반복 변수 대신 이 복사본을 잡도록
                _menuItems[i].focusable = false;
                _menuItems[i].clicked += () => _menuActions[index]();
                // 마우스를 올리면 키보드 선택도 그 항목으로 옮긴다 (선택 표시가 하나만 남게)
                _menuItems[i].RegisterCallback<PointerEnterEvent>(_ => SelectMenu(index));
            }
        }

        void SelectMenu(int index)
        {
            _menuIndex = (index + _menuItems.Length) % _menuItems.Length;
            for (int i = 0; i < _menuItems.Length; i++)
                _menuItems[i].EnableInClassList("selected", i == _menuIndex);
        }

        static void BindButton(VisualElement root, string name, Action action)
        {
            Button button = root.Q<Button>(name);
            button.focusable = false;
            button.clicked += action;
        }

        // ---------- 캐릭터 ----------

        void BuildCharacters(VisualElement list)
        {
            list.Clear();
            for (int i = 0; i < Characters.Count; i++)
            {
                var type = (CharacterType)i;
                var card = new VisualElement();
                card.AddToClassList("character-card");
                // 카드 어디를 눌러도 선택/해금 (안의 버튼은 상태 표시용이라 클릭을 받지 않게)
                card.RegisterCallback<ClickEvent>(_ => ChooseCharacter(type));

                var swatch = new VisualElement();
                swatch.AddToClassList("card-swatch");
                swatch.style.backgroundColor = Characters.Get(type).Color;
                var name = new Label();
                name.AddToClassList("card-name");
                var description = new Label();
                description.AddToClassList("card-desc");
                var button = new Button { focusable = false, pickingMode = PickingMode.Ignore };
                button.AddToClassList("shop-buy");

                card.Add(swatch);
                card.Add(name);
                card.Add(description);
                card.Add(button);
                list.Add(card);
                _characterCards[i] = new ShopRow { Name = name, Effect = description, Buy = button };
            }
        }

        // 해금된 캐릭터는 선택, 잠긴 캐릭터는 코인으로 해금(해금하면 바로 선택된다)
        void ChooseCharacter(CharacterType type)
        {
            if (SaveSystem.Current.IsUnlocked(type)) SaveSystem.SelectCharacter(type);
            else SaveSystem.TryUnlockCharacter(type);
            Refresh();
        }

        // 좌우 키: 해금된 캐릭터 중 이전/다음으로 (잠긴 캐릭터는 키로 해금하지 않는다. 실수로 코인을 쓰지 않게)
        void StepCharacter(int step)
        {
            SaveData save = SaveSystem.Current;
            int current = (int)save.Selected;
            for (int i = 1; i < Characters.Count; i++)
            {
                var type = (CharacterType)(((current + step * i) % Characters.Count + Characters.Count) % Characters.Count);
                if (!save.IsUnlocked(type)) continue;
                SaveSystem.SelectCharacter(type);
                Refresh();
                return;
            }
        }

        // ---------- 강화 상점 ----------

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
                // 버튼이 포커스를 가져가면 Space·Enter가 구매로도 눌려서 끈다
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

        // ---------- 화면 문구 ----------

        // 구매·선택·언어 변경 때만 다시 그린다 (매 프레임 갱신하지 않음)
        void Refresh()
        {
            L10n.ApplyFont(_root);
            foreach ((string element, string key) in StaticTexts)
                _root.Q<TextElement>(element).text = L10n.ForInput(key);
            // 휴대폰에는 키보드가 없으니 "화면을 터치하세요"
            _titlePress.text = L10n.Get(L10n.IsTouch ? "title.tap" : "title.press");

            int language = L10n.CurrentLanguageIndex;
            if (language >= 0 && language < _languageDropdown.choices.Count)
                _languageDropdown.SetValueWithoutNotify(_languageDropdown.choices[language]);
            _settings?.Refresh();

            SaveData save = SaveSystem.Current;
            _coinsText.text = L10n.Get("lobby.coins", save.Coins);
            _bestText.text = save.RunsPlayed == 0
                ? ""
                : L10n.Get("lobby.best", GameHud.FormatTime(Mathf.FloorToInt(save.BestSurvivalTime)), save.BestKills);

            for (int i = 0; i < _characterCards.Length; i++)
            {
                var type = (CharacterType)i;
                ShopRow card = _characterCards[i];
                card.Name.text = L10n.Get($"character.{type}.name");
                card.Effect.text = L10n.Get($"character.{type}.desc");
                bool selected = save.Selected == type;
                card.Buy.parent.EnableInClassList("selected", selected);

                if (save.IsUnlocked(type))
                {
                    card.Buy.text = L10n.Get(selected ? "lobby.selected" : "lobby.select");
                    card.Buy.SetEnabled(!selected);
                    continue;
                }

                int cost = Characters.Get(type).UnlockCost;
                card.Buy.text = L10n.Get("lobby.unlock", cost);
                card.Buy.SetEnabled(save.Coins >= cost);
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

        // 에디터에서는 Application.Quit이 아무 일도 안 해서 Play를 멈춘다
        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
