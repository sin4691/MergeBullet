using MergeBullet.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace MergeBullet.UI
{
    /// <summary>
    /// 음량·전체 화면 설정 줄을 만든다. 로비 설정 화면과 게임 중 일시정지 화면에 같은 걸 붙여서 한 곳만 고치면 되게.
    /// 값은 GameSettings(Meta)가 들고 있고, 여기서는 보여 주고 바꾸기만 한다.
    /// </summary>
    public class SettingsPanel
    {
        readonly Label _volumeLabel;
        readonly Label _fullscreenLabel;
        readonly Slider _volume;
        readonly Toggle _fullscreen;

        public SettingsPanel(VisualElement container)
        {
            _volume = new Slider(0f, 1f) { focusable = false };
            _volume.AddToClassList("settings-slider");
            _volume.RegisterValueChangedCallback(evt => GameSettings.Volume = evt.newValue);
            // 끄는 동안은 바로 들리게만 하고, 손을 뗄 때 한 번 저장한다 (포인터를 잡은 손잡이에서 올라오는 이벤트)
            _volume.RegisterCallback<PointerUpEvent>(_ => GameSettings.Save());
            _volumeLabel = AddRow(container, _volume);

            // 휴대폰은 항상 전체 화면이라 PC에서만 보인다
            if (!Application.isMobilePlatform)
            {
                _fullscreen = new Toggle { focusable = false };
                _fullscreen.AddToClassList("settings-toggle");
                _fullscreen.RegisterValueChangedCallback(evt => GameSettings.Fullscreen = evt.newValue);
                _fullscreenLabel = AddRow(container, _fullscreen);
            }

            Refresh();
        }

        /// <summary>현재 값과 문구를 다시 채운다 (화면을 열 때, 언어가 바뀔 때).</summary>
        public void Refresh()
        {
            _volumeLabel.text = L10n.Get("settings.volume");
            _volume.SetValueWithoutNotify(GameSettings.Volume);
            if (_fullscreen == null) return;
            _fullscreenLabel.text = L10n.Get("settings.fullscreen");
            _fullscreen.SetValueWithoutNotify(GameSettings.Fullscreen);
        }

        static Label AddRow(VisualElement container, VisualElement control)
        {
            var row = new VisualElement();
            row.AddToClassList("settings-row");
            var label = new Label();
            label.AddToClassList("shop-name");
            row.Add(label);
            row.Add(control);
            container.Add(row);
            return label;
        }
    }
}
