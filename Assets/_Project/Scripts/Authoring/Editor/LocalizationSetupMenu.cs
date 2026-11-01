using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using MergeBullet.UI;

namespace MergeBullet.Editor
{
    /// <summary>
    /// 다국어 설정을 만든다: Localization Settings, 언어(en, ko), UI 문자열 표("UI").
    /// 다시 실행하면 빠진 키·빈 값만 채우고, 이미 들어 있는 번역은 건드리지 않는다 (표에서 고친 번역이 덮이지 않게).
    /// 문자열 추가는 아래 Strings 배열에 한 줄 넣고 메뉴를 다시 실행하면 된다.
    /// </summary>
    static class LocalizationSetupMenu
    {
        const string Root = "Assets/_Project/Localization";
        const string TableName = "UI";
        static readonly string[] LocaleCodes = { "en", "ko" };

        // 키, 영어, 한국어. {0} {1}은 코드에서 넣는 값 (숫자·시간·퍼센트)
        static readonly (string Key, string En, string Ko)[] Strings =
        {
            // 게임 HUD
            ("hud.hp", "HP {0} / {1}", "HP {0} / {1}"),
            ("hud.level", "LV {0}", "LV {0}"),
            ("hud.kills", "KILLS {0}", "처치 {0}"),
            ("hud.boss", "BOSS", "보스"),
            ("levelup.title", "LEVEL UP!", "레벨 업!"),
            ("levelup.hint", "Choose an upgrade  (1 / 2 / 3)", "강화를 고르세요  (1 / 2 / 3)"),
            ("gameover.title", "GAME OVER", "게임 오버"),
            ("gameover.summary", "Survived {0}   ·   {1} kills", "생존 {0}   ·   처치 {1}"),
            ("gameover.reward", "+{0} coins   (total {1})", "+{0} 코인   (보유 {1})"),
            ("gameover.newbest", "NEW BEST!", "최고 기록!"),
            ("gameover.retry", "Retry (R)", "다시 하기 (R)"),
            ("gameover.lobby", "Lobby (Esc)", "로비 (Esc)"),

            // 레벨업 카드 (판 안 강화)
            ("upgrade.FireRate.name", "Rapid Fire", "속사"),
            ("upgrade.Damage.name", "Heavy Rounds", "강화탄"),
            ("upgrade.MoveSpeed.name", "Swift Feet", "신속"),
            ("upgrade.BulletSpeed.name", "Velocity", "탄속"),
            ("upgrade.MaxHealth.name", "Vitality", "활력"),
            ("upgrade.ExplosionRadius.name", "Big Boom", "대폭발"),
            ("upgrade.Magnet.name", "Magnet", "자석"),
            ("upgrade.FireRate.desc", "Fire interval -{0}", "발사 간격 -{0}"),
            ("upgrade.Damage.desc", "Bullet damage +{0}", "총알 피해 +{0}"),
            ("upgrade.MoveSpeed.desc", "Move speed +{0}", "이동 속도 +{0}"),
            ("upgrade.BulletSpeed.desc", "Bullet speed +{0}", "총알 속도 +{0}"),
            ("upgrade.MaxHealth.desc", "Max HP +{0} and heal {0}", "최대 체력 +{0}, {0} 회복"),
            ("upgrade.ExplosionRadius.desc", "Explosion radius +{0}", "폭발 범위 +{0}"),
            ("upgrade.Magnet.desc", "Gem pickup range +{0}", "젬 줍기 범위 +{0}"),

            // 로비
            ("lobby.coins", "{0} coins", "{0} 코인"),
            ("lobby.best", "Best  {0}   ·   {1} kills", "최고  {0}   ·   처치 {1}"),
            ("lobby.characters", "CHARACTER", "캐릭터"),
            ("lobby.upgrades", "UPGRADES", "강화"),
            ("lobby.start", "Start (Space)", "시작 (Space)"),
            ("lobby.select", "Select", "선택"),
            ("lobby.selected", "Selected", "선택됨"),
            ("lobby.unlock", "Unlock {0}", "해금 {0}"),
            ("lobby.max", "MAX", "최대"),
            ("lobby.level", "Lv {0}/{1}", "Lv {0}/{1}"),
            // 언어 버튼에는 지금 언어의 이름을 그 언어로 보여 준다
            ("lobby.language", "English", "한국어"),

            // 영구 강화 (로비 상점)
            ("perm.FireRate.name", "Rapid Fire", "속사"),
            ("perm.MaxHealth.name", "Vitality", "활력"),
            ("perm.Damage.name", "Power", "파워"),
            ("perm.MoveSpeed.name", "Agility", "민첩"),
            ("perm.Magnet.name", "Magnet", "자석"),
            ("perm.FireRate.desc", "Fire interval -{0}", "발사 간격 -{0}"),
            ("perm.MaxHealth.desc", "Max HP +{0}", "최대 체력 +{0}"),
            ("perm.Damage.desc", "Bullet damage +{0}", "총알 피해 +{0}"),
            ("perm.MoveSpeed.desc", "Move speed +{0}", "이동 속도 +{0}"),
            ("perm.Magnet.desc", "Gem pickup range +{0}", "젬 줍기 범위 +{0}"),

            // 캐릭터
            ("character.Blaster.name", "Blaster", "블래스터"),
            ("character.Storm.name", "Storm", "스톰"),
            ("character.Blaster.desc", "Level 4 bullets explode", "레벨 4 총알이 폭발"),
            ("character.Storm.desc", "Level 4 bullets chain lightning", "레벨 4 총알이 연쇄 번개"),
        };

        [MenuItem("MergeBullet/Setup Localization")]
        static void SetupLocalization()
        {
            EnsureFolder(Root);
            SetupAddressables();
            LocalizationSettings settings = LoadOrCreateSettings();
            foreach (string code in LocaleCodes) EnsureLocale(code);
            EnsureStartupSelectors(settings);

            StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection(TableName)
                ?? LocalizationEditorSettings.CreateStringTableCollection(TableName, Root + "/Tables");
            int added = FillMissing(collection);

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log($"Localization 설정 완료: 새로 채운 값 {added}개. {CheckCompleteness(collection)}");
        }

        // 문자열 표는 Addressables로 로드된다. 설정이 없으면 Play 때 빌드 결과(settings.json)를 찾다 실패하므로
        // - 에디터 Play: 빌드 없이 에셋을 바로 읽는 "Use Asset Database" 모드 (이 값은 사용자별 설정이라 Library에 저장됨)
        // - 플레이어 빌드: Addressables 콘텐츠를 같이 빌드 (CI 빌드에서도 표가 들어가게)
        static void SetupAddressables()
        {
            AddressableAssetSettings addressables = AddressableAssetSettingsDefaultObject.GetSettings(true);
            int fastMode = addressables.DataBuilders.FindIndex(builder => builder is BuildScriptFastMode);
            if (fastMode >= 0) addressables.ActivePlayModeDataBuilderIndex = fastMode;
            addressables.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;
            EditorUtility.SetDirty(addressables);
        }

        static LocalizationSettings LoadOrCreateSettings()
        {
            LocalizationSettings settings = LocalizationEditorSettings.ActiveLocalizationSettings;
            if (settings == null)
            {
                // 패키지 안의 읽기 전용 에셋이 잡히지 않도록 Assets 아래에서만 찾는다
                string guid = AssetDatabase.FindAssets("t:LocalizationSettings", new[] { "Assets" }).FirstOrDefault();
                settings = guid != null
                    ? AssetDatabase.LoadAssetAtPath<LocalizationSettings>(AssetDatabase.GUIDToAssetPath(guid))
                    : null;
            }
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<LocalizationSettings>();
                AssetDatabase.CreateAsset(settings, Root + "/LocalizationSettings.asset");
            }
            LocalizationEditorSettings.ActiveLocalizationSettings = settings;
            return settings;
        }

        static void EnsureLocale(string code)
        {
            if (LocalizationEditorSettings.GetLocale(code) != null) return;

            EnsureFolder(Root + "/Locales");
            Locale locale = Locale.CreateLocale(code);
            AssetDatabase.CreateAsset(locale, $"{Root}/Locales/{locale.name}.asset");
            LocalizationEditorSettings.AddLocale(locale);
        }

        // 시작 언어: 저장된 선택 → 없으면 OS 언어 → 그것도 없으면 영어 (앞에서부터 처음 고른 것이 쓰인다)
        static void EnsureStartupSelectors(LocalizationSettings settings)
        {
            var selectors = settings.GetStartupLocaleSelectors();
            if (!selectors.OfType<SavedLocaleSelector>().Any())
                selectors.Insert(0, new SavedLocaleSelector());
            if (!selectors.OfType<SystemLocaleSelector>().Any())
                selectors.Add(new SystemLocaleSelector());
            if (!selectors.OfType<SpecificLocaleSelector>().Any())
                selectors.Add(new SpecificLocaleSelector { LocaleId = new LocaleIdentifier("en") });
        }

        // 표에 빠진 키나 빈 값만 채운다. 이미 번역된 값은 그대로 둔다
        static int FillMissing(StringTableCollection collection)
        {
            int added = 0;
            foreach ((string key, string en, string ko) in Strings)
            {
                SharedTableData.SharedTableEntry shared =
                    collection.SharedData.GetEntry(key) ?? collection.SharedData.AddKey(key);

                foreach (StringTable table in collection.StringTables)
                {
                    string value = table.LocaleIdentifier.Code == "ko" ? ko : en;
                    StringTableEntry entry = table.GetEntry(shared.Id);
                    if (entry != null && !string.IsNullOrWhiteSpace(entry.Value)) continue;

                    table.AddEntry(shared.Id, value);
                    EditorUtility.SetDirty(table);
                    added++;
                }
            }

            EditorUtility.SetDirty(collection);
            EditorUtility.SetDirty(collection.SharedData);
            LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null, collection);
            return added;
        }

        // 모든 키가 모든 언어에 값이 있는지 검사한다. 빠진 값은 게임 화면에 "No translation found"로 나온다
        static string CheckCompleteness(StringTableCollection collection)
        {
            int checkedCount = 0;
            var gaps = new System.Collections.Generic.List<string>();
            foreach (SharedTableData.SharedTableEntry key in collection.SharedData.Entries)
            foreach (StringTable table in collection.StringTables)
            {
                checkedCount++;
                StringTableEntry entry = table.GetEntry(key.Id);
                if (entry == null || string.IsNullOrWhiteSpace(entry.Value))
                    gaps.Add($"{table.LocaleIdentifier.Code} / {key.Key}");
            }

            if (checkedCount == 0) return "검사 불가: 표에 언어나 키가 없습니다.";
            return gaps.Count == 0
                ? $"빠진 번역 없음 ({checkedCount}개 확인)"
                : $"빠진 번역 {gaps.Count}개: " + string.Join(", ", gaps);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
