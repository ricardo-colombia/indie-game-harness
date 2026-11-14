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
        private const int Variants = 36;
        private const int W = 104;
        private const int H = 92;
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
            int[,] chosen = new int[PacmanMaze.Width, PacmanMaze.Height];
            for (int y = 0; y < PacmanMaze.Height; y++)
            {
                for (int x = 0; x < PacmanMaze.Width; x++)
                {
                    chosen[x, y] = -1;
                    if (PacmanMaze.Cell(x, y) != '#')
                    {
                        continue;
                    }
                    int hash = (x * 73856093) ^ (y * 19349663);
                    hash ^= hash >> 13;
                    hash *= 1274126177;
                    hash ^= hash >> 16;

                    // Never put the same bush next to itself: pick a variant that differs from every neighbour already placed.
                    int variant = (hash & 0xffff) % Variants;
                    for (int tries = 0; tries < Variants && Clash(chosen, x, y, variant); tries++)
                    {
                        variant = (variant + 7) % Variants;
                    }
                    chosen[x, y] = variant;

                    GameObject bush = new GameObject("Bush");
                    bush.transform.SetParent(root.transform, false);
                    // A few bushes are young (small), a few are old (big): the rows never read as one stamped pattern.
                    float growth = (hash >> 27) % 6 == 0 ? 0.82f : (hash >> 27) % 6 == 1 ? 1.1f : 1f;
                    float sx = (0.88f + ((hash >> 3) & 15) * 0.018f) * growth;
                    float sy = (0.88f + ((hash >> 7) & 15) * 0.018f) * growth;
                    float lift = (((hash >> 11) & 7) - 3.5f) * 0.008f;
                    // Each bush also stands a little left or right of its tile centre, so the trunks do not line up along a row.
                    float shift = (((hash >> 4) & 31) - 15.5f) / 15.5f * 0.13f;
                    bush.transform.localPosition = new Vector3(x + shift, -y - 0.53f + lift, 0f);
                    bush.transform.localScale = new Vector3(((hash >> 15) & 1) == 0 ? sx : -sx, sy, 1f);
                    SpriteRenderer renderer = bush.AddComponent<SpriteRenderer>();
                    renderer.sprite = sprites[variant];
                    renderer.sortingOrder = y;
                    // A slightly different green on each bush, as in a real plantation.
                    float tr = 0.84f + ((hash >> 16) & 15) * 0.018f;
                    float tg = 0.9f + ((hash >> 20) & 15) * 0.011f;
                    float tb = 0.8f + ((hash >> 24) & 15) * 0.019f;
                    renderer.color = new Color(tr, tg, tb, 1f);
                    list.Add(bush.transform);
                    phaseList.Add(((hash >> 9) & 255) / 255f * 6.283f);
                }
            }
            self.bushes = list.ToArray();
            self.phases = phaseList.ToArray();
            return self;
        }

        private static bool Clash(int[,] chosen, int x, int y, int variant)
        {
            return Same(chosen, x - 1, y, variant) || Same(chosen, x, y - 1, variant)
                || Same(chosen, x - 1, y - 1, variant) || Same(chosen, x + 1, y - 1, variant) || Same(chosen, x - 2, y, variant);
        }

        private static bool Same(int[,] chosen, int x, int y, int variant)
        {
            return x >= 0 && y >= 0 && x < chosen.GetLength(0) && y < chosen.GetLength(1) && chosen[x, y] == variant;
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

        /// <summary>One of the painted coffee-plant sprites, for scenes outside the board (the story comic).</summary>
        public static Sprite Variant(int index)
        {
            EnsureSprites();
            return sprites[((index % Variants) + Variants) % Variants];
        }

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

        private static readonly Color LeafDark = new Color(0.04f, 0.16f, 0.07f);
        private static readonly Color LeafMid = new Color(0.08f, 0.38f, 0.13f);
        private static readonly Color LeafLight = new Color(0.26f, 0.64f, 0.2f);
        private static readonly Color OliveMid = new Color(0.2f, 0.42f, 0.1f);
        private static readonly Color OliveLight = new Color(0.52f, 0.68f, 0.16f);
        private static readonly Color Wood = new Color(0.3f, 0.18f, 0.1f);

        /// <summary>
        /// One coffee plant in side view, the way a cafeto really grows: a slim trunk with tiers of nearly horizontal
        /// branches that droop at the tips (longest at the bottom, so the shrub is a cone), pairs of glossy pointed oval
        /// leaves facing each other along every branch, and red cherries bunched at the leaf joints. Branch count, length,
        /// droop, leaf size, colour, fruit and flowers change with the seed so neighbours never repeat.
        /// </summary>
        private static Sprite Paint(int seed)
        {
            Canvas c = new Canvas(W, H);
            System.Random rng = new System.Random(seed * 7919 + 13);
            float R(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }

            float mid = W * 0.5f + R(-9f, 9f);   // the trunk stands left or right of the sprite's middle, so the rows never line up
            float tall = R(0.85f, 1.05f);
            float spread = R(0.85f, 1.1f);
            float lean = R(-7f, 7f);
            float leafScale = R(0.9f, 1.2f);
            float olive = R(0f, 1f) < 0.3f ? R(0.3f, 0.7f) : R(0f, 0.15f);
            Color midGreen = Color.Lerp(LeafMid, OliveMid, olive);
            Color lightGreen = Color.Lerp(LeafLight, OliveLight, olive);
            bool flowering = seed % 4 == 1;
            float cherryBias = R(0f, 1f);

            // Soil mound and shadow under the plant.
            c.Ellipse(mid, 7f, R(30f, 38f), 6f, new Color(0.05f, 0.03f, 0.02f), 0.55f);
            c.Ellipse(mid, 9f, R(22f, 30f), 5f, new Color(0.3f, 0.18f, 0.1f), 1f);

            // Trunk.
            float topY = 72f * tall;
            Vector2 trunkTop = new Vector2(mid + lean, topY);
            c.Line(new Vector2(mid, 9f), new Vector2(mid + lean * 0.5f, topY * 0.55f), 3.4f, Wood);
            c.Line(new Vector2(mid + lean * 0.5f, topY * 0.55f), trunkTop, 2.4f, Wood);

            // A dense rounded mass of foliage around the trunk: a cafeto is a leafy dome, not a bare pole with tiers.
            c.Ellipse(mid + lean * 0.5f, topY * 0.5f + 6f, 30f * spread, topY * 0.4f, Color.Lerp(LeafDark, midGreen, 0.3f), 1f);

            // Tiers of branches, top first so the lower, nearer ones overlap them.
            int tiers = rng.Next(5, 8);
            for (int t = 0; t < tiers; t++)
            {
                float k = t / (float)(tiers - 1);              // 0 = top tier, 1 = bottom tier
                float y = Mathf.Lerp(topY - 3f, 20f * tall + 3f, k) + R(-1.5f, 1.5f);
                float reach = 40f * Mathf.Sin((0.15f + 0.6f * k) * Mathf.PI) * spread + R(-2f, 2f);   // widest around the middle: a rounded crown
                float x0 = mid + lean * (1f - k * 0.5f);
                // Shaded foliage mass behind the branches, so the trunk never shows through between the leaves.
                c.Ellipse(x0, y - 2f, reach * 0.72f, 6.5f, Color.Lerp(LeafDark, midGreen, 0.25f), 1f);
                for (int side = -1; side <= 1; side += 2)
                {
                    float sideReach = reach * R(0.8f, 1.05f);
                    float rise = R(6f, 22f);                    // angle above horizontal near the trunk
                    float droop = R(18f, 34f);                  // extra bend towards the ground along the branch
                    int nodes = Mathf.Max(2, Mathf.RoundToInt(sideReach / 6.5f));
                    Vector2 prev = new Vector2(x0, y);
                    for (int n = 1; n <= nodes; n++)
                    {
                        float u = n / (float)nodes;
                        float ang = rise - droop * u * u * 1.6f;
                        float stepLen = sideReach / nodes;
                        Vector2 pos = prev + new Vector2(side * Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad)) * stepLen;
                        c.Line(prev, pos, Mathf.Lerp(2.1f, 1.1f, u), Wood);

                        // A pair of opposite leaves at this joint, one leaning up, one down, both sweeping outward.
                        float heading = side > 0 ? ang : 180f - ang;
                        float spreadAngle = R(38f, 58f);
                        float len = R(15f, 20f) * leafScale * Mathf.Lerp(1f, 0.8f, k < 0.2f ? 1f : 0f) * Mathf.Lerp(0.85f, 1f, u);
                        float wid = len * R(0.48f, 0.56f);
                        for (int half = -1; half <= 1; half += 2)
                        {
                            float a = heading + half * spreadAngle * side;
                            float sun = Mathf.Clamp01(0.45f + half * 0.28f + R(-0.15f, 0.15f));
                            Color col = Color.Lerp(midGreen, lightGreen, sun);
                            if (R(0f, 1f) < 0.04f)
                            {
                                col = Color.Lerp(col, new Color(0.7f, 0.76f, 0.2f), 0.6f);
                            }
                            c.Leaf(pos, a, len * R(0.9f, 1.05f), wid, col, 0.6f);
                        }
                        // The terminal leaf, pointing straight on.
                        if (n == nodes)
                        {
                            c.Leaf(pos, heading - side * 8f, len * 0.9f, wid * 0.95f, midGreen, 0.6f);
                        }
                        prev = pos;

                        // Cherries cluster at the joint, hanging under the branch.
                        if (k > 0.1f && R(0f, 1f) < 0.62f)
                        {
                            int count = rng.Next(3, 7);
                            for (int j = 0; j < count; j++)
                            {
                                Vector2 cp = pos + new Vector2(R(-3.2f, 3.2f), R(-6.5f, -1.5f));
                                float ripe = R(0f, 1f) * 0.55f + cherryBias * 0.25f;
                                Color cherry = ripe < 0.6f ? new Color(0.88f, 0.06f, 0.1f) : ripe < 0.82f ? new Color(0.95f, 0.42f, 0.08f) : new Color(0.62f, 0.74f, 0.16f);
                                c.Line(pos, cp + new Vector2(0f, 2.5f), 0.9f, Wood);
                                c.Cherry(cp, R(2.7f, 3.4f), cherry);
                            }
                        }
                        else if (flowering && R(0f, 1f) < 0.45f)
                        {
                            Vector2 fp = pos + new Vector2(R(-2f, 2f), R(1f, 4f));
                            for (int j = 0; j < 5; j++)
                            {
                                float pa = j * 72f * Mathf.Deg2Rad;
                                c.Ellipse(fp.x + Mathf.Cos(pa) * 2.2f, fp.y + Mathf.Sin(pa) * 2.2f, 1.7f, 1.7f, new Color(0.99f, 0.98f, 0.93f), 1f);
                            }
                            c.Ellipse(fp.x, fp.y, 1.2f, 1.2f, new Color(0.95f, 0.8f, 0.25f), 1f);
                        }
                    }
                }
            }
            // Crown: the top shoot with its last pair of leaves.
            c.Leaf(trunkTop, 90f + R(-10f, 10f), 14f * leafScale, 6.5f * leafScale, lightGreen, 0.6f);
            c.Leaf(trunkTop, 150f, 12f * leafScale, 6f * leafScale, midGreen, 0.6f);
            c.Leaf(trunkTop, 30f, 12f * leafScale, 6f * leafScale, midGreen, 0.6f);

            // Grass tufts around the foot.
            int tufts = rng.Next(5, 11);
            for (int i = 0; i < tufts; i++)
            {
                float gx = mid + R(-34f, 34f);
                c.Line(new Vector2(gx, 8f), new Vector2(gx + R(-3.5f, 3.5f), R(12f, 19f)), 1.2f, new Color(0.34f, 0.62f, 0.2f));
            }

            c.Outline(PacmanToon.Ink, 1.5f);
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

            /// <summary>Draws a dark outline around everything solid, the shared look of all sprites in the game.</summary>
            public void Outline(Color ink, float radius)
            {
                float[] solid = new float[w * h];
                for (int i = 0; i < solid.Length; i++)
                {
                    solid[i] = pa[i] > 0.85f ? pa[i] : 0f;
                }
                int r = Mathf.CeilToInt(radius);
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        float own = solid[y * w + x];
                        if (own >= 0.999f)
                        {
                            continue;
                        }
                        float best = 0f;
                        for (int dy = -r; dy <= r; dy++)
                        {
                            for (int dx = -r; dx <= r; dx++)
                            {
                                int nx = x + dx;
                                int ny = y + dy;
                                if (nx < 0 || ny < 0 || nx >= w || ny >= h)
                                {
                                    continue;
                                }
                                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                                best = Mathf.Max(best, solid[ny * w + nx] * Mathf.Clamp01(radius + 0.5f - dist));
                            }
                        }
                        Blend(x, y, ink, best * (1f - own));
                    }
                }
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
