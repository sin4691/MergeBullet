using Unity.Entities;
using Unity.Mathematics;

namespace MergeBullet.Components
{
    /// <summary>총알 태그 겸 충돌 반지름. 레벨 등 합성 데이터는 합성 기능을 만들 때 추가한다.</summary>
    public struct Bullet : IComponentData
    {
        public float Radius;
    }

    /// <summary>XY 평면 속도 (units/sec).</summary>
    public struct BulletVelocity : IComponentData
    {
        public float2 Value;
    }
}
