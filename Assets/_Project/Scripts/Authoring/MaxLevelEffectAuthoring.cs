using MergeBullet.Components;
using Unity.Entities;
using UnityEngine;

namespace MergeBullet.Authoring
{
    /// <summary>
    /// 최대 레벨 총알이 터질 때의 효과(MaxLevelEffect). 폭발과 Storm 캐릭터의 연쇄 번개 값을 함께 두고,
    /// 어느 쪽을 쓸지는 게임 시작 시 캐릭터로 정해진다 (CharacterSetupSystem).
    /// </summary>
    [DisallowMultipleComponent]
    public class MaxLevelEffectAuthoring : MonoBehaviour
    {
        public GameObject MaxLevelEffectPrefab;
        [Min(0.01f)] public float EffectRadius = 1.2f;
        [Min(0.01f)] public float EffectDuration = 0.4f;
        [Min(0f)] public float EffectDamage = 5f;

        [Header("Lightning (Storm)")]
        [Min(1)] public int LightningChainCount = 5;
        [Min(0.1f)] public float LightningChainRange = 4f;
        [Min(0f)] public float LightningDamage = 4f;

        class Baker : Baker<MaxLevelEffectAuthoring>
        {
            public override void Bake(MaxLevelEffectAuthoring authoring)
            {
                if (authoring.MaxLevelEffectPrefab == null) return;

                Entity entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new MaxLevelEffect
                {
                    Prefab = GetEntity(authoring.MaxLevelEffectPrefab, TransformUsageFlags.Dynamic),
                    Radius = authoring.EffectRadius,
                    Duration = authoring.EffectDuration,
                    Damage = authoring.EffectDamage,
                    Kind = MaxLevelEffectKind.Explosion,
                    ChainCount = authoring.LightningChainCount,
                    ChainRange = authoring.LightningChainRange,
                    ChainDamage = authoring.LightningDamage,
                });
            }
        }
    }
}
