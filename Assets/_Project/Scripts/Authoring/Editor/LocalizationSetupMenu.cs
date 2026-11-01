using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using MergeBullet.UI;
// UnityEditor에도 L10n 클래스가 있어서 이 파일에서는 우리 것을 가리키게 한다
using L10n = MergeBullet.UI.L10n;

namespace MergeBullet.Editor
{
    /// <summary>
    /// 다국어 설정을 만든다: Localization Settings, 언어들, UI 문자열 표("UI"), 언어별 폰트 표("Fonts").
    /// 번역 원본은 ui_strings.csv (첫 줄 = key와 언어 코드). 표를 직접 고치지 말고 CSV를 고친 뒤 이 메뉴를 다시 실행한다.
    /// CSV에 언어 열을 하나 추가하면 그 언어(Locale)와 표가 자동으로 생긴다.
    /// </summary>
    static class LocalizationSetupMenu
    {
        const string Root = "Assets/_Project/Localization";
        const string CsvPath = Root + "/ui_strings.csv";
        const string FontRoot = "Assets/_Project/Fonts";
        const string UssPath = "Assets/_Project/UI/GameHud.uss";

        // 폰트 에셋 이름 → 원본 폰트 파일. 앞 4개는 각 글자권의 대표 폰트, Latin은 라틴·키릴 문자
        static readonly (string Name, string Source)[] Fonts =
        {
            ("UI-Latin", FontRoot + "/NotoSans-Regular.ttf"),
            ("UI-Korean", FontRoot + "/NanumGothic.ttf"),
            ("UI-Japanese", FontRoot + "/NotoSansJP-Regular.otf"),
            ("UI-ChineseSimplified", FontRoot + "/NotoSansSC-Regular.otf"),
            ("UI-ChineseTraditional", FontRoot + "/NotoSansTC-Regular.otf"),
        };

        // 언어별로 쓸 폰트. 한자는 언어마다 글자 모양이 달라서(일본식·간체·번체) 언어에 맞는 폰트를 따로 고른다
        static string FontFor(string localeCode) => localeCode switch
        {
            "ko" => "UI-Korean",
            "ja" => "UI-Japanese",
            "zh-Hans" => "UI-ChineseSimplified",
            "zh-Hant" => "UI-ChineseTraditional",
            _ => "UI-Latin",
        };

        [MenuItem("MergeBullet/Setup Localization")]
        static void SetupLocalization()
        {
            (string[] codes, List<(string Key, string[] Values)> rows) = ReadCsv();

            EnsureFolder(Root);
            SetupAddressables();
            LocalizationSettings settings = LoadOrCreateSettings();
            foreach (string code in codes) EnsureLocale(code);
            EnsureStartupSelectors(settings);

            StringTableCollection strings = LocalizationEditorSettings.GetStringTableCollection(L10n.Table)
                ?? LocalizationEditorSettings.CreateStringTableCollection(L10n.Table, Root + "/Tables");
            AddMissingTables(strings, codes);
            int changed = ApplyStrings(strings, codes, rows);

            Dictionary<string, FontAsset> fonts = SetupFontAssets();
            AssetTableCollection fontTable = LocalizationEditorSettings.GetAssetTableCollection(L10n.FontTable)
                ?? LocalizationEditorSettings.CreateAssetTableCollection(L10n.FontTable, Root + "/Tables");
            AddMissingTables(fontTable, codes);
            foreach (AssetTable table in fontTable.AssetTables)
                fontTable.AddAssetToTable(table, L10n.FontKey, fonts[FontFor(table.LocaleIdentifier.Code)]);
            EditorUtility.SetDirty(fontTable);
            EditorUtility.SetDirty(fontTable.SharedData);

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            // USS가 폰트 에셋을 참조하므로 폰트 에셋이 생긴 뒤 다시 읽게 한다
            AssetDatabase.ImportAsset(UssPath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"Localization 설정 완료: 언어 {codes.Length}개, 바뀐 값 {changed}개. {CheckCompleteness(strings)}");
        }

        // ---------- CSV ----------

        static (string[] Codes, List<(string Key, string[] Values)> Rows) ReadCsv()
        {
            string[] lines = File.ReadAllLines(CsvPath, Encoding.UTF8).Where(l => l.Trim().Length > 0).ToArray();
            string[] header = ParseCsvLine(lines[0]);
            string[] codes = header.Skip(1).ToArray();

            var rows = new List<(string, string[])>();
            for (int i = 1; i < lines.Length; i++)
            {
                string[] cells = ParseCsvLine(lines[i]);
                if (cells.Length != header.Length)
                    throw new System.Exception($"{CsvPath} {i + 1}번째 줄: 칸 수 {cells.Length}개 (머리글은 {header.Length}개)");
                rows.Add((cells[0], cells.Skip(1).ToArray()));
            }
            return (codes, rows);
        }

        // 쉼표가 든 값은 "..."로 감싼다 ("" = 따옴표 한 개)
        static string[] ParseCsvLine(string line)
        {
            var cells = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { cell.Append('"'); i++; }
                    else if (c == '"') quoted = false;
                    else cell.Append(c);
                }
                else if (c == '"') quoted = true;
                else if (c == ',') { cells.Add(cell.ToString()); cell.Clear(); }
                else cell.Append(c);
            }
            cells.Add(cell.ToString());
            return cells.ToArray();
        }

        // ---------- 설정 · 언어 ----------

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

        // 새로 추가한 언어는 기존 표 묶음에 그 언어의 표가 없으므로 만들어 준다
        static void AddMissingTables(LocalizationTableCollection collection, string[] codes)
        {
            foreach (string code in codes)
            {
                var id = new LocaleIdentifier(code);
                if (collection.GetTable(id) == null) collection.AddNewTable(id);
            }
        }

        // ---------- 문자열 ----------

        // CSV가 원본이라 값이 다르면 덮어쓴다. 언어 코드로 열을 찾는다 (표 순서와 CSV 열 순서가 같다고 가정하지 않음)
        static int ApplyStrings(StringTableCollection collection, string[] codes, List<(string Key, string[] Values)> rows)
        {
            int changed = 0;
            foreach ((string key, string[] values) in rows)
            {
                SharedTableData.SharedTableEntry shared =
                    collection.SharedData.GetEntry(key) ?? collection.SharedData.AddKey(key);

                foreach (StringTable table in collection.StringTables)
                {
                    int column = System.Array.IndexOf(codes, table.LocaleIdentifier.Code);
                    if (column < 0) continue;

                    StringTableEntry entry = table.GetEntry(shared.Id);
                    if (entry != null && entry.Value == values[column]) continue;

                    table.AddEntry(shared.Id, values[column]);
                    EditorUtility.SetDirty(table);
                    changed++;
                }
            }

            EditorUtility.SetDirty(collection);
            EditorUtility.SetDirty(collection.SharedData);
            LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null, collection);
            return changed;
        }

        // 모든 키가 모든 언어에 값이 있는지 검사한다. 빠진 값은 게임 화면에 "No translation found"로 나온다
        static string CheckCompleteness(StringTableCollection collection)
        {
            int checkedCount = 0;
            var gaps = new List<string>();
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

        // ---------- 폰트 ----------

        // UI Toolkit 폰트 에셋(SDF)을 만든다. 글자 수가 많은 한중일 폰트라 쓰는 글자만 그때그때 아틀라스에 굽는
        // 동적(Dynamic) 모드 + 아틀라스 여러 장 허용.
        // UI-Latin은 다른 폰트 전부를 대체(fallback)로 둬서, 어떤 언어 이름이든 깨지지 않는 기본 폰트가 된다
        // (나머지는 자기 폰트에 라틴 글자가 있어서 대체가 필요 없고, 서로 가리키면 순환이 생긴다)
        static Dictionary<string, FontAsset> SetupFontAssets()
        {
            EnsureFolder(FontRoot + "/Assets");
            var assets = new Dictionary<string, FontAsset>();
            foreach ((string name, string source) in Fonts)
            {
                string path = $"{FontRoot}/Assets/{name}.asset";
                FontAsset asset = AssetDatabase.LoadAssetAtPath<FontAsset>(path);
                if (asset == null)
                {
                    Font font = AssetDatabase.LoadAssetAtPath<Font>(source);
                    asset = FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                        AtlasPopulationMode.Dynamic, true);
                    asset.name = name;
                    AssetDatabase.CreateAsset(asset, path);
                    // 아틀라스 텍스처와 머티리얼도 에셋 안에 저장해야 다시 열었을 때 남아 있다
                    foreach (Texture2D atlas in asset.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, asset);
                    AssetDatabase.AddObjectToAsset(asset.material, asset);
                }
                assets[name] = asset;
            }

            FontAsset latin = assets["UI-Latin"];
            latin.fallbackFontAssetTable = assets.Values.Where(a => a != latin).ToList();
            foreach (FontAsset asset in assets.Values) EditorUtility.SetDirty(asset);
            return assets;
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
