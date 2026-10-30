using MergeBullet.Components;
using UnityEngine;

namespace MergeBullet.Presentation
{
    /// <summary>
    /// 효과음 에셋이 없을 때 쓰는 임시 소리. 사인파·잡음으로 짧은 소리를 코드로 만든다.
    /// EffectPresenter에 실제 AudioClip을 넣으면 그걸 우선 쓴다.
    /// </summary>
    public static class ProceduralSounds
    {
        const int SampleRate = 44100;

        public static AudioClip Create(EffectType type)
        {
            switch (type)
            {
                case EffectType.Merge: return Sweep("Merge", 0.08f, 880f, 1320f, 0f);       // 짧게 올라가는 '띵'
                case EffectType.EnemyKilled: return Sweep("Kill", 0.10f, 320f, 120f, 0.3f); // 내려가는 '퍽'
                case EffectType.Explosion: return Sweep("Boom", 0.40f, 90f, 40f, 0.9f);     // 낮고 거친 '쾅'
                default: return Sweep("Hit", 0.15f, 180f, 90f, 0.5f);                       // 둔한 피격음
            }
        }

        // startHz → endHz로 음높이가 바뀌는 사인파에 잡음을 noise 비율만큼 섞고, 끝으로 갈수록 작아지게 한다
        static AudioClip Sweep(string name, float seconds, float startHz, float endHz, float noise)
        {
            int count = Mathf.CeilToInt(seconds * SampleRate);
            var samples = new float[count];
            var random = new System.Random(name.GetHashCode());
            double phase = 0.0;

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / count;
                phase += 2.0 * Mathf.PI * Mathf.Lerp(startHz, endHz, t) / SampleRate;
                float tone = Mathf.Sin((float)phase);
                float hiss = (float)(random.NextDouble() * 2.0 - 1.0);
                float envelope = (1f - t) * (1f - t);
                samples[i] = Mathf.Lerp(tone, hiss, noise) * envelope * 0.8f;
            }

            AudioClip clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
