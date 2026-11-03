using UnityEngine;

namespace MergeBullet.Meta
{
    /// <summary>
    /// 플레이어 설정(음량·전체 화면). 음량은 저장 데이터에 남기고 게임을 켤 때 다시 적용한다.
    /// 전체 화면은 Unity가 스스로 기억하므로(다음 실행에도 유지) 따로 저장하지 않는다.
    /// 로비 설정 화면과 게임 중 일시정지 화면이 같은 값을 쓴다.
    /// </summary>
    public static class GameSettings
    {
        /// <summary>전체 음량 0~1. 바꾸면 바로 들리지만 저장은 Save()에서 (슬라이더를 끄는 동안 매번 쓰지 않게).</summary>
        public static float Volume
        {
            get => SaveSystem.Current.Volume;
            set
            {
                SaveSystem.Current.Volume = Mathf.Clamp01(value);
                AudioListener.volume = SaveSystem.Current.Volume;
            }
        }

        /// <summary>PC 전용. 휴대폰은 항상 전체 화면이다.</summary>
        public static bool Fullscreen
        {
            get => Screen.fullScreen;
            set => Screen.fullScreenMode = value ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        }

        public static void Save() => SaveSystem.Save();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ApplySaved()
        {
            AudioListener.volume = Mathf.Clamp01(SaveSystem.Current.Volume);
        }
    }
}
