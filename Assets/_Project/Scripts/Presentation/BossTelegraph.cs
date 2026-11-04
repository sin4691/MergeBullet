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
    /// 보스 공격 예고와 충격파 그림. 예고(Windup) 중에는 공격마다 다르게 그린다:
    /// 돌진은 맞는 범위 띠(진한 띠가 예고 시간만큼 차오름), 소환은 부하가 나타날 원, 충격파는 닿는 최대 범위.
    /// 퍼지고 있는 충격파 고리도 여기서 그린다. 게임 로직은 바꾸지 않고 ECS 데이터를 읽어 그리기만 한다.
    /// 게임 씬의 Effects 오브젝트에 붙인다 (MergeBullet > Setup > Boss Telegraph).
    /// </summary>
    public class BossTelegraph : MonoBehaviour
    {
        public Material LineMaterial;
        public Color Color = new Color(1f, 0.25f, 0.35f);

        // 원을 그릴 때 둘레를 나누는 점 수
        const int CircleSegments = 48;

        World _world;
        EntityQuery _bossQuery;
        EntityQuery _arenaQuery;
        EntityQuery _shockwaveQuery;
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
                    float2 center = transforms[i].Position.xy;
                    float progress = boss.WindupDuration > 0f ? math.saturate(boss.Timer / boss.WindupDuration) : 1f;
                    switch (boss.Attack)
                    {
                        case BossAttack.Dash:
                            Draw(Line(used++), Line(used++), boss, center, radius, arena.HalfSize - radius, progress);
                            break;
                        case BossAttack.Summon:
                            // 부하가 나타날 원을 점점 진하게
                            SetCircle(Line(used++), center, radius + boss.SummonDistance + 0.2f, 0.12f, math.lerp(0.15f, 0.6f, progress));
                            break;
                        case BossAttack.Shockwave:
                            // 충격파가 닿는 최대 범위(바깥 원) + 예고 시간만큼 그 원까지 커지는 안쪽 원 → 이 안이 위험하다는 걸 미리 보여 준다
                            SetCircle(Line(used++), center, boss.ShockwaveMaxRadius, boss.ShockwaveWidth, 0.18f);
                            SetCircle(Line(used++), center, math.lerp(radius, boss.ShockwaveMaxRadius, progress), 0.08f, 0.5f);
                            break;
                    }
                }

                // 퍼지고 있는 충격파 고리 (실제로 맞는 두께 그대로)
                _shockwaveQuery.CompleteDependency();
                using NativeArray<Shockwave> waves = _shockwaveQuery.ToComponentDataArray<Shockwave>(Allocator.Temp);
                foreach (Shockwave wave in waves)
                    SetCircle(Line(used++), wave.Center, wave.Radius, wave.Width, 0.75f);
            }

            // 이번 프레임에 안 쓴 띠는 숨긴다
            for (int i = used; i < _lines.Count; i++) _lines[i].enabled = false;
        }

        // 맞으면 아픈 범위 = 보스 몸통(한 변 2 × 반지름)이 돌진하며 쓸고 가는 띠. 옅은 띠 위로 예고 시간만큼 진한 띠가 차오르고,
        // 끝까지 차면 돌진한다 → 남은 시간과 위험 범위가 한눈에 보인다
        void Draw(LineRenderer area, LineRenderer fill, Boss boss, float2 start, float radius, float2 limit, float progress)
        {
            // 돌진 거리 = 속도 × 시간. 돌진은 아레나 끝에서 멈추므로 도착 위치도 아레나 안으로 자르고,
            // 도착한 몸통의 앞쪽 반(반지름)까지 띠를 늘린다
            float2 stop = math.clamp(start + boss.DashDirection * boss.DashSpeed * boss.DashSeconds, -limit, limit);
            float2 end = stop + boss.DashDirection * radius;

            SetBand(area, start, end, radius, 0.18f);
            SetBand(fill, start, math.lerp(start, end, progress), radius, 0.45f);
        }

        void SetBand(LineRenderer line, float2 from, float2 to, float radius, float alpha)
        {
            line.enabled = true;
            line.loop = false;
            line.positionCount = 2;
            // 보스 몸이 각져 있어서 띠 끝도 각지게 (둥근 끝을 쓰면 실제 범위보다 넓어 보임)
            line.numCapVertices = 0;
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

        // 중심 center, 반지름 radius인 원을 두께 width로 (고리). 선 하나를 이어 붙인 닫힌 선이라 loop를 켠다
        void SetCircle(LineRenderer line, float2 center, float radius, float width, float alpha)
        {
            line.enabled = true;
            line.loop = true;
            line.positionCount = CircleSegments;
            line.numCapVertices = 0;
            for (int i = 0; i < CircleSegments; i++)
            {
                math.sincos(2f * math.PI * i / CircleSegments, out float sin, out float cos);
                line.SetPosition(i, new Vector3(center.x + cos * radius, center.y + sin * radius, 0.15f));
            }
            line.widthMultiplier = width;
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
                line.useWorldSpace = true;
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
            _shockwaveQuery = world.EntityManager.CreateEntityQuery(typeof(Shockwave));
            return true;
        }
    }
}
