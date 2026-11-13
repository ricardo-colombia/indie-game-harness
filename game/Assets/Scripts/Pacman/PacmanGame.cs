using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Studio.Pacman
{
    public enum PacmanState { Title, Ready, Playing, Paused, Dying, LevelClear, GameOver }

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
        [Tooltip("Tunnel tiles at each end of row 8 where ghosts slow down (the Dossier's tunnel speed).")]
        [SerializeField] private int tunnelSlowColumns = 4;
        [Tooltip("Frames Pac-Man stops after eating a dot (Dossier: one frame at 60 fps).")]
        [SerializeField] private int dotPauseFrames = 1;
        [Tooltip("Frames Pac-Man stops after eating a power pellet (Dossier: three frames at 60 fps).")]
        [SerializeField] private int powerPelletPauseFrames = 3;

        [Header("Timers (seconds)")]
        [SerializeField] private float readyDuration = 2f;
        [SerializeField] private float deathDuration = 1.5f;
        [SerializeField] private float levelClearDuration = 2.5f;

        [Header("Rules")]
        [SerializeField] private int startingLives = 3;
        [SerializeField] private int pelletPoints = 10;
        [SerializeField] private int powerPelletPoints = 50;
        [SerializeField] private int ghostPoints = 200;
        [Tooltip("Distance (in tiles) at which a ghost touches Pac-Man.")]
        [SerializeField] private float collisionDistance = 0.6f;
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

        [Tooltip("Seconds between waka sounds while Pac-Man is moving.")]
        [SerializeField] private float wakaInterval = 0.18f;

        private const float PelletSize = 0.2f;
        private const float PowerPelletSize = 0.5f;
        // Pellets are coffee cherries on the bush: mostly ripe red, some still orange-yellow (pinton).
        private static readonly Color PelletColour = new Color(0.95f, 0.14f, 0.12f);
        private static readonly Color PelletUnripeColour = new Color(1f, 0.62f, 0.15f);
        private static readonly Color PowerPelletColour = new Color(0.9f, 0.08f, 0.1f);
        private static readonly Color LeafColour = new Color(0.3f, 0.8f, 0.25f);
        // The four pests, in ghost order: broca (borer beetle), roya (rust), minador (leaf miner), cochinilla (mealybug).
        private static readonly Color[] GhostColours =
        {
            new Color(0.95f, 0.25f, 0.55f),
            new Color(1f, 0.55f, 0.1f),
            new Color(0.6f, 0.95f, 0.2f),
            new Color(1f, 0.88f, 0.92f),
        };

        private readonly List<PacmanGhost> ghosts = new List<PacmanGhost>();
        private readonly List<GameObject> powerPelletObjects = new List<GameObject>();
        private GameObject[] pelletObjects;
        private bool[] pelletLeft;
        private PacmanPlayer player;
        private Vector2Int playerStart;
        private Vector2Int[] ghostStarts;
        private Vector2Int blinkyStart;
        private int pelletCount;
        private int ghostChain;
        private float stateTimer;
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
        private AudioSource audioSource;
        private PacmanSfx sfx;
        private bool soundOn = true;
        private int menuIndex;
        private float wakaTimer;
        private bool wakaFlip;
        private GameObject fruitObject;
        private Transform fruitRow;
        private bool fruitActive;
        private int fruitsSpawned;
        private float fruitTimer;
        private FruitKind fruitKind;
        private bool extraLifeGiven;
        private readonly PacmanGhostRelease release = new PacmanGhostRelease();
        private int pelletTotal;

        private static readonly string[] TitleItems = { "START", "SOUND", "QUIT" };
        private static readonly string[] PauseItems = { "RESUME", "SOUND", "QUIT TO TITLE" };

        /// <summary>Sound toggle probe for autotest scenarios.</summary>
        public bool SoundOn => soundOn;
        /// <summary>Highlighted menu row. Probe for menu tests.</summary>
        public int MenuIndex => menuIndex;

        /// <summary>Menu rows for the screen being shown (title or pause).</summary>
        public string[] ActiveMenu => State == PacmanState.Title ? TitleItems : PauseItems;
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
            BuildMaze();
            BuildActors();
            SetupCamera();
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            sfx = new PacmanSfx();

            PacmanAudio music = new GameObject("Music").AddComponent<PacmanAudio>();
            music.transform.SetParent(transform, false);
            music.Init(this, new PacmanMusic());
            gameObject.AddComponent<PacmanUi>().Init(this);
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
        }

        /// <summary>Restart from scratch: score, lives and every pellet.</summary>
        public void NewGame()
        {
            Score = 0;
            NewHighScore = false;
            Lives = startingLives;
            extraLifeGiven = false;
            level = 1;
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
            ReadInput();

            switch (State)
            {
                case PacmanState.Ready:
                    stateTimer -= Time.deltaTime;
                    if (stateTimer <= 0f)
                    {
                        State = PacmanState.Playing;
                    }
                    break;

                case PacmanState.Playing:
                    Simulate(Time.deltaTime);
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
                            SaveHighScore();
                            PacmanSfx.Play(audioSource, sfx?.GameOver, soundOn);
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
                    break;

                case PacmanState.LevelClear:
                    stateTimer -= Time.deltaTime;
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
        }

        private void ReadInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            ApplyMenuInput(new MenuInput
            {
                Up = keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame,
                Down = keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame,
                Confirm = keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame,
                Back = keyboard.escapeKey.wasPressedThisFrame,
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
                        PacmanSfx.Play(audioSource, sfx?.Back, soundOn);
                    }
                    return;

                case PacmanState.Paused:
                    if (input.Back)
                    {
                        State = PacmanState.Playing;
                        PacmanSfx.Play(audioSource, sfx?.Back, soundOn);
                        return;
                    }
                    break;

                case PacmanState.Title:
                    break;

                case PacmanState.GameOver:
                    if (input.Confirm)
                    {
                        ShowTitle();
                    }
                    return;

                default:
                    return;
            }

            string[] items = State == PacmanState.Title ? TitleItems : PauseItems;
            if (input.Down)
            {
                menuIndex = (menuIndex + 1) % items.Length;
                PacmanSfx.Play(audioSource, sfx?.Blip, soundOn);
            }
            else if (input.Up)
            {
                menuIndex = (menuIndex + items.Length - 1) % items.Length;
                PacmanSfx.Play(audioSource, sfx?.Blip, soundOn);
            }
            else if (input.Confirm)
            {
                Activate(items[menuIndex]);
            }
        }

        private void Activate(string item)
        {
            switch (item)
            {
                case "START":
                    NewGame();
                    break;
                case "RESUME":
                    State = PacmanState.Playing;
                    break;
                case "SOUND":
                    soundOn = !soundOn;
                    break;
                case "QUIT":
                    Application.Quit();
                    break;
                case "QUIT TO TITLE":
                    ShowTitle();
                    break;
            }
            bool leaving = item == "RESUME" || item == "QUIT TO TITLE" || item == "QUIT";
            PacmanSfx.Play(audioSource, leaving ? sfx?.Back : sfx?.Select, soundOn);
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
                wakaTimer -= dt;
                if (wakaTimer <= 0f)
                {
                    wakaTimer = wakaInterval;
                    wakaFlip = !wakaFlip;
                    PacmanSfx.Play(audioSource, wakaFlip ? sfx?.WakaA : sfx?.WakaB, soundOn);
                }
            }
            else
            {
                wakaTimer = 0f;
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
            for (int i = 0; i < ghosts.Count; i++)
            {
                PacmanGhost ghost = ghosts[i];
                if (!release.IsReleased(i))
                {
                    continue;
                }

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
                    PacmanSfx.Play(audioSource, sfx?.GhostEatenFor(ghostChain), soundOn);
                    ghostChain++;
                    Score += points;
                    ghost.Eat();
                    Color ghostColour = GhostColours[(int)ghost.Kind];
                    PacmanVfx.Pop(ghost.transform.position, ghostColour, 30);
                    PacmanUi.Show(ghost.transform.position, points.ToString(), new Color(0.55f, 0.85f, 1f));
                    post?.Punch(0.7f);
                    post?.Shake(0.12f, 0.18f);
                }
                else if (ghost.Mode != GhostMode.Eaten)
                {
                    State = PacmanState.Dying;
                    stateTimer = deathDuration;
                    player.StartDeath(deathDuration);
                    PacmanSfx.Play(audioSource, sfx?.Death, soundOn);
                    post?.Shake(0.3f, 0.35f);
                    post?.Punch(0.4f);
                    return;
                }
            }
        }

        private void EatPelletAt(int index)
        {
            pelletLeft[index] = false;
            pelletCount--;
            pelletObjects[index].SetActive(false);

            player.Gobble();
            PacmanVfx.Burst(pelletObjects[index].transform.position, IsPowerPellet(index) ? new Color(1f, 0.35f, 0.3f) : new Color(1f, 0.5f, 0.35f), IsPowerPellet(index) ? 16 : 6);
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
                PacmanSfx.Play(audioSource, sfx?.Power, soundOn);
                post?.Punch(0.55f);
            }
            else
            {
                pacPauseTimer = dotPauseFrames / 60f;
                Score += pelletPoints;
                PacmanSfx.Play(audioSource, sfx?.Pellet, soundOn);
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
                PacmanSfx.Play(audioSource, sfx?.LevelClear, soundOn);
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
            Transform art = PacmanFruitArt.Build(fruitObject.transform, fruitKind, 1.1f);
            PacmanJuice.Attach(art, PacmanJuice.Kind.Bob, 0.04f, 1.2f, 0f);
            fruitObject.transform.position = FruitPosition;
            fruitObject.SetActive(true);
            fruitActive = true;
            fruitTimer = fruitDuration;
            PacmanSfx.Play(audioSource, sfx?.CherryAppear, soundOn);
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
            PacmanSfx.Play(audioSource, sfx?.Cherry, soundOn);
            HideFruit();
        }

        private void GrantExtraLife()
        {
            extraLifeGiven = true;
            Lives++;
            PacmanSfx.Play(audioSource, sfx?.Cherry, soundOn);
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
            stateTimer = readyDuration;
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
            renderer.sprite = PacmanSprites.Disc;
            bool unripe = !power && (tile.x * 7 + tile.y * 13) % 5 == 0;
            renderer.color = power ? PowerPelletColour : unripe ? PelletUnripeColour : PelletColour;
            renderer.sortingOrder = 1;
            if (power)
            {
                // The big ripe cherry: a leaf on top and a bright highlight, so it reads as the special one.
                AddPelletPart(pellet.transform, LeafColour, new Vector2(0.18f, 0.5f), new Vector3(0.55f, 0.28f, 1f), 25f, 2);
                AddPelletPart(pellet.transform, new Color(1f, 0.8f, 0.75f), new Vector2(-0.2f, 0.18f), new Vector3(0.22f, 0.22f, 1f), 0f, 2);
            }
            return pellet;
        }

        private static void AddPelletPart(Transform parent, Color colour, Vector2 position, Vector3 scale, float angle, int order)
        {
            GameObject part = new GameObject("Part");
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            part.transform.localScale = scale;
            SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = PacmanSprites.Disc;
            renderer.color = colour;
            renderer.sortingOrder = order;
        }

        /// <summary>The wooden bar across the pest house gate. Cosmetic: the maze grid decides who may pass.</summary>
        private static void CreateGate(Transform parent, Vector2Int tile)
        {
            GameObject bar = new GameObject("Gate");
            bar.transform.SetParent(parent, false);
            bar.transform.position = PacmanMaze.ToWorld(tile);
            bar.transform.localScale = new Vector3(1f, 0.16f, 1f);
            SpriteRenderer renderer = bar.AddComponent<SpriteRenderer>();
            renderer.sprite = PacmanSprites.Square;
            renderer.color = new Color(0.72f, 0.5f, 0.25f);
            renderer.sortingOrder = 2;
        }

        private void BuildActors()
        {
            GameObject playerObject = new GameObject("Pacman");
            playerObject.transform.SetParent(transform, false);
            player = playerObject.AddComponent<PacmanPlayer>();
            player.Init(this);

            // Scatter targets sit in the dead space above and below the maze, as in the original
            // (Blinky top-right, Pinky top-left, Inky bottom-right, Clyde bottom-left).
            Vector2Int[] corners =
            {
                new Vector2Int(PacmanMaze.Width + 2, -3),
                new Vector2Int(-3, -3),
                new Vector2Int(PacmanMaze.Width + 2, PacmanMaze.Height + 2),
                new Vector2Int(-3, PacmanMaze.Height + 2),
            };
            GhostKind[] kinds = { GhostKind.Blinky, GhostKind.Pinky, GhostKind.Inky, GhostKind.Clyde };
            for (int i = 0; i < kinds.Length; i++)
            {
                GameObject ghostObject = new GameObject("Ghost_" + kinds[i]);
                ghostObject.transform.SetParent(transform, false);
                Transform body = PacmanGhostView.Create(ghostObject.transform, GhostColours[i], kinds[i]);
                PacmanGhost ghost = ghostObject.AddComponent<PacmanGhost>();
                ghost.Init(this, kinds[i], corners[i]);
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
            // Fit the whole board (31 rows) plus a margin for the HUD.
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
