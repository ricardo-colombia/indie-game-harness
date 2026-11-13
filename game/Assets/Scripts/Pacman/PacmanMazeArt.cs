using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// Draws the maze walls as coffee mountains, into two sprites: a rim light (white, tinted at runtime by the
    /// scenery) and a baked-colour fill of terraced hillside with rows of coffee bushes (some with red cherries).
    /// The wall tiles are rasterised, their convex corners are rounded with a morphological opening, and the
    /// rim is drawn a little inside the wall edge, so thin walls read as ridgelines and thick blocks as
    /// whole hills.
    /// </summary>
    public static class PacmanMazeArt
    {
        /// <summary>Pixels per tile in the generated textures.</summary>
        private const int PixelsPerTile = 24;

        /// <summary>Tiles of empty margin around the board.</summary>
        private const int Pad = 1;

        private const float Infinity = 1e9f;

        private static Sprite lineSprite;
        private static Sprite fillSprite;

        /// <summary>The outline and fill sprites, centred on the board. Built once and cached.</summary>
        public static void Get(out Sprite line, out Sprite fill)
        {
            if (lineSprite == null || fillSprite == null)
            {
                Build();
            }
            line = lineSprite;
            fill = fillSprite;
        }

        private static void Build()
        {
            int s = PixelsPerTile;
            int pw = (PacmanMaze.Width + 2 * Pad) * s;
            int ph = (PacmanMaze.Height + 2 * Pad) * s;

            // Texture row 0 is the bottom, maze row 0 the top.
            bool[] wall = new bool[pw * ph];
            for (int py = 0; py < ph; py++)
            {
                int row = (ph - 1 - py) / s - Pad;
                for (int px = 0; px < pw; px++)
                {
                    int col = px / s - Pad;
                    wall[py * pw + px] = col >= 0 && row >= 0 && col < PacmanMaze.Width && row < PacmanMaze.Height
                        && PacmanMaze.Cell(col, row) == '#';
                }
            }

            float radius = 0.32f * s;
            float inset = 0.08f * s;
            float halfWidth = 0.032f * s;
            float glowReach = 0.08f * s;

            // Opening: erode the walls by the radius, then grow them back. Convex corners come out round.
            float[] toOpen = Distance(Invert(wall), pw, ph);
            bool[] eroded = new bool[wall.Length];
            for (int i = 0; i < wall.Length; i++)
            {
                eroded[i] = wall[i] && toOpen[i] >= radius;
            }
            float[] toEroded = Distance(eroded, pw, ph);
            bool[] rounded = new bool[wall.Length];
            for (int i = 0; i < wall.Length; i++)
            {
                rounded[i] = toEroded[i] <= radius;
            }

            // Signed distance to the rounded shape's edge: positive inside.
            float[] toOutside = Distance(Invert(rounded), pw, ph);
            float[] toInside = Distance(rounded, pw, ph);

            Color32[] lineColours = new Color32[wall.Length];
            Color32[] fillColours = new Color32[wall.Length];
            for (int i = 0; i < wall.Length; i++)
            {
                float signed = rounded[i] ? toOutside[i] : -toInside[i];
                float away = Mathf.Abs(signed - inset);
                float line = Mathf.Clamp01(halfWidth + 0.5f - away);
                float glow = 0.07f * Mathf.Exp(-away / glowReach);
                lineColours[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Max(line, glow) * 255f));
                Color32 terrain = Terrain(i % pw, i / pw, signed, s);
                terrain.a = (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f + signed) * 255f);
                fillColours[i] = terrain;
            }

            lineSprite = ToSprite("PacmanWallLine", lineColours, pw, ph, s);
            fillSprite = ToSprite("PacmanWallFill", fillColours, pw, ph, s);
        }

        private static readonly Color Slope = new Color(0.2f, 0.46f, 0.17f);
        private static readonly Color Summit = new Color(0.12f, 0.33f, 0.13f);
        private static readonly Color Terrace = new Color(0.06f, 0.2f, 0.08f);
        private static readonly Color BushDark = new Color(0.06f, 0.36f, 0.09f);
        private static readonly Color BushLight = new Color(0.4f, 0.82f, 0.28f);
        private static readonly Color Cherry = new Color(0.95f, 0.15f, 0.12f);

        /// <summary>
        /// Colour of the hillside at one pixel. signed is the distance in pixels inside the wall edge: slopes
        /// darken towards the middle, terrace lines run parallel to the edge, and staggered rows of round
        /// coffee bushes sit between them, a few carrying a red cherry.
        /// </summary>
        private static Color32 Terrain(int px, int py, float signed, int pixelsPerTile)
        {
            float tiles = signed / pixelsPerTile;
            Color c = Color.Lerp(Slope, Summit, Mathf.Clamp01(tiles / 0.7f));

            // Terrace lines: thin dark contours every quarter tile from the edge.
            float contour = Mathf.Repeat(signed, 0.25f * pixelsPerTile);
            if (tiles > 0.05f && contour < 1.4f)
            {
                c = Color.Lerp(c, Terrace, 0.8f);
            }

            // Bushes on a staggered grid, kept off the very edge.
            float cell = 0.34f * pixelsPerTile;
            int row = Mathf.FloorToInt(py / cell);
            float shift = (row & 1) * cell * 0.5f;
            int col = Mathf.FloorToInt((px + shift) / cell);
            float cx = (col + 0.5f) * cell - shift;
            float cy = (row + 0.5f) * cell;
            float dx = px - cx;
            float dy = py - cy;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float radius = cell * 0.42f;
            if (tiles > 0.16f && d < radius)
            {
                // Lit from the top left: the highlight slides to that side of each bush.
                float light = Mathf.Clamp01(0.5f + (-dx + dy) / (radius * 1.8f));
                Color bush = Color.Lerp(BushDark, BushLight, light * light);
                c = Color.Lerp(c, bush, Mathf.Clamp01((radius - d) * 1.2f));
                int hash = (col * 73856093 ^ row * 19349663) & 15;
                if (hash < 4)
                {
                    float cherryX = dx - radius * 0.25f;
                    float cherryY = dy + radius * 0.2f;
                    if (cherryX * cherryX + cherryY * cherryY < 2.6f * 2.6f)
                    {
                        c = Cherry;
                    }
                }
            }
            return c;
        }

        private static Sprite ToSprite(string name, Color32[] pixels, int width, int height, int pixelsPerUnit)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = name,
                hideFlags = HideFlags.DontSave,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        private static bool[] Invert(bool[] mask)
        {
            bool[] result = new bool[mask.Length];
            for (int i = 0; i < mask.Length; i++)
            {
                result[i] = !mask[i];
            }
            return result;
        }

        /// <summary>Euclidean distance in pixels from every pixel to the nearest pixel where source is true.</summary>
        private static float[] Distance(bool[] source, int width, int height)
        {
            float[] grid = new float[width * height];
            for (int i = 0; i < grid.Length; i++)
            {
                grid[i] = source[i] ? 0f : Infinity;
            }

            int longest = Mathf.Max(width, height);
            float[] f = new float[longest];
            float[] d = new float[longest];
            int[] v = new int[longest];
            float[] z = new float[longest + 1];

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    f[y] = grid[y * width + x];
                }
                Pass(f, d, v, z, height);
                for (int y = 0; y < height; y++)
                {
                    grid[y * width + x] = d[y];
                }
            }
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    f[x] = grid[y * width + x];
                }
                Pass(f, d, v, z, width);
                for (int x = 0; x < width; x++)
                {
                    grid[y * width + x] = Mathf.Sqrt(d[x]);
                }
            }
            return grid;
        }

        /// <summary>One dimension of the Felzenszwalb squared-distance transform (lower envelope of parabolas).</summary>
        private static void Pass(float[] f, float[] d, int[] v, float[] z, int n)
        {
            int k = 0;
            v[0] = 0;
            z[0] = -Infinity;
            z[1] = Infinity;
            for (int q = 1; q < n; q++)
            {
                float s;
                while (true)
                {
                    int p = v[k];
                    s = ((f[q] + q * q) - (f[p] + p * p)) / (2f * q - 2f * p);
                    if (s <= z[k] && k > 0)
                    {
                        k--;
                    }
                    else
                    {
                        break;
                    }
                }
                if (s <= z[k])
                {
                    // Only possible at k == 0: the new parabola dominates everything so far.
                    v[0] = q;
                    z[0] = -Infinity;
                    z[1] = Infinity;
                    continue;
                }
                k++;
                v[k] = q;
                z[k] = s;
                z[k + 1] = Infinity;
            }

            k = 0;
            for (int q = 0; q < n; q++)
            {
                while (z[k + 1] < q)
                {
                    k++;
                }
                int p = v[k];
                d[q] = (q - p) * (q - p) + f[p];
            }
        }
    }
}
