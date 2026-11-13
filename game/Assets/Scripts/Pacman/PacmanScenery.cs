using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// The set dressing around the maze: glowing floor grid, drifting stars, tunnel portals and the neon wall
    /// trim, which changes colour with the level and turns violet while the ghosts are frightened.
    /// Purely visual: it reads game state and never writes it.
    /// </summary>
    public class PacmanScenery : MonoBehaviour
    {
        // One neon colour per level group, cycling after the last.
        private static readonly Color[] Themes =
        {
            new Color(0.15f, 0.45f, 1f),
            new Color(0.1f, 0.9f, 0.6f),
            new Color(1f, 0.25f, 0.7f),
            new Color(1f, 0.6f, 0.1f),
            new Color(0.6f, 0.3f, 1f),
        };

        private static readonly Color AlertColour = new Color(0.55f, 0.25f, 1f);

        [Tooltip("HDR multiplier on the neon trim colour. Above 1 the bloom picks it up.")]
        [SerializeField] private float trimIntensity = 2.6f;
        [Tooltip("Seconds the trim takes to change colour.")]
        [SerializeField] private float colourBlend = 0.4f;
        [Tooltip("Breathing of the trim glow, as a fraction of its brightness.")]
        [SerializeField] private float breathe = 0.12f;

        private Material trimMaterial;
        private Material floorMaterial;
        private Material portalMaterial;
        private Color themeColour = Themes[0];
        private Color currentColour = Themes[0];
        private bool alert;
        private bool flash;

        /// <summary>Builds the scenery under a parent. The wall trim mesh comes from the maze builder.</summary>
        public static PacmanScenery Build(Transform parent, Mesh trimMesh)
        {
            GameObject root = new GameObject("Scenery");
            root.transform.SetParent(parent, false);
            PacmanScenery scenery = root.AddComponent<PacmanScenery>();
            scenery.Construct(trimMesh);
            return scenery;
        }

        /// <summary>Picks the neon theme for a level (1-based).</summary>
        public void SetLevel(int level)
        {
            themeColour = Themes[(Mathf.Max(1, level) - 1) % Themes.Length];
        }

        /// <summary>Makes the trim blink white, for the end-of-level celebration.</summary>
        public void SetFlash(bool on)
        {
            flash = on;
        }

        /// <summary>Turns the violet frightened tint on or off.</summary>
        public void SetAlert(bool on)
        {
            alert = on;
        }

        private void Construct(Mesh trimMesh)
        {
            float centreX = PacmanMaze.Width * 0.5f - 0.5f;
            float centreZ = -(PacmanMaze.Height * 0.5f - 0.5f);

            // Neon trim, just above the wall tops.
            trimMaterial = PacmanMaterials.Neon(themeColour, trimIntensity);
            GameObject trim = new GameObject("WallTrim");
            trim.transform.SetParent(transform, false);
            trim.AddComponent<MeshFilter>().sharedMesh = trimMesh;
            MeshRenderer trimRenderer = trim.AddComponent<MeshRenderer>();
            trimRenderer.sharedMaterial = trimMaterial;
            trimRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Floor: a big dark plane with a faint grid, one cell per tile.
            floorMaterial = PacmanMaterials.Lit(Color.white, 0.05f);
            Texture2D grid = MakeGridTexture();
            floorMaterial.mainTexture = grid;
            floorMaterial.SetTexture("_BaseMap", grid);
            // One tile of margin around the maze, so the stars show in the void beyond the panel.
            float width = PacmanMaze.Width + 2f;
            float depth = PacmanMaze.Height + 2f;
            floorMaterial.mainTextureScale = new Vector2(width, depth);
            floorMaterial.SetTextureScale("_BaseMap", new Vector2(width, depth));
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
            floor.name = "Floor";
            floor.transform.SetParent(transform, false);
            floor.transform.position = new Vector3(centreX, -0.45f, centreZ);
            floor.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            floor.transform.localScale = new Vector3(width, depth, 1f);
            Destroy(floor.GetComponent<Collider>());
            floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;

            BuildPortals();
            BuildStars(centreX, centreZ);
        }

        private void BuildPortals()
        {
            portalMaterial = PacmanMaterials.Neon(new Color(0.9f, 0.3f, 1f), 3f);
            float z = -PacmanMaze.TunnelRow;
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? -0.62f : PacmanMaze.Width - 1 + 0.62f;
                GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                post.name = "Portal";
                post.transform.SetParent(transform, false);
                post.transform.position = new Vector3(x, 0.2f, z);
                post.transform.localScale = new Vector3(0.08f, 0.4f, 1.0f);
                Destroy(post.GetComponent<Collider>());
                post.GetComponent<Renderer>().sharedMaterial = portalMaterial;
            }
        }

        private void BuildStars(float centreX, float centreZ)
        {
            GameObject host = new GameObject("Stars");
            host.transform.SetParent(transform, false);
            host.transform.position = new Vector3(centreX, -2f, centreZ);
            host.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            ParticleSystem system = host.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 10f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(12f, 20f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.16f);
            main.maxParticles = 220;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.prewarm = true;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.35f, 0.55f, 2.2f), new Color(2f, 0.5f, 1.4f));

            var emission = system.emission;
            emission.rateOverTime = 14f;

            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(44f, 30f, 0.1f);

            var size = system.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve fade = new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(0.15f, 1f), new Keyframe(0.8f, 1f), new Keyframe(1f, 0f));
            size.size = new ParticleSystem.MinMaxCurve(1f, fade);

            ParticleSystemRenderer renderer = host.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = PacmanMaterials.Particle();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            system.Play();
        }

        private void Update()
        {
            Color target = alert ? AlertColour : themeColour;
            if (flash)
            {
                target = Mathf.FloorToInt(Time.time * 6f) % 2 == 0 ? Color.white : themeColour;
                currentColour = target;
            }
            currentColour = Color.Lerp(currentColour, target, 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, colourBlend)));
            float pulse = 1f + breathe * Mathf.Sin(Time.time * (alert ? 9f : 1.6f));
            if (trimMaterial != null)
            {
                PacmanMaterials.SetColour(trimMaterial, currentColour * 0.1f);
                PacmanMaterials.SetEmission(trimMaterial, currentColour * trimIntensity * pulse);
            }
            if (portalMaterial != null)
            {
                float flicker = 0.8f + 0.2f * Mathf.Sin(Time.time * 5f);
                PacmanMaterials.SetEmission(portalMaterial, new Color(0.9f, 0.3f, 1f) * 3f * flicker);
            }
        }

        /// <summary>Tile-sized grid cell: dark navy with a faint line on two edges and a soft glow in the middle.</summary>
        private static Texture2D MakeGridTexture()
        {
            const int n = 64;
            Texture2D texture = new Texture2D(n, n, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 4,
                name = "PacmanGrid",
            };
            Color baseColour = new Color(0.012f, 0.016f, 0.05f);
            Color line = new Color(0.05f, 0.09f, 0.22f);
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x - n * 0.5f) / (n * 0.5f);
                    float dy = (y - n * 0.5f) / (n * 0.5f);
                    float glow = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy)) * 0.025f;
                    bool onLine = x < 2 || y < 2;
                    Color c = onLine ? line : baseColour + new Color(glow * 0.4f, glow * 0.6f, glow, 0f);
                    c.a = 1f;
                    texture.SetPixel(x, y, c);
                }
            }
            texture.Apply(true);
            return texture;
        }
    }
}
