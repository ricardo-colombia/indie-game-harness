using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// The game's own music, composed as data and rendered at startup with PacmanSynth:
    /// a bambuco (Andean 6/8 against 3/4) A-minor harvest loop written for an arcade game: chip-synth lead in parallel thirds, tiple arpeggio, strummed guitar chords, bass, tambora and shaker, a tense
    /// frightened loop, a short ready fanfare and a slow D-minor pasillo waltz of its own for the title menu.
    /// </summary>
    public class PacmanMusic
    {
        public AudioClip Title { get; private set; }
        public AudioClip Chase { get; private set; }
        public AudioClip Frightened { get; private set; }
        public AudioClip Intro { get; private set; }

        // Bambuco: 6/8 against 3/4 (the "sesquialtera"), A minor with a lift to the relative major (C - G) and back through E.
        // Eight bars of Am - E - Am - Dm - C - G - E - Am. Each bar is six eighths = twelve sixteenth steps.
        private static readonly float[] Roots = { 45f, 40f, 45f, 38f, 48f, 43f, 40f, 45f };
        private static readonly float[][] Strum =
        {
            new[] { 57f, 60f, 64f },
            new[] { 56f, 59f, 64f },
            new[] { 57f, 60f, 64f },
            new[] { 57f, 62f, 65f },
            new[] { 55f, 60f, 64f },
            new[] { 55f, 59f, 62f },
            new[] { 56f, 59f, 64f },
            new[] { 57f, 60f, 64f },
        };

        // Lead melody, a sung pasillo-style line: pairs of MIDI pitch (0 = rest) and length in eighths, six eighths per bar.
        private static readonly int[][] Melody =
        {
            new[] { 76, 2, 72, 1, 69, 1, 72, 2 },
            new[] { 71, 2, 76, 1, 80, 1, 76, 2 },
            new[] { 81, 2, 76, 1, 72, 1, 76, 2 },
            new[] { 77, 2, 74, 1, 77, 1, 81, 1, 77, 1 },
            new[] { 79, 2, 76, 1, 72, 1, 76, 2 },
            new[] { 74, 2, 79, 1, 83, 1, 79, 2 },
            new[] { 80, 2, 76, 1, 71, 1, 76, 1, 80, 1 },
            new[] { 81, 3, 76, 1, 69, 2 },
        };

        // Pitch classes of A minor; over the E chord the seventh degree is raised (G#).
        private static readonly bool[] MinorScale = { true, false, true, false, true, true, false, true, false, true, false, true };

        public PacmanMusic()
        {
            Title = BuildTitle();
            Chase = BuildChase();
            Frightened = BuildFrightened();
            Intro = BuildIntro();
        }

        /// <summary>The note a diatonic third below, so the lead sings in the parallel thirds of the tiple duets.</summary>
        private static float ThirdBelow(int midi, int bar)
        {
            bool dominant = bar == 1 || bar == 6;
            for (int drop = 3; drop <= 4; drop++)
            {
                int pc = ((midi - drop) % 12 + 12) % 12;
                bool inScale = dominant && pc == 8 || (!(dominant && pc == 7) && MinorScale[pc]);
                if (inScale)
                {
                    return midi - drop;
                }
            }
            return midi - 3;
        }

        // Title menu: a slow pasillo (3/4 waltz) in D minor, eight bars of Dm - Gm - A - Dm - Bb - F - A - Dm, played twice.
        private static readonly float[] TitleRoots = { 38f, 43f, 45f, 38f, 46f, 41f, 45f, 38f };
        private static readonly float[][] TitleChords =
        {
            new[] { 62f, 65f, 69f },
            new[] { 62f, 67f, 70f },
            new[] { 61f, 64f, 69f },
            new[] { 62f, 65f, 69f },
            new[] { 62f, 65f, 70f },
            new[] { 60f, 65f, 69f },
            new[] { 61f, 64f, 69f },
            new[] { 62f, 65f, 69f },
        };
        private static readonly int[][] TitleMelody =
        {
            new[] { 74, 3, 77, 2, 76, 1 },
            new[] { 74, 3, 70, 2, 72, 1 },
            new[] { 69, 2, 73, 2, 76, 2 },
            new[] { 77, 4, 74, 2 },
            new[] { 79, 3, 77, 2, 74, 1 },
            new[] { 72, 2, 77, 2, 81, 2 },
            new[] { 79, 3, 76, 2, 73, 1 },
            new[] { 74, 6 },
        };

        /// <summary>The note a diatonic third below in D minor (the seventh is raised over the A chord).</summary>
        private static float TitleThirdBelow(int midi, int bar)
        {
            bool dominant = bar == 2 || bar == 6;
            for (int drop = 3; drop <= 4; drop++)
            {
                int pc = ((midi - drop) % 12 + 12) % 12;
                bool inScale = pc == 2 || pc == 4 || pc == 5 || pc == 7 || pc == 9 || pc == 10 || (dominant ? pc == 1 : pc == 0);
                if (inScale)
                {
                    return midi - drop;
                }
            }
            return midi - 3;
        }

        private static AudioClip BuildTitle()
        {
            const float eighth = 0.27f;
            const int bars = 16;
            float barLength = eighth * 6f;
            float[] melodic = PacmanSynth.Buffer(barLength * bars);
            float[] rhythm = PacmanSynth.Buffer(barLength * bars);

            for (int bar = 0; bar < bars; bar++)
            {
                int chord = bar % 8;
                bool second = bar >= 8;
                float barStart = bar * barLength;
                float root = TitleRoots[chord];

                // Waltz bass: the root on one, a softer fifth on the second beat.
                PacmanSynth.Note(rhythm, barStart, eighth * 3.2f, root, Wave.Triangle, 0.32f, 3f);
                PacmanSynth.Note(rhythm, barStart + eighth * 2f, eighth * 1.6f, root + 7f, Wave.Triangle, 0.15f, 5f);

                // Rolled guitar arpeggio, one note per eighth after the bass.
                int[] order = { 0, 1, 2, 1, 2 };
                for (int e = 1; e < 6; e++)
                {
                    PacmanSynth.Note(rhythm, barStart + e * eighth, eighth * 1.5f, TitleChords[chord][order[e - 1]], Wave.Pulse125, 0.07f, 9f);
                }

                // Barely there percussion: a soft thump on one and a shaker on the beats.
                PacmanSynth.Kick(rhythm, barStart, 0.16f);
                for (int h = 0; h < 3; h++)
                {
                    PacmanSynth.Hat(rhythm, barStart + h * eighth * 2f, 0.035f, false, bar * 7 + h);
                }

                // Sung melody; the second time round it doubles in thirds with a brighter voice.
                int[] notes = TitleMelody[chord];
                float cursor = barStart;
                for (int n = 0; n < notes.Length; n += 2)
                {
                    float length = notes[n + 1] * eighth;
                    Wave wave = second ? Wave.Pulse25 : Wave.Triangle;
                    float gain = second ? 0.1f : 0.2f;
                    PacmanSynth.Note(melodic, cursor, length * 0.97f, notes[n], wave, gain, 1.4f, 5f, 0.25f);
                    if (second)
                    {
                        PacmanSynth.Note(melodic, cursor, length * 0.97f, TitleThirdBelow(notes[n], chord), wave, gain * 0.7f, 1.4f, 5f, 0.25f);
                    }
                    cursor += length;
                }
            }

            PacmanSynth.Echo(melodic, eighth * 3f, 0.35f, 0.34f, true);
            Mix(melodic, rhythm);
            PacmanSynth.Limit(melodic, 0.9f);
            return PacmanSynth.ToClip("music_title", melodic, true);
        }

        private static AudioClip BuildChase()
        {
            const float eighth = 0.168f;
            const int bars = 16;
            float step = eighth / 2f;
            float barLength = eighth * 6f;
            float[] melodic = PacmanSynth.Buffer(barLength * bars);
            float[] rhythm = PacmanSynth.Buffer(barLength * bars);

            for (int bar = 0; bar < bars; bar++)
            {
                int chord = bar % 8;
                bool second = bar >= 8;
                // Every other bar the accents slide from 6/8 (two groups of three) to 3/4 (three groups of two).
                bool hemiola = bar % 2 == 1;
                float barStart = bar * barLength;
                float root = Roots[chord];

                // Guitar bass: root on one, the fifth answering on the second group.
                PacmanSynth.Note(rhythm, barStart, eighth * 2.6f, root, Wave.Triangle, 0.34f, 3.5f);
                PacmanSynth.Note(rhythm, barStart + eighth * 3f, eighth * 2.2f, root + 7f, Wave.Triangle, 0.26f, 4f);
                if (hemiola)
                {
                    PacmanSynth.Note(rhythm, barStart + eighth * 2f, eighth * 1.2f, root + 12f, Wave.Triangle, 0.18f, 5f);
                    PacmanSynth.Note(rhythm, barStart + eighth * 4f, eighth * 1.2f, root + 7f, Wave.Triangle, 0.18f, 5f);
                }

                // Strummed chords on the off-eighths: 6/8 hits 2,3,5,6; 3/4 hits 3 and 5 hard.
                int[] hits = hemiola ? new[] { 2, 4 } : new[] { 1, 2, 4, 5 };
                foreach (int hit in hits)
                {
                    float t = barStart + hit * eighth;
                    for (int n = 0; n < Strum[chord].Length; n++)
                    {
                        PacmanSynth.Note(rhythm, t + n * 0.011f, eighth * 1.1f, Strum[chord][n], Wave.Pulse125, hemiola ? 0.085f : 0.06f, 11f);
                    }
                }

                // Tiple: a bright running arpeggio, busier the second time round.
                for (int e = 0; e < 6; e++)
                {
                    if (!second && e % 2 == 1)
                    {
                        continue;
                    }
                    float midi = Strum[chord][e % 3] + 12f;
                    PacmanSynth.Note(melodic, barStart + e * eighth, eighth * 1.3f, midi, Wave.Pulse25, second ? 0.06f : 0.045f, 14f);
                }

                // Percussion: tambora kick on one (and four), a dry rim where the hemiola bites, shaker on every eighth.
                PacmanSynth.Kick(rhythm, barStart, 0.4f);
                PacmanSynth.Kick(rhythm, barStart + eighth * 3f, 0.22f);
                foreach (int sn in hemiola ? new[] { 2, 4 } : new[] { 2, 5 })
                {
                    PacmanSynth.Snare(rhythm, barStart + sn * eighth, 0.07f, bar * 5 + sn);
                }
                for (int h = 0; h < 12; h++)
                {
                    PacmanSynth.Hat(rhythm, barStart + h * step, h % 2 == 0 ? 0.06f : 0.025f, false, bar * 13 + h);
                }

                // Lead in parallel thirds. First pass: soft triangle; second pass: a brighter pulse an octave up in places.
                int[] notes = Melody[chord];
                float cursor = barStart;
                for (int n = 0; n < notes.Length; n += 2)
                {
                    float length = notes[n + 1] * eighth;
                    if (notes[n] > 0)
                    {
                        float lift = second && bar % 4 >= 2 ? 12f : 0f;
                        Wave wave = second ? Wave.Pulse25 : Wave.Triangle;
                        float gain = second ? 0.11f : 0.2f;
                        PacmanSynth.Note(melodic, cursor, length * 0.96f, notes[n] + lift, wave, gain, 1.8f, 5.5f, 0.2f);
                        PacmanSynth.Note(melodic, cursor, length * 0.96f, ThirdBelow(notes[n], chord) + lift, wave, gain * 0.7f, 1.8f, 5.5f, 0.2f);
                    }
                    cursor += length;
                }
            }

            // Echo wraps, so the tail rolls into the start of the loop.
            PacmanSynth.Echo(melodic, eighth * 3f, 0.3f, 0.32f, true);
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
