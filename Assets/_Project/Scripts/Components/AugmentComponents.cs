using Unity.Entities;

namespace MergeBullet.Components
{
    /// <summary>
    /// 증강: 5레벨마다 일반 강화 대신 고르는 규칙 바꾸기. 큰 이득과 큰 대가를 함께 줘서 판마다 빌드 성격이 달라진다.
    /// 수치는 Systems/AugmentTuning, 적용은 ProgressionSystem. 저장 데이터의 비트 위치로도 쓰므로 순서를 바꾸지 않는다.
    /// </summary>
    public enum AugmentType : byte
    {
        // 연사 빌드
        TwinBarrel,
        Overheat,
        // 한방 빌드
        HeavyCaliber,
        Ricochet,
        // 범위 빌드 (발사를 줄이고 효과를 키움)
        Overload,
        ChainMerge,
        // 합성 규칙
        LevelFive,
        Endurance,
        Count,
    }

    /// <summary>이번 판에 고른 증강 (1 &lt;&lt; AugmentType 비트). 같은 증강은 한 번만 나온다. 재시작하면 0.</summary>
    public struct Augments : IComponentData
    {
        public uint Taken;
    }
}
