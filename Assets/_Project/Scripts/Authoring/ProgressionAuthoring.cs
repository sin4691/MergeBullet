using MergeBullet.Components;
using Unity.Entities;
using UnityEngine;

namespace MergeBullet.Authoring
{
    /// <summary>
    /// 판 진행 규칙: 레벨업 경험치 곡선(PlayerProgress, LevelUpOffer, BaseStats)과 판 길이(RunRules).
    /// </summary>
    [DisallowMultipleComponent]
    public class ProgressionAuthoring : MonoBehaviour
    {
        [Min(1)] public int FirstLevelUpXp = 5;
        [Min(0)] public int LevelUpXpGrowth = 3;
        // 레벨업 선택지를 뽑는 난수 시드
        public uint Seed = 12347;

        [Header("Run")]
        // 30분 버티면 클리어. 끝이 있어야 판마다 보상을 받고 다음 판으로 넘어간다
        [Min(0f)] public float RunDurationSeconds = 1800f;

        class Baker : Baker<ProgressionAuthoring>
        {
            public override void Bake(ProgressionAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new PlayerProgress
                {
                    Level = 1,
                    NextLevelXp = authoring.FirstLevelUpXp,
                    FirstStep = authoring.FirstLevelUpXp,
                    StepGrowth = authoring.LevelUpXpGrowth,
                    Random = Unity.Mathematics.Random.CreateFromIndex(authoring.Seed),
                });
                AddComponent(entity, new LevelUpOffer { Selected = -1 });
                AddComponent(entity, new BaseStats());
                AddComponent(entity, new RunRules { DurationSeconds = authoring.RunDurationSeconds });
            }
        }
    }
}
