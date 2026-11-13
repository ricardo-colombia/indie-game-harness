using UnityEngine;
using UnityEngine.Rendering;

namespace Studio.Pacman
{
    /// <summary>
    /// The walls of the maze as a plantation: one coffee bush in side view on every wall cell, like a hillside
    /// finca seen from the front. Bushes in lower rows overlap those behind them, and each sways a little in
    /// the wind. The bush art is painted in code (trunk, tiers of branches, glossy paired leaves, hanging cherries).
    /// </summary>
    public class PacmanBushes : MonoBehaviour
    {
        private const int Variants = 6;
        private const int W = 96;
        private const int H = 84;
        private const float PixelsPerUnit = 86f;

        private static Sprite[] sprites;

        [Tooltip("Sway of each bush in degrees.")]
        [SerializeField] private float swayDegrees = 1.6f;
        [Tooltip("Sway speed in radians per second.")]
        [SerializeField] private float swaySpeed = 1.3f;

        private Transform[] bushes;
        private float[] phases;

        /// <summary>Creates a bush on every wall cell under a parent.</summary>
        public static PacmanBushes Build(Transform parent)
        {
            GameObject root = new GameObject("Bushes");
            root.transform.SetParent(parent, false);
            SortingGroup group = root.AddComponent<SortingGroup>();
            group.sortingOrder = -3;
            PacmanBushes self = root.AddComponent<PacmanBushes>();
            EnsureSprites();

            var list = new System.Collections.Generic.List<Transform>();
            var phaseList = new System.Collections.Generic.List<float>();
            for (int y = 0; y < PacmanMaze.Height; y++)
            {
                for (int x = 0; x < PacmanMaze.Width; x++)
                {
                    if (PacmanMaze.Cell(x, y) != '#')
                    {
                        continue;
                    }
                    int hash = (x * 73856093) ^ (y * 19349663);
                    GameObject bush = new GameObject("Bush");
                    bush.transform.SetParent(root.transform, false);
                    float scale = 0.95f + ((hash >> 3) & 7) * 0.02f;
                    bush.transform.localPosition = new Vector3(x, -y - 0.53f, 0f);
                    bush.transform.localScale = new Vector3(((hash >> 6) & 1) == 0 ? scale : -scale, scale, 1f);
                    SpriteRenderer renderer = bush.AddComponent<SpriteRenderer>();
                    renderer.sprite = sprites[(hash & 0xff) % Variants];
                    renderer.sortingOrder = y;
                    list.Add(bush.transform);
                    phaseList.Add(((hash >> 9) & 255) / 255f * 6.283f);
                }
            }
            self.bushes = list.ToArray();
            self.phases = phaseList.ToArray();
            return self;
        }

        private void Update()
        {
            if (bushes == null)
            {
                return;
            }
            float t = Time.time * swaySpeed;
            for (int i = 0; i < bushes.Length; i++)
            {
                // A slow gust that travels along the rows, plus each bush's own flutter.
                float angle = (Mathf.Sin(t + phases[i]) * 0.7f + Mathf.Sin(t * 2.3f + bushes[i].localPosition.x * 0.5f) * 0.3f) * swayDegrees;
                bushes[i].localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        // ---------------------------------------------------------------- painting

        private static void EnsureSprites()
        {
            if (sprites != null && sprites[0] != null)
            {
                return;
            }
            sprites = new Sprite[Variants];
            for (int i = 0; i < Variants; i++)
            {
                sprites[i] = Paint(i);
            }
        }

        private static readonly Color LeafDark = new Color(0.03f, 0.17f, 0.07f);
        private static readonly Color LeafMid = new Color(0.07f, 0.36f, 0.12f);
        private static readonly Color LeafLight = new Color(0.22f, 0.6f, 0.2f);
        private static readonly Color Wood = new Color(0.3f, 0.18f, 0.1f);

        private static Sprite Paint(int seed)
        {
            Canvas c = new Canvas(W, H);
            System.Random rng = new System.Random(seed * 7919 + 13);
            float R(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }

            float mid = W * 0.5f;

            // Soil mound and shadow under the plant.
            c.Ellipse(mid, 8f, 38f, 7f, new Color(0.05f, 0.03f, 0.02f), 0.55f);
            c.Ellipse(mid, 10f, 30f, 6f, new Color(0.28f, 0.17f, 0.09f), 1f);

            // A coffee shrub is low and round, as wide as it is tall: a dark dome of foliage first, so no gaps show.
            c.Ellipse(mid, 24f, 44f, 17f, LeafDark, 1f);
            c.Ellipse(mid, 38f, 42f, 24f, LeafDark, 1f);
            c.Ellipse(mid, 52f, 34f, 22f, LeafDark, 1f);
            c.Ellipse(mid, 63f, 22f, 14f, LeafDark, 1f);

            // Short woody stem at the foot, with a couple of low branches.
            c.Line(new Vector2(mid, 8f), new Vector2(mid + R(-1f, 1f), 24f), 4.2f, Wood);

            // Broad, rounded, glossy leaves covering the dome, painted from the heart outwards so the outer
            // ones overlap. Rounded (not needle-like) leaves are what makes it read as a broadleaf shrub.
            Vector2 heart = new Vector2(mid, 28f);
            for (int i = 0; i < 70; i++)
            {
                float ring = Mathf.Lerp(0.15f, 1f, i / 69f);
                float theta = R(8f, 172f) * Mathf.Deg2Rad;
                Vector2 pos = heart + new Vector2(Mathf.Cos(theta) * 40f * ring, Mathf.Sin(theta) * 38f * ring);
                Vector2 outward = pos - heart;
                float angle = Mathf.Atan2(outward.y, outward.x) * Mathf.Rad2Deg + R(-28f, 28f);
                float height = Mathf.Clamp01((pos.y - 14f) / 56f);
                Color col = Color.Lerp(LeafMid, LeafLight, Mathf.Clamp01(height * 0.9f + R(-0.2f, 0.2f)));
                c.Leaf(pos, angle, R(15f, 21f), R(9.5f, 12f), col, 0.55f);
            }

            // Cherries grow in tight clusters tucked under the leaves, not scattered over them.
            int clusters = 2 + rng.Next(0, 3);
            for (int cl = 0; cl < clusters; cl++)
            {
                float theta = R(25f, 155f) * Mathf.Deg2Rad;
                Vector2 pos = heart + new Vector2(Mathf.Cos(theta) * 30f, Mathf.Sin(theta) * 22f - 6f);
                for (int j = 0; j < 4; j++)
                {
                    Vector2 cp = pos + new Vector2(R(-4f, 4f), R(-4f, 4f));
                    float ripe = R(0f, 1f);
                    Color cherry = ripe < 0.7f ? new Color(0.88f, 0.06f, 0.1f) : ripe < 0.88f ? new Color(0.95f, 0.42f, 0.08f) : new Color(0.65f, 0.75f, 0.18f);
                    c.Cherry(cp, 2.9f, cherry);
                }
            }

            // In flower (a third of the bushes): small white blossom clusters along the branches, like the reference photos.
            if (seed % 3 == 0)
            {
                for (int f = 0; f < 5; f++)
                {
                    float theta = R(20f, 160f) * Mathf.Deg2Rad;
                    Vector2 pos = heart + new Vector2(Mathf.Cos(theta) * 34f, Mathf.Sin(theta) * 30f);
                    for (int j = 0; j < 3; j++)
                    {
                        c.Cherry(pos + new Vector2(R(-3f, 3f), R(-3f, 3f)), 2.2f, new Color(0.97f, 0.97f, 0.92f));
                    }
                }
            }

            // Grass tufts around the foot.
            for (int i = 0; i < 9; i++)
            {
                float gx = mid + R(-34f, 34f);
                c.Line(new Vector2(gx, 9f), new Vector2(gx + R(-3f, 3f), R(15f, 20f)), 1.1f, new Color(0.32f, 0.6f, 0.2f));
            }
            return c.ToSprite("CoffeeBush" + seed, PixelsPerUnit);
        }

        /// <summary>A tiny software painter: antialiased ellipses, lines, glossy leaves and cherries composited over a transparent canvas.</summary>
        private sealed class Canvas
        {
            private readonly int w;
            private readonly int h;
            private readonly float[] pr;
            private readonly float[] pg;
            private readonly float[] pb;
            private readonly float[] pa;

            public Canvas(int width, int height)
            {
                w = width;
                h = height;
                pr = new float[w * h];
                pg = new float[w * h];
                pb = new float[w * h];
                pa = new float[w * h];
            }

            private void Blend(int x, int y, Color colour, float coverage)
            {
                if (x < 0 || y < 0 || x >= w || y >= h || coverage <= 0f)
                {
                    return;
                }
                int i = y * w + x;
                float keep = 1f - coverage;
                pr[i] = colour.r * coverage + pr[i] * keep;
                pg[i] = colour.g * coverage + pg[i] * keep;
                pb[i] = colour.b * coverage + pb[i] * keep;
                pa[i] = coverage + pa[i] * keep;
            }

            public void Ellipse(float cx, float cy, float rx, float ry, Color colour, float alpha)
            {
                for (int y = Mathf.Max(0, (int)(cy - ry - 1)); y <= Mathf.Min(h - 1, (int)(cy + ry + 1)); y++)
                {
                    for (int x = Mathf.Max(0, (int)(cx - rx - 1)); x <= Mathf.Min(w - 1, (int)(cx + rx + 1)); x++)
                    {
                        float dx = (x + 0.5f - cx) / rx;
                        float dy = (y + 0.5f - cy) / ry;
                        float f = 1f - Mathf.Sqrt(dx * dx + dy * dy);
                        Blend(x, y, colour, Mathf.Clamp01(f * Mathf.Min(rx, ry) + 0.5f) * alpha);
                    }
                }
            }

            public void Line(Vector2 a, Vector2 b, float thickness, Color colour)
            {
                int x0 = Mathf.Max(0, (int)(Mathf.Min(a.x, b.x) - thickness - 1));
                int x1 = Mathf.Min(w - 1, (int)(Mathf.Max(a.x, b.x) + thickness + 1));
                int y0 = Mathf.Max(0, (int)(Mathf.Min(a.y, b.y) - thickness - 1));
                int y1 = Mathf.Min(h - 1, (int)(Mathf.Max(a.y, b.y) + thickness + 1));
                Vector2 ab = b - a;
                float lenSq = Mathf.Max(0.0001f, ab.sqrMagnitude);
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
                        float d = (p - (a + ab * t)).magnitude;
                        Blend(x, y, colour, Mathf.Clamp01(thickness * 0.5f - d + 0.5f));
                    }
                }
            }

            /// <summary>A pointed, glossy leaf rooted at a point and pointing along an angle (degrees, 0 = right, 90 = up).</summary>
            public void Leaf(Vector2 root, float angleDegrees, float length, float width, Color baseColour, float roundness = 1f)
            {
                float rad = angleDegrees * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                Vector2 perp = new Vector2(-dir.y, dir.x);
                Vector2 centre = root + dir * (length * 0.5f);
                float a = length * 0.5f;
                float b = width * 0.5f;
                int reach = Mathf.CeilToInt(length) + 2;
                for (int y = Mathf.Max(0, (int)root.y - reach); y <= Mathf.Min(h - 1, (int)root.y + reach); y++)
                {
                    for (int x = Mathf.Max(0, (int)root.x - reach); x <= Mathf.Min(w - 1, (int)root.x + reach); x++)
                    {
                        Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - centre;
                        float u = Vector2.Dot(p, dir);
                        float v = Vector2.Dot(p, perp);
                        float k = u / a;
                        if (k < -1f || k > 1f)
                        {
                            continue;
                        }
                        float limit = b * Mathf.Pow(1f - k * k, roundness);
                        float cov = Mathf.Clamp01(limit - Mathf.Abs(v) + 0.5f);
                        if (cov <= 0f)
                        {
                            continue;
                        }
                        // Shading: the upper side of the leaf catches the light, the rim is darker, a pale midrib runs along it.
                        float up = Mathf.Clamp01(0.5f + (y + 0.5f - centre.y) / (b * 2.2f));
                        Color col = Color.Lerp(baseColour * 0.75f, baseColour * 1.3f, up);
                        float rim = Mathf.Clamp01((Mathf.Abs(v) - (limit - 1f)) * 0.6f);
                        col = Color.Lerp(col, LeafDark, rim * 0.5f);
                        if (Mathf.Abs(v) < 0.55f)
                        {
                            col = Color.Lerp(col, new Color(0.55f, 0.78f, 0.35f), 0.45f * (1f - Mathf.Abs(k)));
                        }
                        col.a = 1f;
                        Blend(x, y, col, cov);
                    }
                }
            }

            /// <summary>A round cherry with a dark rim and a small shine.</summary>
            public void Cherry(Vector2 p, float radius, Color colour)
            {
                Ellipse(p.x, p.y, radius + 0.7f, radius + 0.7f, new Color(0.25f, 0.03f, 0.03f), 1f);
                Ellipse(p.x, p.y, radius, radius, colour, 1f);
                Ellipse(p.x - radius * 0.35f, p.y + radius * 0.4f, radius * 0.32f, radius * 0.32f, Color.Lerp(colour, Color.white, 0.75f), 1f);
            }

            public Sprite ToSprite(string name, float pixelsPerUnit)
            {
                Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    name = name,
                    hideFlags = HideFlags.DontSave,
                };
                Color32[] pixels = new Color32[w * h];
                for (int i = 0; i < pixels.Length; i++)
                {
                    float a = pa[i];
                    if (a <= 0.001f)
                    {
                        pixels[i] = new Color32(0, 0, 0, 0);
                        continue;
                    }
                    pixels[i] = new Color32(
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(pr[i] / a) * 255f),
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(pg[i] / a) * 255f),
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(pb[i] / a) * 255f),
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
                texture.SetPixels32(pixels);
                texture.Apply(false);
                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
                sprite.name = name;
                sprite.hideFlags = HideFlags.DontSave;
                return sprite;
            }
        }
    }
}
