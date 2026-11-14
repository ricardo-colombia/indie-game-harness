using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// A whitewashed finca house with a terracotta roof and green door on each hillside behind the board.
    /// Smoke curls from the chimney all the time; the windows glow warm as the sky darkens (dusk and night levels).
    /// Painted pixel by pixel, no imported art. The scenery tells it the sky tint every frame.
    /// </summary>
    public class PacmanFarmhouse : MonoBehaviour
    {
        [Tooltip("Width of the house in tiles.")]
        [SerializeField] private float width = 2.3f;
        [Tooltip("Seconds for a puff of chimney smoke to rise and fade.")]
        [SerializeField] private float smokeSeconds = 9f;

        private const int Puffs = 6;
        private const int PixelW = 64;
        private const int PixelH = 48;
        // Window rectangles in texture pixels (x0, y0, x1, y1), used to place the glow quads over the panes.
        private static readonly int[][] Windows = { new[] { 11, 9, 18, 17 }, new[] { 38, 9, 45, 17 } };

        private static Sprite houseSprite;
        private static Sprite squareSprite;

        private SpriteRenderer body;
        private SpriteRenderer[] glow;
        private SpriteRenderer halo;
        private SpriteRenderer[] smoke;
        private float smokeSeed;
        private float unit;
        private Vector2 chimney;
        private float flip = 1f;

        /// <summary>Creates a house at a world position under a parent; flip mirrors it so the pair face each other.</summary>
        public static PacmanFarmhouse Build(Transform parent, Vector2 position, bool mirrored, float seed)
        {
            EnsureSprites();
            GameObject root = new GameObject("Farmhouse");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(position.x, position.y, 0f);
            PacmanFarmhouse self = root.AddComponent<PacmanFarmhouse>();
            self.smokeSeed = seed;
            self.flip = mirrored ? -1f : 1f;
            self.Construct();
            return self;
        }

        /// <summary>Tints the house with the sky and lights the windows as the light fades (tint.r is about 1 by day, 0.3 at night).</summary>
        public void SetSky(Color tint)
        {
            float dark = Mathf.Clamp01((1.05f - tint.r) / 0.6f);
            Color c = new Color(0.25f + 0.7f * tint.r, 0.25f + 0.7f * tint.g, 0.25f + 0.7f * tint.b, 1f);
            body.color = c;
            Color lamp = new Color(1f, 0.74f, 0.32f, 0.25f + 0.75f * dark);
            foreach (SpriteRenderer g in glow)
            {
                g.color = lamp;
            }
            halo.color = new Color(1f, 0.6f, 0.2f, 0.4f * dark);
            foreach (SpriteRenderer puff in smoke)
            {
                puff.color = new Color(0.85f * c.r, 0.85f * c.g, 0.88f * c.b, puff.color.a);
            }
        }

        private void Construct()
        {
            unit = width / PixelW;
            GameObject house = new GameObject("Body");
            house.transform.SetParent(transform, false);
            house.transform.localScale = new Vector3(flip, 1f, 1f);
            body = house.AddComponent<SpriteRenderer>();
            body.sprite = houseSprite;
            body.sortingOrder = -26;
            house.transform.localScale = new Vector3(width / (PixelW / 32f) * flip, width / (PixelW / 32f), 1f);

            float w = width / PixelW;
            glow = new SpriteRenderer[Windows.Length];
            for (int i = 0; i < Windows.Length; i++)
            {
                int[] r = Windows[i];
                GameObject g = new GameObject("Pane");
                g.transform.SetParent(transform, false);
                float cx = ((r[0] + r[2] + 1) * 0.5f - PixelW * 0.5f) * w * flip;
                float cy = ((r[1] + r[3] + 1) * 0.5f - PixelH * 0.5f) * w;
                g.transform.localPosition = new Vector3(cx, cy, 0f);
                g.transform.localScale = new Vector3((r[2] - r[0] + 1) * w, (r[3] - r[1] + 1) * w, 1f);
                glow[i] = g.AddComponent<SpriteRenderer>();
                glow[i].sprite = squareSprite;
                glow[i].sortingOrder = -25;
            }

            GameObject h = new GameObject("Halo");
            h.transform.SetParent(transform, false);
            h.transform.localPosition = new Vector3(0f, -PixelH * 0.18f * w, 0f);
            h.transform.localScale = new Vector3(width * 1.5f, width * 0.9f, 1f);
            halo = h.AddComponent<SpriteRenderer>();
            halo.sprite = PacmanSprites.SoftDisc;
            halo.sortingOrder = -27;

            chimney = new Vector2((40.5f - PixelW * 0.5f) * w * flip, (41f - PixelH * 0.5f) * w);
            smoke = new SpriteRenderer[Puffs];
            for (int i = 0; i < Puffs; i++)
            {
                GameObject p = new GameObject("Smoke");
                p.transform.SetParent(transform, false);
                smoke[i] = p.AddComponent<SpriteRenderer>();
                smoke[i].sprite = PacmanSprites.SoftDisc;
                smoke[i].sortingOrder = -24;
                smoke[i].color = new Color(0.85f, 0.85f, 0.88f, 0f);
            }
            SetSky(Color.white);
        }

        private void Update()
        {
            for (int i = 0; i < Puffs; i++)
            {
                float t = Mathf.Repeat(Time.time / smokeSeconds + i / (float)Puffs + smokeSeed, 1f);
                float sway = Mathf.Sin(t * 6f + i * 1.7f + smokeSeed * 9f) * 0.12f * t;
                smoke[i].transform.localPosition = new Vector3(chimney.x + (sway + t * 0.55f) * flip, chimney.y + t * 1.5f, 0f);
                float size = 0.16f + t * 0.55f;
                smoke[i].transform.localScale = new Vector3(size, size, 1f);
                Color c = smoke[i].color;
                c.a = 0.4f * Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * (1f - t * 0.4f);
                smoke[i].color = c;
            }
        }

        private static void EnsureSprites()
        {
            if (squareSprite == null)
            {
                Texture2D t = new Texture2D(2, 2);
                t.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
                t.Apply();
                squareSprite = Sprite.Create(t, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
            }
            if (houseSprite == null)
            {
                houseSprite = Sprite.Create(PaintHouse(), new Rect(0, 0, PixelW, PixelH), new Vector2(0.5f, 0.5f), 32f);
            }
        }

        private static Texture2D PaintHouse()
        {
            Color[] px = new Color[PixelW * PixelH];
            bool[] solid = new bool[px.Length];
            void Fill(int x0, int y0, int x1, int y1, Color c)
            {
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        px[y * PixelW + x] = c;
                        solid[y * PixelW + x] = true;
                    }
                }
            }

            // Whitewashed wall with a soft vertical shade, a darker plinth.
            for (int x = 6; x <= 50; x++)
            {
                float shade = 1f - 0.1f * (x - 6) / 44f;
                Fill(x, 4, x, 22, new Color(0.95f * shade, 0.9f * shade, 0.8f * shade, 1f));
            }
            Fill(6, 4, 50, 5, new Color(0.55f, 0.38f, 0.25f, 1f));

            // Terracotta roof: a trapezoid of tile rows alternating in tone, with a shadow line under the eave.
            for (int y = 22; y <= 33; y++)
            {
                int inset = (y - 22) * 4 / 3;
                for (int x = 2 + inset; x <= 54 - inset; x++)
                {
                    bool row = (y % 2 == 0);
                    bool seam = ((x + (row ? 2 : 0)) % 5) == 0;
                    Color tile = row ? new Color(0.78f, 0.34f, 0.2f, 1f) : new Color(0.7f, 0.28f, 0.16f, 1f);
                    if (seam)
                    {
                        tile *= 0.82f;
                        tile.a = 1f;
                    }
                    px[y * PixelW + x] = tile;
                    solid[y * PixelW + x] = true;
                }
            }
            Fill(2, 21, 54, 21, new Color(0.45f, 0.3f, 0.2f, 1f));
            for (int x = 6; x <= 50; x++)
            {
                px[20 * PixelW + x] *= 0.8f;
                px[20 * PixelW + x].a = 1f;
            }

            // Brick chimney with a pale cap.
            Fill(38, 30, 43, 41, new Color(0.62f, 0.3f, 0.22f, 1f));
            Fill(37, 41, 44, 42, new Color(0.85f, 0.8f, 0.72f, 1f));

            // Green door with a lighter panel, and two windows (frame, cross bars; the lit panes are separate quads).
            Color green = new Color(0.16f, 0.45f, 0.27f, 1f);
            Fill(24, 5, 32, 17, green);
            Fill(26, 7, 30, 15, new Color(0.22f, 0.55f, 0.34f, 1f));
            foreach (int[] w in Windows)
            {
                Fill(w[0] - 1, w[1] - 1, w[2] + 1, w[3] + 1, green);
                Fill(w[0], w[1], w[2], w[3], new Color(0.12f, 0.1f, 0.08f, 1f));
                Fill((w[0] + w[2]) / 2, w[1], (w[0] + w[2]) / 2, w[3], green);
                Fill(w[0], (w[1] + w[3]) / 2, w[2], (w[1] + w[3]) / 2, green);
            }
            // A flower box under each window.
            foreach (int[] w in Windows)
            {
                Fill(w[0] - 1, w[1] - 3, w[2] + 1, w[1] - 2, new Color(0.42f, 0.26f, 0.15f, 1f));
                for (int x = w[0]; x <= w[2]; x += 2)
                {
                    px[(w[1] - 1) * PixelW + x] = x % 4 == 0 ? new Color(0.85f, 0.2f, 0.25f, 1f) : new Color(0.95f, 0.75f, 0.2f, 1f);
                    solid[(w[1] - 1) * PixelW + x] = true;
                }
            }

            // Ink outline: any transparent pixel next to a painted one becomes dark brown.
            Color ink = new Color(0.12f, 0.07f, 0.04f, 1f);
            Color[] outlined = (Color[])px.Clone();
            for (int y = 1; y < PixelH - 1; y++)
            {
                for (int x = 1; x < PixelW - 1; x++)
                {
                    int i = y * PixelW + x;
                    if (!solid[i] && (solid[i - 1] || solid[i + 1] || solid[i - PixelW] || solid[i + PixelW]))
                    {
                        outlined[i] = ink;
                    }
                }
            }
            Texture2D tex = new Texture2D(PixelW, PixelH, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels(outlined);
            tex.Apply();
            return tex;
        }
    }
}
