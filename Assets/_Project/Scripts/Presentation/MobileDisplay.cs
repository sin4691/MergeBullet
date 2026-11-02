using UnityEngine;

namespace MergeBullet.Presentation
{
    /// <summary>
    /// 휴대폰 화면 설정. 안드로이드는 기본 목표 프레임이 30이라 60으로 올리고,
    /// 조이스틱을 놓고 있어도(발사는 자동) 화면이 꺼지지 않게 한다. PC에서는 아무것도 하지 않는다.
    /// </summary>
    static class MobileDisplay
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Configure()
        {
            if (!Application.isMobilePlatform) return;
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }
    }
}
