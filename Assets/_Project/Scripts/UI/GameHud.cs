using MergeBullet.Components;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MergeBullet.UI
{
    /// <summary>
    /// 프로토타입용 HUD (IMGUI). ECS에서 체력·게임 상태를 읽어 그리고, ECS에는 재시작 요청만 쓴다.
    /// 메인 씬(SubScene 밖)에 둔다. 정식 UI(UI Toolkit)로 바꿀 때 이 파일만 교체하면 된다.
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        World _world;
        EntityQuery _playerQuery;
        EntityQuery _gameQuery;
        GUIStyle _labelStyle;
        GUIStyle _titleStyle;

        // OnGUI는 한 프레임에 여러 번(Layout, Repaint…) 불리므로 Update에서 한 번 읽어 둔 값으로 그린다
        bool _hasHealth;
        PlayerHealth _health;
        GameState _game;

        void Update()
        {
            _hasHealth = false;
            _game = default;
            if (!TryBindWorld()) return;

            // 이 값을 쓰는 잡(PlayerDamageSystem)이 아직 돌고 있을 수 있다.
            // EntityQuery로 직접 읽을 땐 그 잡을 먼저 끝내야 한다 (안 하면 안전 검사 예외 → 그리는 도중 중단 → 깜빡임)
            _playerQuery.CompleteDependency();
            _gameQuery.CompleteDependency();
            _hasHealth = _playerQuery.TryGetSingleton(out _health);
            _gameQuery.TryGetSingleton(out _game);

            Keyboard keyboard = Keyboard.current;
            if (_game.IsGameOver && keyboard != null && keyboard.rKey.wasPressedThisFrame)
                RequestRestart();
        }

        void OnGUI()
        {
            EnsureStyles();
            if (_hasHealth) DrawHealthBar(_health);
            if (_game.IsGameOver) DrawGameOver();
        }

        // 기본 월드는 Play마다 새로 만들어지므로, 바뀌었으면 쿼리를 다시 만든다
        bool TryBindWorld()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return false;
            if (world == _world) return true;

            _world = world;
            _playerQuery = world.EntityManager.CreateEntityQuery(typeof(PlayerHealth));
            _gameQuery = world.EntityManager.CreateEntityQuery(typeof(GameState));
            return true;
        }

        void RequestRestart()
        {
            if (_world == null || !_world.IsCreated) return;

            // EntityManager 읽기/쓰기는 관련 잡을 알아서 끝낸 뒤 실행된다
            Entity entity = _gameQuery.GetSingletonEntity();
            GameState game = _world.EntityManager.GetComponentData<GameState>(entity);
            game.RestartRequested = true;
            _world.EntityManager.SetComponentData(entity, game);
        }

        void DrawHealthBar(PlayerHealth health)
        {
            var back = new Rect(20f, 20f, 260f, 26f);
            float ratio = health.Max > 0f ? Mathf.Clamp01(health.Value / health.Max) : 0f;
            var fill = new Rect(back.x, back.y, back.width * ratio, back.height);

            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(back, Texture2D.whiteTexture);
            GUI.color = Color.Lerp(new Color(0.9f, 0.2f, 0.2f), new Color(0.3f, 0.85f, 0.4f), ratio);
            GUI.DrawTexture(fill, Texture2D.whiteTexture);
            GUI.color = previous;

            GUI.Label(back, $"HP {Mathf.CeilToInt(health.Value)} / {Mathf.CeilToInt(health.Max)}", _labelStyle);
        }

        void DrawGameOver()
        {
            var screen = new Rect(0f, 0f, Screen.width, Screen.height);
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(screen, Texture2D.whiteTexture);
            GUI.color = previous;

            float centerY = Screen.height * 0.5f;
            GUI.Label(new Rect(0f, centerY - 80f, Screen.width, 60f), "GAME OVER", _titleStyle);
            if (GUI.Button(new Rect(Screen.width * 0.5f - 90f, centerY, 180f, 44f), "Restart (R)"))
                RequestRestart();
        }

        void EnsureStyles()
        {
            if (_labelStyle != null) return;
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
            };
            _titleStyle = new GUIStyle(_labelStyle) { fontSize = 48 };
        }
    }
}
