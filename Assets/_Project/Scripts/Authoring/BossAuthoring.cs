using MergeBullet.Components;
using Unity.Entities;
using UnityEngine;

namespace MergeBullet.Authoring
{
    /// <summary>
    /// 보스 프리팹에 EnemyAuthoring과 함께 붙인다. 적 공통 데이터는 EnemyAuthoring이, 돌진 패턴 값은 이쪽이 베이킹한다.
    /// </summary>
    public class BossAuthoring : MonoBehaviour
    {
        [Min(0.1f)] public float ChaseSeconds = 4f;
        [Min(0.1f)] public float WindupSeconds = 0.8f;
        [Min(0.1f)] public float DashSeconds = 0.6f;
        [Min(0f)] public float DashSpeed = 10f;

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
                });
                AddComponent(entity, new BossOverride());
                SetComponentEnabled<BossOverride>(entity, false);
            }
        }
    }
}
