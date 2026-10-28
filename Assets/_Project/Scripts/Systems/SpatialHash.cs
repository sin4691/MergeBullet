using Unity.Mathematics;

namespace MergeBullet.Systems
{
    /// <summary>
    /// 균일 격자 공간 해시. 셀 크기를 가장 큰 총알의 지름 이상으로 잡으면
    /// 겹칠 수 있는 총알은 항상 자기 셀 + 주변 8칸 안에 있다.
    /// </summary>
    public static class SpatialHash
    {
        public static int2 CellOf(float2 position, float cellSize) => (int2)math.floor(position / cellSize);

        // 서로 다른 셀이 같은 키가 될 수도 있지만, 후보를 거리로 다시 검사하므로 결과는 정확하다
        public static int KeyOf(int2 cell) => (int)math.hash(cell);
    }
}
