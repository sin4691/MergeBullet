using MergeBullet.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace MergeBullet.Systems
{
    /// <summary>일반 적과 보스 스폰이 같이 쓰는 위치 고르기·생성 기록. 두 스포너가 같은 규칙으로 나오게 한곳에 둔다.</summary>
    public static class EnemySpawnUtility
    {
        // 안전 거리를 만족하는 자리를 찾을 때까지 최대 이만큼 다시 뽑는다
        const int MaxSpawnTries = 8;
        // 화면 가장자리에서 이만큼 더 바깥에 나온다 (적 반지름과 별도로)
        const float OffscreenMargin = 0.5f;

        /// <summary>
        /// 적이 나올 테두리(사각형). 카메라가 있으면 화면 바로 바깥, 없으면 아레나 가장자리.
        /// 큰 맵에서 아레나 가장자리에 나오면 플레이어까지 너무 멀어서, 생존 게임처럼 화면 밖 가까이에서 나오게 한다.
        /// </summary>
        public static void SpawnRing(bool hasView, in CameraView view, float2 arenaLimit, float radius,
            out float2 center, out float2 halfSize)
        {
            if (hasView)
            {
                center = view.Center;
                halfSize = view.HalfSize + radius + OffscreenMargin;
            }
            else
            {
                center = float2.zero;
                halfSize = arenaLimit;
            }
        }

        /// <summary>
        /// 적 하나의 생성을 ECB에 기록한다. stats는 배율까지 곱한 최종 능력치이고, 체력은 MaxHealth로 채운다.
        /// 생성은 다음 프레임 시작(BeginSimulation) ECB로 넘겨야 원점에 한 프레임 그려지지 않는다.
        /// </summary>
        public static void RecordSpawn(EntityCommandBuffer ecb, Entity prefab, Enemy stats, float2 position)
        {
            Entity enemy = ecb.Instantiate(prefab);

            // 회전하지 않는 정사각형, 한 변 = 2 * 반지름. 총알은 이 상자 면에서 튕긴다 (BulletEnemyCollisionSystem).
            // 적이 모이면 반듯한 면들이 이어져 벽처럼 보이고, 실제로도 벽처럼 반사한다
            ecb.SetComponent(enemy, LocalTransform.FromPositionRotationScale(
                new float3(position, 0f), quaternion.identity, stats.Radius * 2f));

            ecb.SetComponent(enemy, stats);
            ecb.SetComponent(enemy, new EnemyHealth { Value = stats.MaxHealth });
        }

        // 테두리(ringCenter ± ringHalf)에서 임의로 뽑되, 아레나 안이고 플레이어와 safeDistance 이상 떨어진 첫 자리를 쓴다.
        // 끝까지 못 찾으면(화면이 아레나 모서리에 붙었거나 아레나가 화면보다 작을 때) 아레나 안으로 당긴 후보 중
        // 플레이어에서 가장 먼 자리를 쓴다. 아레나가 화면보다 작으면 결국 아레나 가장자리에서 나온다 (예전 방식)
        public static float2 PickSpawnPoint(ref Random random, float2 arenaLimit, float2 ringCenter, float2 ringHalf,
            float2 player, float safeDistance)
        {
            float2 best = float2.zero;
            float bestDistanceSq = -1f;
            for (int i = 0; i < MaxSpawnTries; i++)
            {
                float2 raw = ringCenter + RandomPointOnEdge(ref random, ringHalf);
                float2 candidate = math.clamp(raw, -arenaLimit, arenaLimit);
                float distanceSq = math.distancesq(candidate, player);
                bool insideArena = math.all(raw == candidate);
                if (insideArena && distanceSq >= safeDistance * safeDistance) return candidate;
                if (distanceSq > bestDistanceSq)
                {
                    bestDistanceSq = distanceSq;
                    best = candidate;
                }
            }
            return best;
        }

        static float2 RandomPointOnEdge(ref Random random, float2 limit)
        {
            float t = random.NextFloat(-1f, 1f);
            switch (random.NextInt(4))
            {
                case 0: return new float2(t * limit.x, limit.y);   // 위
                case 1: return new float2(t * limit.x, -limit.y);  // 아래
                case 2: return new float2(-limit.x, t * limit.y);  // 왼쪽
                default: return new float2(limit.x, t * limit.y);  // 오른쪽
            }
        }
    }
}
