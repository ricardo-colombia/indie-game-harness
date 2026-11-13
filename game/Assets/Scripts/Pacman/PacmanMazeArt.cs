using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// Draws the maze walls into two sprites: a neon outline (white, tinted at runtime by the scenery) and a
    /// dark fill. The wall tiles are rasterised, their convex corners are rounded with a morphological
    /// opening, and the outline is drawn a little inside the wall edge, so thin walls read as the classic
    /// double line and thick blocks as hollow outlines.
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
            float inset = 0.2f * s;
            float halfWidth = 0.06f * s;
            float glowReach = 0.1f * s;

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
                float glow = 0.3f * Mathf.Exp(-away / glowReach);
                lineColours[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Max(line, glow) * 255f));
                fillColours[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f + signed) * 255f));
            }

            lineSprite = ToSprite("PacmanWallLine", lineColours, pw, ph, s);
            fillSprite = ToSprite("PacmanWallFill", fillColours, pw, ph, s);
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
