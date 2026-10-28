using Unity.Entities;

namespace MergeBullet.Components
{
    /// <summary>방치 소멸 규칙 싱글톤.</summary>
    public struct BulletIdleSettings : IComponentData
    {
        /// <summary>레벨 변화 없이 이 시간(초)이 지나면 소멸.</summary>
        public float Lifetime;
        /// <summary>소멸 직전 이 시간(초) 동안 점점 작아지며 경고한다.</summary>
        public float WarningTime;
    }
}
