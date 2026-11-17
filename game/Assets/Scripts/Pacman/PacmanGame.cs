using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Studio.Pacman
{
    public enum PacmanState { Title, Ready, Playing, Paused, Dying, LevelClear, GameOver, Story, Cast }

    /// <summary>
    /// Game bootstrap and rules: builds the maze, owns scoring, lives, ghost mode timing and state flow.
    /// Tunables are serialized so they can be changed in the inspector without code edits.
    /// Public properties are read-only probes for autotest scenarios.
    /// </summary>
    public class PacmanGame : MonoBehaviour
    {
        [Header("Speeds")]
        [Tooltip("Tiles per second at 100% speed. Per-level speeds are percentages of this (PacmanLevelTable). Taste call: 6 keeps the current feel at levels 5-20.")]
        [SerializeField] private float fullSpeed = 6f;
        [Tooltip("Tiles per second for eaten ghosts returning home (not in the Dossier table).")]
        [SerializeField] private float eatenGhostSpeed = 10f;
        [Tooltip("Tunnel tiles at each end of the tunnel row where ghosts slow down (the Dossier's tunnel speed).")]
        [SerializeField] private int tunnelSlowColumns = 6;
        [Tooltip("Frames Pac-Man stops after eating a dot (Dossier: one frame at 60 fps).")]
        [SerializeField] private int dotPauseFrames = 1;
        [Tooltip("Frames Pac-Man stops after eating a power pellet (Dossier: three frames at 60 fps).")]
        [SerializeField] private int powerPelletPauseFrames = 3;

        [Header("Timers (seconds)")]
        [SerializeField] private float readyDuration = 2f;
        [Tooltip("Seconds the whole farm is shown at the start of a new game before the camera zooms in on the farmer (close view only).")]
        [SerializeField] private float introSeconds = 2.2f;
        [SerializeField] private float deathDuration = 1.5f;
        [SerializeField] private float levelClearDuration = 2.5f;

        [Header("Rules")]
        [SerializeField] private int startingLives = 3;
        [SerializeField] private int pelletPoints = 10;
        [SerializeField] private int powerPelletPoints = 50;
        [SerializeField] private int ghostPoints = 200;
        [Tooltip("Distance (in tiles) at which a ghost touches Pac-Man.")]
        [SerializeField] private float collisionDistance = 0.6f;
        [Tooltip("Chasing ghosts steer by walking distance through the maze instead of straight-line distance.")]
        [SerializeField] private bool pathChase = true;
        [Tooltip("Score that earns one extra life (once per game).")]
        [SerializeField] private int extraLifeScore = 10000;

        [Header("Bonus fruit")]
        [Tooltip("Dots eaten in a level before the first fruit appears.")]
        [SerializeField] private int firstFruitAt = 70;
        [Tooltip("Dots eaten in a level before the second fruit appears.")]
        [SerializeField] private int secondFruitAt = 170;
        [Tooltip("Seconds a fruit stays on the maze before it disappears.")]
        [SerializeField] private float fruitDuration = 9.5f;
        [Tooltip("Distance (in tiles) at which Pac-Man picks the fruit up.")]
        [SerializeField] private float fruitPickupDistance = 0.75f;
        [Tooltip("How many past levels' fruits the row under the maze shows.")]
        [SerializeField] private int fruitRowLength = 7;

        [Tooltip("Seconds between footstep sounds while the farmer is moving.")]
        [FormerlySerializedAs("stepInterval")]
        [SerializeField] private float stepInterval = 0.18f;
        [Tooltip("Loudness of the footsteps, 0..1. They sit under the music and the pellet sounds.")]
        [SerializeField] private float stepVolume = 0.3f;

        private const float PelletSize = 0.46f;
        private const float PowerPelletSize = 0.95f;
        // Pellets are coffee cherries on the bush: mostly ripe red, some still orange-yellow (pinton).
        private static readonly Color PelletColour = new Color(0.72f, 0.42f, 0.2f);
        private static readonly Color PelletUnripeColour = new Color(0.88f, 0.66f, 0.36f);
        private static readonly Color PowerPelletColour = new Color(1f, 0.65f, 0.25f, 0.4f);
        private static readonly Color LeafColour = new Color(0.3f, 0.8f, 0.25f);
        // The four pests, in ghost order: the DIAN agent (steel blue suit), the guerrilla fighter (olive), the narco
        // (cream linen) and the coral snake (red).
        public static readonly Color[] GhostColours =
        {
            new Color(0.5f, 0.62f, 0.95f),
            new Color(0.55f, 0.68f, 0.22f),
            new Color(0.98f, 0.9f, 0.68f),
            new Color(0.95f, 0.22f, 0.2f),
        };

        private readonly List<PacmanGhost> ghosts = new List<PacmanGhost>();
        private readonly List<GameObject> powerPelletObjects = new List<GameObject>();
        private GameObject[] pelletObjects;
        private GameObject[] harvestMarks;
        private bool[] pelletLeft;
        private PacmanPlayer player;
        private Vector2Int playerStart;
        private Vector2Int[] ghostStarts;
        private Vector2Int blinkyStart;
        private int pelletCount;
        private int ghostChain;
        private float stateTimer;
        private float celebrationTimer;
        private float frightenedTimer;
        private float pacPauseTimer;
        private float playTime;

        /// <summary>Seconds of play since the last reset. Probe for ghost release timing.</summary>
        public float PlayTime => playTime;
        private float modeTimer;
        private int modeIndex;
        private PacmanScenery scenery;
        private PacmanPost post;
        private const string HighScoreKey = "studio.pacman.highscore";
        private const string ShoeKey = "studio.pacman.shoe";
        private AudioSource audioSource;
        private PacmanSfx sfx;
        private bool soundOn = true;
        private const string MusicKey = "studio.pacman.vol.music";
        private const string StepsKey = "studio.pacman.vol.steps";
        private const string EffectsKey = "studio.pacman.vol.effects";
        public const int VolumeNotches = 10;
        private int menuIndex;
        private float stepTimer;
        private bool stepFlip;
        private GameObject fruitObject;
        private Transform fruitRow;
        private bool fruitActive;
        private int fruitsSpawned;
        private float fruitTimer;
        private FruitKind fruitKind;
        private bool extraLifeGiven;
        private readonly PacmanGhostRelease release = new PacmanGhostRelease();
        private int pelletTotal;
        private PacmanUi ui;
        private readonly PacmanSwipe swipe = new PacmanSwipe();
        private int fittedWidth;
        private int fittedHeight;
        private float fullSize = 20f;
        private float viewSize = 20f;
        private Vector2 viewCentre;
        private bool viewStarted;
        private bool closeView;
        private const string CloseViewKey = "pacman.closeview";
        private float gameOverSince;

        // The prologue comic: shown before the first harvest, replayable from the title.
        private const string StorySeenKey = "pacman.storyseen";
        private PacmanStoryPlayer story;
        private bool storyReplay;
        private float storyHold;

        // Don Eliseo's sayings: now and then he says something while he walks, and reacts to what happens.
        [SerializeField, Tooltip("Shortest and longest wait, in seconds of play, between two sayings while he walks.")]
        private Vector2 sayWalkEvery = new Vector2(9f, 16f);
        [SerializeField] private float sayShowSeconds = 2.6f;
        [SerializeField, Range(0f, 1f)] private float sayPowerChance = 0.6f;
        [SerializeField, Range(0f, 1f)] private float sayEatPestChance = 0.5f;
        private readonly System.Random sayRandom = new System.Random();
        private readonly Dictionary<SayKind, PacmanSayings.Bag> sayBags = new Dictionary<SayKind, PacmanSayings.Bag>();
        private string sayText;
        private float sayTimer;
        private float sayWalkLeft = -1f;

        /// <summary>What the farmer is saying now (null when quiet).</summary>
        public string SayingText => sayText;
        /// <summary>0 to 1: fade of the speech bubble.</summary>
        public float SayingAlpha => sayText == null ? 0f : Mathf.Clamp01(Mathf.Min(sayTimer, sayShowSeconds - sayTimer) / 0.25f);
        /// <summary>Where the farmer is in the world, for the speech bubble.</summary>
        public Vector3 PlayerWorldPosition => player != null ? player.transform.position : Vector3.zero;

        // The cast sheets (PERSONAJES), and the name tag each character gets the first time they appear.
        private const string MetKey = "studio.pacman.met";
        private const float TagSeconds = 2.4f;
        private int castIndex;
        private int metMask;
        private readonly Queue<CastMember> tagQueue = new Queue<CastMember>();
        private CastMember tagMember;
        private float tagTimer;

        /// <summary>The character whose sheet shows while <see cref="State"/> is Cast.</summary>
        public CastMember CastCurrent => PacmanCast.Members[PacmanCast.Wrap(castIndex)];
        public int CastIndex => PacmanCast.Wrap(castIndex);

        /// <summary>The character whose name tag is on screen (first time they appear), or null.</summary>
        public CastMember TagMember => tagMember;
        /// <summary>0 to 1: fade of the name tag (it eases in and out).</summary>
        public float TagAlpha => tagMember == null ? 0f : Mathf.Clamp01(Mathf.Min(tagTimer, TagSeconds - tagTimer) / 0.35f);

        /// <summary>The comic being shown while <see cref="State"/> is Story, otherwise null.</summary>
        public PacmanStoryPlayer Story => State == PacmanState.Story ? story : null;

#if UNITY_WEBGL
        // A web page cannot quit, so the title has no Quit row there.
        private static readonly string[] TitleItems = { "START", "STORY", "CAST", "OPTIONS" };
#else
        private static readonly string[] TitleItems = { "START", "STORY", "CAST", "OPTIONS", "QUIT" };
#endif
        private static readonly string[] PauseItems = { "RESUME", "OPTIONS", "QUIT TO TITLE" };

        // Everything the player can tune lives here. THEME is a development switch for trying the art styles:
        // the game will keep one of them eventually.
        private static readonly string[] OptionsItems = { "VIEW", "SOUND", "MUSIC", "STEPS", "EFFECTS", "SHOES", "THEME", "BACK" };

        private bool inOptions;

        /// <summary>True while the player is using a touch screen: the UI shows touch hints and a pause button.</summary>
        public bool TouchMode { get; private set; }

        /// <summary>True when the camera follows the player up close instead of showing the whole board.</summary>
        public bool CloseView => closeView;

        // Counts down while a new game shows the whole farm; the camera then eases in on the farmer.
        private float introTimer;

        /// <summary>Switches between the whole-board view and the close follow view. Remembered between runs.</summary>
        public void ToggleView()
        {
            closeView = !closeView;
            introTimer = 0f;
            try
            {
                PlayerPrefs.SetInt(CloseViewKey, closeView ? 1 : 0);
            }
            catch (System.Exception)
            {
                // The choice just is not remembered.
            }
        }

        /// <summary>Id of the art theme that the next game will use. Probe for menu tests.</summary>
        public string ThemeId => PacmanTheme.Current;
        /// <summary>The shoes the farmer wears: they change his footsteps and are kept between runs.</summary>
        public Shoe CurrentShoe { get; private set; }

        /// <summary>Sound toggle probe for autotest scenarios.</summary>
        public bool SoundOn => soundOn;

        /// <summary>Player volume settings, in notches 0..10 (10 = the mix as designed). Kept between runs.</summary>
        public int MusicNotch { get; private set; } = VolumeNotches;
        public int StepsNotch { get; private set; } = VolumeNotches;
        public int EffectsNotch { get; private set; } = VolumeNotches;
        public float MusicGain => MusicNotch / (float)VolumeNotches;
        private float StepGain => stepVolume * StepsNotch / VolumeNotches;
        private float EffectsGain => EffectsNotch / (float)VolumeNotches;

        /// <summary>Notch of a volume row (MUSIC, STEPS, EFFECTS) or -1 if the row is not a volume.</summary>
        public int NotchOf(string item)
        {
            switch (item)
            {
                case "MUSIC": return MusicNotch;
                case "STEPS": return StepsNotch;
                case "EFFECTS": return EffectsNotch;
                default: return -1;
            }
        }

        public static bool IsVolumeRow(string item)
        {
            return item == "MUSIC" || item == "STEPS" || item == "EFFECTS";
        }

        private void AdjustVolume(string item, int delta, bool wrap)
        {
            int now = NotchOf(item);
            int next = now + delta;
            next = wrap ? (next + VolumeNotches + 1) % (VolumeNotches + 1) : Mathf.Clamp(next, 0, VolumeNotches);
            switch (item)
            {
                case "MUSIC": MusicNotch = next; PlayerPrefs.SetInt(MusicKey, next); break;
                case "STEPS": StepsNotch = next; PlayerPrefs.SetInt(StepsKey, next); break;
                default: EffectsNotch = next; PlayerPrefs.SetInt(EffectsKey, next); break;
            }
            PlayerPrefs.Save();
            // Let the director hear the level they just set.
            AudioClip sample = item == "STEPS" ? sfx?.StepFor(CurrentShoe, stepFlip = !stepFlip) : sfx?.Pellet;
            PacmanSfx.Play(audioSource, sample, soundOn, item == "STEPS" ? StepGain : EffectsGain);
        }
        /// <summary>Highlighted menu row. Probe for menu tests.</summary>
        public int MenuIndex => menuIndex;

        /// <summary>Menu rows for the screen being shown (title or pause).</summary>
        public string[] ActiveMenu => inOptions ? OptionsItems : (State == PacmanState.Title ? TitleItems : PauseItems);

        /// <summary>True while the options submenu is shown (from the title or the pause menu).</summary>
        public bool InOptions => inOptions;
        /// <summary>Best score so far, kept between runs. Probe for UI tests.</summary>
        public int HighScore { get; private set; }
        /// <summary>True once the current run has beaten the saved high score.</summary>
        public bool NewHighScore { get; private set; }
        /// <summary>Seconds of frightened time left (0 when not frightened).</summary>
        public float FrightenedRemaining => frightenedTimer;
        /// <summary>Length of the frightened time the current level gives.</summary>
        public float FrightenedDuration => Rule.FrightenedSeconds;
        /// <summary>Pellets the level started with.</summary>
        public int PelletTotal => pelletTotal;

        public int Score { get; private set; }
        public int Lives { get; private set; }
        public int PelletsRemaining => pelletCount;
        /// <summary>True while a bonus fruit is on the maze. Probe for bonus-fruit tests.</summary>
        public bool FruitActive => fruitActive;
        /// <summary>The fruit on offer (or last offered) this level. Probe for bonus-fruit tests.</summary>
        public FruitKind CurrentFruit => fruitKind;
        /// <summary>How many fruits have appeared this level (0..2). Probe for bonus-fruit tests.</summary>
        public int FruitsSpawned => fruitsSpawned;
        /// <summary>World position of the fruit spot, below the ghost house.</summary>
        public static Vector3 FruitPosition => new Vector3(PacmanFruit.SpawnX, -PacmanFruit.SpawnRow, 0f);
        /// <summary>True once the ghost has left the house (Blinky always has). Probe for release tests.</summary>
        public bool IsGhostReleased(int index) => release.IsReleased(index);
        /// <summary>True once the extra life for reaching the bonus score has been given.</summary>
        public bool ExtraLifeGiven => extraLifeGiven;
        public PacmanState State { get; private set; }
        public bool FrightenedActive => frightenedTimer > 0f;
        public int GhostsEatenInChain => ghostChain;
        public Vector2Int PacmanTile => player != null ? player.Tile : Vector2Int.zero;
        public Vector2Int PacmanDir => player != null ? player.Dir : Vector2Int.zero;
        public GhostMode GlobalMode => (modeIndex % 2 == 0) ? GhostMode.Scatter : GhostMode.Chase;

        /// <summary>1-based level number. Probe for level progression tests.</summary>
        public int Level => level;
        private int level = 1;

        /// <summary>Difficulty row for the current level (Dossier table).</summary>
        private LevelRule Rule => PacmanLevelTable.For(level);

        /// <summary>
        /// Cruise Elroy: 0 normal, 1 or 2 when pellets drop to the level's thresholds. Blinky then ignores
        /// scatter and keeps chasing (Dossier). Probe for autotest scenarios.
        /// </summary>
        public int ElroyLevel => pelletCount <= Rule.ElroyTwoPellets ? 2 : pelletCount <= Rule.ElroyOnePellets ? 1 : 0;

        /// <summary>The global mode a ghost should follow right now, taking Elroy into account.</summary>
        private GhostMode ModeFor(PacmanGhost ghost)
        {
            return ghost.Kind == GhostKind.Blinky && ElroyLevel > 0 ? GhostMode.Chase : GlobalMode;
        }

        private void Awake()
        {
            HighScore = PlayerPrefs.GetInt(HighScoreKey, 0);
            closeView = PlayerPrefs.GetInt(CloseViewKey, 1) == 1;
            BuildMaze();
            BuildActors();
            CurrentShoe = (Shoe)Mathf.Clamp(PlayerPrefs.GetInt(ShoeKey, 0), 0, PacmanShoes.Count - 1);
            MusicNotch = Mathf.Clamp(PlayerPrefs.GetInt(MusicKey, VolumeNotches), 0, VolumeNotches);
            StepsNotch = Mathf.Clamp(PlayerPrefs.GetInt(StepsKey, VolumeNotches), 0, VolumeNotches);
            EffectsNotch = Mathf.Clamp(PlayerPrefs.GetInt(EffectsKey, VolumeNotches), 0, VolumeNotches);
            metMask = PlayerPrefs.GetInt(MetKey, 0);
            player.SetShoe(CurrentShoe);
            SetupCamera();
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            sfx = new PacmanSfx();

            PacmanAudio music = new GameObject("Music").AddComponent<PacmanAudio>();
            music.transform.SetParent(transform, false);
            music.Init(this, new PacmanMusic());
            TouchMode = Application.isMobilePlatform;
            ui = gameObject.AddComponent<PacmanUi>();
            ui.Init(this);
            ShowTitle();
        }

        private void SaveHighScore()
        {
            PlayerPrefs.SetInt(HighScoreKey, HighScore);
            PlayerPrefs.Save();
        }

        private void OnApplicationQuit()
        {
            SaveHighScore();
        }

        private void ShowTitle()
        {
            SaveHighScore();
            State = PacmanState.Title;
            menuIndex = 0;
            inOptions = false;
            tagQueue.Clear();
            tagMember = null;
            sayText = null;
            sayWalkLeft = -1f;
        }

        /// <summary>Opens the character sheets from the title.</summary>
        public void BeginCast()
        {
            castIndex = 0;
            State = PacmanState.Cast;
            menuIndex = 0;
        }

        private void ExitCast()
        {
            ShowTitle();
            menuIndex = System.Array.IndexOf(TitleItems, "CAST");
            PacmanSfx.Play(audioSource, sfx?.Back, soundOn, EffectsGain);
        }

        private void StepCast(int delta)
        {
            castIndex = PacmanCast.Wrap(castIndex + delta);
            PacmanSfx.Play(audioSource, sfx?.Blip, soundOn, EffectsGain);
        }

        /// <summary>
        /// A character comes out for the first time on this install: their name tag is queued (one tag at a time,
        /// never blocking play). Not in batch mode, where scenarios drive the game.
        /// </summary>
        private void Meet(int member)
        {
            int bit = 1 << member;
            if ((metMask & bit) != 0)
            {
                return;
            }
            metMask |= bit;
            PlayerPrefs.SetInt(MetKey, metMask);
            PlayerPrefs.Save();
            if (!Application.isBatchMode)
            {
                tagQueue.Enqueue(PacmanCast.Members[member]);
            }
        }

        /// <summary>The farmer says something of this kind (a new line from its shuffled bag).</summary>
        private void Say(SayKind kind)
        {
            if (!sayBags.TryGetValue(kind, out PacmanSayings.Bag bag))
            {
                bag = new PacmanSayings.Bag(PacmanSayings.Pool(kind), sayRandom);
                sayBags[kind] = bag;
            }
            sayText = bag.Next();
            sayTimer = 0f;
        }

        private void SayMaybe(SayKind kind, float chance)
        {
            if (sayRandom.NextDouble() < chance)
            {
                Say(kind);
            }
        }

        private float NextWalkWait()
        {
            return Mathf.Lerp(sayWalkEvery.x, sayWalkEvery.y, (float)sayRandom.NextDouble());
        }

        private void TickSaying(float dt)
        {
            if (sayText != null)
            {
                sayTimer += dt;
                if (sayTimer >= sayShowSeconds)
                {
                    sayText = null;
                }
            }
        }

        private void TickTag(float dt)
        {
            if (tagMember != null)
            {
                tagTimer += dt;
                if (tagTimer >= TagSeconds)
                {
                    tagMember = null;
                }
            }
            if (tagMember == null && tagQueue.Count > 0 && State == PacmanState.Playing)
            {
                tagMember = tagQueue.Dequeue();
                tagTimer = 0f;
            }
        }

        /// <summary>Starts the prologue comic. A replay returns to the title when it ends; the first showing starts the game.</summary>
        public void BeginStory(bool replay)
        {
            story = new PacmanStoryPlayer(PacmanStoryData.Panels);
            storyReplay = replay;
            storyHold = 0f;
            State = PacmanState.Story;
            menuIndex = 0;
        }

        private void FinishStory()
        {
            PlayerPrefs.SetInt(StorySeenKey, 1);
            PlayerPrefs.Save();
            story = null;
            if (storyReplay)
            {
                ShowTitle();
            }
            else
            {
                NewGame();
            }
        }

        /// <summary>Restart from scratch: score, lives and every pellet.</summary>
        public void NewGame()
        {
            Score = 0;
            NewHighScore = false;
            Lives = startingLives;
            extraLifeGiven = false;
            level = 1;
            introTimer = closeView ? introSeconds : 0f;
            RestoreLevel();
        }

        /// <summary>Force the level to end with Pac-Man eating everything. Test hook.</summary>
        public void ClearLevelForTest()
        {
            for (int i = 0; i < pelletLeft.Length; i++)
            {
                if (pelletLeft[i])
                {
                    EatPelletAt(i);
                }
            }
        }

        public void OnPlayerTile(Vector2Int tile)
        {
            int index = Index(tile);
            if (index >= 0 && pelletLeft[index])
            {
                EatPelletAt(index);
            }
        }

        private void Update()
        {
            FitCamera();
            // The opening zoom is slower than the usual follow so it reads as a camera move.
            UpdateView(1f - Mathf.Exp(-(State == PacmanState.Ready ? 2.6f : 7f) * Time.unscaledDeltaTime));
            post?.SetPower(FrightenedActive && State == PacmanState.Playing ? 1f : 0f);
            post?.SetGloom(State == PacmanState.Dying || State == PacmanState.GameOver ? 1f : 0f);
            ReadInput();
            TickTag(Time.unscaledDeltaTime);
            TickSaying(Time.unscaledDeltaTime);

            switch (State)
            {
                case PacmanState.Ready:
                    introTimer = Mathf.Max(0f, introTimer - Time.deltaTime);
                    stateTimer -= Time.deltaTime;
                    if (stateTimer <= 0f)
                    {
                        State = PacmanState.Playing;
                    }
                    break;

                case PacmanState.Playing:
                    // Long frames are cut into short steps so actors cannot pass through each other.
                    int steps = PacmanTiming.Steps(Time.deltaTime, out float step);
                    for (int s = 0; s < steps && State == PacmanState.Playing; s++)
                    {
                        Simulate(step);
                    }
                    break;

                case PacmanState.Dying:
                    stateTimer -= Time.deltaTime;
                    if (stateTimer <= 0f)
                    {
                        PacmanVfx.Pop(player.transform.position, new Color(0.95f, 0.85f, 0.6f), 40);
                        post?.Shake(0.18f, 0.2f);
                        Lives--;
                        if (Lives <= 0)
                        {
                            State = PacmanState.GameOver;
                            gameOverSince = Time.unscaledTime;
                            SaveHighScore();
                            PacmanSfx.Play(audioSource, sfx?.GameOver, soundOn, EffectsGain);
                        }
                        else
                        {
                            ResetActors(true);
                            State = PacmanState.Ready;
                            stateTimer = readyDuration;
                        }
                    }
                    break;

                case PacmanState.Title:
                case PacmanState.Paused:
                case PacmanState.GameOver:
                case PacmanState.Cast:
                    break;

                case PacmanState.Story:
                    story.Tick(Time.unscaledDeltaTime);
                    if (story.Done)
                    {
                        FinishStory();
                    }
                    break;

                case PacmanState.LevelClear:
                    stateTimer -= Time.deltaTime;
                    // A harvest celebration: coloured bursts pop over random spots of the farm while the banner shows.
                    celebrationTimer -= Time.deltaTime;
                    if (celebrationTimer <= 0f)
                    {
                        celebrationTimer = 0.12f;
                        Color[] festive = { new Color(1f, 0.82f, 0.25f), new Color(0.9f, 0.2f, 0.2f), new Color(0.3f, 0.6f, 0.9f), new Color(0.4f, 0.8f, 0.3f) };
                        Vector3 spot = new Vector3(Random.Range(1f, PacmanMaze.Width - 2f), -Random.Range(1f, PacmanMaze.Height - 2f), 0f);
                        PacmanVfx.Pop(spot, festive[Random.Range(0, festive.Length)], 60);
                    }
                    if (stateTimer <= 0f)
                    {
                        level++;
                        RestoreLevel();
                    }
                    break;
            }

            if (!extraLifeGiven && Score >= extraLifeScore)
            {
                GrantExtraLife();
            }

            if (Score > HighScore)
            {
                HighScore = Score;
                NewHighScore = true;
            }

            if (scenery != null)
            {
                scenery.SetAlert(State == PacmanState.Playing && FrightenedActive);
                scenery.SetFlash(State == PacmanState.LevelClear);
            }

            AnimatePowerPellets();
        }

        /// <summary>One frame of menu-relevant input. Built from the keyboard, or set directly by tests.</summary>
        public struct MenuInput
        {
            public bool Up;
            public bool Down;
            public bool Confirm;
            public bool Back;
            public bool Left;
            public bool Right;
        }

        private void ReadInput()
        {
            ReadPointer();

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.anyKey.wasPressedThisFrame)
            {
                TouchMode = false;
            }

            if (keyboard.vKey.wasPressedThisFrame || keyboard.tabKey.wasPressedThisFrame)
            {
                ToggleView();
            }

            ApplyMenuInput(new MenuInput
            {
                Up = keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame,
                Down = keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame,
                Confirm = keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame,
                Back = keyboard.escapeKey.wasPressedThisFrame,
                Left = keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame,
                Right = keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame,
            });

            if (State != PacmanState.Playing && State != PacmanState.Ready)
            {
                return;
            }

            if (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed)
            {
                player.Request(PacmanMaze.Up);
            }
            else if (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed)
            {
                player.Request(PacmanMaze.Down);
            }
            else if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed)
            {
                player.Request(PacmanMaze.Left);
            }
            else if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed)
            {
                player.Request(PacmanMaze.Right);
            }
        }

        /// <summary>
        /// Touch and mouse: tap menu rows and the pause button, tap to leave game over, and drag anywhere on
        /// the screen to steer Pac-Man.
        /// </summary>
        private void ReadPointer()
        {
            Pointer pointer = Pointer.current;
            if (pointer == null)
            {
                return;
            }

            Vector2 position = pointer.position.ReadValue();
            bool pressed = pointer.press.wasPressedThisFrame;
            if (pressed && pointer is Touchscreen)
            {
                TouchMode = true;
            }
            swipe.Threshold = Mathf.Max(24f, 0.04f * Mathf.Min(Screen.width, Screen.height));

            // The UI reports positions with y down, like IMGUI.
            Vector2 gui = new Vector2(position.x, Screen.height - position.y);

            if (State == PacmanState.Title || State == PacmanState.Paused)
            {
                if (pressed && ui != null)
                {
                    int row = ui.MenuRowAt(gui);
                    if (row >= 0)
                    {
                        menuIndex = row;
                        // A tap on a volume row turns it down on the left half and up on the right half.
                        bool volume = IsVolumeRow(ActiveMenu[Mathf.Clamp(row, 0, ActiveMenu.Length - 1)]);
                        ApplyMenuInput(volume
                            ? new MenuInput { Left = gui.x < Screen.width * 0.5f, Right = gui.x >= Screen.width * 0.5f }
                            : new MenuInput { Confirm = true });
                    }
                }
                return;
            }

            if (State == PacmanState.Cast)
            {
                if (pressed && ui != null && ui.OnSkipButton(gui))
                {
                    ExitCast();
                }
                else if (pressed)
                {
                    StepCast(1);
                }
                return;
            }

            if (State == PacmanState.Story)
            {
                if (pressed && ui != null && ui.OnSkipButton(gui))
                {
                    FinishStory();
                    return;
                }
                if (pressed)
                {
                    ApplyMenuInput(new MenuInput { Confirm = true });
                }
                // Holding a finger or the button down also skips.
                storyHold = pointer.press.isPressed ? storyHold + Time.unscaledDeltaTime : 0f;
                if (storyHold > 0.9f && State == PacmanState.Story)
                {
                    FinishStory();
                }
                return;
            }

            if (State == PacmanState.GameOver)
            {
                if (pressed && Time.unscaledTime - gameOverSince > 0.8f)
                {
                    ApplyMenuInput(new MenuInput { Confirm = true });
                }
                return;
            }

            if (State != PacmanState.Playing && State != PacmanState.Ready)
            {
                swipe.End();
                return;
            }

            if (pressed)
            {
                if (ui != null && ui.OnZoomButton(gui))
                {
                    swipe.End();
                    ToggleView();
                    return;
                }
                if (ui != null && ui.OnPauseButton(gui))
                {
                    swipe.End();
                    ApplyMenuInput(new MenuInput { Back = true });
                    return;
                }
                swipe.Begin(position);
            }
            else if (pointer.press.isPressed)
            {
                Vector2Int step = swipe.Move(position);
                if (step != Vector2Int.zero)
                {
                    // Screen y grows upward, maze rows grow downward.
                    player.Request(new Vector2Int(step.x, -step.y));
                }
            }
            else
            {
                if (pointer.press.wasReleasedThisFrame)
                {
                    // A quick flick can start and end between two frames; count where the finger ended.
                    Vector2Int step = swipe.Move(position);
                    if (step != Vector2Int.zero)
                    {
                        player.Request(new Vector2Int(step.x, -step.y));
                    }
                }
                swipe.End();
            }
        }

        /// <summary>
        /// Keeps the whole board in view on any screen shape: tall phones are limited by width, wide
        /// monitors by height.
        /// </summary>
        private void FitCamera()
        {
            if (Screen.width == fittedWidth && Screen.height == fittedHeight)
            {
                return;
            }
            fittedWidth = Screen.width;
            fittedHeight = Screen.height;

            Camera cam = Camera.main;
            if (cam == null || Screen.height <= 0)
            {
                return;
            }
            float aspect = (float)Screen.width / Screen.height;
            float forHeight = PacmanMaze.Height * 0.5f + 3f;
            float forWidth = (PacmanMaze.Width * 0.5f + 1.5f) / aspect;
            fullSize = Mathf.Max(forHeight, forWidth);
            if (!viewStarted)
            {
                viewSize = fullSize;
            }
            UpdateView(1f);
        }

        /// <summary>
        /// Eases the camera between the whole-board view and a close view that follows the player. The close
        /// view never leaves the board, and the title and game-over screens always show the whole board.
        /// </summary>
        private void UpdateView(float blend)
        {
            Camera cam = Camera.main;
            if (cam == null || Screen.height <= 0)
            {
                return;
            }
            float aspect = (float)Screen.width / Screen.height;
            float centreX = PacmanMaze.Width * 0.5f - 0.5f;
            float centreY = -(PacmanMaze.Height * 0.5f - 0.5f);
            // The harvest celebration pulls back to show the whole farm, like the opening does.
            bool close = closeView && introTimer <= 0f && State != PacmanState.Title && State != PacmanState.LevelClear && State != PacmanState.GameOver && State != PacmanState.Story && State != PacmanState.Cast && player != null;
            float targetSize = close ? Mathf.Min(fullSize, Mathf.Max(6f, 5.4f / aspect)) : fullSize;
            Vector2 target = new Vector2(centreX, centreY);
            if (close)
            {
                float halfWidth = targetSize * aspect;
                float slackX = Mathf.Max(0f, PacmanMaze.Width * 0.5f + 1f - halfWidth);
                float slackY = Mathf.Max(0f, PacmanMaze.Height * 0.5f + 1.5f - targetSize);
                Vector3 at = player.transform.position;
                target = new Vector2(Mathf.Clamp(at.x, centreX - slackX, centreX + slackX), Mathf.Clamp(at.y, centreY - slackY, centreY + slackY));
            }
            if (!viewStarted)
            {
                viewCentre = target;
                viewStarted = true;
            }
            viewSize = Mathf.Lerp(viewSize, targetSize, blend);
            viewCentre = Vector2.Lerp(viewCentre, target, blend);
            if (post != null)
            {
                post.SetBaseSize(viewSize);
                post.SetBasePosition(new Vector3(viewCentre.x, viewCentre.y, -10f));
            }
            else
            {
                cam.orthographicSize = viewSize;
                cam.transform.position = new Vector3(viewCentre.x, viewCentre.y, -10f);
            }
        }

        /// <summary>
        /// Applies one frame of menu input. Escape pauses play and resumes from pause; Up/Down move the
        /// highlight; Confirm activates it. Game over waits for Confirm to return to the title.
        /// </summary>
        public void ApplyMenuInput(MenuInput input)
        {
            switch (State)
            {
                case PacmanState.Playing:
                    if (input.Back)
                    {
                        State = PacmanState.Paused;
                        menuIndex = 0;
                        inOptions = false;
                        PacmanSfx.Play(audioSource, sfx?.Back, soundOn, EffectsGain);
                    }
                    return;

                case PacmanState.Paused:
                    if (input.Back && inOptions)
                    {
                        CloseOptions();
                        return;
                    }
                    if (input.Back)
                    {
                        State = PacmanState.Playing;
                        PacmanSfx.Play(audioSource, sfx?.Back, soundOn, EffectsGain);
                        return;
                    }
                    break;

                case PacmanState.Title:
                    if (input.Back && inOptions)
                    {
                        CloseOptions();
                        return;
                    }
                    break;

                case PacmanState.GameOver:
                    if (input.Confirm)
                    {
                        ShowTitle();
                    }
                    return;

                case PacmanState.Cast:
                    if (input.Back)
                    {
                        ExitCast();
                    }
                    else if (input.Confirm || input.Down)
                    {
                        StepCast(1);
                    }
                    else if (input.Up)
                    {
                        StepCast(-1);
                    }
                    return;

                case PacmanState.Story:
                    if (input.Back)
                    {
                        FinishStory();
                    }
                    else if (input.Confirm || input.Down)
                    {
                        story.Advance();
                        if (story.Done)
                        {
                            FinishStory();
                        }
                        else
                        {
                            PacmanSfx.Play(audioSource, sfx?.Blip, soundOn, EffectsGain);
                        }
                    }
                    return;

                default:
                    return;
            }

            string[] items = ActiveMenu;
            if ((input.Left || input.Right) && IsVolumeRow(items[menuIndex]))
            {
                AdjustVolume(items[menuIndex], input.Right ? 1 : -1, false);
            }
            else if (input.Confirm && IsVolumeRow(items[menuIndex]))
            {
                AdjustVolume(items[menuIndex], 1, true);
            }
            else if (input.Down)
            {
                menuIndex = (menuIndex + 1) % items.Length;
                PacmanSfx.Play(audioSource, sfx?.Blip, soundOn, EffectsGain);
            }
            else if (input.Up)
            {
                menuIndex = (menuIndex + items.Length - 1) % items.Length;
                PacmanSfx.Play(audioSource, sfx?.Blip, soundOn, EffectsGain);
            }
            else if (input.Confirm)
            {
                Activate(items[menuIndex]);
            }
        }

        /// <summary>Leaves the options submenu with the cursor back on the OPTIONS row that opened it.</summary>
        private void CloseOptions()
        {
            inOptions = false;
            menuIndex = System.Array.IndexOf(ActiveMenu, "OPTIONS");
            PacmanSfx.Play(audioSource, sfx?.Back, soundOn, EffectsGain);
        }

        private void Activate(string item)
        {
            switch (item)
            {
                case "START":
                    // The first harvest opens with the comic (never in batch mode, where scenarios drive the game).
                    if (PlayerPrefs.GetInt(StorySeenKey, 0) == 0 && !Application.isBatchMode)
                    {
                        BeginStory(false);
                    }
                    else
                    {
                        NewGame();
                    }
                    break;
                case "STORY":
                    BeginStory(true);
                    break;
                case "CAST":
                    BeginCast();
                    break;
                case "RESUME":
                    State = PacmanState.Playing;
                    break;
                case "OPTIONS":
                    inOptions = true;
                    menuIndex = 0;
                    break;
                case "BACK":
                    CloseOptions();
                    return;
                case "SOUND":
                    soundOn = !soundOn;
                    break;
                case "VIEW":
                    ToggleView();
                    break;
                case "THEME":
                    // Everything is painted once at startup, so reloading the scene is what applies the new look.
                    PacmanTheme.Current = PacmanTheme.Current == PacmanTheme.Vintage ? PacmanTheme.Procedural : PacmanTheme.Vintage;
                    UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
                    break;
                case "SHOES":
                    CurrentShoe = PacmanShoes.Next(CurrentShoe);
                    player.SetShoe(CurrentShoe);
                    PlayerPrefs.SetInt(ShoeKey, (int)CurrentShoe);
                    PlayerPrefs.Save();
                    // Let the director hear the new pair straight away.
                    PacmanSfx.Play(audioSource, sfx?.StepFor(CurrentShoe, false), soundOn, StepGain);
                    return;
                case "QUIT":
                    Application.Quit();
                    break;
                case "QUIT TO TITLE":
                    ShowTitle();
                    break;
            }
            bool leaving = item == "RESUME" || item == "QUIT TO TITLE" || item == "QUIT";
            PacmanSfx.Play(audioSource, leaving ? sfx?.Back : sfx?.Select, soundOn, EffectsGain);
        }

        private void Simulate(float dt)
        {
            float pacPercent = FrightenedActive && Rule.PacmanFrightenedPercent > 0f
                ? Rule.PacmanFrightenedPercent
                : Rule.PacmanPercent;
            if (pacPauseTimer > 0f)
            {
                // Pac-Man stops for a frame or three after eating (Dossier).
                pacPauseTimer -= dt;
            }
            else
            {
                player.Tick(fullSpeed * pacPercent / 100f * dt);
            }

            if (player.Dir != Vector2Int.zero)
            {
                stepTimer -= dt;
                if (stepTimer <= 0f)
                {
                    stepTimer = stepInterval;
                    stepFlip = !stepFlip;
                    PacmanSfx.Play(audioSource, sfx?.StepFor(CurrentShoe, stepFlip), soundOn, StepGain);
                }
            }
            else
            {
                stepTimer = 0f;
            }

            if (frightenedTimer > 0f)
            {
                frightenedTimer -= dt;
                if (frightenedTimer <= 0f)
                {
                    frightenedTimer = 0f;
                    EndFrightenedIfNeeded();
                }
            }

            // The scatter/chase timer pauses while ghosts are frightened (Dossier).
            if (frightenedTimer <= 0f)
            {
                modeTimer -= dt;
                if (modeTimer <= 0f)
                {
                    float[] schedule = Rule.ModeSchedule;
                    modeIndex = Mathf.Min(modeIndex + 1, schedule.Length - 1);
                    modeTimer = schedule[modeIndex];
                    foreach (PacmanGhost ghost in ghosts)
                    {
                        if (ghost.Mode == GhostMode.Scatter || ghost.Mode == GhostMode.Chase)
                        {
                            ghost.SetMode(ModeFor(ghost));
                        }
                    }
                }
            }

            if (fruitActive)
            {
                fruitTimer -= dt;
                Vector2 pac = player.transform.position;
                if (Vector2.Distance(pac, FruitPosition) <= fruitPickupDistance)
                {
                    EatFruit();
                }
                else if (fruitTimer <= 0f)
                {
                    HideFruit();
                }
            }

            playTime += dt;
            release.Tick(dt);
            Meet(0);
            if (sayWalkLeft < 0f)
            {
                sayWalkLeft = NextWalkWait();
            }
            sayWalkLeft -= dt;
            if (sayWalkLeft <= 0f && sayText == null && tagMember == null && tagQueue.Count == 0 && player.Dir != Vector2Int.zero)
            {
                Say(SayKind.Walk);
                sayWalkLeft = NextWalkWait();
            }
            for (int i = 0; i < ghosts.Count; i++)
            {
                PacmanGhost ghost = ghosts[i];
                if (!release.IsReleased(i))
                {
                    continue;
                }
                Meet(System.Array.IndexOf(PacmanCast.Members, PacmanCast.For(ghost.Kind)));

                float speed;
                if (ghost.Mode == GhostMode.Eaten)
                {
                    speed = eatenGhostSpeed;
                }
                else if (ghost.Mode == GhostMode.Frightened)
                {
                    speed = fullSpeed * Rule.GhostFrightenedPercent / 100f;
                }
                else if (ghost.Kind == GhostKind.Blinky && ElroyLevel > 0)
                {
                    float elroyPercent = ElroyLevel == 2 ? Rule.ElroyTwoPercent : Rule.ElroyOnePercent;
                    speed = fullSpeed * elroyPercent / 100f;
                }
                else if (InTunnel(ghost.Tile))
                {
                    speed = fullSpeed * Rule.GhostTunnelPercent / 100f;
                }
                else
                {
                    speed = fullSpeed * Rule.GhostPercent / 100f;
                }
                ghost.Tick(speed * dt);
            }

            CheckGhostCollisions();
        }

        /// <summary>True on the wrapping tunnel row, within tunnelSlowColumns of either end.</summary>
        private bool InTunnel(Vector2Int tile)
        {
            return tile.y == PacmanMaze.TunnelRow
                && (tile.x < tunnelSlowColumns || tile.x >= PacmanMaze.Width - tunnelSlowColumns);
        }

        private void CheckGhostCollisions()
        {
            Vector3 pacPos = player.transform.position;
            foreach (PacmanGhost ghost in ghosts)
            {
                if (Vector3.Distance(pacPos, ghost.transform.position) > collisionDistance)
                {
                    continue;
                }

                if (ghost.IsScared)
                {
                    int points = ghostPoints << ghostChain;
                    PacmanSfx.Play(audioSource, sfx?.GhostEatenFor(ghostChain), soundOn, EffectsGain);
                    ghostChain++;
                    Score += points;
                    ghost.Eat();
                    Color ghostColour = GhostColours[(int)ghost.Kind];
                    PacmanVfx.Pop(ghost.transform.position, ghostColour, 30);
                    PacmanUi.Show(ghost.transform.position, points.ToString(), new Color(0.55f, 0.85f, 1f));
                    post?.Punch(0.7f);
                    post?.Shake(0.12f, 0.18f);
                    SayMaybe(SayKind.EatPest, sayEatPestChance);
                }
                else if (ghost.Mode != GhostMode.Eaten)
                {
                    State = PacmanState.Dying;
                    stateTimer = deathDuration;
                    float away = Mathf.Sign(pacPos.x - ghost.transform.position.x);
                    player.StartDeath(deathDuration, ghost.Kind, away == 0f ? 1f : away);
                    PacmanDeathScene.Play(ghost.Kind, ghost.transform, player.transform, deathDuration);
                    PacmanSfx.Play(audioSource, sfx?.Death, soundOn, EffectsGain);
                    post?.Shake(0.3f, 0.35f);
                    post?.Punch(0.4f);
                    Say(SayKind.Hurt);
                    return;
                }
            }
        }

        private void EatPelletAt(int index)
        {
            pelletLeft[index] = false;
            pelletCount--;
            pelletObjects[index].SetActive(false);
            if (harvestMarks[index] != null)
            {
                harvestMarks[index].SetActive(true);
            }

            player.Gobble();
            PacmanVfx.Burst(pelletObjects[index].transform.position, IsPowerPellet(index) ? new Color(0.8f, 0.5f, 0.2f) : new Color(0.85f, 0.55f, 0.3f), IsPowerPellet(index) ? 16 : 6);
            if (IsPowerPellet(index))
            {
                pacPauseTimer = powerPelletPauseFrames / 60f;
                Score += powerPelletPoints;
                ghostChain = 0;
                // Levels 17 and up have no frightened mode (Dossier), so the pellet only scores.
                if (Rule.FrightenedSeconds > 0f)
                {
                    frightenedTimer = Rule.FrightenedSeconds;
                    foreach (PacmanGhost ghost in ghosts)
                    {
                        ghost.Frighten();
                    }
                }
                PacmanSfx.Play(audioSource, sfx?.Power, soundOn, EffectsGain);
                post?.Punch(0.55f);
                SayMaybe(SayKind.Power, sayPowerChance);
            }
            else
            {
                pacPauseTimer = dotPauseFrames / 60f;
                Score += pelletPoints;
                PacmanSfx.Play(audioSource, sfx?.Pellet, soundOn, EffectsGain);
            }

            release.OnDot();

            int eaten = pelletTotal - pelletCount;
            if ((fruitsSpawned == 0 && eaten >= firstFruitAt) || (fruitsSpawned == 1 && eaten >= secondFruitAt))
            {
                SpawnFruit();
            }

            if (pelletCount <= 0)
            {
                State = PacmanState.LevelClear;
                stateTimer = levelClearDuration;
                PacmanSfx.Play(audioSource, sfx?.LevelClear, soundOn, EffectsGain);
                Say(SayKind.Clear);
            }
        }

        /// <summary>Puts this level's fruit on the maze for a fixed time. A second fruit replaces an uneaten first.</summary>
        private void SpawnFruit()
        {
            fruitsSpawned++;
            fruitKind = PacmanFruit.For(level);
            for (int i = fruitObject.transform.childCount - 1; i >= 0; i--)
            {
                Destroy(fruitObject.transform.GetChild(i).gameObject);
            }
            // A soft shadow on the ground under the bonus, which stays put while the fruit bobs.
            AddPelletPart(fruitObject.transform, new Color(0f, 0f, 0f, 0.35f), new Vector2(0f, -0.38f), new Vector3(0.62f, 0.2f, 1f), 0f, 4);
            Transform art = PacmanFruitArt.Build(fruitObject.transform, fruitKind, 0.95f);
            PacmanJuice.Attach(art, PacmanJuice.Kind.Bob, 0.04f, 1.2f, 0f);
            fruitObject.transform.position = FruitPosition;
            fruitObject.SetActive(true);
            fruitActive = true;
            fruitTimer = fruitDuration;
            PacmanSfx.Play(audioSource, sfx?.CherryAppear, soundOn, EffectsGain);
        }

        private void HideFruit()
        {
            fruitActive = false;
            fruitTimer = 0f;
            if (fruitObject != null)
            {
                fruitObject.SetActive(false);
            }
        }

        private void EatFruit()
        {
            int points = PacmanFruit.Points(fruitKind);
            Score += points;
            PacmanVfx.Pop(fruitObject.transform.position, new Color(1f, 0.5f, 0.3f), 24);
            PacmanUi.Show(fruitObject.transform.position, points.ToString(), new Color(1f, 0.75f, 0.45f));
            PacmanSfx.Play(audioSource, sfx?.Cherry, soundOn, EffectsGain);
            HideFruit();
        }

        private void GrantExtraLife()
        {
            extraLifeGiven = true;
            Lives++;
            PacmanSfx.Play(audioSource, sfx?.Cherry, soundOn, EffectsGain);
            if (player != null)
            {
                PacmanUi.Show(player.transform.position + Vector3.up, "+1 VIDA", new Color(0.4f, 1f, 0.5f));
            }
        }

        /// <summary>Redraws the row of fruit under the maze: one per recent level, the current level's at the right.</summary>
        private void RefreshFruitRow()
        {
            if (fruitRow == null)
            {
                return;
            }
            for (int i = fruitRow.childCount - 1; i >= 0; i--)
            {
                Destroy(fruitRow.GetChild(i).gameObject);
            }

            int first = Mathf.Max(1, level - fruitRowLength + 1);
            float y = -(PacmanMaze.Height + 0.6f);
            for (int l = first; l <= level; l++)
            {
                float x = PacmanMaze.Width - 1.2f - (level - l) * 1.15f;
                Transform fruit = PacmanFruitArt.Build(fruitRow, PacmanFruit.For(l), 0.85f);
                fruit.localPosition = new Vector3(x, y, 0f);
            }
        }

        private void EndFrightenedIfNeeded()
        {
            foreach (PacmanGhost ghost in ghosts)
            {
                ghost.CalmInHouse();
                if (ghost.Mode == GhostMode.Frightened)
                {
                    ghost.SetMode(ModeFor(ghost));
                }
            }
        }

        private void RestoreLevel()
        {
            for (int i = 0; i < pelletLeft.Length; i++)
            {
                if (pelletObjects[i] != null)
                {
                    pelletLeft[i] = true;
                    pelletObjects[i].SetActive(true);
                    if (harvestMarks[i] != null)
                    {
                        harvestMarks[i].SetActive(false);
                    }
                }
            }
            pelletCount = 0;
            for (int i = 0; i < pelletLeft.Length; i++)
            {
                if (pelletLeft[i])
                {
                    pelletCount++;
                }
            }

            pelletTotal = pelletCount;
            scenery?.SetLevel(level);
            fruitsSpawned = 0;
            HideFruit();
            RefreshFruitRow();

            ghostChain = 0;
            frightenedTimer = 0f;
            modeIndex = 0;
            modeTimer = Rule.ModeSchedule[0];
            ResetActors(false);
            State = PacmanState.Ready;
            stateTimer = readyDuration + introTimer;
        }

        private void ResetActors(bool afterDeath)
        {
            release.Reset(level, afterDeath);
            ghostChain = 0;
            frightenedTimer = 0f;
            pacPauseTimer = 0f;
            playTime = 0f;
            player.CancelDeath();
            player.Place(playerStart);
            player.ResetFacing();
            player.Request(Vector2Int.zero);
            for (int i = 0; i < ghosts.Count; i++)
            {
                Vector2Int start = i == 0 ? blinkyStart : ghostStarts[i - 1];
                // Blinky starts outside and scatters at once. The others start inside the house and
                // head for the house exit first, as in the original, before joining the global mode.
                ghosts[i].ResetTo(start, i == 0 ? ModeFor(ghosts[i]) : GhostMode.Exiting);
            }
        }

        /// <summary>Current tile of the named ghost. Used by Inky's targeting.</summary>
        public Vector2Int GhostTile(GhostKind kind)
        {
            foreach (PacmanGhost ghost in ghosts)
            {
                if (ghost.Kind == kind)
                {
                    return ghost.Tile;
                }
            }
            return Vector2Int.zero;
        }

        private bool IsPowerPellet(int index)
        {
            return PacmanMaze.Cell(index % PacmanMaze.Width, index / PacmanMaze.Width) == 'o';
        }

        private int Index(Vector2Int tile)
        {
            if (tile.x < 0 || tile.y < 0 || tile.x >= PacmanMaze.Width || tile.y >= PacmanMaze.Height)
            {
                return -1;
            }
            return tile.y * PacmanMaze.Width + tile.x;
        }

        private void AnimatePowerPellets()
        {
            float pulse = 1f + 0.18f * Mathf.Sin(Time.time * 8f);
            foreach (GameObject pellet in powerPelletObjects)
            {
                if (pellet != null && pellet.activeSelf)
                {
                    pellet.transform.localScale = Vector3.one * PowerPelletSize * pulse;
                }
            }
        }

        private void BuildMaze()
        {
            int width = PacmanMaze.Width;
            int height = PacmanMaze.Height;
            pelletObjects = new GameObject[width * height];
            harvestMarks = new GameObject[width * height];
            pelletLeft = new bool[width * height];

            Transform mazeRoot = new GameObject("Maze").transform;
            mazeRoot.SetParent(transform, false);

            var ghostSpawns = new List<Vector2Int>();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector2Int tile = new Vector2Int(x, y);
                    char c = PacmanMaze.Cell(x, y);
                    switch (c)
                    {
                        case '.':
                        case 'o':
                            int index = Index(tile);
                            pelletLeft[index] = true;
                            pelletCount++;
                            GameObject pellet = CreatePellet(mazeRoot, tile, c == 'o');
                            pelletObjects[index] = pellet;
                            harvestMarks[index] = PacmanPath.CreateHarvestMark(mazeRoot, tile);
                            if (c == 'o')
                            {
                                powerPelletObjects.Add(pellet);
                            }
                            break;
                        case 'P':
                            playerStart = tile;
                            break;
                        case 'G':
                            ghostSpawns.Add(tile);
                            break;
                        case '-':
                            CreateGate(mazeRoot, tile);
                            break;
                    }
                }
            }

            // Walls, floor, stars and portals: sprites drawn from the maze grid by the scenery.
            scenery = PacmanScenery.Build(mazeRoot);

            fruitObject = new GameObject("Fruit");
            fruitObject.transform.SetParent(mazeRoot, false);
            fruitObject.SetActive(false);
            fruitRow = new GameObject("FruitRow").transform;
            fruitRow.SetParent(mazeRoot, false);

            // The house holds three starts side by side: Pinky in the middle, Inky on the left, Clyde on the right.
            ghostSpawns.Sort((a, b) => a.x.CompareTo(b.x));
            if (ghostSpawns.Count == 3)
            {
                ghostSpawns = new List<Vector2Int> { ghostSpawns[1], ghostSpawns[0], ghostSpawns[2] };
            }
            ghostStarts = ghostSpawns.ToArray();
            blinkyStart = PacmanMaze.BlinkyStart;
        }

        private static GameObject CreatePellet(Transform parent, Vector2Int tile, bool power)
        {
            GameObject pellet = new GameObject(power ? "PowerPellet" : "Pellet");
            pellet.transform.SetParent(parent, false);
            pellet.transform.position = PacmanMaze.ToWorld(tile);
            pellet.transform.localScale = Vector3.one * (power ? PowerPelletSize : PelletSize);
            SpriteRenderer renderer = pellet.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 1;
            Sprite themed = PacmanTheme.Get(power ? "mug" : "bean");
            if (!power && themed != null)
            {
                // A themed bean: its own sprite, each tilted a little differently.
                // Every cherry differs: free rotation, mirror, size and ripeness (from deep red to a few still-yellow ones).
                renderer.sprite = themed;
                int h = (tile.x * 73856093) ^ (tile.y * 19349663);
                h ^= h >> 13;
                h *= 1274126177;
                h ^= h >> 16;
                float size = 0.82f + ((h >> 3) & 15) * 0.02f;
                pellet.transform.localScale = new Vector3(((h >> 7) & 1) == 0 ? size : -size, size, 1f) * PelletSize;
                pellet.transform.localRotation = Quaternion.Euler(0f, 0f, ((h >> 8) & 255) / 255f * 360f);
                float ripe = ((h >> 16) & 15) / 15f;
                renderer.color = (h >> 20) % 7 == 0
                    ? new Color(1f, 0.95f, 0.55f)
                    : new Color(1f, 0.82f + 0.18f * ripe, 0.8f + 0.2f * ripe);
                AddPelletShadow(pellet.transform, themed);
                return pellet;
            }
            if (!power)
            {
                // A coffee bean: caramel body with a dark crease, each tilted a little differently.
                bool light = (tile.x * 7 + tile.y * 13) % 5 == 0;
                renderer.sprite = PacmanSprites.Bean;
                renderer.color = light ? PelletUnripeColour : PelletColour;
                pellet.transform.localRotation = Quaternion.Euler(0f, 0f, ((tile.x * 31 + tile.y * 17) % 7 - 3) * 14f);
                AddPelletPart(pellet.transform, new Color(0.22f, 0.1f, 0.04f), new Vector2(0f, 0f), new Vector3(0.1f, 0.74f, 1f), 0f, 2, PacmanSprites.Square);
                AddPelletShadow(pellet.transform, PacmanSprites.Bean);
                return pellet;
            }

            // The espresso: a mug in side view with black coffee, a handle and steam. Drinking it is what lets
            // the picker chase the pests. The root disc is a warm glow behind it.
            renderer.sprite = PacmanSprites.SoftDisc;
            renderer.color = PowerPelletColour;
            Transform t = pellet.transform;
            if (themed != null)
            {
                AddPelletPart(t, Color.white, Vector2.zero, Vector3.one * 0.92f, 0f, 3, themed);
                return pellet;
            }
            Color cream = new Color(0.95f, 0.91f, 0.8f);
            Color dark = new Color(0.07f, 0.04f, 0.025f);
            AddPelletPart(t, cream, new Vector2(0.27f, -0.08f), new Vector3(0.36f, 0.36f, 1f), 0f, 2);
            AddPelletPart(t, dark, new Vector2(0.28f, -0.08f), new Vector3(0.16f, 0.16f, 1f), 0f, 3);
            AddPelletPart(t, cream, new Vector2(-0.1f, -0.08f), new Vector3(0.58f, 0.5f, 1f), 0f, 3, PacmanSprites.Square);
            AddPelletPart(t, cream, new Vector2(-0.1f, -0.33f), new Vector3(0.58f, 0.2f, 1f), 0f, 3);
            AddPelletPart(t, new Color(1f, 0.98f, 0.9f), new Vector2(-0.1f, 0.17f), new Vector3(0.58f, 0.22f, 1f), 0f, 4);
            AddPelletPart(t, dark, new Vector2(-0.1f, 0.17f), new Vector3(0.46f, 0.15f, 1f), 0f, 5);
            AddPelletPart(t, new Color(0.62f, 0.36f, 0.16f), new Vector2(-0.18f, 0.19f), new Vector3(0.18f, 0.05f, 1f), 0f, 6);
            Color steam = new Color(1f, 1f, 1f, 0.75f);
            AddPelletPart(t, steam, new Vector2(-0.24f, 0.4f), new Vector3(0.06f, 0.26f, 1f), 14f, 6);
            AddPelletPart(t, steam, new Vector2(-0.08f, 0.46f), new Vector3(0.06f, 0.3f, 1f), -14f, 6);
            AddPelletPart(t, steam, new Vector2(0.06f, 0.4f), new Vector3(0.06f, 0.24f, 1f), 14f, 6);
            return pellet;
        }

        /// <summary>A contact shadow, the bean's own silhouette nudged below-right, so it lies on the earth and not painted on it.</summary>
        private static void AddPelletShadow(Transform pellet, Sprite silhouette)
        {
            GameObject shadow = new GameObject("Shadow");
            shadow.transform.SetParent(pellet, false);
            shadow.transform.position = pellet.position + new Vector3(0.045f, -0.07f, 0f);
            shadow.transform.localRotation = Quaternion.identity;
            shadow.transform.localScale = Vector3.one * 1.08f;
            SpriteRenderer renderer = shadow.AddComponent<SpriteRenderer>();
            renderer.sprite = silhouette;
            renderer.color = new Color(0.04f, 0.02f, 0.008f, 0.62f);
            renderer.sortingOrder = 0;
        }

        private static void AddPelletPart(Transform parent, Color colour, Vector2 position, Vector3 scale, float angle, int order, Sprite sprite = null)
        {
            GameObject part = new GameObject("Part");
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            part.transform.localScale = scale;
            SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite != null ? sprite : PacmanSprites.Disc;
            renderer.color = colour;
            renderer.sortingOrder = order;
        }

        /// <summary>The wooden bar across the pest house gate. Cosmetic: the maze grid decides who may pass.</summary>
        private static void CreateGate(Transform parent, Vector2Int tile)
        {
            GameObject gate = new GameObject("Gate");
            gate.transform.SetParent(parent, false);
            gate.transform.position = PacmanMaze.ToWorld(tile);
            Color wood = new Color(0.72f, 0.5f, 0.25f);
            Color dark = new Color(0.34f, 0.2f, 0.09f);
            // Two rails with a darker underside, and a post at each end, like a farm gate.
            AddGatePart(gate.transform, dark, new Vector2(0f, -0.04f), new Vector3(1f, 0.2f, 1f), 2);
            AddGatePart(gate.transform, wood, new Vector2(0f, 0.01f), new Vector3(0.96f, 0.11f, 1f), 3);
            AddGatePart(gate.transform, wood, new Vector2(0f, -0.09f), new Vector3(0.96f, 0.05f, 1f), 3);
            AddGatePart(gate.transform, dark, new Vector2(-0.48f, 0f), new Vector3(0.1f, 0.34f, 1f), 3);
            AddGatePart(gate.transform, dark, new Vector2(0.48f, 0f), new Vector3(0.1f, 0.34f, 1f), 3);
        }

        private static void AddGatePart(Transform parent, Color colour, Vector2 position, Vector3 scale, int order)
        {
            GameObject part = new GameObject("Part");
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = PacmanSprites.Square;
            renderer.color = colour;
            renderer.sortingOrder = order;
        }

        private void BuildActors()
        {
            GameObject playerObject = new GameObject("Pacman");
            playerObject.transform.SetParent(transform, false);
            player = playerObject.AddComponent<PacmanPlayer>();
            player.Init(this);
            playerObject.AddComponent<PacmanDust>();
            playerObject.AddComponent<PacmanTracks>();

            // Scatter targets sit in the dead space above and below the maze, as in the original
            // (Blinky top-right, Pinky top-left, Inky bottom-right, Clyde bottom-left).
            Vector2Int[] corners =
            {
                new Vector2Int(PacmanMaze.Width - 3, -3),
                new Vector2Int(2, -3),
                new Vector2Int(PacmanMaze.Width - 1, PacmanMaze.Height),
                new Vector2Int(0, PacmanMaze.Height),
            };
            GhostKind[] kinds = { GhostKind.Blinky, GhostKind.Pinky, GhostKind.Inky, GhostKind.Clyde };
            for (int i = 0; i < kinds.Length; i++)
            {
                GameObject ghostObject = new GameObject("Ghost_" + kinds[i]);
                ghostObject.transform.SetParent(transform, false);
                Transform body = PacmanGhostView.Create(ghostObject.transform, GhostColours[i], kinds[i]);
                PacmanGhost ghost = ghostObject.AddComponent<PacmanGhost>();
                ghost.Init(this, kinds[i], corners[i]);
                ghost.PathChase = pathChase;
                body.GetComponent<PacmanGhostView>()?.Bind(ghost, this);
                ghosts.Add(ghost);
            }
        }

        private void SetupCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }
            cam.orthographic = true;
            // Placeholder; FitCamera sizes the view to the actual screen shape on the first frame.
            cam.orthographicSize = PacmanMaze.Height * 0.5f + 3f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.07f, 0.04f);
            // A plain 2D view: the board lies in the XY plane and the camera looks along +Z.
            cam.transform.position = new Vector3(PacmanMaze.Width * 0.5f - 0.5f, -(PacmanMaze.Height * 0.5f - 0.5f), -10f);
            cam.transform.rotation = Quaternion.identity;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 50f;
            post = PacmanPost.Attach(cam);
        }
    }
}
