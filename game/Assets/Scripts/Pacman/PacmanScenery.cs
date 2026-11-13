using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// The set dressing around the maze, all 2D sprites: glowing floor grid, drifting stars, tunnel portals and
    /// the neon wall outline, which changes colour with the level and turns violet while the ghosts are
    /// frightened. Purely visual: it reads game state and never writes it.
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
        private static readonly Color PortalColour = new Color(0.9f, 0.3f, 1f);

        [Tooltip("Seconds the outline takes to change colour.")]
        [SerializeField] private float colourBlend = 0.4f;
        [Tooltip("Breathing of the outline glow, as a fraction of its brightness.")]
        [SerializeField] private float breathe = 0.1f;

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

        /// <summary>Picks the neon theme for a level (1-based).</summary>
        public void SetLevel(int level)
        {
            themeColour = Themes[(Mathf.Max(1, level) - 1) % Themes.Length];
        }

        /// <summary>Makes the outline blink white, for the end-of-level celebration.</summary>
        public void SetFlash(bool on)
        {
            flash = on;
        }

        /// <summary>Turns the violet frightened tint on or off.</summary>
        public void SetAlert(bool on)
        {
            alert = on;
        }

        private void Construct()
        {
            float centreX = PacmanMaze.Width * 0.5f - 0.5f;
            float centreY = -(PacmanMaze.Height * 0.5f - 0.5f);
            Vector3 centre = new Vector3(centreX, centreY, 0f);

            // Floor: the grid tile repeated across the board plus one tile of margin.
            GameObject floor = new GameObject("Floor");
            floor.transform.SetParent(transform, false);
            floor.transform.position = centre;
            SpriteRenderer floorRenderer = floor.AddComponent<SpriteRenderer>();
            floorRenderer.sprite = PacmanSprites.GridTile;
            floorRenderer.drawMode = SpriteDrawMode.Tiled;
            floorRenderer.size = new Vector2(PacmanMaze.Width + 2f, PacmanMaze.Height + 2f);
            floorRenderer.sortingOrder = -10;

            PacmanMazeArt.Get(out Sprite lineSprite, out Sprite fillSprite);

            GameObject fill = new GameObject("WallFill");
            fill.transform.SetParent(transform, false);
            fill.transform.position = centre;
            SpriteRenderer fillRenderer = fill.AddComponent<SpriteRenderer>();
            fillRenderer.sprite = fillSprite;
            fillRenderer.color = new Color(0.015f, 0.022f, 0.07f);
            fillRenderer.sortingOrder = -5;

            GameObject line = new GameObject("WallOutline");
            line.transform.SetParent(transform, false);
            line.transform.position = centre;
            outline = line.AddComponent<SpriteRenderer>();
            outline.sprite = lineSprite;
            outline.color = themeColour;
            outline.sortingOrder = -4;

            BuildPortals();
            BuildStars(centre);
        }

        /// <summary>A glowing bar on each side of the tunnel row, where actors leave and re-enter the board.</summary>
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

        private void BuildStars(Vector3 centre)
        {
            GameObject host = new GameObject("Stars");
            host.transform.SetParent(transform, false);
            // Behind the board plane, so the sprites always draw over the particles.
            host.transform.position = centre + new Vector3(0f, 0f, 1f);

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
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.35f, 0.55f, 1f), new Color(1f, 0.5f, 0.8f));

            var emission = system.emission;
            emission.rateOverTime = 14f;

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
                Color c = currentColour * pulse;
                c.a = 1f;
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
