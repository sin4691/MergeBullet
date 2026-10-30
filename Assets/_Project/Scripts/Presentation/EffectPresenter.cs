using System;
using System.Collections.Generic;
using MergeBullet.Components;
using Unity.Entities;
using UnityEngine;

namespace MergeBullet.Presentation
{
    /// <summary>
    /// ECS가 남긴 연출 이벤트(EffectEvent)를 읽어 파티클과 효과음을 재생한다. 메인 씬(SubScene 밖)에 둔다.
    /// 게임 로직은 이 클래스를 모르고 이벤트만 남기므로, 이펙트·소리 에셋을 바꿔도 로직은 그대로다.
    /// - 파티클: 종류별 풀에서 꺼내 재사용 (매번 생성·파괴하지 않음)
    /// - 한 프레임에 같은 종류가 몰려도(합성 수십 건 등) MaxPerFrame개만 재생하고, 소리는 종류당 한 번만 낸다
    /// </summary>
    public class EffectPresenter : MonoBehaviour
    {
        [Serializable]
        public class EffectEntry
        {
            public EffectType Type;
            public ParticleSystem Prefab;
            [Tooltip("비워 두면 코드로 만든 임시 소리를 쓴다")]
            public AudioClip Clip;
            [Range(0f, 1f)] public float Volume = 0.4f;
            [Min(1)] public int MaxPerFrame = 6;
            [Min(1)] public int PoolSize = 24;
        }

        // 엔티티는 z = 0 평면에 있고 카메라는 -z에서 본다 → 파티클을 살짝 앞에 둬서 가려지지 않게
        const float EffectZ = -1f;

        public EffectEntry[] Effects = Array.Empty<EffectEntry>();

        readonly Dictionary<EffectType, EffectEntry> _entries = new Dictionary<EffectType, EffectEntry>();
        readonly Dictionary<EffectType, List<ParticleSystem>> _pools = new Dictionary<EffectType, List<ParticleSystem>>();
        readonly Dictionary<EffectType, int> _playedThisFrame = new Dictionary<EffectType, int>();
        AudioSource _audio;
        World _world;
        EntityQuery _queueQuery;

        void Awake()
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;

            foreach (EffectEntry entry in Effects)
            {
                if (entry.Clip == null) entry.Clip = ProceduralSounds.Create(entry.Type);
                _entries[entry.Type] = entry;
                _pools[entry.Type] = new List<ParticleSystem>();
            }
        }

        // 이벤트는 시뮬레이션이 끝난 뒤 쌓여 있으므로 LateUpdate에서 읽는다. 비우는 건 ECS(EffectEventCleanupSystem)가 한다
        void LateUpdate()
        {
            if (!TryBindWorld() || !_queueQuery.TryGetSingletonEntity<EffectQueue>(out Entity queue)) return;

            // 이벤트를 쓰는 ECB 재생은 이미 끝났지만, 안전 검사를 위해 관련 잡을 끝낸 뒤 읽는다
            _queueQuery.CompleteDependency();
            DynamicBuffer<EffectEvent> events = _world.EntityManager.GetBuffer<EffectEvent>(queue, true);
            if (events.Length == 0) return;

            _playedThisFrame.Clear();
            foreach (EffectEvent effect in events)
                Play(effect);
        }

        void Play(EffectEvent effect)
        {
            if (!_entries.TryGetValue(effect.Type, out EffectEntry entry)) return;

            _playedThisFrame.TryGetValue(effect.Type, out int played);
            if (played >= entry.MaxPerFrame) return;
            _playedThisFrame[effect.Type] = played + 1;

            // 같은 소리를 한 프레임에 여러 번 겹치면 시끄럽기만 해서 종류당 한 번만 낸다
            if (played == 0 && entry.Clip != null) _audio.PlayOneShot(entry.Clip, entry.Volume);

            if (entry.Prefab == null) return;
            ParticleSystem particles = Rent(entry);
            particles.transform.position = new Vector3(effect.Position.x, effect.Position.y, EffectZ);
            // 프리팹은 반지름 1 기준으로 만들어져 있고, 대상 크기에 맞춰 늘린다 (Scaling Mode: Hierarchy)
            particles.transform.localScale = Vector3.one * Mathf.Max(effect.Size, 0.05f);

            ParticleSystem.MainModule main = particles.main;
            // 이벤트 색은 셰이더용 선형 값이라, 파티클 색(감마)으로 되돌린다
            main.startColor = new Color(effect.Color.x, effect.Color.y, effect.Color.z, effect.Color.w).gamma;
            particles.Play();
        }

        // 재생이 끝난 것을 재사용하고, 다 쓰고 있으면 풀 크기까지만 새로 만든다 (그 이상이면 가장 오래된 것을 다시 쓴다)
        ParticleSystem Rent(EffectEntry entry)
        {
            List<ParticleSystem> pool = _pools[entry.Type];
            foreach (ParticleSystem particles in pool)
                if (!particles.IsAlive(true)) return particles;

            if (pool.Count < entry.PoolSize)
            {
                ParticleSystem created = Instantiate(entry.Prefab, transform);
                pool.Add(created);
                return created;
            }

            ParticleSystem oldest = pool[0];
            pool.RemoveAt(0);
            pool.Add(oldest);
            oldest.Clear(true);
            return oldest;
        }

        // 기본 월드는 Play마다 새로 만들어지므로, 바뀌었으면 쿼리를 다시 만든다
        bool TryBindWorld()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return false;
            if (world == _world) return true;

            _world = world;
            _queueQuery = world.EntityManager.CreateEntityQuery(typeof(EffectQueue));
            return true;
        }
    }
}
