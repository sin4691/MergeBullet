using MergeBullet.Systems;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace MergeBullet.UI
{
    /// <summary>
    /// 휴대폰용 플로팅 가상 조이스틱. 화면 아무 곳이나 누르면 그 자리에 받침이 나타나고, 끄는 방향·거리로 이동한다.
    /// 발사는 자동이라 조작은 이동 하나뿐이어서, 위치를 고정하지 않고 손가락이 닿은 곳을 중심으로 삼는다.
    /// 값은 VirtualStickInput에 쓰고 PlayerInputSystem이 키보드·게임패드 입력과 합친다 (입력 → ECS 경로는 하나).
    /// UI Toolkit 포인터 이벤트라 에디터에서는 마우스 드래그로도 시험할 수 있다.
    /// 화면 맨 뒤 층에 깔아서, 레벨업 카드·버튼 같은 UI가 위에 있으면 그쪽이 먼저 터치를 받는다.
    /// </summary>
    public class TouchJoystick : VisualElement
    {
        const float Radius = 110f;      // 끝까지 당기면 1이 되는 거리 (패널 픽셀, 기준 1920x1080)
        const float DeadZone = 0.15f;   // 살짝 닿은 손가락 떨림은 무시

        readonly VisualElement _base;
        readonly VisualElement _knob;
        int _pointerId = PointerId.invalidPointerId;
        Vector2 _origin;

        public TouchJoystick()
        {
            AddToClassList("joystick-area");
            _base = new VisualElement { pickingMode = PickingMode.Ignore };
            _base.AddToClassList("joystick-base");
            _knob = new VisualElement { pickingMode = PickingMode.Ignore };
            _knob.AddToClassList("joystick-knob");
            _base.Add(_knob);
            Add(_base);
            _base.style.display = DisplayStyle.None;

            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCaptureOutEvent>(_ => Release());
            // 화면이 꺼지거나(다른 씬) 패널에서 빠지면 입력이 남지 않게
            RegisterCallback<DetachFromPanelEvent>(_ => Release());
        }

        void OnPointerDown(PointerDownEvent evt)
        {
            if (_pointerId != PointerId.invalidPointerId) return; // 두 번째 손가락은 무시
            _pointerId = evt.pointerId;
            this.CapturePointer(_pointerId);

            _origin = evt.localPosition;
            _base.style.left = _origin.x - Radius;
            _base.style.top = _origin.y - Radius;
            _base.style.display = DisplayStyle.Flex;
            MoveKnob(Vector2.zero);
        }

        void OnPointerMove(PointerMoveEvent evt)
        {
            if (evt.pointerId != _pointerId) return;

            Vector2 offset = Vector2.ClampMagnitude((Vector2)evt.localPosition - _origin, Radius);
            MoveKnob(offset);

            float2 value = new float2(offset.x, -offset.y) / Radius; // 화면은 y가 아래로, 게임은 위로
            VirtualStickInput.Move = math.length(value) < DeadZone ? float2.zero : value;
        }

        void OnPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != _pointerId) return;
            this.ReleasePointer(_pointerId);
            Release();
        }

        void Release()
        {
            _pointerId = PointerId.invalidPointerId;
            _base.style.display = DisplayStyle.None;
            VirtualStickInput.Move = float2.zero;
        }

        void MoveKnob(Vector2 offset)
        {
            _knob.style.translate = new Translate(offset.x, offset.y);
        }
    }
}
