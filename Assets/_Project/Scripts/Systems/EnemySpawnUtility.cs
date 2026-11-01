using MergeBullet.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace MergeBullet.Systems
{
    /// <summary>일반 적과 보스 스폰이 같이 쓰는 위치 고르기·생성 기록. 두 스포너가 같은 규칙으로 나오게 한곳에 둔다.</summary>
    public static class EnemySpawnUtility
    {
        // 마름모 옆폭을 줄여(로컬 Y) 앞뒤로 긴 모양 → 어느 쪽을 향하는지 보인다.
        // 앞뒤 길이는 그대로 2 * 반지름이라 진행 방향으로는 충돌 원과 크기가 맞는다
        const float BodyWidth = 0.55f;
        // 안전 거리를 만족하는 자리를 찾을 때까지 최대 이만큼 다시 뽑는다
        const int MaxSpawnTries = 8;

        /// <summary>
        /// 적 하나의 생성을 ECB에 기록한다. stats는 배율까지 곱한 최종 능력치이고, 체력은 MaxHealth로 채운다.
        /// 생성은 다음 프레임 시작(BeginSimulation) ECB로 넘겨야 원점에 한 프레임 그려지지 않는다.
        /// </summary>
        public static void RecordSpawn(EntityCommandBuffer ecb, Entity prefab, Enemy stats, float2 position)
        {
            Entity enemy = ecb.Instantiate(prefab);

            // 모양(PostTransformMatrix): 정육면체를 45도 돌려 마름모로 만든 뒤 옆폭(로컬 Y)을 줄인다.
            // 방향(LocalTransform.Rotation)은 이와 분리돼서, 추적 시스템은 회전값만 바꾸면 된다.
            ecb.AddComponent(enemy, new PostTransformMatrix
            {
                Value = math.mul(float4x4.Scale(1f, BodyWidth, 1f), float4x4.RotateZ(math.PI * 0.25f)),
            });

            // 대각선 절반이 반지름이 되도록 한 변을 radius * √2로. 첫 프레임은 아레나 중앙을 바라본다
            float facing = math.atan2(-position.y, -position.x);
            ecb.SetComponent(enemy, LocalTransform.FromPositionRotationScale(
                new float3(position, 0f), quaternion.RotateZ(facing), stats.Radius * math.SQRT2));

            ecb.SetComponent(enemy, stats);
            ecb.SetComponent(enemy, new EnemyHealth { Value = stats.MaxHealth });
        }

        // 가장자리에서 임의로 뽑되, 플레이어와 safeDistance 이상 떨어진 첫 자리를 쓴다.
        // 끝까지 못 찾으면(플레이어가 모서리에 있는 등) 뽑은 것 중 가장 먼 자리를 쓴다
        public static float2 PickSpawnPoint(ref Random random, float2 limit, float2 player, float safeDistance)
        {
            float2 best = float2.zero;
            float bestDistanceSq = -1f;
            for (int i = 0; i < MaxSpawnTries; i++)
            {
                float2 candidate = RandomPointOnEdge(ref random, limit);
                float distanceSq = math.distancesq(candidate, player);
                if (distanceSq >= safeDistance * safeDistance) return candidate;
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
