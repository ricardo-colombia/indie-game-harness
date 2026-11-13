using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// The game's own music, composed as data and rendered at startup with PacmanSynth:
    /// a cumbia-flavoured A-minor harvest loop (flute lead, tumbao bass, guacharaca scraper), a tense
    /// frightened loop and a short ready fanfare.
    /// </summary>
    public class PacmanMusic
    {
        public AudioClip Chase { get; private set; }
        public AudioClip Frightened { get; private set; }
        public AudioClip Intro { get; private set; }

        // Chord roots (MIDI, low octave) and arpeggio tones for Am - Dm - E - Am, two bars each (a cumbia turnaround).
        private static readonly float[] Roots = { 45f, 50f, 52f, 45f };
        private static readonly float[][] ArpTones =
        {
            new[] { 57f, 60f, 64f, 69f },
            new[] { 62f, 65f, 69f, 74f },
            new[] { 59f, 64f, 68f, 71f },
            new[] { 57f, 60f, 64f, 69f },
        };
        private static readonly int[] ArpOrder = { 0, 1, 2, 3, 2, 1, 2, 1 };

        // Lead melody (a folk-flute line), one entry per note: MIDI pitch (0 = rest) and length in sixteenth steps. 16 steps per bar.
        private static readonly int[][] Melody =
        {
            new[] { 76, 3, 72, 1, 69, 4, 72, 2, 76, 2, 72, 4 },
            new[] { 69, 2, 72, 2, 76, 4, 74, 2, 72, 2, 69, 4 },
            new[] { 74, 3, 77, 1, 74, 4, 77, 2, 81, 2, 77, 4 },
            new[] { 74, 2, 77, 2, 81, 4, 79, 2, 77, 2, 74, 4 },
            new[] { 76, 3, 80, 1, 83, 4, 80, 2, 76, 2, 71, 4 },
            new[] { 71, 2, 76, 2, 80, 4, 83, 2, 80, 2, 76, 4 },
            new[] { 81, 3, 76, 1, 72, 4, 76, 2, 81, 2, 76, 4 },
            new[] { 72, 2, 69, 2, 76, 4, 72, 2, 69, 2, 69, 2, 0, 2 },
        };

        public PacmanMusic()
        {
            Chase = BuildChase();
            Frightened = BuildFrightened();
            Intro = BuildIntro();
        }

        private static AudioClip BuildChase()
        {
            const float bpm = 108f;
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

                // Tumbao bass: root on 1, fifth on the "and" of 2, root again on 3, octave pickup.
                float root = Roots[chord];
                float[] bassSteps = { 0f, 6f, 8f, 14f };
                float[] bassNotes = { 0f, 7f, 0f, 12f };
                for (int b = 0; b < 4; b++)
                {
                    PacmanSynth.Note(rhythm, barStart + bassSteps[b] * step, step * 3.4f, root + bassNotes[b], Wave.Triangle, 0.32f, 3.5f);
                }

                // Drums: kick on 1 and 3, a soft rim on 2 and 4, guacharaca scraper in a steady "tchk-a-tchk".
                foreach (int k in new[] { 0, 8 })
                {
                    PacmanSynth.Kick(rhythm, barStart + k * step, 0.45f);
                }
                foreach (int sn in new[] { 4, 12 })
                {
                    PacmanSynth.Snare(rhythm, barStart + sn * step, 0.09f, bar * 3 + sn);
                }
                for (int h = 0; h < 16; h++)
                {
                    bool accent = h % 4 == 2;
                    PacmanSynth.Hat(rhythm, barStart + h * step, accent ? 0.1f : 0.04f, h % 8 == 6, bar * 17 + h);
                }

                // Lead.
                int[] notes = Melody[bar];
                float cursor = barStart;
                for (int n = 0; n < notes.Length; n += 2)
                {
                    float length = notes[n + 1] * step;
                    if (notes[n] > 0)
                    {
                        PacmanSynth.Note(melodic, cursor, length * 0.96f, notes[n], Wave.Triangle, 0.2f, 1.8f, 5.5f, 0.2f);
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
