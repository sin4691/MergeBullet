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

    /// <summary>
    /// 레벨이 마지막으로 바뀐 뒤 흐른 시간. LastLevel과 현재 레벨을 비교해 변화를 스스로 감지하므로
    /// 합성 시스템은 이 컴포넌트를 몰라도 된다.
    /// </summary>
    public struct BulletIdle : IComponentData
    {
        public float Time;
        public int LastLevel;
    }

    /// <summary>
    /// 방치 시간 초과로 이번 프레임 끝에 제거될 총알 표시.
    /// enableable이라 켜고 끄는 데 구조 변경이 없고, 켜진 즉시 합성 대상에서 빠진다.
    /// </summary>
    public struct BulletExpired : IComponentData, IEnableableComponent
    {
    }
}
