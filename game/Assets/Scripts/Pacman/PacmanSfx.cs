using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// Procedural sound effects. Clips are synthesised at startup so the game ships no audio files.
    /// Square-wave sweeps and arpeggios keep the arcade character.
    /// </summary>
    public class PacmanSfx
    {
        private const int SampleRate = 22050;

        public AudioClip WakaA { get; private set; }
        public AudioClip WakaB { get; private set; }
        public AudioClip Pellet { get; private set; }
        public AudioClip Power { get; private set; }
        public AudioClip GhostEaten { get; private set; }
        public AudioClip Death { get; private set; }
        public AudioClip LevelClear { get; private set; }
        public AudioClip Blip { get; private set; }

        public PacmanSfx()
        {
            WakaA = Sweep("waka_a", 260f, 520f, 0.09f, 0.22f);
            WakaB = Sweep("waka_b", 300f, 600f, 0.09f, 0.22f);
            Pellet = Sweep("pellet", 700f, 900f, 0.05f, 0.14f);
            Power = Sweep("power", 180f, 420f, 0.45f, 0.2f);
            GhostEaten = Sweep("ghost_eaten", 220f, 1300f, 0.3f, 0.22f);
            Death = Sweep("death", 900f, 120f, 1.2f, 0.25f);
            LevelClear = Sequence("level_clear", new[] { 523f, 659f, 784f, 1047f, 784f, 1047f }, 0.11f, 0.2f);
            Blip = Sweep("blip", 880f, 880f, 0.05f, 0.15f);
        }

        public static void Play(AudioSource source, AudioClip clip, bool enabled)
        {
            if (enabled && source != null && clip != null)
            {
                source.PlayOneShot(clip);
            }
        }

        private static AudioClip Sweep(string name, float fromHz, float toHz, float seconds, float volume)
        {
            return Sequence(name, new[] { fromHz, toHz }, seconds, volume, true);
        }

        /// <summary>Square-wave tone sequence. With glide=true the frequencies are a linear sweep.</summary>
        private static AudioClip Sequence(string name, float[] freqs, float noteSeconds, float volume, bool glide = false)
        {
            int total = Mathf.CeilToInt(SampleRate * noteSeconds * (glide ? 1 : freqs.Length));
            float[] data = new float[total];
            double phase = 0;
            int noteLength = Mathf.CeilToInt(SampleRate * noteSeconds);

            for (int i = 0; i < total; i++)
            {
                float t = (float)i / SampleRate;
                float freq;
                float local;
                if (glide)
                {
                    float p = Mathf.Clamp01((float)i / total);
                    freq = Mathf.Lerp(freqs[0], freqs[1], p);
                    local = (float)i / total;
                }
                else
                {
                    int note = Mathf.Min(i / noteLength, freqs.Length - 1);
                    freq = freqs[note];
                    local = (float)(i % noteLength) / noteLength;
                }

                phase += 2.0 * Mathf.PI * freq / SampleRate;
                float square = Mathf.Sin((float)phase) >= 0f ? 1f : -1f;
                float envelope = Mathf.Min(1f, t * 200f) * (1f - local * 0.6f);
                data[i] = square * envelope * volume;
            }

            AudioClip clip = AudioClip.Create(name, total, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
