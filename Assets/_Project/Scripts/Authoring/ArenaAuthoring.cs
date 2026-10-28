using MergeBullet.Components;
using Unity.Entities;
using UnityEngine;

namespace MergeBullet.Authoring
{
    /// <summary>
    /// SubScene 안의 Arena 오브젝트에 붙인다. 아레나 크기와 초기 총알 스폰 설정을
    /// ArenaBounds / BulletSpawner 싱글톤으로 베이킹한다.
    /// </summary>
    public class ArenaAuthoring : MonoBehaviour
    {
        public Vector2 ArenaSize = new Vector2(16f, 9f);
        public GameObject BulletPrefab;
        [Min(1)] public int BulletCount = 100;
        [Min(0.01f)] public float BulletRadius = 0.12f;
        public float BulletSpeed = 5f;
        public uint Seed = 12345;

        class Baker : Baker<ArenaAuthoring>
        {
            public override void Bake(ArenaAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new ArenaBounds { HalfSize = authoring.ArenaSize * 0.5f });

                if (authoring.BulletPrefab == null) return;
                AddComponent(entity, new BulletSpawner
                {
                    Prefab = GetEntity(authoring.BulletPrefab, TransformUsageFlags.Dynamic),
                    Count = authoring.BulletCount,
                    Speed = authoring.BulletSpeed,
                    Radius = authoring.BulletRadius,
                    Seed = authoring.Seed,
                });
            }
        }
    }
}
