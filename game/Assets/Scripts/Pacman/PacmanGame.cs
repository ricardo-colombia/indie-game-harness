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
        [Header("Speeds (tiles per second)")]
        [SerializeField] private float pacmanSpeed = 6f;
        [SerializeField] private float ghostSpeed = 5.5f;
        [SerializeField] private float frightenedGhostSpeed = 3f;
        [SerializeField] private float eatenGhostSpeed = 10f;

        [Header("Timers (seconds)")]
        [SerializeField] private float readyDuration = 2f;
        [SerializeField] private float deathDuration = 1.5f;
        [SerializeField] private float levelClearDuration = 2.5f;
        [SerializeField] private float frightenedDuration = 6f;
        [Tooltip("Alternating scatter / chase durations. The last value holds for the rest of the level.")]
        [SerializeField] private float[] ghostModeSchedule = { 7f, 20f, 7f, 20f, 5f, 20f, 5f, 9999f };

        [Header("Rules")]
        [SerializeField] private int startingLives = 3;
        [SerializeField] private int pelletPoints = 10;
        [SerializeField] private int powerPelletPoints = 50;
        [SerializeField] private int ghostPoints = 200;
        [Tooltip("Distance (in tiles) at which a ghost touches Pac-Man.")]
        [SerializeField] private float collisionDistance = 0.6f;
        [Tooltip("Seconds of play before the first house ghost leaves. Blinky is always out.")]
        [SerializeField] private float ghostReleaseDelay = 3f;
        [Tooltip("Seconds between each further house ghost leaving.")]
        [SerializeField] private float ghostReleaseStagger = 2f;

        [Tooltip("Seconds between waka sounds while Pac-Man is moving.")]
        [SerializeField] private float wakaInterval = 0.18f;

        [Header("Models (optional: primitives are used when empty)")]
        [SerializeField] private GameObject ghostModel;
        [SerializeField] private GameObject ghostEyesModel;

        private static readonly Color WallColour = new Color(0.12f, 0.22f, 0.95f);
        private static readonly Color PelletColour = new Color(1f, 0.95f, 0.75f);
        private static readonly Color[] GhostColours =
        {
            new Color(1f, 0.1f, 0.1f),
            new Color(1f, 0.55f, 0.85f),
            new Color(0.2f, 0.95f, 0.95f),
            new Color(1f, 0.6f, 0.15f),
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
        private float playTime;

        /// <summary>Seconds of play since the last reset. Probe for ghost release timing.</summary>
        public float PlayTime => playTime;
        private float modeTimer;
        private int modeIndex;
        private GUIStyle hudStyle;
        private GUIStyle bigStyle;
        private GUIStyle menuStyle;
        private Material wallMaterial;
        private AudioSource audioSource;
        private PacmanSfx sfx;
        private bool soundOn = true;
        private int menuIndex;
        private float wakaTimer;
        private bool wakaFlip;

        private static readonly string[] TitleItems = { "START", "SOUND", "QUIT" };
        private static readonly string[] PauseItems = { "RESUME", "SOUND", "QUIT TO TITLE" };

        /// <summary>Sound toggle probe for autotest scenarios.</summary>
        public bool SoundOn => soundOn;
        /// <summary>Highlighted menu row. Probe for menu tests.</summary>
        public int MenuIndex => menuIndex;

        public int Score { get; private set; }
        public int Lives { get; private set; }
        public int PelletsRemaining => pelletCount;
        public PacmanState State { get; private set; }
        public bool FrightenedActive => frightenedTimer > 0f;
        public int GhostsEatenInChain => ghostChain;
        public Vector2Int PacmanTile => player != null ? player.Tile : Vector2Int.zero;
        public Vector2Int PacmanDir => player != null ? player.Dir : Vector2Int.zero;
        public GhostMode GlobalMode => (modeIndex % 2 == 0) ? GhostMode.Scatter : GhostMode.Chase;

        private void Awake()
        {
            BuildMaze();
            BuildActors();
            SetupCamera();
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            sfx = new PacmanSfx();
            ShowTitle();
        }

        private void ShowTitle()
        {
            State = PacmanState.Title;
            menuIndex = 0;
        }

        /// <summary>Restart from scratch: score, lives and every pellet.</summary>
        public void NewGame()
        {
            Score = 0;
            Lives = startingLives;
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
                        Lives--;
                        if (Lives <= 0)
                        {
                            State = PacmanState.GameOver;
                        }
                        else
                        {
                            ResetActors();
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
                        RestoreLevel();
                    }
                    break;
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
                    }
                    return;

                case PacmanState.Paused:
                    if (input.Back)
                    {
                        State = PacmanState.Playing;
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
            if (item != "SOUND")
            {
                PacmanSfx.Play(audioSource, sfx?.Blip, soundOn);
            }
        }

        private void Simulate(float dt)
        {
            player.Tick(pacmanSpeed * dt);

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

            modeTimer -= dt;
            if (modeTimer <= 0f)
            {
                modeIndex = Mathf.Min(modeIndex + 1, ghostModeSchedule.Length - 1);
                modeTimer = ghostModeSchedule[modeIndex];
                foreach (PacmanGhost ghost in ghosts)
                {
                    if (ghost.Mode == GhostMode.Scatter || ghost.Mode == GhostMode.Chase)
                    {
                        ghost.SetMode(GlobalMode);
                    }
                }
            }

            playTime += dt;
            for (int i = 0; i < ghosts.Count; i++)
            {
                PacmanGhost ghost = ghosts[i];
                if (!GhostReleased(i))
                {
                    continue;
                }

                float speed = ghostSpeed;
                if (ghost.Mode == GhostMode.Frightened)
                {
                    speed = frightenedGhostSpeed;
                }
                else if (ghost.Mode == GhostMode.Eaten)
                {
                    speed = eatenGhostSpeed;
                }
                ghost.Tick(speed * dt);
            }

            CheckGhostCollisions();
        }

        /// <summary>Blinky starts outside the house; the others wait in it for their release time.</summary>
        private bool GhostReleased(int index)
        {
            if (index == 0)
            {
                return true;
            }
            return playTime >= ghostReleaseDelay + (index - 1) * ghostReleaseStagger;
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

                if (ghost.Mode == GhostMode.Frightened)
                {
                    int points = ghostPoints << ghostChain;
                    ghostChain++;
                    Score += points;
                    ghost.Eat();
                    PacmanSfx.Play(audioSource, sfx?.GhostEaten, soundOn);
                }
                else if (ghost.Mode != GhostMode.Eaten)
                {
                    State = PacmanState.Dying;
                    stateTimer = deathDuration;
                    PacmanSfx.Play(audioSource, sfx?.Death, soundOn);
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
            if (IsPowerPellet(index))
            {
                Score += powerPelletPoints;
                ghostChain = 0;
                frightenedTimer = frightenedDuration;
                foreach (PacmanGhost ghost in ghosts)
                {
                    ghost.Frighten();
                }
                PacmanSfx.Play(audioSource, sfx?.Power, soundOn);
            }
            else
            {
                Score += pelletPoints;
                PacmanSfx.Play(audioSource, sfx?.Pellet, soundOn);
            }

            if (pelletCount <= 0)
            {
                State = PacmanState.LevelClear;
                stateTimer = levelClearDuration;
                PacmanSfx.Play(audioSource, sfx?.LevelClear, soundOn);
            }
        }

        private void EndFrightenedIfNeeded()
        {
            foreach (PacmanGhost ghost in ghosts)
            {
                if (ghost.Mode == GhostMode.Frightened)
                {
                    ghost.SetMode(GlobalMode);
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

            ghostChain = 0;
            frightenedTimer = 0f;
            modeIndex = 0;
            modeTimer = ghostModeSchedule[0];
            ResetActors();
            State = PacmanState.Ready;
            stateTimer = readyDuration;
        }

        private void ResetActors()
        {
            ghostChain = 0;
            frightenedTimer = 0f;
            playTime = 0f;
            player.Place(playerStart);
            player.Request(Vector2Int.zero);
            for (int i = 0; i < ghosts.Count; i++)
            {
                Vector2Int start = i == 0 ? blinkyStart : ghostStarts[i - 1];
                // Blinky starts outside and scatters at once. The others start inside the house and
                // head for the house exit first, as in the original, before joining the global mode.
                ghosts[i].ResetTo(start, i == 0 ? GlobalMode : GhostMode.Exiting);
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
            float pulse = 1f + 0.25f * Mathf.Sin(Time.time * 8f);
            foreach (GameObject pellet in powerPelletObjects)
            {
                if (pellet != null && pellet.activeSelf)
                {
                    pellet.transform.localScale = Vector3.one * 0.32f * pulse;
                }
            }
        }

        private void BuildMaze()
        {
            int width = PacmanMaze.Width;
            int height = PacmanMaze.Height;
            pelletObjects = new GameObject[width * height];
            pelletLeft = new bool[width * height];
            wallMaterial = MakeMaterial(WallColour);
            Material pelletMaterial = MakeMaterial(PelletColour);

            Transform mazeRoot = new GameObject("Maze").transform;
            mazeRoot.SetParent(transform, false);

            var walls = new List<Vector2Int>();
            var ghostSpawns = new List<Vector2Int>();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector2Int tile = new Vector2Int(x, y);
                    char c = PacmanMaze.Cell(x, y);
                    switch (c)
                    {
                        case '#':
                            walls.Add(tile);
                            break;
                        case '.':
                        case 'o':
                            int index = Index(tile);
                            pelletLeft[index] = true;
                            pelletCount++;
                            GameObject pellet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                            pellet.name = c == 'o' ? "PowerPellet" : "Pellet";
                            pellet.transform.SetParent(mazeRoot, false);
                            pellet.transform.position = PacmanMaze.ToWorld(tile);
                            float size = c == 'o' ? 0.32f : 0.12f;
                            pellet.transform.localScale = Vector3.one * size;
                            Destroy(pellet.GetComponent<Collider>());
                            pellet.GetComponent<Renderer>().sharedMaterial = pelletMaterial;
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
                    }
                }
            }

            foreach (Vector2Int tile in walls)
            {
                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "Wall";
                wall.transform.SetParent(mazeRoot, false);
                wall.transform.position = PacmanMaze.ToWorld(tile) + Vector3.up * 0.5f;
                Destroy(wall.GetComponent<Collider>());
                wall.GetComponent<Renderer>().sharedMaterial = wallMaterial;
            }

            ghostStarts = ghostSpawns.ToArray();
            blinkyStart = new Vector2Int(9, 5);
        }

        private void BuildActors()
        {
            GameObject playerObject = new GameObject("Pacman");
            playerObject.transform.SetParent(transform, false);
            player = playerObject.AddComponent<PacmanPlayer>();
            player.Init(this, MakeMaterial(new Color(1f, 0.82f, 0.05f)));

            Vector2Int[] corners = { new Vector2Int(17, 0), new Vector2Int(1, 0), new Vector2Int(18, 16), new Vector2Int(0, 16) };
            GhostKind[] kinds = { GhostKind.Blinky, GhostKind.Pinky, GhostKind.Inky, GhostKind.Clyde };
            for (int i = 0; i < kinds.Length; i++)
            {
                GameObject ghostObject = new GameObject("Ghost_" + kinds[i]);
                ghostObject.transform.SetParent(transform, false);
                Transform body;
                if (ghostModel != null)
                {
                    body = Instantiate(ghostModel, ghostObject.transform).transform;
                    foreach (Renderer r in body.GetComponentsInChildren<Renderer>())
                    {
                        r.material.SetColor("_BaseColor", GhostColours[i]);
                        r.material.SetColor("_Color", GhostColours[i]);
                    }
                    if (ghostEyesModel != null)
                    {
                        Instantiate(ghostEyesModel, body);
                    }
                }
                else
                {
                    body = CreatePrimitiveModel(ghostObject.transform, PrimitiveType.Sphere, GhostColours[i], 0.8f);
                }

                PacmanGhost ghost = ghostObject.AddComponent<PacmanGhost>();
                ghost.Init(this, kinds[i], corners[i], body);
                ghosts.Add(ghost);
            }
        }

        private static Transform CreatePrimitiveModel(Transform parent, PrimitiveType type, Color colour, float scale)
        {
            GameObject model = GameObject.CreatePrimitive(type);
            model.name = "Model";
            model.transform.SetParent(parent, false);
            model.transform.localScale = Vector3.one * scale;
            Destroy(model.GetComponent<Collider>());
            model.GetComponent<Renderer>().sharedMaterial = MakeMaterial(colour);
            return model.transform;
        }

        private void SetupCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }
            cam.orthographic = true;
            cam.orthographicSize = 11f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.transform.position = new Vector3(PacmanMaze.Width * 0.5f - 0.5f, 30f, -(PacmanMaze.Height * 0.5f - 0.5f));
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        private static Material MakeMaterial(Color colour)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new Material(shader);
            material.SetColor("_BaseColor", colour);
            material.SetColor("_Color", colour);
            return material;
        }

        private void OnGUI()
        {
            if (hudStyle == null)
            {
                hudStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
                bigStyle = new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                hudStyle.normal.textColor = Color.white;
                bigStyle.normal.textColor = Color.yellow;
            }

            if (menuStyle == null)
            {
                menuStyle = new GUIStyle(hudStyle) { fontSize = 30, alignment = TextAnchor.MiddleCenter };
            }

            if (State == PacmanState.Title)
            {
                DrawMenu("PAC-MAN", TitleItems);
                return;
            }

            GUI.Label(new Rect(16, 12, 300, 36), "SCORE " + Score, hudStyle);
            GUI.Label(new Rect(Screen.width - 220, 12, 200, 36), "LIVES " + Lives, hudStyle);

            if (State == PacmanState.Paused)
            {
                DrawMenu("PAUSED", PauseItems);
                return;
            }

            string message = State switch
            {
                PacmanState.Ready => "READY!",
                PacmanState.LevelClear => "LEVEL CLEAR",
                PacmanState.GameOver => "GAME OVER - ENTER for title",
                _ => string.Empty,
            };
            if (message.Length > 0)
            {
                GUI.Label(new Rect(0, Screen.height * 0.5f - 30, Screen.width, 60), message, bigStyle);
            }
        }

        private void DrawMenu(string header, string[] items)
        {
            GUI.Label(new Rect(0, Screen.height * 0.28f, Screen.width, 60), header, bigStyle);
            float top = Screen.height * 0.5f;
            for (int i = 0; i < items.Length; i++)
            {
                string label = items[i] == "SOUND" ? "SOUND: " + (soundOn ? "ON" : "OFF") : items[i];
                string text = i == menuIndex ? "> " + label + " <" : label;
                GUI.Label(new Rect(0, top + i * 48f, Screen.width, 44), text, menuStyle);
            }
        }
    }
}
