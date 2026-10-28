using Unity.Entities;
using Unity.Mathematics;

namespace MergeBullet.Components
{
    /// <summary>아레나 경계 싱글톤. 아레나 중심은 월드 원점(0,0)이다.</summary>
    public struct ArenaBounds : IComponentData
    {
        public float2 HalfSize;
    }

    /// <summary>시작 시 한 번 총알을 뿌리는 요청. BulletSpawnSystem이 처리 후 제거한다.</summary>
    public struct BulletSpawner : IComponentData
    {
        public Entity Prefab;
        public int Count;
        public float Speed;
        public uint Seed;
    }
}
