using System;
using MergeBullet.Meta;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace MergeBullet.UI
{
    /// <summary>
    /// 화면 문구를 지금 언어로 가져오는 얇은 도우미 (UI 문자열 표 "UI").
    /// 언어 바꾸기는 패키지의 SelectedLocale로만 하고, 고른 언어는 저장해서 다음 실행 때 다시 적용한다.
    /// </summary>
    public static class L10n
    {
        const string Table = "UI";

        /// <summary>언어가 바뀌면 화면 문구를 다시 그리도록 알린다.</summary>
        public static event Action Changed;

        static L10n()
        {
            LocalizationSettings.SelectedLocaleChanged += _ => Changed?.Invoke();
        }

        public static string Get(string key) => Get(key, null);

        public static string Get(string key, params object[] args)
        {
            // 설정 에셋이 아직 없으면(Setup Localization 메뉴를 안 돌림) 예외를 쏟는 대신 키를 보여 주고 한 번만 알린다
            if (!LocalizationSettings.HasSettings)
            {
                if (!_warnedMissingSettings)
                {
                    Debug.LogWarning("Localization 설정이 없습니다. MergeBullet > Setup Localization 메뉴를 실행하세요.");
                    _warnedMissingSettings = true;
                }
                return key;
            }
            return LocalizationSettings.StringDatabase.GetLocalizedString(Table, key, args);
        }

        static bool _warnedMissingSettings;

        /// <summary>0.15 → "15%"</summary>
        public static string Percent(float value) => $"{Mathf.RoundToInt(value * 100f)}%";

        /// <summary>다음 언어로 바꾸고 저장한다 (언어 목록을 한 바퀴 돈다).</summary>
        public static void CycleLanguage()
        {
            var locales = LocalizationSettings.AvailableLocales.Locales;
            if (locales.Count == 0) return;

            int current = locales.IndexOf(LocalizationSettings.SelectedLocale);
            Locale next = locales[(current + 1) % locales.Count];
            LocalizationSettings.SelectedLocale = next;

            SaveSystem.Current.Language = next.Identifier.Code;
            SaveSystem.Save();
        }

    }

    /// <summary>
    /// 시작 언어 선택기: 저장해 둔 언어가 있으면 그걸로 시작한다. Localization Settings의 선택기 목록 맨 앞에 두고
    /// (Setup Localization 메뉴가 넣는다), 저장값이 없으면 null을 돌려 다음 선택기(OS 언어 → 영어)에 넘긴다.
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
