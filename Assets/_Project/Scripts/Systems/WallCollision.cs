using MergeBullet.Components;
using Unity.Collections;
using Unity.Mathematics;

namespace MergeBullet.Systems
{
    /// <summary>
    /// 원(총알·플레이어·적)과 벽 상자(WallBox) 사이 계산. 벽은 수십 개뿐이라 공간 해시 없이 전부 훑는다.
    /// 면에 닿으면 반듯하게, 모서리에 닿으면 모서리에서 원 쪽으로 밀어낸다 (적 상자 판정과 같은 방식).
    /// </summary>
    public static class WallCollision
    {
        /// <summary>
        /// 원을 겹친 벽 밖으로 밀어낸다. 하나라도 겹쳤으면 true.
        /// normal은 마지막으로 밀어낸 방향, box는 그 벽의 번호 (총알 반사·문 피해에 쓴다).
        /// </summary>
        public static bool PushOut(ref float2 position, float radius, NativeArray<WallBox> boxes, out float2 normal, out int box)
        {
            normal = float2.zero;
            box = -1;
            for (int i = 0; i < boxes.Length; i++)
            {
                WallBox wall = boxes[i];
                float2 relative = position - wall.Center;
                float2 nearest = math.clamp(relative, -wall.HalfSize, wall.HalfSize);
                float2 offset = relative - nearest;
                float distanceSq = math.lengthsq(offset);
                if (distanceSq >= radius * radius) continue;

                if (distanceSq > 1e-8f)
                {
                    float distance = math.sqrt(distanceSq);
                    normal = offset / distance;
                    position += normal * (radius - distance);
                }
                else
                {
                    // 중심이 벽 안까지 들어왔으면 덜 파고든 축 쪽 면으로 꺼낸다
                    float2 depth = wall.HalfSize - math.abs(relative);
                    if (depth.x < depth.y)
                    {
                        normal = new float2(relative.x >= 0f ? 1f : -1f, 0f);
                        position.x = wall.Center.x + normal.x * (wall.HalfSize.x + radius);
                    }
                    else
                    {
                        normal = new float2(0f, relative.y >= 0f ? 1f : -1f);
                        position.y = wall.Center.y + normal.y * (wall.HalfSize.y + radius);
                    }
                }
                box = i;
            }
            return box >= 0;
        }

        public static bool PushOut(ref float2 position, float radius, NativeArray<WallBox> boxes)
            => PushOut(ref position, radius, boxes, out _, out _);

        /// <summary>이 자리에 반지름 radius인 것이 놓일 수 없는가 (벽과 겹치거나 닫힌 방 안). 스폰 자리 고르기용.</summary>
        public static bool Blocked(float2 position, float radius, NativeArray<WallBox> boxes, NativeArray<RoomState> rooms)
        {
            for (int i = 0; i < boxes.Length; i++)
            {
                float2 relative = position - boxes[i].Center;
                float2 nearest = math.clamp(relative, -boxes[i].HalfSize, boxes[i].HalfSize);
                if (math.lengthsq(relative - nearest) < radius * radius) return true;
            }
            for (int i = 0; i < rooms.Length; i++)
            {
                if (rooms[i].Open) continue;
                if (math.all(math.abs(position - rooms[i].Center) <= rooms[i].HalfSize)) return true;
            }
            return false;
        }
    }
}
