using MergeBullet.Components;
using Unity.Entities;
using UnityEngine;

namespace MergeBullet.Authoring
{
    /// <summary>
    /// 성능 측정 조건(BenchmarkConfig). 조건은 항상 굽고, 켜는 건 실행 시점에 정한다 (체크박스 또는 실행 인자 -benchmark).
    /// 채우는 총알은 게임과 같은 총알이어야 하므로 BulletRulesAuthoring의 프리팹·속도를 읽는다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BulletRulesAuthoring))]
    public class BenchmarkAuthoring : MonoBehaviour
    {
        [Tooltip("에디터에서 측정할 때만 켠다. 빌드는 실행 인자 -benchmark")]
        public bool BenchmarkMode;
        // 레벨 1 총알 1000개 ≈ 아레나 면적의 31%. 5000개는 면적의 157%라 겹쳐서 바로 합성돼 유지가 안 된다
        [Min(0)] public int BenchmarkBullets = 1000;
        [Min(0)] public int BenchmarkEnemies = 300;
        [Min(0f)] public float BenchmarkWarmupSeconds = 8f;
        [Min(1f)] public float BenchmarkSampleSeconds = 10f;
        public uint Seed = 12348;

        class Baker : Baker<BenchmarkAuthoring>
        {
            public override void Bake(BenchmarkAuthoring authoring)
            {
                // 같은 오브젝트의 다른 Authoring을 읽는다. Baker의 GetComponent라 그 값이 바뀌어도 다시 베이킹된다
                BulletRulesAuthoring bullets = GetComponent<BulletRulesAuthoring>();
                if (bullets == null || bullets.BulletPrefab == null) return;

                Entity entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new BenchmarkConfig
                {
                    StartEnabled = authoring.BenchmarkMode,
                    MaxEnemies = authoring.BenchmarkEnemies,
                    Settings = new BenchmarkSettings
                    {
                        BulletPrefab = GetEntity(bullets.BulletPrefab, TransformUsageFlags.Dynamic),
                        TargetBullets = authoring.BenchmarkBullets,
                        BulletSpeed = bullets.BulletSpeed,
                        WarmupSeconds = authoring.BenchmarkWarmupSeconds,
                        SampleSeconds = authoring.BenchmarkSampleSeconds,
                        Random = Unity.Mathematics.Random.CreateFromIndex(authoring.Seed),
                    },
                });
            }
        }
    }
}
