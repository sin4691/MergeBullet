using Unity.Entities;
using Unity.Mathematics;

namespace MergeBullet.Components
{
    /// <summary>
    /// 카메라가 보는 범위 싱글톤 (게임 상태). CameraFollowSystem이 플레이어를 따라 Center를 옮기고,
    /// GameCamera(연출)는 이 값을 실제 카메라에 복사만 한다. 카메라 위치를 ECS에 두는 이유는
    /// 총알 벽·적 스폰 같은 게임 로직이 "지금 화면이 어디인지"를 알아야 하기 때문.
    /// HalfSize는 기기 화면비와 상관없이 고정이라, 어떤 화면에서도 게임 규칙(총알이 튕기는 범위)이 같다.
    /// </summary>
    public struct CameraView : IComponentData
    {
        public float2 Center;
        public float2 HalfSize;
        /// <summary>따라가는 빠르기. 클수록 플레이어에 바짝 붙는다 (지수 감쇠 계수, 1/초).</summary>
        public float FollowSharpness;
        /// <summary>true: 총알이 화면 가장자리에서 튕긴다. false: 아레나 벽에서 튕긴다.</summary>
        public bool BulletsBounceOnView;
        /// <summary>false면 다음 업데이트에 부드럽게 따라가지 않고 플레이어 위치로 바로 맞춘다 (시작·재시작).</summary>
        public bool Snapped;
    }
}
