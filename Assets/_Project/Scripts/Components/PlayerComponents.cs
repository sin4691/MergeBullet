using Unity.Entities;
using Unity.Mathematics;

namespace MergeBullet.Components
{
    /// <summary>플레이어 태그 겸 이동 속도·몸통 반지름.</summary>
    public struct Player : IComponentData
    {
        public float MoveSpeed;
        public float Radius;
    }

    /// <summary>이번 프레임 입력. PlayerInputSystem이 채우고 이동·발사 시스템이 읽는다.</summary>
    public struct PlayerMoveInput : IComponentData
    {
        /// <summary>길이 0~1의 이동 방향.</summary>
        public float2 Move;
    }

    /// <summary>마지막으로 움직인 방향. 조준할 적이 없을 때 발사 방향으로 쓴다.</summary>
    public struct FacingDirection : IComponentData
    {
        public float2 Value;
    }

    /// <summary>일정 간격으로 레벨 1 총알을 쏘는 무기.</summary>
    public struct AutoFire : IComponentData
    {
        public Entity BulletPrefab;
        public float Interval;
        public float BulletSpeed;
        /// <summary>다음 발사까지 남은 시간.</summary>
        public float Cooldown;
    }
}
