using Unity.Entities;
using Unity.Mathematics;

namespace MergeBullet.Components
{
    /// <summary>화면 연출(파티클·효과음) 종류. 실제 이펙트·소리는 EffectPresenter가 종류별로 정한다.</summary>
    public enum EffectType : byte
    {
        Merge,
        EnemyKilled,
        Explosion,
        PlayerHit,
    }

    /// <summary>
    /// 연출 이벤트 큐가 붙은 엔티티 표시. 게임 로직 시스템은 ECB.AppendToBuffer로 이벤트를 남기기만 하고,
    /// 화면 연출은 EffectPresenter(MonoBehaviour)가 읽어서 재생한다 → 로직과 연출이 서로를 모른다.
    /// </summary>
    public struct EffectQueue : IComponentData
    {
    }

    /// <summary>
    /// 연출 이벤트 한 건. 한 프레임 동안 쌓였다가 EffectPresenter가 읽고, 다음 프레임 시작에 비워진다.
    /// </summary>
    public struct EffectEvent : IBufferElementData
    {
        public EffectType Type;
        public float2 Position;
        /// <summary>연출 크기 기준 (보통 대상의 반지름).</summary>
        public float Size;
        /// <summary>선형 색공간 색. 파티클 색에 쓴다.</summary>
        public float4 Color;
    }
}
