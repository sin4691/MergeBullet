using Unity.Entities;

namespace MergeBullet.Components
{
    /// <summary>적 태그 겸 이동 속도·몸통 반지름.</summary>
    public struct Enemy : IComponentData
    {
        public float MoveSpeed;
        public float Radius;
    }

    /// <summary>일정 간격으로 아레나 가장자리에 적을 만드는 스포너 싱글톤.</summary>
    public struct EnemySpawner : IComponentData
    {
        public Entity Prefab;
        public float Interval;
        public int MaxAlive;
        /// <summary>다음 스폰까지 남은 시간.</summary>
        public float Cooldown;
        /// <summary>스폰 위치용 난수 상태. 컴포넌트에 두어야 프레임마다 이어서 뽑힌다.</summary>
        public Unity.Mathematics.Random Random;
    }
}
