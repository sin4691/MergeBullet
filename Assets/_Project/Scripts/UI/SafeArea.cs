using UnityEngine;
using UnityEngine.UIElements;

namespace MergeBullet.UI
{
    /// <summary>
    /// 휴대폰의 노치·둥근 모서리·제스처 바에 UI가 가려지지 않게, 화면 전체를 덮는 요소를 Screen.safeArea 안으로 줄인다.
    /// 화면 픽셀 → UI 패널 좌표 변환은 RuntimePanelUtils로 (PanelSettings가 1920x1080 기준으로 늘리고 줄이므로).
    /// 크기·회전이 바뀔 때마다 다시 맞춘다. PC처럼 안전 영역이 화면 전체면 아무것도 바뀌지 않는다.
    /// </summary>
    public static class SafeArea
    {
        public static void Fit(VisualElement fullScreen)
        {
            fullScreen.RegisterCallback<GeometryChangedEvent>(_ => Apply(fullScreen));
            Apply(fullScreen);
        }

        static void Apply(VisualElement element)
        {
            IPanel panel = element.panel;
            if (panel == null) return;

            // Screen.safeArea는 왼쪽 아래가 원점, 패널 좌표는 왼쪽 위가 원점이라 y를 뒤집는다
            Rect safe = Screen.safeArea;
            Vector2 topLeft = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(safe.xMin, Screen.height - safe.yMax));
            Vector2 bottomRight = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(safe.xMax, Screen.height - safe.yMin));
            Vector2 screen = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(Screen.width, Screen.height));

            // 같은 값을 다시 넣으면 GeometryChanged가 또 생기지 않지만, 바뀔 때만 써서 확실히 반복을 막는다
            SetIfChanged(element.style.left, topLeft.x, v => element.style.left = v);
            SetIfChanged(element.style.top, topLeft.y, v => element.style.top = v);
            SetIfChanged(element.style.right, screen.x - bottomRight.x, v => element.style.right = v);
            SetIfChanged(element.style.bottom, screen.y - bottomRight.y, v => element.style.bottom = v);
        }

        static void SetIfChanged(StyleLength current, float value, System.Action<float> set)
        {
            if (current.keyword == StyleKeyword.Undefined && Mathf.Approximately(current.value.value, value)) return;
            set(value);
        }
    }
}
