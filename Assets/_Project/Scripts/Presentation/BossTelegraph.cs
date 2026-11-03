using System.Collections.Generic;
using MergeBullet.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace MergeBullet.Presentation
{
    /// <summary>
    /// 보스 돌진 예고. 보스가 예고(Windup) 중이면 지금 조준 방향(Boss.DashDirection)으로 돌진할 때 맞는 범위를 띠로 그리고,
    /// 예고 시간만큼 진한 띠가 차올라 "곧 온다"를 알려 준다. 게임 로직은 바꾸지 않고 ECS 데이터를 읽어 그리기만 한다.
    /// 게임 씬의 Effects 오브젝트에 붙인다 (MergeBullet > Create Boss Telegraph).
    /// </summary>
    public class BossTelegraph : MonoBehaviour
    {
        public Material LineMaterial;
        public Color Color = new Color(1f, 0.25f, 0.35f);

        World _world;
        EntityQuery _bossQuery;
        EntityQuery _arenaQuery;
        readonly List<LineRenderer> _lines = new List<LineRenderer>();

        void LateUpdate()
        {
            int used = 0;
            if (TryBindWorld())
            {
                // Boss·LocalTransform은 잡(BossBehavior)이 쓰므로 읽기 전에 끝낸다 (보스 몇 마리뿐이라 짧다)
                _bossQuery.CompleteDependency();
                _arenaQuery.TryGetSingleton(out ArenaBounds arena);

                using NativeArray<Boss> bosses = _bossQuery.ToComponentDataArray<Boss>(Allocator.Temp);
                using NativeArray<LocalTransform> transforms = _bossQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
                using NativeArray<Enemy> enemies = _bossQuery.ToComponentDataArray<Enemy>(Allocator.Temp);
                for (int i = 0; i < bosses.Length; i++)
                {
                    Boss boss = bosses[i];
                    if (boss.Phase != BossPhase.Windup) continue;
                    float radius = enemies[i].Radius;
                    Draw(Line(used++), Line(used++), boss, transforms[i].Position.xy, radius, arena.HalfSize - radius);
                }
            }

            // 이번 프레임에 안 쓴 띠는 숨긴다
            for (int i = used; i < _lines.Count; i++) _lines[i].enabled = false;
        }

        // 맞으면 아픈 범위 = 보스 몸통(한 변 2 × 반지름)이 돌진하며 쓸고 가는 띠. 옅은 띠 위로 예고 시간만큼 진한 띠가 차오르고,
        // 끝까지 차면 돌진한다 → 남은 시간과 위험 범위가 한눈에 보인다
        void Draw(LineRenderer area, LineRenderer fill, Boss boss, float2 start, float radius, float2 limit)
        {
            // 돌진 거리 = 속도 × 시간. 돌진은 아레나 끝에서 멈추므로 도착 위치도 아레나 안으로 자르고,
            // 도착한 몸통의 앞쪽 반(반지름)까지 띠를 늘린다
            float2 stop = math.clamp(start + boss.DashDirection * boss.DashSpeed * boss.DashSeconds, -limit, limit);
            float2 end = stop + boss.DashDirection * radius;
            float progress = boss.WindupSeconds > 0f ? math.saturate(boss.Timer / boss.WindupSeconds) : 1f;

            SetBand(area, start, end, radius, 0.18f);
            SetBand(fill, start, math.lerp(start, end, progress), radius, 0.45f);
        }

        void SetBand(LineRenderer line, float2 from, float2 to, float radius, float alpha)
        {
            line.enabled = true;
            // 바닥(z 0.5)보다 앞, 적·총알(z 0)보다 뒤. 차오르는 띠가 옅은 띠보다 살짝 앞에 오게 alpha로 순서를 나눈다
            float z = 0.2f - alpha * 0.1f;
            line.SetPosition(0, new Vector3(from.x, from.y, z));
            line.SetPosition(1, new Vector3(to.x, to.y, z));
            line.widthMultiplier = radius * 2f;
            Color color = Color;
            color.a = alpha;
            line.startColor = color;
            line.endColor = color;
        }

        LineRenderer Line(int index)
        {
            while (_lines.Count <= index)
            {
                var go = new GameObject("BossTelegraphLine");
                go.transform.SetParent(transform, false);
                var line = go.AddComponent<LineRenderer>();
                line.sharedMaterial = LineMaterial;
                line.positionCount = 2;
                line.useWorldSpace = true;
                // 보스가 네모라 띠 끝도 각지게 (둥근 끝을 쓰면 실제 범위보다 넓어 보임)
                line.numCapVertices = 0;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                _lines.Add(line);
            }
            return _lines[index];
        }

        // 기본 월드는 Play마다 새로 만들어지므로, 바뀌었으면 쿼리를 다시 만든다
        bool TryBindWorld()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return false;
            if (world == _world) return true;

            _world = world;
            _bossQuery = world.EntityManager.CreateEntityQuery(typeof(Boss), typeof(LocalTransform), typeof(Enemy));
            _arenaQuery = world.EntityManager.CreateEntityQuery(typeof(ArenaBounds));
            return true;
        }
    }
}
