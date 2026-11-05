using MergeBullet.Components;
using Unity.Entities;
using UnityEngine;

namespace MergeBullet.Authoring
{
    /// <summary>카메라에 보이는 범위와 따라가기 속도. CameraView 싱글톤으로 굽는다.</summary>
    [DisallowMultipleComponent]
    public class CameraViewAuthoring : MonoBehaviour
    {
        // 어떤 기기에서도 이만큼은 보인다 (화면비가 다르면 한쪽이 더 보임). 화면 모드일 때 총알 벽도 이 크기.
        // 시작은 가깝게, 난이도가 오를수록(Difficulty RampSeconds) LateViewSize까지 넓어진다
        public Vector2 ViewSize = new Vector2(14f, 7.875f);
        // 맵(24x13.5) 안에 들어오는 선에서 멀리 (16:9 비율 유지)
        public Vector2 LateViewSize = new Vector2(21f, 11.8125f);
        [Min(0.1f)] public float CameraFollowSharpness = 8f;
        // 켜면 총알이 화면 가장자리에서, 끄면 아레나 벽에서 튕긴다.
        // 화면 벽은 합성·폭발이 화면 끝에 몰려서, 적이 벽이 되는 느낌이 안 나 기본은 끈다
        public bool BulletsBounceOnView;

        class Baker : Baker<CameraViewAuthoring>
        {
            public override void Bake(CameraViewAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new CameraView
                {
                    HalfSize = authoring.ViewSize * 0.5f,
                    StartHalfSize = authoring.ViewSize * 0.5f,
                    LateHalfSize = authoring.LateViewSize * 0.5f,
                    FollowSharpness = authoring.CameraFollowSharpness,
                    BulletsBounceOnView = authoring.BulletsBounceOnView,
                });
            }
        }
    }
}
