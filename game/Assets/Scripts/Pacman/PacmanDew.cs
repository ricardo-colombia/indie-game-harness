using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// Morning dew: small four-point glints that twinkle on the leaves of the coffee bushes, each on its own rhythm,
    /// so the plantation sparkles where the light catches the wet leaves. Drawn above the bushes and mist, below beans
    /// and characters. The glint sprite is painted in code.
    /// </summary>
    public class PacmanDew : MonoBehaviour
    {
        private const int Glints = 60;

        [Tooltip("Twinkle speed in radians per second.")]
        [SerializeField] private Vector2 speed = new Vector2(0.9f, 2.2f);
        [Tooltip("Size of a fully lit glint, in tiles.")]
        [SerializeField] private float size = 0.55f;

        private static Sprite glintSprite;

        private Transform[] glints;
        private SpriteRenderer[] renderers;
        private float[] speeds;
        private float[] phases;

        /// <summary>Scatters the glints over the bushes under a parent.</summary>
        public static PacmanDew Build(Transform parent)
        {
            GameObject root = new GameObject("Dew");
            root.transform.SetParent(parent, false);
            PacmanDew self = root.AddComponent<PacmanDew>();
            EnsureSprite();

            var walls = new System.Collections.Generic.List<Vector2Int>();
            for (int y = 0; y < PacmanMaze.Height; y++)
            {
                for (int x = 0; x < PacmanMaze.Width; x++)
                {
                    if (PacmanMaze.Cell(x, y) == '#')
                    {
                        walls.Add(new Vector2Int(x, y));
                    }
                }
            }
            self.glints = new Transform[Glints];
            self.renderers = new SpriteRenderer[Glints];
            self.speeds = new float[Glints];
            self.phases = new float[Glints];
            for (int i = 0; i < Glints; i++)
            {
                Vector2Int cell = walls[Mathf.FloorToInt(PacmanToon.Hash(i, 21) * walls.Count) % walls.Count];
                float x = cell.x + (PacmanToon.Hash(i, 23) - 0.5f) * 0.8f;
                float y = -cell.y + Mathf.Lerp(-0.2f, 0.28f, PacmanToon.Hash(i, 25));
                GameObject glint = new GameObject("Glint");
                glint.transform.SetParent(root.transform, false);
                glint.transform.localPosition = new Vector3(x, y, 0f);
                SpriteRenderer renderer = glint.AddComponent<SpriteRenderer>();
                renderer.sprite = glintSprite;
                renderer.sortingOrder = 0;
                renderer.color = new Color(1f, 0.97f, 0.85f, 0f);
                self.glints[i] = glint.transform;
                self.renderers[i] = renderer;
                self.speeds[i] = Mathf.Lerp(self.speed.x, self.speed.y, PacmanToon.Hash(i, 27));
                self.phases[i] = PacmanToon.Hash(i, 29) * 6.283f;
            }
            return self;
        }

        private void Update()
        {
            if (glints == null)
            {
                return;
            }
            for (int i = 0; i < glints.Length; i++)
            {
                // A short bright flash once per cycle: the sine is raised to a high power so most of the time it is dark.
                float s = Mathf.Max(0f, Mathf.Sin(Time.time * speeds[i] + phases[i]));
                float flash = s * s * s * s;
                Color c = renderers[i].color;
                c.a = flash * 0.95f;
                renderers[i].color = c;
                glints[i].localScale = Vector3.one * (size * (0.4f + 0.6f * flash));
                glints[i].localRotation = Quaternion.Euler(0f, 0f, 45f * flash);
            }
        }

        private static void EnsureSprite()
        {
            if (glintSprite != null)
            {
                return;
            }
            const int n = 32;
            Texture2D texture = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "PacmanGlint",
                hideFlags = HideFlags.DontSave,
            };
            Color[] pixels = new Color[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = Mathf.Abs((x + 0.5f) / n * 2f - 1f);
                    float v = Mathf.Abs((y + 0.5f) / n * 2f - 1f);
                    // Four thin spikes along the axes plus a round core: min-product star.
                    float spike = Mathf.Clamp01(1f - Mathf.Pow(u * v * 28f, 0.5f)) * Mathf.Clamp01(1f - Mathf.Max(u, v));
                    float core = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v) * 3.2f);
                    float a = Mathf.Clamp01(spike * 1.1f + core);
                    pixels[y * n + x] = new Color(1f, 1f, 1f, a);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false);
            glintSprite = Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n, 0, SpriteMeshType.FullRect);
            glintSprite.hideFlags = HideFlags.DontSave;
        }
    }
}
