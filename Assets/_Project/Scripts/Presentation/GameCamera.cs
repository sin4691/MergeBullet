using MergeBullet.Components;
using Unity.Entities;
using UnityEngine;

namespace MergeBullet.Presentation
{
    /// <summary>
    /// 게임 씬의 Main Camera에 붙인다. ECS의 CameraView(어디를 볼지)를 실제 카메라 위치·크기에 복사만 한다.
    /// 어디를 볼지는 게임 로직(CameraFollowSystem)이 정하고, 여기서는 기기 화면비에 맞춰 CameraView가
    /// 전부 들어오도록 직교 크기만 계산한다 (16:9보다 좁은 휴대폰은 위아래가, 넓은 화면은 좌우가 더 보인다).
    /// 그러다 보니 실제로 보이는 넓이는 화면비마다 달라서, 벽 바깥 여백이 보이지 않게 하는 마지막 정리는
    /// 실제 넓이를 아는 여기서 한다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class GameCamera : MonoBehaviour
    {
        // 바깥 경계 너머로 더 보여 줄 여백. 바깥 경계가 테두리 벽이라 0이면 경계 밖 빈 공간이 안 보인다
        [Min(0f)] public float WallMargin;

        Camera _camera;
        World _world;
        EntityQuery _viewQuery;
        EntityQuery _arenaQuery;

        void Awake()
        {
            _camera = GetComponent<Camera>();
            _camera.orthographic = true;
        }

        // 시뮬레이션(Update)이 끝난 뒤, 그리기 전에 맞춘다
        void LateUpdate()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;
            if (world != _world)
            {
                _world = world;
                _viewQuery = world.EntityManager.CreateEntityQuery(typeof(CameraView));
                _arenaQuery = world.EntityManager.CreateEntityQuery(typeof(ArenaBounds));
            }

            // CameraView는 잡(CameraFollowJob)이 쓰므로 읽기 전에 끝낸다 (작은 잡이라 기다림이 짧다)
            _viewQuery.CompleteDependency();
            if (!_viewQuery.TryGetSingleton(out CameraView view)) return;

            float aspect = _camera.aspect;
            float size = Mathf.Max(view.HalfSize.y, view.HalfSize.x / aspect);
            Vector2 center = new Vector2(view.Center.x, view.Center.y);

            if (_arenaQuery.TryGetSingleton(out ArenaBounds arena))
            {
                // 벽까지만 보이게: 보이는 넓이가 (아레나 + 벽)보다 크면 줄이고, 카메라가 벽 너머로 가지 않게 위치를 자른다
                Vector2 limit = new Vector2(arena.HalfSize.x, arena.HalfSize.y) + Vector2.one * WallMargin;
                size = Mathf.Min(size, limit.y, limit.x / aspect);
                Vector2 visible = new Vector2(size * aspect, size);
                Vector2 room = Vector2.Max(limit - visible, Vector2.zero);
                var arenaCenter = new Vector2(arena.Center.x, arena.Center.y);
                center = Vector2.Max(Vector2.Min(center, arenaCenter + room), arenaCenter - room);
            }

            transform.position = new Vector3(center.x, center.y, transform.position.z);
            _camera.orthographicSize = size;
        }
    }
}
