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
        private AudioClip chase;

        /// <summary>The gameplay loop (~97 s). Rendered the first time it is asked for, so the title screen shows before the synth works on it.</summary>
        public AudioClip Chase
        {
            get
            {
                // Asked for before the background render finished: do the rest now (a hitch, but only if the player was very quick).
                while (chase == null)
                {
                    WarmChase(1000f);
                }
                return chase;
            }
        }

        private System.Collections.Generic.IEnumerator<bool> chaseJob;

        /// <summary>Renders the gameplay loop in small slices of about this many milliseconds, so no single frame stalls.</summary>
        public void WarmChase(float milliseconds)
        {
            if (chase != null)
            {
                // The two short tracks follow, one per frame, so neither lands on the first power pellet or the first Ready.
                if (frightened == null)
                {
                    frightenedJob ??= FrightenedSteps(made => frightened = made).GetEnumerator();
                    System.Diagnostics.Stopwatch slice = System.Diagnostics.Stopwatch.StartNew();
                    do
                    {
                        if (!frightenedJob.MoveNext())
                        {
                            break;
                        }
                    }
                    while (slice.Elapsed.TotalMilliseconds < milliseconds);
                }
                else if (intro == null)
                {
                    intro = BuildIntro();
                }
                return;
            }
            chaseJob ??= ChaseSteps();
            System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
            do
            {
                if (!chaseJob.MoveNext())
                {
                    break;
                }
            }
            while (clock.Elapsed.TotalMilliseconds < milliseconds);
        }

        public bool ChaseReady => chase != null && frightened != null && intro != null;
        private AudioClip frightened;
        private AudioClip intro;
        public AudioClip Frightened
        {
            get
            {
                if (frightened == null)
                {
                    // Not rendered in the background yet: finish it now.
                    foreach (bool tick in FrightenedSteps(made => frightened = made))
                    {
                    }
                }
                return frightened;
            }
        }

        private System.Collections.Generic.IEnumerator<bool> frightenedJob;
        public AudioClip Intro => intro != null ? intro : (intro = BuildIntro());

        // Bambuco: 6/8 against 3/4 (the "sesquialtera"), A minor with a lift to the relative major. Chord tables are in the chase form below.

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
        }

        /// <summary>The note a diatonic third below, so the lead sings in the parallel thirds of the tiple duets.</summary>
        private static float ThirdBelow(int midi, bool dominant)
        {
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

        // Title menu: a slow pasillo (3/4 waltz): A = eight bars in D minor (Dm - Gm - A - Dm - Bb - F - A - Dm), B = eight bars in F major; A B then A B again doubled in thirds (~52 s).
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

        // Title section B: a second tune in the relative major (F), Dm ends it so it falls back into A.
        private static readonly float[] TitleRootsB = { 41f, 48f, 38f, 46f, 41f, 48f, 45f, 38f };
        private static readonly float[][] TitleChordsB =
        {
            new[] { 60f, 65f, 69f },
            new[] { 60f, 64f, 67f },
            new[] { 62f, 65f, 69f },
            new[] { 58f, 62f, 65f },
            new[] { 60f, 65f, 69f },
            new[] { 60f, 64f, 67f },
            new[] { 61f, 64f, 69f },
            new[] { 62f, 65f, 69f },
        };
        private static readonly int[][] TitleMelodyB =
        {
            new[] { 77, 2, 81, 2, 79, 1, 77, 1 },
            new[] { 76, 3, 72, 2, 74, 1 },
            new[] { 74, 2, 77, 2, 81, 2 },
            new[] { 82, 3, 81, 2, 77, 1 },
            new[] { 79, 2, 77, 2, 72, 2 },
            new[] { 76, 2, 79, 2, 84, 2 },
            new[] { 81, 3, 79, 2, 73, 1 },
            new[] { 74, 6 },
        };

        /// <summary>The note a diatonic third below in D minor / F major (the seventh is raised over the A chord).</summary>
        private static float TitleThirdBelow(int midi, bool dominant)
        {
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
            const int bars = 32;
            float barLength = eighth * 6f;
            float[] melodic = PacmanSynth.Buffer(barLength * bars);
            float[] rhythm = PacmanSynth.Buffer(barLength * bars);

            for (int bar = 0; bar < bars; bar++)
            {
                int chord = bar % 8;
                bool second = bar >= 16;
                bool sectionB = (bar / 8) % 2 == 1;
                bool dominant = chord == 6 || (!sectionB && chord == 2);
                float[][] chords = sectionB ? TitleChordsB : TitleChords;
                float barStart = bar * barLength;
                float root = sectionB ? TitleRootsB[chord] : TitleRoots[chord];

                // Waltz bass: the root on one, a softer fifth on the second beat.
                PacmanSynth.Note(rhythm, barStart, eighth * 3.2f, root, Wave.Triangle, 0.32f, 3f);
                PacmanSynth.Note(rhythm, barStart + eighth * 2f, eighth * 1.6f, root + 7f, Wave.Triangle, 0.15f, 5f);

                // Rolled guitar arpeggio, one note per eighth after the bass.
                int[] order = { 0, 1, 2, 1, 2 };
                for (int e = 1; e < 6; e++)
                {
                    PacmanSynth.Note(rhythm, barStart + e * eighth, eighth * 1.5f, chords[chord][order[e - 1]], Wave.Pulse125, 0.07f, 9f);
                }

                // Barely there percussion: a soft thump on one and a shaker on the beats.
                PacmanSynth.Kick(rhythm, barStart, 0.16f);
                for (int h = 0; h < 3; h++)
                {
                    PacmanSynth.Hat(rhythm, barStart + h * eighth * 2f, 0.035f, false, bar * 7 + h);
                }

                // Sung melody; the second time round it doubles in thirds with a brighter voice.
                int[] notes = (sectionB ? TitleMelodyB : TitleMelody)[chord];
                float cursor = barStart;
                for (int n = 0; n < notes.Length; n += 2)
                {
                    float length = notes[n + 1] * eighth;
                    Wave wave = second ? Wave.Pulse25 : Wave.Triangle;
                    float gain = second ? 0.1f : 0.2f;
                    PacmanSynth.Note(melodic, cursor, length * 0.97f, notes[n], wave, gain, 1.4f, 5f, 0.25f);
                    if (second)
                    {
                        PacmanSynth.Note(melodic, cursor, length * 0.97f, TitleThirdBelow(notes[n], dominant), wave, gain * 0.7f, 1.4f, 5f, 0.25f);
                    }
                    cursor += length;
                }
            }

            PacmanSynth.Echo(melodic, eighth * 3f, 0.35f, 0.34f, true);
            Mix(melodic, rhythm);
            PacmanSynth.Limit(melodic, 0.9f);
            return PacmanSynth.ToClip("music_title", melodic, true);
        }

        // ---- Chase form: six sections of sixteen bars (~97 s) so the loop is long enough not to be noticed:
        // A (the original tune), B (a new tune in the relative major), C (breakdown and build-up),
        // A' and B' (the same tunes in the bright voice, an octave up), then a soft reprise of A that falls back into the start.
        private struct ChaseBar
        {
            public string Chord;
            public int[] Melody;
            public bool Bright;
            public bool Lift;
            public bool Busy;
            public bool Fill;
        }

        private static readonly string[] ChordsA = { "Am", "E", "Am", "Dm", "C", "G", "E", "Am" };
        private static readonly string[] ChordsB = { "C", "G", "Am", "Em", "F", "C", "Dm", "E" };
        private static readonly string[] ChordsC = { "Am", "Am", "Dm", "Dm", "E", "E", "Am", "E" };

        private static readonly int[][] MelodyB =
        {
            new[] { 76, 2, 79, 1, 76, 1, 72, 2 },
            new[] { 74, 2, 71, 1, 74, 1, 79, 2 },
            new[] { 81, 2, 79, 1, 76, 1, 72, 2 },
            new[] { 76, 3, 71, 1, 74, 1, 76, 1 },
            new[] { 77, 2, 81, 1, 77, 1, 72, 2 },
            new[] { 79, 2, 76, 1, 72, 1, 76, 2 },
            new[] { 74, 2, 77, 1, 81, 1, 77, 2 },
            new[] { 80, 2, 76, 1, 71, 1, 76, 2 },
        };

        private static readonly int[][] MelodyC =
        {
            new[] { 76, 3, 72, 1, 69, 2 },
            new[] { 72, 2, 76, 2, 81, 2 },
            new[] { 77, 3, 74, 1, 69, 2 },
            new[] { 74, 2, 77, 2, 81, 2 },
            new[] { 80, 2, 76, 2, 71, 2 },
            new[] { 83, 3, 80, 1, 76, 2 },
            new[] { 81, 3, 76, 1, 72, 2 },
            new[] { 80, 4, 0, 2 },
        };

        private static float ChordRoot(string chord)
        {
            switch (chord)
            {
                case "Am": return 45f;
                case "E": return 40f;
                case "Dm": return 38f;
                case "C": return 48f;
                case "G": return 43f;
                case "Em": return 40f;
                default: return 41f; // F
            }
        }

        private static float[] ChordStrum(string chord)
        {
            switch (chord)
            {
                case "Am": return new[] { 57f, 60f, 64f };
                case "E": return new[] { 56f, 59f, 64f };
                case "Dm": return new[] { 57f, 62f, 65f };
                case "C": return new[] { 55f, 60f, 64f };
                case "G": return new[] { 55f, 59f, 62f };
                case "Em": return new[] { 55f, 59f, 64f };
                default: return new[] { 57f, 60f, 65f }; // F
            }
        }

        private static void AddPass(System.Collections.Generic.List<ChaseBar> form, string[] chords, int[][] melody, bool bright, bool liftAll, bool busy, bool fillAtEnd = false)
        {
            for (int i = 0; i < 8; i++)
            {
                form.Add(new ChaseBar
                {
                    Chord = chords[i],
                    Melody = melody != null ? melody[i] : null,
                    Bright = bright,
                    Lift = bright && (liftAll || i % 4 >= 2),
                    Busy = busy,
                    Fill = fillAtEnd && i == 7,
                });
            }
        }

        private static System.Collections.Generic.List<ChaseBar> ChaseForm()
        {
            var form = new System.Collections.Generic.List<ChaseBar>();
            AddPass(form, ChordsA, Melody, false, false, false);      // A
            AddPass(form, ChordsA, Melody, true, false, true);
            AddPass(form, ChordsB, MelodyB, false, false, false);     // B
            AddPass(form, ChordsB, MelodyB, true, false, true);
            AddPass(form, ChordsC, null, false, false, true);         // C: breakdown (no lead), then the lead returns
            AddPass(form, ChordsC, MelodyC, true, false, true, true);
            AddPass(form, ChordsA, Melody, true, true, true);         // A', bright all the way
            AddPass(form, ChordsA, Melody, true, false, true);
            AddPass(form, ChordsB, MelodyB, true, false, true);       // B', bright
            AddPass(form, ChordsB, MelodyB, true, true, true);
            AddPass(form, ChordsA, Melody, false, false, false);      // soft reprise of A, back into the start
            AddPass(form, ChordsA, Melody, false, false, false);
            return form;
        }

        private System.Collections.Generic.IEnumerator<bool> ChaseSteps()
        {
            const float eighth = 0.168f;
            var form = ChaseForm();
            int bars = form.Count;
            float step = eighth / 2f;
            float barLength = eighth * 6f;
            yield return true;
            float[] melodic = PacmanSynth.Buffer(barLength * bars);
            yield return true;
            float[] rhythm = PacmanSynth.Buffer(barLength * bars);
            yield return true;

            for (int bar = 0; bar < bars; bar++)
            {
                yield return true;
                ChaseBar cb = form[bar];
                bool second = cb.Bright;
                // Every other bar the accents slide from 6/8 (two groups of three) to 3/4 (three groups of two).
                bool hemiola = bar % 2 == 1;
                float barStart = bar * barLength;
                float root = ChordRoot(cb.Chord);
                float[] strum = ChordStrum(cb.Chord);

                // Guitar bass: root on one, the fifth answering on the second group.
                PacmanSynth.Note(rhythm, barStart, eighth * 2.6f, root, Wave.Triangle, 0.34f, 3.5f);
                PacmanSynth.Note(rhythm, barStart + eighth * 3f, eighth * 2.2f, root + 7f, Wave.Triangle, 0.26f, 4f);
                if (hemiola)
                {
                    PacmanSynth.Note(rhythm, barStart + eighth * 2f, eighth * 1.2f, root + 12f, Wave.Triangle, 0.18f, 5f);
                    PacmanSynth.Note(rhythm, barStart + eighth * 4f, eighth * 1.2f, root + 7f, Wave.Triangle, 0.18f, 5f);
                }

                yield return true;
                // Strummed chords on the off-eighths: 6/8 hits 2,3,5,6; 3/4 hits 3 and 5 hard.
                int[] hits = hemiola ? new[] { 2, 4 } : new[] { 1, 2, 4, 5 };
                foreach (int hit in hits)
                {
                    float t = barStart + hit * eighth;
                    for (int n = 0; n < strum.Length; n++)
                    {
                        PacmanSynth.Note(rhythm, t + n * 0.011f, eighth * 1.1f, strum[n], Wave.Pulse125, hemiola ? 0.085f : 0.06f, 11f);
                    }
                }

                yield return true;
                // Tiple: a bright running arpeggio, busier in the bright passes.
                for (int e = 0; e < 6; e++)
                {
                    if (!cb.Busy && e % 2 == 1)
                    {
                        continue;
                    }
                    float midi = strum[e % 3] + 12f;
                    PacmanSynth.Note(melodic, barStart + e * eighth, eighth * 1.3f, midi, Wave.Pulse25, cb.Busy ? 0.06f : 0.045f, 14f);
                }

                yield return true;
                // Percussion: tambora kick on one (and four), a dry rim where the hemiola bites, shaker on every eighth.
                PacmanSynth.Kick(rhythm, barStart, 0.4f);
                PacmanSynth.Kick(rhythm, barStart + eighth * 3f, 0.22f);
                foreach (int sn in hemiola ? new[] { 2, 4 } : new[] { 2, 5 })
                {
                    PacmanSynth.Snare(rhythm, barStart + sn * eighth, 0.07f, bar * 5 + sn);
                }
                yield return true;
                for (int h = 0; h < 12; h++)
                {
                    PacmanSynth.Hat(rhythm, barStart + h * step, h % 2 == 0 ? 0.06f : 0.025f, false, bar * 13 + h);
                }
                if (cb.Fill)
                {
                    // The build-up before the bright reprise: a snare roll that grows over the last two eighths.
                    for (int f = 0; f < 4; f++)
                    {
                        PacmanSynth.Snare(rhythm, barStart + (4f + f * 0.5f) * eighth, 0.05f + f * 0.03f, bar * 17 + f);
                    }
                }

                yield return true;
                // Lead in parallel thirds. Soft passes: triangle; bright passes: a pulse, an octave up in places.
                int[] notes = cb.Melody;
                if (notes == null)
                {
                    continue;
                }
                bool dominant = cb.Chord == "E";
                float cursor = barStart;
                for (int n = 0; n < notes.Length; n += 2)
                {
                    float length = notes[n + 1] * eighth;
                    if (notes[n] > 0)
                    {
                        float lift = cb.Lift ? 12f : 0f;
                        Wave wave = second ? Wave.Pulse25 : Wave.Triangle;
                        float gain = second ? 0.11f : 0.2f;
                        PacmanSynth.Note(melodic, cursor, length * 0.96f, notes[n] + lift, wave, gain, 1.8f, 5.5f, 0.2f);
                        PacmanSynth.Note(melodic, cursor, length * 0.96f, ThirdBelow(notes[n], dominant) + lift, wave, gain * 0.7f, 1.8f, 5.5f, 0.2f);
                    }
                    cursor += length;
                }
            }

            // Echo wraps, so the tail rolls into the start of the loop.
            yield return true;
            foreach (bool tick in PacmanSynth.EchoSteps(melodic, eighth * 3f, 0.3f, 0.32f, true))
            {
                yield return true;
            }
            Mix(melodic, rhythm);
            yield return true;
            foreach (bool tick in PacmanSynth.LimitSteps(melodic, 0.9f))
            {
                yield return true;
            }
            foreach (bool tick in PacmanSynth.ClipSteps("music_chase", melodic, made => chase = made))
            {
                yield return true;
            }
        }

        private static System.Collections.Generic.IEnumerable<bool> FrightenedSteps(System.Action<AudioClip> done)
        {
            const float bpm = 168f;
            const int bars = 16;
            float step = 60f / bpm / 4f;
            float[] buf = PacmanSynth.Buffer(step * 16 * bars);

            // Four phrases of four bars so the loop is ~23 s: the chromatic bass creeps up, falls back, stalls and climbs higher,
            // while the stab and the drums change shape every phrase.
            float[][] phraseRoots =
            {
                new[] { 33f, 34f, 35f, 36f },
                new[] { 36f, 35f, 34f, 33f },
                new[] { 33f, 33f, 36f, 35f },
                new[] { 38f, 37f, 36f, 35f },
            };
            yield return true;
            for (int bar = 0; bar < bars; bar++)
            {
                yield return true;
                int phrase = bar / 4;
                float barStart = bar * 16 * step;
                float root = phraseRoots[phrase][bar % 4];
                for (int e = 0; e < 8; e++)
                {
                    float midi = root + (e % 2 == 1 ? 12f : 0f);
                    if (phrase == 2 && e % 4 == 3)
                    {
                        midi = root + 7f;
                    }
                    PacmanSynth.Note(buf, barStart + e * 2 * step, step * 1.8f, midi, Wave.Triangle, 0.32f, 5f);
                }
                for (int s = 0; s < 16; s++)
                {
                    bool stab = phrase switch
                    {
                        0 => s % 4 == 2,
                        1 => s % 4 == 2 || s == 15,
                        2 => s % 4 == 3,
                        _ => s % 2 == 0 && s >= 8,
                    };
                    if (stab)
                    {
                        float pitch = root + 36f + (s == 10 ? 1f : 0f) + (phrase == 3 ? 12f : 0f);
                        PacmanSynth.Note(buf, barStart + s * step, step * 2.2f, pitch, Wave.Pulse125, 0.1f, 6f, 9f, 0.5f);
                    }
                    PacmanSynth.Hat(buf, barStart + s * step, s % 2 == 0 ? 0.06f : 0.03f, phrase == 3 && s % 4 == 3, bar * 19 + s);
                }
                int[] kicks = phrase == 2 ? new[] { 0, 6, 8, 12 } : new[] { 0, 4, 8, 12 };
                foreach (int k in kicks)
                {
                    PacmanSynth.Kick(buf, barStart + k * step, 0.42f);
                }
                PacmanSynth.Snare(buf, barStart + 8 * step, 0.12f, bar + 40);
                if (phrase == 3)
                {
                    PacmanSynth.Snare(buf, barStart + 4 * step, 0.08f, bar + 90);
                    PacmanSynth.Snare(buf, barStart + 14 * step, 0.08f, bar + 120);
                }
            }
            PacmanSynth.Echo(buf, step * 3f, 0.3f, 0.3f, true);
            yield return true;
            PacmanSynth.Limit(buf, 0.9f);
            yield return true;
            done(PacmanSynth.ToClip("music_frightened", buf, true));
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
