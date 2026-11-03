using UnityEngine;

namespace MergeBullet.Presentation
{
    /// <summary>
    /// 음악 에셋이 없을 때 쓰는 임시 배경음악. 코드 진행(Am-F-C-G)에 맞춰 베이스·아르페지오·드럼을 합성해
    /// 끊김 없이 반복되는 AudioClip을 만든다 (ProceduralSounds의 음악판).
    /// 실제 음악이 생기면 MusicPlayer에서 이 대신 그 클립을 쓰면 된다.
    /// </summary>
    public static class ProceduralMusic
    {
        // 음악은 길어서 효과음(44.1kHz)의 절반으로 만든다. 메모리·생성 시간이 반으로 줄고, 이 음색엔 차이가 거의 없다
        const int SampleRate = 22050;

        // 4마디 코드 진행을 두 번 반복 (마디당 코드 하나). 근음 주파수와 화음 구성음(반음 단위)
        static readonly float[] ChordRoots = { 110f, 87.31f, 130.81f, 98f };         // A2, F2, C3, G2
        static readonly int[][] ChordTones = { new[] { 0, 3, 7 }, new[] { 0, 4, 7 }, new[] { 0, 4, 7 }, new[] { 0, 4, 7 } };
        const int Bars = 8;

        /// <summary>로비: 느리고 잔잔하게 (패드 화음 + 8분음표 아르페지오, 드럼 없음).</summary>
        public static AudioClip CreateLobby()
        {
            const float bpm = 88f;
            float beat = 60f / bpm;
            float[] samples = NewBuffer(beat);

            for (int bar = 0; bar < Bars; bar++)
            {
                int chord = bar % ChordRoots.Length;
                float barStart = bar * 4 * beat;

                // 패드: 화음 세 음을 한 마디 내내 부드럽게
                foreach (int tone in ChordTones[chord])
                    AddNote(samples, barStart, 4 * beat, Pitch(ChordRoots[chord] * 2f, tone), Wave.Triangle, 0.10f, 0.6f, 0.6f);

                // 아르페지오: 한 옥타브 위에서 구성음을 차례로
                for (int step = 0; step < 8; step++)
                {
                    int tone = ChordTones[chord][step % 3] + (step >= 4 ? 12 : 0);
                    AddNote(samples, barStart + step * beat * 0.5f, beat * 0.45f, Pitch(ChordRoots[chord] * 4f, tone),
                        Wave.Sine, 0.12f, 0.01f, 0.35f);
                }
            }

            return ToClip("Music_Lobby", samples);
        }

        /// <summary>게임: 빠르고 긴장감 있게 (8분음표 베이스 + 16분음표 아르페지오 + 킥·스네어·하이햇).</summary>
        public static AudioClip CreateGame()
        {
            const float bpm = 132f;
            float beat = 60f / bpm;
            float[] samples = NewBuffer(beat);

            for (int bar = 0; bar < Bars; bar++)
            {
                int chord = bar % ChordRoots.Length;
                float barStart = bar * 4 * beat;
                float root = ChordRoots[chord];

                // 베이스: 근음 8분음표, 넷째마다 옥타브 위로 튀어 리듬감
                for (int step = 0; step < 8; step++)
                    AddNote(samples, barStart + step * beat * 0.5f, beat * 0.4f, Pitch(root, step % 4 == 3 ? 12 : 0),
                        Wave.SoftSquare, 0.22f, 0.005f, 0.05f);

                // 아르페지오: 두 옥타브 위 16분음표, 짧게 끊어서
                for (int step = 0; step < 16; step++)
                {
                    int[] tones = ChordTones[chord];
                    int tone = tones[step % tones.Length] + (step % 8 >= 4 ? 12 : 0);
                    AddNote(samples, barStart + step * beat * 0.25f, beat * 0.2f, Pitch(root * 4f, tone),
                        Wave.Triangle, 0.10f, 0.003f, 0.12f);
                }

                // 드럼: 킥 1·3박, 스네어 2·4박, 하이햇 엇박
                for (int b = 0; b < 4; b++)
                {
                    float t = barStart + b * beat;
                    if (b % 2 == 0) AddKick(samples, t, 0.55f);
                    else AddNoise(samples, t, 0.12f, 0.25f, false);
                    AddNoise(samples, t + beat * 0.5f, 0.04f, 0.10f, true);
                }
            }

            return ToClip("Music_Game", samples);
        }

        enum Wave { Sine, Triangle, SoftSquare }

        static float[] NewBuffer(float beat) => new float[Mathf.RoundToInt(Bars * 4 * beat * SampleRate)];

        static float Pitch(float baseHz, int semitones) => baseHz * Mathf.Pow(2f, semitones / 12f);

        // 음 하나를 더한다. attack 동안 커지고 release 동안 작아진다 (클릭 소리 방지). 버퍼 끝을 넘으면 앞으로 감아서 반복 이음매를 매끄럽게
        static void AddNote(float[] buffer, float start, float duration, float hz, Wave wave, float amp, float attack, float release)
        {
            int first = Mathf.RoundToInt(start * SampleRate);
            int count = Mathf.RoundToInt(duration * SampleRate);
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                float envelope = Mathf.Min(1f, t / attack) * Mathf.Clamp01((duration - t) / release);
                float phase = hz * t;
                float value = wave switch
                {
                    Wave.Triangle => 4f * Mathf.Abs(phase - Mathf.Floor(phase + 0.5f)) - 1f,
                    // 사각파의 날카로운 배음을 tanh로 눌러 부드러운 베이스 음색으로
                    Wave.SoftSquare => (float)System.Math.Tanh(3.0 * Mathf.Sin(2f * Mathf.PI * phase)),
                    _ => Mathf.Sin(2f * Mathf.PI * phase),
                };
                buffer[(first + i) % buffer.Length] += value * envelope * amp;
            }
        }

        // 킥: 음높이가 빠르게 떨어지는 사인파
        static void AddKick(float[] buffer, float start, float amp)
        {
            const float duration = 0.18f;
            int first = Mathf.RoundToInt(start * SampleRate);
            int count = Mathf.RoundToInt(duration * SampleRate);
            double phase = 0.0;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / count;
                phase += 2.0 * Mathf.PI * Mathf.Lerp(130f, 40f, t) / SampleRate;
                buffer[(first + i) % buffer.Length] += Mathf.Sin((float)phase) * (1f - t) * (1f - t) * amp;
            }
        }

        // 스네어·하이햇: 잡음. bright면 앞 샘플과의 차이(고음만 남김)로 쇳소리 나는 하이햇
        static void AddNoise(float[] buffer, float start, float duration, float amp, bool bright)
        {
            int first = Mathf.RoundToInt(start * SampleRate);
            int count = Mathf.RoundToInt(duration * SampleRate);
            var random = new System.Random(first);
            float previous = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / count;
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                float value = bright ? noise - previous : noise;
                previous = noise;
                buffer[(first + i) % buffer.Length] += value * (1f - t) * (1f - t) * amp;
            }
        }

        // 가장 큰 소리가 0.9가 되게 맞춰 찢어지지 않게 한다
        static AudioClip ToClip(string name, float[] samples)
        {
            float peak = 0f;
            foreach (float s in samples) peak = Mathf.Max(peak, Mathf.Abs(s));
            if (peak > 0f)
                for (int i = 0; i < samples.Length; i++) samples[i] *= 0.9f / peak;

            AudioClip clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
