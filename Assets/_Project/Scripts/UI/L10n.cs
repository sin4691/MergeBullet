using System;
using System.Collections.Generic;
using MergeBullet.Meta;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace MergeBullet.UI
{
    /// <summary>
    /// 화면 문구와 폰트를 지금 언어로 가져오는 얇은 도우미 (문자열 표 "UI", 폰트 표 "Fonts").
    /// 언어 바꾸기는 패키지의 SelectedLocale로만 하고, 고른 언어는 저장해서 다음 실행 때 다시 적용한다.
    /// </summary>
    public static class L10n
    {
        public const string Table = "UI";
        public const string FontTable = "Fonts";
        public const string FontKey = "ui";
        // 각 언어 표에 그 언어의 이름이 그 언어로 들어 있다 (English, 한국어, 日本語 ...)
        const string LanguageNameKey = "lobby.language";

        /// <summary>언어가 바뀌면 화면 문구를 다시 그리도록 알린다.</summary>
        public static event Action Changed;

        static bool _warnedMissingSettings;

        static L10n()
        {
            LocalizationSettings.SelectedLocaleChanged += _ => Changed?.Invoke();
        }

        public static string Get(string key) => Get(key, null);

        public static string Get(string key, params object[] args)
        {
            // 설정 에셋이 아직 없으면(MergeBullet/Update/Localization From CSV 메뉴를 안 돌림) 예외를 쏟는 대신 키를 보여 주고 한 번만 알린다
            if (!LocalizationSettings.HasSettings)
            {
                if (!_warnedMissingSettings)
                {
                    Debug.LogWarning("Localization 설정이 없습니다. MergeBullet > Update > Localization From CSV 메뉴를 실행하세요.");
                    _warnedMissingSettings = true;
                }
                return key;
            }
            return LocalizationSettings.StringDatabase.GetLocalizedString(Table, key, args);
        }

        /// <summary>터치 기기인가 (휴대폰, 에디터의 Device Simulator 포함). 키 안내 문구를 바꾸는 데 쓴다.</summary>
        public static bool IsTouch => Application.isMobilePlatform;

        /// <summary>
        /// 입력 방식에 맞는 문구. 터치 기기에서는 "키.touch" 문구가 표에 있으면 그걸 쓴다
        /// ("Retry (R)" → "Retry"처럼 키보드 안내를 뺀 판). 없으면 보통 문구.
        /// </summary>
        public static string ForInput(string key)
        {
            if (IsTouch && LocalizationSettings.HasSettings)
            {
                string touchKey = key + ".touch";
                var table = LocalizationSettings.StringDatabase.GetTable(Table);
                if (table != null && table.GetEntry(touchKey) != null) return Get(touchKey);
            }
            return Get(key);
        }

        /// <summary>0.15 → "15%"</summary>
        public static string Percent(float value) => $"{Mathf.RoundToInt(value * 100f)}%";

        /// <summary>
        /// 지금 언어의 폰트를 화면 루트에 적용한다 (하위 요소가 상속). 한자는 언어마다 모양이 달라서
        /// 일본어·간체·번체가 각자 맞는 폰트를 쓰게 한다. USS의 기본 폰트(UI-Latin)를 덮어쓴다.
        /// </summary>
        public static void ApplyFont(VisualElement root)
        {
            if (!LocalizationSettings.HasSettings) return;
            FontAsset font = LocalizationSettings.AssetDatabase.GetLocalizedAsset<FontAsset>(FontTable, FontKey);
            if (font != null) root.style.unityFontDefinition = FontDefinition.FromSDFFont(font);
        }

        /// <summary>언어 목록을 각 언어 자신의 이름으로 (언어 선택 드롭다운용).</summary>
        public static List<string> LanguageNames()
        {
            var names = new List<string>();
            if (!LocalizationSettings.HasSettings) return names;
            foreach (Locale locale in LocalizationSettings.AvailableLocales.Locales)
                names.Add(LocalizationSettings.StringDatabase.GetLocalizedString(Table, LanguageNameKey, locale));
            return names;
        }

        /// <summary>지금 언어가 목록에서 몇 번째인지 (LanguageNames와 같은 순서).</summary>
        public static int CurrentLanguageIndex => LocalizationSettings.HasSettings
            ? LocalizationSettings.AvailableLocales.Locales.IndexOf(LocalizationSettings.SelectedLocale)
            : -1;

        /// <summary>목록의 index번째 언어로 바꾸고 저장한다.</summary>
        public static void SetLanguage(int index)
        {
            List<Locale> locales = LocalizationSettings.AvailableLocales.Locales;
            if (index < 0 || index >= locales.Count) return;

            Locale locale = locales[index];
            LocalizationSettings.SelectedLocale = locale;
            SaveSystem.Current.Language = locale.Identifier.Code;
            SaveSystem.Save();
        }
    }

    /// <summary>
    /// 시작 언어 선택기: 저장해 둔 언어가 있으면 그걸로 시작한다. Localization Settings의 선택기 목록 맨 앞에 두고
    /// (Localization From CSV 메뉴가 넣는다), 저장값이 없으면 null을 돌려 다음 선택기(OS 언어 → 영어)에 넘긴다.
    /// </summary>
    [Serializable]
    public class SavedLocaleSelector : IStartupLocaleSelector
    {
        public Locale GetStartupLocale(ILocalesProvider availableLocales)
        {
            string code = SaveSystem.Current.Language;
            return string.IsNullOrEmpty(code) ? null : availableLocales.GetLocale(code);
        }
    }
}
