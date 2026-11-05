using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace MergeBullet.UI
{
    /// <summary>
    /// 로비 배경 연출: 공(총알)이 화면 가장자리에 튕기고, 같은 레벨끼리 닿으면 합쳐져 다음 레벨이 되며,
    /// 레벨 4는 잠시 뒤 터진다. 게임의 핵심 규칙을 첫 화면에서 보여 주는 장식이라 판 안 로직(ECS)과 무관하고
    /// UI Toolkit의 벡터 그리기(Painter2D)로 그린다. 공이 수십 개뿐이라 충돌은 단순한 이중 루프로 충분하다.
    /// LobbyScreen이 매 프레임 Tick을 불러 움직인다.
    /// </summary>
    public class TitleBackground : VisualElement
    {
        const int TargetCount = 18;
        const int MaxLevel = 4;
        const float BaseRadius = 16f;
        const float RadiusGrowth = 0.45f;   // 레벨 하나당 반지름 +45% (게임과 비슷한 느낌으로)
        const float MinSpeed = 100f;        // 패널 픽셀/초 (기준 해상도 1920x1080)
        const float MaxSpeed = 180f;
        const float FadeInTime = 0.6f;
        const float MaxLevelLife = 1.5f;
        const float PopTime = 0.5f;
        const float Alpha = 0.55f;          // 배경이라 글자보다 흐리게

        // 게임 총알 색과 같게 (BulletRulesAuthoring.LevelColors 기본값: 노랑 → 주황 → 보라 → 흰색)
        static readonly Color[] LevelColors =
        {
            new Color(1f, 0.85f, 0.2f),
            new Color(1f, 0.5f, 0.1f),
            new Color(0.75f, 0.3f, 1f),
            new Color(1f, 1f, 1f),
        };

        struct Ball
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public int Level;
            public float Age;   // 등장 후 지난 시간 (나타날 때 서서히 보이게)
            public float Life;  // 레벨 4가 된 뒤 지난 시간
        }

        struct Pop
        {
            public Vector2 Position;
            public float Radius;
            public float Age;
        }

        readonly List<Ball> _balls = new List<Ball>();
        readonly List<Pop> _pops = new List<Pop>();

        public TitleBackground()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        static float RadiusOf(int level) => BaseRadius * (1f + RadiusGrowth * (level - 1));

        public void Tick(float deltaTime)
        {
            Rect area = contentRect;
            if (area.width < 1f || area.height < 1f) return; // 아직 레이아웃 전

            while (_balls.Count < TargetCount) _balls.Add(Spawn(area));
            Move(area, deltaTime);
            Merge();
            Expire(deltaTime);
            MarkDirtyRepaint();
        }

        static Ball Spawn(Rect area)
        {
            float radius = RadiusOf(1);
            Vector2 direction = Random.insideUnitCircle.normalized;
            if (direction == Vector2.zero) direction = Vector2.right;
            return new Ball
            {
                Position = new Vector2(Random.Range(radius, area.width - radius), Random.Range(radius, area.height - radius)),
                Velocity = direction * Random.Range(MinSpeed, MaxSpeed),
                Level = 1,
            };
        }

        // 가장자리에 닿으면 그 축의 속도를 뒤집는다 (게임의 벽 튕김과 같은 방식)
        void Move(Rect area, float deltaTime)
        {
            for (int i = 0; i < _balls.Count; i++)
            {
                Ball ball = _balls[i];
                float radius = RadiusOf(ball.Level);
                ball.Position += ball.Velocity * deltaTime;
                ball.Age += deltaTime;

                if (ball.Position.x < radius) { ball.Position.x = radius; ball.Velocity.x = Mathf.Abs(ball.Velocity.x); }
                else if (ball.Position.x > area.width - radius) { ball.Position.x = area.width - radius; ball.Velocity.x = -Mathf.Abs(ball.Velocity.x); }
                if (ball.Position.y < radius) { ball.Position.y = radius; ball.Velocity.y = Mathf.Abs(ball.Velocity.y); }
                else if (ball.Position.y > area.height - radius) { ball.Position.y = area.height - radius; ball.Velocity.y = -Mathf.Abs(ball.Velocity.y); }

                _balls[i] = ball;
            }
        }

        // 같은 레벨 두 공이 겹치면 한 공으로 합친다 (가운데 위치, 평균 방향, 다음 레벨)
        void Merge()
        {
            for (int i = 0; i < _balls.Count; i++)
            {
                for (int j = i + 1; j < _balls.Count; j++)
                {
                    Ball a = _balls[i];
                    Ball b = _balls[j];
                    if (a.Level != b.Level || a.Level >= MaxLevel) continue;
                    float reach = RadiusOf(a.Level) + RadiusOf(b.Level);
                    if ((a.Position - b.Position).sqrMagnitude > reach * reach) continue;

                    Vector2 direction = (a.Velocity + b.Velocity).normalized;
                    if (direction == Vector2.zero) direction = a.Velocity.normalized;
                    a.Position = (a.Position + b.Position) * 0.5f;
                    a.Velocity = direction * Mathf.Max(a.Velocity.magnitude, b.Velocity.magnitude);
                    a.Level++;
                    a.Age = FadeInTime;
                    _balls[i] = a;
                    _balls.RemoveAt(j);
                    j--;
                }
            }
        }

        // 레벨 4는 잠시 빛나다 터진다 (빈자리는 다음 Tick에 레벨 1이 새로 채운다)
        void Expire(float deltaTime)
        {
            for (int i = _balls.Count - 1; i >= 0; i--)
            {
                Ball ball = _balls[i];
                if (ball.Level < MaxLevel) continue;
                ball.Life += deltaTime;
                if (ball.Life < MaxLevelLife) { _balls[i] = ball; continue; }

                _pops.Add(new Pop { Position = ball.Position, Radius = RadiusOf(ball.Level) });
                _balls.RemoveAt(i);
            }

            for (int i = _pops.Count - 1; i >= 0; i--)
            {
                Pop pop = _pops[i];
                pop.Age += deltaTime;
                if (pop.Age >= PopTime) _pops.RemoveAt(i);
                else _pops[i] = pop;
            }
        }

        void Draw(MeshGenerationContext context)
        {
            Painter2D painter = context.painter2D;

            foreach (Ball ball in _balls)
            {
                float radius = RadiusOf(ball.Level);
                Color color = LevelColors[ball.Level - 1];
                float fade = Mathf.Clamp01(ball.Age / FadeInTime);

                // 레벨 4는 바깥에 은은한 빛
                if (ball.Level >= MaxLevel)
                    FillCircle(painter, ball.Position, radius * 1.8f, WithAlpha(color, Alpha * 0.25f * fade));
                FillCircle(painter, ball.Position, radius, WithAlpha(color, Alpha * fade));
            }

            // 터질 때: 커지면서 사라지는 고리
            foreach (Pop pop in _pops)
            {
                float t = pop.Age / PopTime;
                painter.strokeColor = WithAlpha(LevelColors[MaxLevel - 1], Alpha * (1f - t));
                painter.lineWidth = 4f;
                painter.BeginPath();
                painter.Arc(pop.Position, pop.Radius * (1f + 2.5f * t), Angle.Degrees(0f), Angle.Degrees(360f));
                painter.Stroke();
            }
        }

        static void FillCircle(Painter2D painter, Vector2 center, float radius, Color color)
        {
            painter.fillColor = color;
            painter.BeginPath();
            painter.Arc(center, radius, Angle.Degrees(0f), Angle.Degrees(360f));
            painter.Fill();
        }

        static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);
    }
}
