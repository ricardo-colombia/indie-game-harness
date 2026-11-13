using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// The game's own music, composed as data and rendered at startup with PacmanSynth:
    /// a driving A-minor chase loop, a tense frightened loop and a short ready fanfare.
    /// </summary>
    public class PacmanMusic
    {
        public AudioClip Chase { get; private set; }
        public AudioClip Frightened { get; private set; }
        public AudioClip Intro { get; private set; }

        // Chord roots (MIDI, low octave) and arpeggio tones for Am - F - C - G, two bars each.
        private static readonly float[] Roots = { 45f, 41f, 48f, 43f };
        private static readonly float[][] ArpTones =
        {
            new[] { 57f, 60f, 64f, 69f },
            new[] { 53f, 57f, 60f, 65f },
            new[] { 60f, 64f, 67f, 72f },
            new[] { 55f, 59f, 62f, 67f },
        };
        private static readonly int[] ArpOrder = { 0, 1, 2, 3, 2, 1, 2, 1 };

        // Lead melody, one entry per note: MIDI pitch (0 = rest) and length in sixteenth steps. 16 steps per bar.
        private static readonly int[][] Melody =
        {
            new[] { 76, 3, 79, 1, 76, 2, 72, 2, 69, 2, 72, 2, 76, 4 },
            new[] { 74, 3, 76, 1, 74, 2, 72, 2, 69, 4, 0, 4 },
            new[] { 77, 3, 81, 1, 77, 2, 76, 2, 72, 2, 76, 2, 77, 4 },
            new[] { 76, 3, 77, 1, 76, 2, 74, 2, 72, 4, 0, 4 },
            new[] { 79, 3, 76, 1, 72, 2, 76, 2, 79, 2, 84, 2, 79, 4 },
            new[] { 79, 2, 77, 2, 76, 2, 74, 2, 72, 4, 67, 4 },
            new[] { 74, 3, 79, 1, 74, 2, 71, 2, 67, 2, 71, 2, 74, 4 },
            new[] { 71, 2, 74, 2, 79, 4, 78, 2, 79, 2, 0, 4 },
        };

        public PacmanMusic()
        {
            Chase = BuildChase();
            Frightened = BuildFrightened();
            Intro = BuildIntro();
        }

        private static AudioClip BuildChase()
        {
            const float bpm = 132f;
            const int bars = 8;
            float step = 60f / bpm / 4f;
            float[] melodic = PacmanSynth.Buffer(step * 16 * bars);
            float[] rhythm = PacmanSynth.Buffer(step * 16 * bars);

            for (int bar = 0; bar < bars; bar++)
            {
                int chord = bar / 2;
                float barStart = bar * 16 * step;

                // Arpeggio: sixteenths, plucky, one octave above the chord.
                for (int s = 0; s < 16; s++)
                {
                    float midi = ArpTones[chord][ArpOrder[s % 8]] + 12f;
                    PacmanSynth.Note(melodic, barStart + s * step, step * 1.4f, midi, Wave.Pulse25, 0.075f, 16f);
                }

                // Bass: eighths, root with octave jumps.
                float root = Roots[chord];
                float[] bassPattern = { 0f, 0f, 12f, 0f, 0f, 0f, 12f, 7f };
                for (int e = 0; e < 8; e++)
                {
                    PacmanSynth.Note(rhythm, barStart + e * 2 * step, step * 1.9f, root + bassPattern[e], Wave.Triangle, 0.3f, 4f);
                }

                // Drums: kick syncopated, snare on 2 and 4, hats on every eighth with open ones off the beat.
                foreach (int k in new[] { 0, 6, 8, 14 })
                {
                    PacmanSynth.Kick(rhythm, barStart + k * step, 0.5f);
                }
                foreach (int sn in new[] { 4, 12 })
                {
                    PacmanSynth.Snare(rhythm, barStart + sn * step, 0.16f, bar * 3 + sn);
                }
                for (int h = 0; h < 16; h += 2)
                {
                    PacmanSynth.Hat(rhythm, barStart + h * step, h % 4 == 2 ? 0.09f : 0.05f, h % 4 == 2 && h != 14, bar * 17 + h);
                }

                // Lead.
                int[] notes = Melody[bar];
                float cursor = barStart;
                for (int n = 0; n < notes.Length; n += 2)
                {
                    float length = notes[n + 1] * step;
                    if (notes[n] > 0)
                    {
                        PacmanSynth.Note(melodic, cursor, length * 0.96f, notes[n], Wave.Square, 0.12f, 1.8f, 5.5f, 0.2f);
                    }
                    cursor += length;
                }
            }

            // Echo wraps, so the tail rolls into the start of the loop.
            PacmanSynth.Echo(melodic, step * 3f, 0.35f, 0.38f, true);
            Mix(melodic, rhythm);
            PacmanSynth.Limit(melodic, 0.9f);
            return PacmanSynth.ToClip("music_chase", melodic, true);
        }

        private static AudioClip BuildFrightened()
        {
            const float bpm = 168f;
            const int bars = 4;
            float step = 60f / bpm / 4f;
            float[] buf = PacmanSynth.Buffer(step * 16 * bars);

            // A creeping chromatic bass line with a wobbling stab above it.
            float[] roots = { 33f, 34f, 35f, 36f };
            for (int bar = 0; bar < bars; bar++)
            {
                float barStart = bar * 16 * step;
                float root = roots[bar];
                for (int e = 0; e < 8; e++)
                {
                    float midi = root + (e % 2 == 1 ? 12f : 0f);
                    PacmanSynth.Note(buf, barStart + e * 2 * step, step * 1.8f, midi, Wave.Triangle, 0.32f, 5f);
                }
                for (int s = 0; s < 16; s++)
                {
                    if (s % 4 == 2)
                    {
                        PacmanSynth.Note(buf, barStart + s * step, step * 2.2f, root + 36f + (s == 10 ? 1f : 0f), Wave.Pulse125, 0.1f, 6f, 9f, 0.5f);
                    }
                    PacmanSynth.Hat(buf, barStart + s * step, s % 2 == 0 ? 0.06f : 0.03f, false, bar * 19 + s);
                }
                foreach (int k in new[] { 0, 4, 8, 12 })
                {
                    PacmanSynth.Kick(buf, barStart + k * step, 0.42f);
                }
                PacmanSynth.Snare(buf, barStart + 8 * step, 0.12f, bar + 40);
            }
            PacmanSynth.Echo(buf, step * 3f, 0.3f, 0.3f, true);
            PacmanSynth.Limit(buf, 0.9f);
            return PacmanSynth.ToClip("music_frightened", buf, true);
        }

        private static AudioClip BuildIntro()
        {
            const float step = 0.1f;
            float[] buf = PacmanSynth.Buffer(2.6f);
            // Rising arpeggio fanfare that lands on a held top note, over a bass pedal.
            int[] notes = { 69, 1, 72, 1, 76, 1, 81, 1, 76, 1, 72, 1, 76, 1, 81, 1, 84, 2, 81, 1, 84, 1, 88, 6 };
            float cursor = 0f;
            for (int n = 0; n < notes.Length; n += 2)
            {
                float length = notes[n + 1] * step;
                PacmanSynth.Note(buf, cursor, length * 1.1f, notes[n], Wave.Pulse25, 0.16f, notes[n + 1] > 2 ? 2f : 6f, notes[n + 1] > 2 ? 5.5f : 0f, 0.2f);
                PacmanSynth.Note(buf, cursor, length * 1.1f, notes[n] - 12f, Wave.Square, 0.05f, 6f);
                cursor += length;
            }
            PacmanSynth.Note(buf, 0f, 0.9f, 33f, Wave.Triangle, 0.3f, 1.5f);
            PacmanSynth.Note(buf, 0.9f, 1.2f, 45f, Wave.Triangle, 0.3f, 1.5f);
            PacmanSynth.Kick(buf, 0f, 0.5f);
            PacmanSynth.Kick(buf, 0.8f, 0.5f);
            PacmanSynth.Snare(buf, 1.2f, 0.14f);
            PacmanSynth.Echo(buf, 0.15f, 0.5f, 0.3f, false);
            PacmanSynth.Limit(buf, 0.9f);
            return PacmanSynth.ToClip("music_intro", buf);
        }

        private static void Mix(float[] into, float[] from)
        {
            for (int i = 0; i < into.Length; i++)
            {
                into[i] += from[i];
            }
        }
    }
}
