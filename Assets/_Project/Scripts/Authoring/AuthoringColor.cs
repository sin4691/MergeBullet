using Unity.Mathematics;
using UnityEngine;

namespace MergeBullet.Authoring
{
    /// <summary>베이킹할 때 머티리얼 색을 머티리얼 프로퍼티 오버라이드용 값(선형 색공간)으로 바꾼다.</summary>
    static class AuthoringColor
    {
        public static float4 LinearBaseColor(Material material)
        {
            if (material == null) return new float4(1f);

            Color color = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.color;
            Color linear = color.linear;
            return new float4(linear.r, linear.g, linear.b, linear.a);
        }
    }
}
