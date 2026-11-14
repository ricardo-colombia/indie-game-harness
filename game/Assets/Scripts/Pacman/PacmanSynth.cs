using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>Oscillator shapes for the procedural synth.</summary>
    public enum Wave { Square, Pulse25, Pulse125, Triangle, Sine, Saw }

    /// <summary>
    /// A tiny offline chip-synth. It renders notes, drums and echo into a float buffer, so the game
    /// ships no audio files. Writes wrap around the buffer end, which keeps music loops seamless.
    /// </summary>
    public static class PacmanSynth
    {
        public const int SampleRate = 44100;

        /// <summary>Equal-tempered frequency of a MIDI note number (69 = A4 = 440 Hz).</summary>
        public static float Hz(float midi)
        {
            return 440f * Mathf.Pow(2f, (midi - 69f) / 12f);
        }

        public static float[] Buffer(float seconds)
        {
            return new float[Mathf.CeilToInt(seconds * SampleRate)];
        }

        private static float Osc(Wave wave, double cycle)
        {
            float p = (float)(cycle - System.Math.Floor(cycle));
            switch (wave)
            {
                case Wave.Square: return p < 0.5f ? 1f : -1f;
                case Wave.Pulse25: return p < 0.25f ? 1f : -1f;
                case Wave.Pulse125: return p < 0.125f ? 1f : -1f;
                case Wave.Triangle: return p < 0.5f ? 4f * p - 1f : 3f - 4f * p;
                case Wave.Saw: return 2f * p - 1f;
                default: return Mathf.Sin(p * 2f * Mathf.PI);
            }
        }

        /// <summary>
        /// Adds a note. The pitch glides linearly from hzFrom to hzTo. decay is an exponential fall in 1/s,
        /// vibratoHz and vibratoSemis add a wobble that fades in after 0.1 s.
        /// </summary>
        public static void Tone(float[] buf, float startSec, float lenSec, float hzFrom, float hzTo, Wave wave, float vol,
            float decay = 0f, float attack = 0.004f, float release = 0.03f, float vibratoHz = 0f, float vibratoSemis = 0f)
        {
            int start = Mathf.RoundToInt(startSec * SampleRate);
            int count = Mathf.Max(1, Mathf.RoundToInt(lenSec * SampleRate));
            double cycle = 0.0;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                float p = (float)i / count;
                float hz = Mathf.Lerp(hzFrom, hzTo, p);
                if (vibratoSemis > 0f)
                {
                    float depth = Mathf.Clamp01((t - 0.1f) * 8f) * vibratoSemis;
                    hz *= Mathf.Pow(2f, depth * Mathf.Sin(t * vibratoHz * 2f * Mathf.PI) / 12f);
                }
                cycle += hz / SampleRate;

                float env = Mathf.Min(1f, t / Mathf.Max(0.0005f, attack));
                if (decay > 0f)
                {
                    env *= Mathf.Exp(-decay * t);
                }
                float left = lenSec - t;
                if (left < release)
                {
                    env *= Mathf.Max(0f, left / release);
                }
                Add(buf, start + i, Osc(wave, cycle) * env * vol);
            }
        }

        /// <summary>Convenience: a note at a MIDI pitch.</summary>
        public static void Note(float[] buf, float startSec, float lenSec, float midi, Wave wave, float vol,
            float decay = 0f, float vibratoHz = 0f, float vibratoSemis = 0f)
        {
            float hz = Hz(midi);
            Tone(buf, startSec, lenSec, hz, hz, wave, vol, decay, 0.004f, 0.03f, vibratoHz, vibratoSemis);
        }

        /// <summary>Filtered noise burst (hats, snares, whooshes). lowpass 0..1 darkens it.</summary>
        public static void Noise(float[] buf, float startSec, float lenSec, float vol, float decay, float lowpass = 1f, int seed = 1)
        {
            int start = Mathf.RoundToInt(startSec * SampleRate);
            int count = Mathf.Max(1, Mathf.RoundToInt(lenSec * SampleRate));
            uint state = (uint)(seed * 2654435761u + 12345u);
            float last = 0f;
            for (int i = 0; i < count; i++)
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                float n = (state / (float)uint.MaxValue) * 2f - 1f;
                last += (n - last) * lowpass;
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-decay * t) * Mathf.Min(1f, t * 800f);
                Add(buf, start + i, last * env * vol);
            }
        }

        /// <summary>Sine kick with a fast downward pitch sweep.</summary>
        public static void Kick(float[] buf, float startSec, float vol)
        {
            Tone(buf, startSec, 0.22f, 150f, 42f, Wave.Sine, vol, 9f, 0.001f, 0.02f);
        }

        public static void Snare(float[] buf, float startSec, float vol, int seed = 3)
        {
            Noise(buf, startSec, 0.16f, vol, 22f, 0.6f, seed);
            Tone(buf, startSec, 0.09f, 220f, 160f, Wave.Triangle, vol * 0.6f, 25f, 0.001f, 0.02f);
        }

        public static void Hat(float[] buf, float startSec, float vol, bool open = false, int seed = 7)
        {
            Noise(buf, startSec, open ? 0.14f : 0.04f, vol, open ? 22f : 90f, 1f, seed);
        }

        /// <summary>
        /// Feedback echo. With wrap=true the tail wraps to the start of the buffer, so a loop stays seamless.
        /// </summary>
        public static void Echo(float[] buf, float delaySec, float feedback, float mix, bool wrap)
        {
            int delay = Mathf.RoundToInt(delaySec * SampleRate);
            if (delay <= 0 || delay >= buf.Length)
            {
                return;
            }
            float[] source = (float[])buf.Clone();
            float[] tail = new float[buf.Length];
            // Three taps are plenty for a short slap that decays by `feedback` each time.
            float gain = mix;
            for (int tap = 1; tap <= 4; tap++)
            {
                int offset = delay * tap;
                for (int i = 0; i < source.Length; i++)
                {
                    int j = i + offset;
                    if (j >= buf.Length)
                    {
                        if (!wrap)
                        {
                            break;
                        }
                        j %= buf.Length;
                    }
                    tail[j] += source[i] * gain;
                }
                gain *= feedback;
            }
            for (int i = 0; i < buf.Length; i++)
            {
                buf[i] += tail[i];
            }
        }

        /// <summary>Soft-clips to avoid harsh digital overs when voices stack.</summary>
        public static void Limit(float[] buf, float ceiling = 0.95f)
        {
            for (int i = 0; i < buf.Length; i++)
            {
                buf[i] = (float)System.Math.Tanh(buf[i]) * ceiling;
            }
        }

        public static AudioClip ToClip(string name, float[] buf, bool loop = false)
        {
            AudioClip clip = AudioClip.Create(name, buf.Length, 1, SampleRate, false);
            clip.SetData(buf, 0);
            return clip;
        }

        private static void Add(float[] buf, int index, float value)
        {
            if (index >= buf.Length)
            {
                index %= buf.Length;
            }
            if (index >= 0)
            {
                buf[index] += value;
            }
        }
    }
}
