using Unity.Mathematics;

namespace MergeBullet.Systems
{
    /// <summary>
    /// 적 정다각형이 어느 쪽을 정면(로컬 +x, 플레이어 쪽)으로 두는지. 총알 판정(BulletEnemyCollisionSystem)과
    /// 메시 생성(에디터 메뉴)이 같은 값을 써야 보이는 모양과 튕기는 면이 맞는다.
    /// - 변이 홀수(삼각형·오각형): 꼭짓점이 정면 → 화살촉처럼 찌르며 다가온다
    /// - 변이 짝수(사각형·육각형·보스): 평평한 면이 정면 → 벽이 밀려오는 느낌
    /// </summary>
    public static class EnemyShape
    {
        /// <summary>정면에 면 하나가 오는 기본 배치에서 얼마나 돌릴지 (라디안).</summary>
        public static float FacingOffset(int sides) => sides % 2 == 1 ? math.PI / sides : 0f;
    }
}
