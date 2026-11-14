using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// Procedural sound effects, synthesised at startup (see PacmanSynth) so the game ships no audio files.
    /// </summary>
    public class PacmanSfx
    {
                public AudioClip Pellet { get; private set; }
        public AudioClip Power { get; private set; }
        public AudioClip GhostEaten { get; private set; }
        public AudioClip Death { get; private set; }
        public AudioClip LevelClear { get; private set; }
        public AudioClip Blip { get; private set; }
        public AudioClip Select { get; private set; }
        public AudioClip Back { get; private set; }
        public AudioClip Cherry { get; private set; }
        public AudioClip CherryAppear { get; private set; }
        public AudioClip GameOver { get; private set; }

        private AudioClip[] ghostChain;
        private AudioClip[] steps;

        public PacmanSfx()
        {
            steps = new AudioClip[PacmanShoes.Count * 2];
            for (int shoe = 0; shoe < PacmanShoes.Count; shoe++)
            {
                steps[shoe * 2] = BuildStep((Shoe)shoe, false);
                steps[shoe * 2 + 1] = BuildStep((Shoe)shoe, true);
            }
            Pellet = Pickup("pellet", 83f, 88f, 0.05f, 0.14f);
            Power = BuildPower();
            Death = BuildDeath();
            LevelClear = BuildLevelClear();
            Blip = Pickup("blip", 79f, 79f, 0.045f, 0.12f);
            Select = Arp("select", new[] { 72f, 79f }, 0.06f, Wave.Square, 0.16f);
            Back = Arp("back", new[] { 72f, 65f }, 0.07f, Wave.Square, 0.14f);
            Cherry = Arp("cherry", new[] { 84f, 88f, 91f, 96f }, 0.055f, Wave.Pulse25, 0.16f);
            CherryAppear = Arp("cherry_appear", new[] { 91f, 96f }, 0.07f, Wave.Triangle, 0.2f);
            GameOver = BuildGameOver();

            // Each ghost eaten in a chain sounds a little higher: 200, 400, 800, 1600.
            ghostChain = new AudioClip[4];
            for (int i = 0; i < ghostChain.Length; i++)
            {
                ghostChain[i] = BuildGhostEaten(i);
            }
            GhostEaten = ghostChain[0];
        }

        /// <summary>The ghost-eaten sound for a position in the chain (0 = first).</summary>
        public AudioClip GhostEatenFor(int chain)
        {
            return ghostChain[Mathf.Clamp(chain, 0, ghostChain.Length - 1)];
        }

        public static void Play(AudioSource source, AudioClip clip, bool enabled, float volume = 1f)
        {
            if (enabled && source != null && clip != null)
            {
                source.PlayOneShot(clip, volume);
            }
        }

        /// <summary>The footstep for a shoe. The two feet (flip) differ a little so a walk does not sound like a machine.</summary>
        public AudioClip StepFor(Shoe shoe, bool flip)
        {
            int index = Mathf.Clamp((int)shoe, 0, PacmanShoes.Count - 1) * 2 + (flip ? 1 : 0);
            return steps[index];
        }

        /// <summary>
        /// One footfall on packed earth, low and soft. Leather boots are a deep dull thump; rubber boots add a wet
        /// slap and a longer tail; espadrilles are almost only a dark rustle; flip-flops are a light double slap.
        /// </summary>
        private static AudioClip BuildStep(Shoe shoe, bool flip)
        {
            float[] buf = PacmanSynth.Buffer(0.2f);
            int seed = flip ? 2 : 1;
            float drift = flip ? -2f : 0f;
            switch (shoe)
            {
                case Shoe.RubberBoots:
                    PacmanSynth.Tone(buf, 0f, 0.11f, PacmanSynth.Hz(41f + drift), PacmanSynth.Hz(31f + drift), Wave.Sine, 0.24f, 26f, 0.002f, 0.03f);
                    PacmanSynth.Noise(buf, 0f, 0.12f, 0.2f, 24f, 0.2f, seed);
                    PacmanSynth.Noise(buf, 0.03f, 0.05f, 0.1f, 70f, 0.55f, seed + 10);
                    break;
                case Shoe.Espadrilles:
                    PacmanSynth.Tone(buf, 0f, 0.07f, PacmanSynth.Hz(36f + drift), PacmanSynth.Hz(30f + drift), Wave.Sine, 0.07f, 40f, 0.003f, 0.02f);
                    PacmanSynth.Noise(buf, 0f, 0.1f, 0.15f, 55f, 0.1f, seed);
                    break;
                case Shoe.FlipFlops:
                    PacmanSynth.Tone(buf, 0f, 0.05f, PacmanSynth.Hz(44f + drift), PacmanSynth.Hz(38f + drift), Wave.Sine, 0.08f, 50f, 0.002f, 0.02f);
                    PacmanSynth.Noise(buf, 0f, 0.04f, 0.2f, 90f, 0.5f, seed);
                    PacmanSynth.Noise(buf, 0.07f, 0.05f, 0.12f, 80f, 0.4f, seed + 10);
                    break;
                default:
                    PacmanSynth.Tone(buf, 0f, 0.12f, PacmanSynth.Hz(37f + drift), PacmanSynth.Hz(27f + drift), Wave.Sine, 0.3f, 28f, 0.002f, 0.03f);
                    PacmanSynth.Noise(buf, 0f, 0.09f, 0.14f, 45f, 0.07f, seed);
                    break;
            }
            return PacmanSynth.ToClip("step_" + shoe + (flip ? "_b" : "_a"), buf);
        }

        private static AudioClip Pickup(string name, float fromMidi, float toMidi, float seconds, float volume)
        {
            float[] buf = PacmanSynth.Buffer(seconds + 0.04f);
            PacmanSynth.Tone(buf, 0f, seconds, PacmanSynth.Hz(fromMidi), PacmanSynth.Hz(toMidi), Wave.Square, volume, 14f, 0.002f, 0.02f);
            return PacmanSynth.ToClip(name, buf);
        }

        private static AudioClip Arp(string name, float[] midi, float step, Wave wave, float volume)
        {
            float[] buf = PacmanSynth.Buffer(step * midi.Length + 0.25f);
            for (int i = 0; i < midi.Length; i++)
            {
                PacmanSynth.Note(buf, i * step, step * 1.6f, midi[i], wave, volume, 10f);
            }
            PacmanSynth.Echo(buf, 0.07f, 0.4f, 0.35f, false);
            PacmanSynth.Limit(buf);
            return PacmanSynth.ToClip(name, buf);
        }

        private static AudioClip BuildPower()
        {
            float[] buf = PacmanSynth.Buffer(0.7f);
            // A rising sweep with a shimmer on top and a breath of noise underneath.
            PacmanSynth.Tone(buf, 0f, 0.5f, 110f, 660f, Wave.Saw, 0.14f, 1.5f, 0.01f, 0.1f);
            PacmanSynth.Tone(buf, 0.02f, 0.5f, 220f, 1320f, Wave.Square, 0.08f, 2.5f, 0.01f, 0.1f);
            PacmanSynth.Noise(buf, 0f, 0.4f, 0.1f, 5f, 0.2f, 11);
            PacmanSynth.Note(buf, 0.42f, 0.2f, 84f, Wave.Triangle, 0.18f, 8f);
            PacmanSynth.Echo(buf, 0.09f, 0.45f, 0.3f, false);
            PacmanSynth.Limit(buf);
            return PacmanSynth.ToClip("power", buf);
        }

        private static AudioClip BuildGhostEaten(int chain)
        {
            float[] buf = PacmanSynth.Buffer(0.6f);
            float shift = chain * 2f;
            // Quick glide up, then a bright two-note sparkle.
            PacmanSynth.Tone(buf, 0f, 0.16f, PacmanSynth.Hz(60f + shift), PacmanSynth.Hz(91f + shift), Wave.Pulse25, 0.2f, 3f, 0.002f, 0.03f);
            PacmanSynth.Note(buf, 0.16f, 0.09f, 88f + shift, Wave.Square, 0.16f, 10f);
            PacmanSynth.Note(buf, 0.23f, 0.18f, 95f + shift, Wave.Square, 0.16f, 9f);
            PacmanSynth.Noise(buf, 0f, 0.08f, 0.1f, 40f, 0.5f, 5 + chain);
            PacmanSynth.Echo(buf, 0.08f, 0.4f, 0.3f, false);
            PacmanSynth.Limit(buf);
            return PacmanSynth.ToClip("ghost_eaten_" + chain, buf);
        }

        private static AudioClip BuildDeath()
        {
            float[] buf = PacmanSynth.Buffer(1.6f);
            // Wobbling nosedive in steps, ending in a little pop.
            float t = 0f;
            for (int i = 0; i < 12; i++)
            {
                float from = 78f - i * 3.2f;
                PacmanSynth.Tone(buf, t, 0.1f, PacmanSynth.Hz(from), PacmanSynth.Hz(from - 2.6f), Wave.Pulse25, 0.22f, 2f, 0.003f, 0.02f);
                PacmanSynth.Tone(buf, t, 0.1f, PacmanSynth.Hz(from - 12f), PacmanSynth.Hz(from - 14.6f), Wave.Triangle, 0.2f, 2f, 0.003f, 0.02f);
                t += 0.085f;
            }
            PacmanSynth.Note(buf, t + 0.02f, 0.12f, 45f, Wave.Triangle, 0.25f, 12f);
            PacmanSynth.Noise(buf, t, 0.2f, 0.18f, 18f, 0.35f, 21);
            PacmanSynth.Echo(buf, 0.11f, 0.4f, 0.25f, false);
            PacmanSynth.Limit(buf);
            return PacmanSynth.ToClip("death", buf);
        }

        private static AudioClip BuildLevelClear()
        {
            float[] buf = PacmanSynth.Buffer(2.6f);
            float[] notes = { 72f, 76f, 79f, 84f, 79f, 84f, 88f, 91f };
            float step = 0.1f;
            for (int i = 0; i < notes.Length; i++)
            {
                PacmanSynth.Note(buf, i * step, step * 1.7f, notes[i], Wave.Pulse25, 0.16f, 7f);
                PacmanSynth.Note(buf, i * step, step * 1.7f, notes[i] - 12f, Wave.Triangle, 0.12f, 7f);
            }
            float tail = notes.Length * step;
            // Held major chord with a bass root, shimmering with vibrato.
            PacmanSynth.Note(buf, tail, 1.4f, 84f, Wave.Square, 0.11f, 2f, 5.5f, 0.2f);
            PacmanSynth.Note(buf, tail, 1.4f, 88f, Wave.Square, 0.09f, 2f, 5.5f, 0.2f);
            PacmanSynth.Note(buf, tail, 1.4f, 91f, Wave.Square, 0.09f, 2f, 5.5f, 0.2f);
            PacmanSynth.Note(buf, tail, 1.5f, 48f, Wave.Triangle, 0.26f, 1.5f);
            PacmanSynth.Echo(buf, 0.14f, 0.5f, 0.3f, false);
            PacmanSynth.Limit(buf);
            return PacmanSynth.ToClip("level_clear", buf);
        }

        private static AudioClip BuildGameOver()
        {
            float[] buf = PacmanSynth.Buffer(2.2f);
            float[] notes = { 67f, 64f, 60f, 55f };
            for (int i = 0; i < notes.Length; i++)
            {
                PacmanSynth.Note(buf, i * 0.28f, 0.3f, notes[i], Wave.Pulse25, 0.2f, 3f, 5f, 0.25f);
                PacmanSynth.Note(buf, i * 0.28f, 0.3f, notes[i] - 12f, Wave.Triangle, 0.2f, 3f);
            }
            PacmanSynth.Note(buf, 1.15f, 0.9f, 43f, Wave.Triangle, 0.3f, 3f, 4f, 0.3f);
            PacmanSynth.Echo(buf, 0.16f, 0.5f, 0.3f, false);
            PacmanSynth.Limit(buf);
            return PacmanSynth.ToClip("game_over", buf);
        }
    }
}
