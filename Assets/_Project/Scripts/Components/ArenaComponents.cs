using Unity.Entities;
using Unity.Mathematics;

namespace MergeBullet.Components
{
    /// <summary>
    /// 아레나 바깥 경계 싱글톤 (중심 + 반 크기, 범위는 Center ± HalfSize). 가운데 맵과 문 뒤 방들을 모두 감싸는 사각형이고,
    /// 그 안의 벽은 벽 상자(WallBox)가 막는다. 지금은 중심이 원점이지만, 맵이 한쪽으로 치우친 구성도 되도록 중심을 따로 둔다.
    /// </summary>
    public struct ArenaBounds : IComponentData
    {
        public float2 Center;
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
