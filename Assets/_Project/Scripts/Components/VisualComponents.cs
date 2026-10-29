using Unity.Entities;
using Unity.Mathematics;

namespace MergeBullet.Components
{
    /// <summary>
    /// 원래 색(선형 색공간). 피격·깜빡임 연출이 URPMaterialPropertyBaseColor를 잠깐 바꾼 뒤 이 색으로 되돌린다.
    /// </summary>
    public struct BaseColor : IComponentData
    {
        public float4 Value;
    }

    /// <summary>맞았을 때 잠깐 하얗게 번쩍이는 연출. Timer가 Duration에서 0까지 줄며 원래 색으로 돌아온다.</summary>
    public struct HitFlash : IComponentData
    {
        public float Duration;
        public float Timer;
    }
}
