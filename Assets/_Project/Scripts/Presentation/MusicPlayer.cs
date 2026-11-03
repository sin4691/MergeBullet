using MergeBullet.Meta;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MergeBullet.Presentation
{
    /// <summary>
    /// 배경음악. 게임을 켤 때 스스로 하나 만들어져 씬이 바뀌어도 남고(DontDestroyOnLoad), 씬에 맞는 곡으로 바꾼다
    /// (로비 = 잔잔한 곡, 게임 = 빠른 곡). 곡이 바뀔 때 끊기지 않게 AudioSource 두 개로 엇갈려 페이드한다.
    /// 일시정지·게임 오버(timeScale 0)에도 음악은 계속 흐르도록 페이드는 unscaled 시간으로 한다.
    /// 씬에 따로 배치할 필요가 없어서 로비·게임 씬 어느 쪽에서 Play해도 바로 들린다.
    /// </summary>
    public class MusicPlayer : MonoBehaviour
    {
        const float FadeSeconds = 1.2f;
        const string GameSceneName = "Game";

        readonly AudioSource[] _sources = new AudioSource[2];
        int _current;
        AudioClip _lobbyClip;
        AudioClip _gameClip;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            var go = new GameObject("MusicPlayer");
            DontDestroyOnLoad(go);
            go.AddComponent<MusicPlayer>();
        }

        void Awake()
        {
            for (int i = 0; i < _sources.Length; i++)
            {
                _sources[i] = gameObject.AddComponent<AudioSource>();
                _sources[i].loop = true;
                _sources[i].playOnAwake = false;
                _sources[i].volume = 0f;
            }
        }

        void OnEnable()
        {
            SceneManager.activeSceneChanged += OnSceneChanged;
            PlayFor(SceneManager.GetActiveScene());
        }

        void OnDisable()
        {
            SceneManager.activeSceneChanged -= OnSceneChanged;
        }

        void OnSceneChanged(Scene previous, Scene next) => PlayFor(next);

        void PlayFor(Scene scene)
        {
            // 곡은 처음 필요할 때 한 번만 만든다 (합성에 수십 ms가 걸려서 시작할 때 둘 다 만들지 않음)
            AudioClip clip = scene.name == GameSceneName
                ? _gameClip ??= ProceduralMusic.CreateGame()
                : _lobbyClip ??= ProceduralMusic.CreateLobby();

            if (_sources[_current].clip == clip && _sources[_current].isPlaying) return;

            // 다른 쪽 소스에서 새 곡을 0부터 시작하고, Update에서 두 소스의 음량을 엇갈려 옮긴다
            _current = 1 - _current;
            AudioSource next = _sources[_current];
            next.clip = clip;
            next.volume = 0f;
            next.Play();
            _fade = 0f;
        }

        // 페이드 진행도 0 → 1. 음량은 매 프레임 "설정 음량 × 진행도"로 다시 계산해서, 슬라이더를 움직이면 바로 따라간다
        float _fade = 1f;

        void Update()
        {
            float target = GameSettings.MusicVolume;
            _fade = Mathf.Min(1f, _fade + Time.unscaledDeltaTime / FadeSeconds);

            AudioSource current = _sources[_current];
            AudioSource previous = _sources[1 - _current];
            current.volume = target * _fade;
            previous.volume = target * (1f - _fade);
            if (_fade >= 1f && previous.isPlaying) previous.Stop();
        }
    }
}
