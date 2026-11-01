using MergeBullet.Components;
using Unity.Entities;
using UnityEngine;

namespace MergeBullet.Presentation
{
    /// <summary>
    /// 게임 씬의 Main Camera에 붙인다. ECS의 CameraView(어디를 볼지)를 실제 카메라 위치·크기에 복사만 한다.
    /// 어디를 볼지는 게임 로직(CameraFollowSystem)이 정하고, 여기서는 기기 화면비에 맞춰 CameraView가
    /// 전부 들어오도록 직교 크기만 계산한다 (16:9보다 좁은 휴대폰은 위아래가, 넓은 화면은 좌우가 더 보인다).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class GameCamera : MonoBehaviour
    {
        Camera _camera;
        World _world;
        EntityQuery _viewQuery;

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
            }

            // CameraView는 잡(CameraFollowJob)이 쓰므로 읽기 전에 끝낸다 (작은 잡이라 기다림이 짧다)
            _viewQuery.CompleteDependency();
            if (!_viewQuery.TryGetSingleton(out CameraView view)) return;

            transform.position = new Vector3(view.Center.x, view.Center.y, transform.position.z);
            _camera.orthographicSize = Mathf.Max(view.HalfSize.y, view.HalfSize.x / _camera.aspect);
        }
    }
}
