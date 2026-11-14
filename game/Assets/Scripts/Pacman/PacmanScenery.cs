using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// The set dressing around the maze, all 2D sprites: the earth path floor, drifting fireflies and pollen,
    /// tunnel posts and the mountain rim light, which changes with the season (level) and turns copper-sulphate
    /// blue while the pests are frightened. Purely visual: it reads game state and never writes it.
    /// </summary>
    public class PacmanScenery : MonoBehaviour
    {
        // One rim-light colour per level, cycling after the last: spring green, harvest gold, morning mist,
        // sunset, dusk.
        private static readonly Color[] Themes =
        {
            new Color(0.85f, 0.72f, 0.4f),
            new Color(1f, 0.8f, 0.25f),
            new Color(0.6f, 0.82f, 0.72f),
            new Color(0.95f, 0.55f, 0.28f),
            new Color(0.78f, 0.62f, 0.9f),
        };

        private static readonly Color AlertColour = new Color(0.25f, 0.55f, 1f);
        private static readonly Color PortalColour = new Color(1f, 0.7f, 0.2f);

        [Tooltip("Seconds the outline takes to change colour.")]
        [SerializeField] private float colourBlend = 0.4f;
        [Tooltip("Breathing of the outline glow, as a fraction of its brightness.")]
        [SerializeField] private float breathe = 0.1f;

        // Sky: each level has its own time of day (dawn, day, dusk, night), blended smoothly when the level changes.
        private struct Sky
        {
            public Color Top;
            public Color Horizon;
            public Color Tint;
        }

        private static readonly Sky[] Skies =
        {
            new Sky { Top = new Color(0.16f, 0.2f, 0.36f), Horizon = new Color(0.9f, 0.5f, 0.38f), Tint = new Color(1.5f, 1.15f, 1.05f) },   // dawn
            new Sky { Top = new Color(0.16f, 0.4f, 0.66f), Horizon = new Color(0.68f, 0.84f, 0.9f), Tint = new Color(1.9f, 1.9f, 1.8f) },    // day
            new Sky { Top = new Color(0.22f, 0.1f, 0.28f), Horizon = new Color(0.95f, 0.42f, 0.18f), Tint = new Color(1.5f, 1f, 0.85f) },     // dusk
            new Sky { Top = new Color(0.03f, 0.07f, 0.04f), Horizon = new Color(0.05f, 0.16f, 0.13f), Tint = new Color(1f, 1f, 1f) },         // night
        };

        private PacmanRays rays;
        private SpriteRenderer horizon;
        private SpriteRenderer[] mountains;
        private Color[] mountainBase;
        private Camera skyCamera;
        private int skyIndex;
        private Sky skyNow = Skies[0];

        private SpriteRenderer outline;
        private SpriteRenderer[] portals;
        private Color themeColour = Themes[0];
        private Color currentColour = Themes[0];
        private bool alert;
        private bool flash;

        /// <summary>Builds the scenery under a parent, from the wall sprites made by <see cref="PacmanMazeArt"/>.</summary>
        public static PacmanScenery Build(Transform parent)
        {
            GameObject root = new GameObject("Scenery");
            root.transform.SetParent(parent, false);
            PacmanScenery scenery = root.AddComponent<PacmanScenery>();
            scenery.Construct();
            return scenery;
        }

        /// <summary>Picks the rim-light colour for a level (1-based).</summary>
        public void SetLevel(int level)
        {
            themeColour = Themes[(Mathf.Max(1, level) - 1) % Themes.Length];
            skyIndex = (Mathf.Max(1, level) - 1) % Skies.Length;
        }

        /// <summary>Makes the rim blink white, for the end-of-level celebration.</summary>
        public void SetFlash(bool on)
        {
            flash = on;
        }

        /// <summary>Turns the blue frightened tint on or off.</summary>
        public void SetAlert(bool on)
        {
            alert = on;
        }

        private void Construct()
        {
            float centreX = PacmanMaze.Width * 0.5f - 0.5f;
            float centreY = -(PacmanMaze.Height * 0.5f - 0.5f);
            Vector3 centre = new Vector3(centreX, centreY, 0f);

            // Floor: the earth tile repeated across the board plus one tile of margin.
            GameObject floor = new GameObject("Floor");
            floor.transform.SetParent(transform, false);
            floor.transform.position = centre;
            SpriteRenderer floorRenderer = floor.AddComponent<SpriteRenderer>();
            floorRenderer.sprite = PacmanSprites.GridTile;
            floorRenderer.drawMode = SpriteDrawMode.Tiled;
            floorRenderer.size = new Vector2(PacmanMaze.Width + 2f, PacmanMaze.Height + 2f);
            floorRenderer.sortingOrder = -10;

            PacmanPath.Build(transform);
            PacmanPatio.Build(transform);

            PacmanMazeArt.Get(out Sprite lineSprite, out Sprite fillSprite);

            GameObject fill = new GameObject("WallFill");
            fill.transform.SetParent(transform, false);
            fill.transform.position = centre;
            SpriteRenderer fillRenderer = fill.AddComponent<SpriteRenderer>();
            fillRenderer.sprite = fillSprite;
            fillRenderer.color = Color.white; // the hillside colours are baked into the sprite
            fillRenderer.sortingOrder = -5;

            GameObject line = new GameObject("WallOutline");
            line.transform.SetParent(transform, false);
            line.transform.position = centre;
            outline = line.AddComponent<SpriteRenderer>();
            outline.sprite = lineSprite;
            outline.color = themeColour;
            outline.sortingOrder = -4;
            // The glowing line around the walls made the bushes hard to read, so it is not drawn.
            outline.enabled = false;

            PacmanFence.Build(transform);
            PacmanBunting.Build(transform);
            PacmanBushes.Build(transform);
            rays = PacmanRays.Build(transform);
            PacmanMist.Build(transform);
            PacmanLeaves.Build(transform);
            PacmanButterflies.Build(transform);
            PacmanDew.Build(transform);
            BuildMountains();
            BuildPortals();
            BuildStars(centre);
            BuildHorizon(centre);
            skyNow = Skies[0];
        }

        /// <summary>Two ranges of mountains rising behind the board on either side, so the farm sits in the hills.</summary>
        private void BuildMountains()
        {
            Color far = new Color(0.08f, 0.2f, 0.18f);
            Color near = new Color(0.1f, 0.27f, 0.12f);
            float floor = -(PacmanMaze.Height + 2.5f);
            float right = PacmanMaze.Width - 1f;
            // x centre, width, height, colour, sorting order. Far ranges first so the near ones overlap them.
            object[][] peaks =
            {
                new object[] { -9f, 16f, 15f, far, -30 },
                new object[] { right + 9f, 16f, 15f, far, -30 },
                new object[] { -15f, 14f, 10f, far, -29 },
                new object[] { right + 15f, 14f, 10f, far, -29 },
                new object[] { -6f, 11f, 8f, near, -28 },
                new object[] { right + 6f, 11f, 8f, near, -28 },
                new object[] { -13f, 10f, 6f, near, -27 },
                new object[] { right + 13f, 10f, 6f, near, -27 },
            };
            mountains = new SpriteRenderer[peaks.Length];
            mountainBase = new Color[peaks.Length];
            int mountainIndex = 0;
            foreach (object[] peak in peaks)
            {
                GameObject go = new GameObject("Mountain");
                go.transform.SetParent(transform, false);
                float height = (float)peak[2];
                go.transform.position = new Vector3((float)peak[0], floor + height * 0.5f, 0f);
                go.transform.localScale = new Vector3((float)peak[1], height, 1f);
                SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = PacmanSprites.Mountain;
                renderer.color = (Color)peak[3];
                renderer.sortingOrder = (int)peak[4];
                mountains[mountainIndex] = renderer;
                mountainBase[mountainIndex] = (Color)peak[3];
                mountainIndex++;
            }
        }

        /// <summary>A lantern post on each side of the tunnel row, where actors leave and re-enter the board.</summary>
        private void BuildPortals()
        {
            portals = new SpriteRenderer[2];
            float y = -PacmanMaze.TunnelRow;
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? -0.62f : PacmanMaze.Width - 1 + 0.62f;
                GameObject post = new GameObject("Portal");
                post.transform.SetParent(transform, false);
                post.transform.position = new Vector3(x, y, 0f);
                post.transform.localScale = new Vector3(0.08f, 1f, 1f);
                SpriteRenderer renderer = post.AddComponent<SpriteRenderer>();
                renderer.sprite = PacmanSprites.Square;
                renderer.color = PortalColour;
                renderer.sortingOrder = 2;
                portals[side] = renderer;
            }
        }

        /// <summary>A soft band of horizon light behind the mountains, tinted by the time of day.</summary>
        private void BuildHorizon(Vector3 centre)
        {
            const int h = 64;
            Texture2D texture = new Texture2D(2, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "PacmanHorizon",
                hideFlags = HideFlags.DontSave,
            };
            Color[] pixels = new Color[2 * h];
            for (int y = 0; y < h; y++)
            {
                float t = y / (h - 1f);
                // Brightest a third of the way up, fading smoothly both ways.
                float d = t < 0.33f ? (0.33f - t) / 0.33f : (t - 0.33f) / 0.67f;
                float a = Mathf.Pow(1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(d)), 1.6f);
                pixels[y * 2] = pixels[y * 2 + 1] = new Color(1f, 1f, 1f, a);
            }
            texture.SetPixels(pixels);
            texture.Apply(false);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 2, h), new Vector2(0.5f, 0f), 2f, 0, SpriteMeshType.FullRect);
            sprite.hideFlags = HideFlags.DontSave;
            GameObject go = new GameObject("Horizon");
            go.transform.SetParent(transform, false);
            float floor = -(PacmanMaze.Height + 2.5f);
            float height = 36f;
            go.transform.position = new Vector3(centre.x, floor - height * 0.33f, 0f);
            go.transform.localScale = new Vector3(60f, height / h * 2f, 1f);
            horizon = go.AddComponent<SpriteRenderer>();
            horizon.sprite = sprite;
            horizon.sortingOrder = -40;
        }

        private void ApplySky(float blend)
        {
            Sky target = Skies[skyIndex];
            skyNow.Top = Color.Lerp(skyNow.Top, target.Top, blend);
            skyNow.Horizon = Color.Lerp(skyNow.Horizon, target.Horizon, blend);
            skyNow.Tint = Color.Lerp(skyNow.Tint, target.Tint, blend);
            if (skyCamera == null)
            {
                skyCamera = Camera.main;
            }
            if (skyCamera != null)
            {
                skyCamera.backgroundColor = skyNow.Top;
            }
            if (skyCamera != null)
            {
                PacmanPost grading = skyCamera.GetComponent<PacmanPost>();
                if (grading != null)
                {
                    grading.SetNight(Mathf.Clamp01((1.5f - skyNow.Tint.r) / 0.5f));
                }
            }
            if (rays != null)
            {
                // Light is the horizon colour brightened toward white; strongest at dawn and dusk, faint at night.
                Color light = Color.Lerp(skyNow.Horizon, Color.white, 0.55f);
                light.a = Mathf.Clamp01(skyNow.Tint.r - 0.7f);
                rays.SetTint(light);
            }
            if (horizon != null)
            {
                Color c = skyNow.Horizon;
                c.a = 0.85f;
                horizon.color = c;
            }
            if (mountains != null)
            {
                for (int i = 0; i < mountains.Length; i++)
                {
                    Color b = mountainBase[i];
                    mountains[i].color = new Color(b.r * skyNow.Tint.r, b.g * skyNow.Tint.g, b.b * skyNow.Tint.b, 1f);
                }
            }
        }

        private void BuildStars(Vector3 centre)
        {
            GameObject host = new GameObject("Stars");
            host.transform.SetParent(transform, false);
            // Behind the board plane, so the sprites always draw over the particles (fireflies and pollen).
            host.transform.position = centre + new Vector3(0f, 0f, 1f);

            ParticleSystem system = host.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 10f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(12f, 20f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
            main.maxParticles = 220;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.prewarm = true;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 1f, 0.45f), new Color(1f, 0.75f, 0.35f));

            var emission = system.emission;
            emission.rateOverTime = 7f;

            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(60f, 40f, 0.1f);

            var size = system.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve fade = new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(0.15f, 1f), new Keyframe(0.8f, 1f), new Keyframe(1f, 0f));
            size.size = new ParticleSystem.MinMaxCurve(1f, fade);

            ParticleSystemRenderer renderer = host.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = PacmanMaterials.Particle();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.sortingOrder = -20;
            system.Play();
        }

        private void Update()
        {
            ApplySky(1f - Mathf.Exp(-Time.unscaledDeltaTime * 0.8f));
            Color target = alert ? AlertColour : themeColour;
            if (flash)
            {
                target = Mathf.FloorToInt(Time.time * 6f) % 2 == 0 ? Color.white : themeColour;
                currentColour = target;
            }
            currentColour = Color.Lerp(currentColour, target, 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, colourBlend)));
            float pulse = 1f + breathe * Mathf.Sin(Time.time * (alert ? 9f : 1.6f));
            if (outline != null)
            {
                Color c = currentColour * (pulse * 0.8f);
                c.a = 0.9f;
                outline.color = c;
            }
            if (portals != null)
            {
                float flicker = 0.8f + 0.2f * Mathf.Sin(Time.time * 5f);
                Color c = PortalColour * flicker;
                c.a = 1f;
                foreach (SpriteRenderer portal in portals)
                {
                    portal.color = c;
                }
            }
        }
    }
}
