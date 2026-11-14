using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// Chooses which music plays from the game state and fades between tracks. It only reads the game's
    /// public probes, so it can never change gameplay. The chase track speeds up as the maze empties.
    /// </summary>
    public class PacmanAudio : MonoBehaviour
    {
        private enum Track { None, Title, Intro, Chase, Frightened }

        [Header("Mix")]
        [SerializeField] [Range(0f, 1f)] private float musicVolume = 0.55f;
        [SerializeField] [Range(0f, 1f)] private float titleVolume = 0.35f;
        [Tooltip("Fade speed (volume per second) when switching tracks.")]
        [SerializeField] private float fadeSpeed = 3f;
        [Tooltip("Chase music pitch gain at an empty maze (0.1 = ten percent faster).")]
        [SerializeField] private float tensionPitch = 0.1f;

        private PacmanGame game;
        private PacmanMusic music;
        private AudioSource source;
        private AudioLowPassFilter lowPass;
        private Track track;
        private Track wanted;
        private float fade;

        /// <summary>Name of the track currently playing. Probe for audio tests.</summary>
        public string CurrentTrack => track.ToString();

        public void Init(PacmanGame owner, PacmanMusic tracks)
        {
            game = owner;
            music = tracks;
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            lowPass = gameObject.AddComponent<AudioLowPassFilter>();
            lowPass.cutoffFrequency = 22000f;
        }

        private void Update()
        {
            if (game == null || music == null)
            {
                return;
            }

            PacmanState state = game.State;
            wanted = Pick(state);
            if (wanted != track)
            {
                // Fade out the current track, then swap.
                fade = Mathf.MoveTowards(fade, 0f, fadeSpeed * 4f * Time.unscaledDeltaTime);
                if (fade <= 0.001f || track == Track.None)
                {
                    Switch(wanted);
                }
            }
            else
            {
                fade = Mathf.MoveTowards(fade, 1f, fadeSpeed * Time.unscaledDeltaTime);
            }

            float level = track == Track.Title ? titleVolume : musicVolume;
            source.volume = game.SoundOn ? level * fade : 0f;

            // Muffle the mix while paused so the menu feels like it sits in front of the game.
            float cutoff = state == PacmanState.Paused ? 700f : 22000f;
            lowPass.cutoffFrequency = Mathf.MoveTowards(lowPass.cutoffFrequency, cutoff, 40000f * Time.unscaledDeltaTime);

            if (track == Track.Chase)
            {
                float emptied = game.PelletTotal > 0 ? 1f - game.PelletsRemaining / (float)game.PelletTotal : 0f;
                source.pitch = 1f + tensionPitch * emptied + 0.03f * game.ElroyLevel;
            }
            else if (track == Track.Frightened)
            {
                // The last seconds of fright speed up as a warning that the ghosts are about to recover.
                source.pitch = game.FrightenedRemaining < 2f ? 1.12f : 1f;
            }
            else
            {
                source.pitch = 1f;
            }
        }

        private Track Pick(PacmanState state)
        {
            switch (state)
            {
                case PacmanState.Title:
                    return Track.Title;
                case PacmanState.Ready:
                    return Track.Intro;
                case PacmanState.Playing:
                    return game.FrightenedActive ? Track.Frightened : Track.Chase;
                case PacmanState.Paused:
                    return track == Track.None ? Track.Chase : track;
                default:
                    return Track.None;
            }
        }

        private void Switch(Track next)
        {
            track = next;
            source.Stop();
            source.pitch = 1f;
            switch (next)
            {
                case Track.Title:
                    source.clip = music.Title;
                    source.loop = true;
                    break;
                case Track.Intro:
                    source.clip = music.Intro;
                    source.loop = false;
                    break;
                case Track.Chase:
                    source.clip = music.Chase;
                    source.loop = true;
                    break;
                case Track.Frightened:
                    source.clip = music.Frightened;
                    source.loop = true;
                    break;
                default:
                    source.clip = null;
                    break;
            }

            fade = next == Track.Intro || next == Track.Frightened ? 1f : 0f;
            if (source.clip != null)
            {
                source.Play();
            }
        }
    }
}
