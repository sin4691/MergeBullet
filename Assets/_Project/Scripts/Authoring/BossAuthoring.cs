using MergeBullet.Components;
using Unity.Entities;
using UnityEngine;

namespace MergeBullet.Authoring
{
    /// <summary>
    /// 보스 프리팹에 EnemyAuthoring과 함께 붙인다. 적 공통 데이터는 EnemyAuthoring이, 패턴 값은 이쪽이 베이킹한다.
    /// </summary>
    public class BossAuthoring : MonoBehaviour
    {
        [Min(0.1f)] public float ChaseSeconds = 4f;
        [Min(0.1f)] public float WindupSeconds = 0.8f;
        [Min(0.1f)] public float DashSeconds = 0.6f;
        [Min(0f)] public float DashSpeed = 10f;

        [Header("Summon")]
        // 부하로 소환할 적 (Update/Enemy Tiers가 삼각형으로 연결)
        public GameObject SummonPrefab;
        [Min(1)] public int SummonCount = 6;
        [Min(0f)] public float SummonDistance = 1.2f;

        [Header("Shockwave")]
        [Min(0.5f)] public float ShockwaveMaxRadius = 7f;
        [Min(0.1f)] public float ShockwaveSpeed = 5f;
        [Min(0.05f)] public float ShockwaveWidth = 0.6f;
        [Min(0f)] public float ShockwaveDamage = 25f;

        class Baker : Baker<BossAuthoring>
        {
            public override void Bake(BossAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Boss
                {
                    Phase = BossPhase.Chase,
                    ChaseSeconds = authoring.ChaseSeconds,
                    WindupSeconds = authoring.WindupSeconds,
                    DashSeconds = authoring.DashSeconds,
                    DashSpeed = authoring.DashSpeed,
                    SummonPrefab = authoring.SummonPrefab != null
                        ? GetEntity(authoring.SummonPrefab, TransformUsageFlags.Dynamic)
                        : Entity.Null,
                    SummonCount = authoring.SummonCount,
                    SummonDistance = authoring.SummonDistance,
                    ShockwaveMaxRadius = authoring.ShockwaveMaxRadius,
                    ShockwaveSpeed = authoring.ShockwaveSpeed,
                    ShockwaveWidth = authoring.ShockwaveWidth,
                    ShockwaveDamage = authoring.ShockwaveDamage,
                });
                AddComponent(entity, new BossOverride());
                SetComponentEnabled<BossOverride>(entity, false);
            }
        }
    }
}
