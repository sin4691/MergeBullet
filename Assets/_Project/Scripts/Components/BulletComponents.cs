using Unity.Entities;
using Unity.Mathematics;

namespace MergeBullet.Components
{
    /// <summary>총알 레벨과 충돌 반지름. 반지름은 레벨로부터 계산된 값을 캐시해 둔 것.</summary>
    public struct Bullet : IComponentData
    {
        public int Level;
        public float Radius;
    }

    /// <summary>XY 평면 속도 (units/sec).</summary>
    public struct BulletVelocity : IComponentData
    {
        public float2 Value;
    }
}
