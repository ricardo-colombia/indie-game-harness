using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// The trodden dirt trail under the coffee: one soft-edged path through every corridor cell, joined to its
    /// neighbours, painted once into a single sprite. Its colour is the roasted-bean brown of the pellets, so the
    /// beans lie on the path and blend into it; the edges feather into the dark earth.
    /// </summary>
    public static class PacmanPath
    {
        private const int CellPixels = 28;

        /// <summary>Creates the path sprite as a child of the parent, centred on the maze, drawn above the floor and below the walls.</summary>
        public static void Build(Transform parent)
        {
            int w = PacmanMaze.Width;
            int h = PacmanMaze.Height;
            bool[,] path = new bool[w, h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    char c = PacmanMaze.Cell(x, y);
                    path[x, y] = c == '.' || c == 'o' || (c == ' ' && !PacmanMaze.IsGhostHouse(new Vector2Int(x, y)));
                }
            }

            int tw = w * CellPixels;
            int th = h * CellPixels;
            Texture2D texture = new Texture2D(tw, th, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "PacmanPath",
                hideFlags = HideFlags.DontSave,
            };
            bool vintage = PacmanToon.IsVintage;
            Color earth = new Color(0.34f, 0.205f, 0.105f);
            Color worn = new Color(0.46f, 0.3f, 0.16f);
            Color rim = new Color(0.17f, 0.1f, 0.055f);
            Color[] pixels = new Color[tw * th];
            for (int py = 0; py < th; py++)
            {
                for (int px = 0; px < tw; px++)
                {
                    // Cell units: cell (x, y) is centred on (x, y); image row 0 is the bottom of the maze (world y = -(h - 0.5)).
                    float fx = (px + 0.5f) / CellPixels - 0.5f;
                    float fy = (py + 0.5f) / CellPixels - (h - 0.5f);
                    float d = Distance(path, fx, fy, w, h);
                    // The edge wanders, like a path worn by feet and not cut with a ruler.
                    float wobble = (Noise(px * 0.09f, py * 0.09f) - 0.5f) * 0.16f + (Noise(px * 0.35f, py * 0.35f) - 0.5f) * 0.05f;
                    float e = d + wobble;
                    float cover = 1f - Mathf.SmoothStep(0.2f, 0.31f, e);
                    if (cover <= 0.001f)
                    {
                        pixels[py * tw + px] = new Color(0f, 0f, 0f, 0f);
                        continue;
                    }
                    float grit = PacmanToon.Hash(px, py);
                    float centre = 1f - Mathf.SmoothStep(0.03f, 0.24f, e);
                    Color col = Color.Lerp(earth, worn, centre * 0.8f + (Noise(px * 0.18f, py * 0.18f) - 0.5f) * 0.5f);
                    col *= 0.93f + grit * 0.14f;
                    // Darker, packed earth along the rim, so the trail sits in the ground.
                    col = Color.Lerp(col, rim, Mathf.SmoothStep(0.1f, 0.3f, e) * 0.7f);
                    // A few pebbles and chaff flecks.
                    if (grit > 0.992f)
                    {
                        col = Color.Lerp(col, new Color(0.68f, 0.52f, 0.34f), 0.7f);
                    }
                    else if (grit < 0.006f)
                    {
                        col = Color.Lerp(col, rim, 0.8f);
                    }
                    if (vintage)
                    {
                        // Etched ink hatching: short diagonal strokes in the trail.
                        float stroke = Mathf.Repeat((px + py) * 0.35f, 1f);
                        if (stroke < 0.12f && Noise(px * 0.12f, py * 0.12f) > 0.45f)
                        {
                            col *= 0.82f;
                        }
                    }
                    col.a = Mathf.Clamp01(cover * 0.96f);
                    pixels[py * tw + px] = col;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false);

            GameObject go = new GameObject("CoffeePath");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3((w - 1) * 0.5f, -(h - 1) * 0.5f, 0f);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, tw, th), new Vector2(0.5f, 0.5f), CellPixels, 0, SpriteMeshType.FullRect);
            sprite.hideFlags = HideFlags.DontSave;
            renderer.sprite = sprite;
            renderer.sortingOrder = -9;
        }

        /// <summary>Distance in cells from a point to the nearest path cell centre or to the segment joining two adjacent path cells.</summary>
        private static float Distance(bool[,] path, float fx, float fy, int w, int h)
        {
            int cx = Mathf.RoundToInt(fx);
            int cy = Mathf.RoundToInt(-fy);
            float best = 10f;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int x = cx + dx;
                    int y = cy + dy;
                    if (x < 0 || y < 0 || x >= w || y >= h || !path[x, y])
                    {
                        continue;
                    }
                    best = Mathf.Min(best, Vector2.Distance(new Vector2(fx, fy), new Vector2(x, -y)));
                    if (x + 1 < w && path[x + 1, y])
                    {
                        best = Mathf.Min(best, SegmentDistance(fx, fy, x, -y, x + 1, -y));
                    }
                    if (y + 1 < h && path[x, y + 1])
                    {
                        best = Mathf.Min(best, SegmentDistance(fx, fy, x, -y, x, -(y + 1)));
                    }
                }
            }
            return best;
        }

        private static float SegmentDistance(float px, float py, float ax, float ay, float bx, float by)
        {
            float abx = bx - ax;
            float aby = by - ay;
            float t = Mathf.Clamp01(((px - ax) * abx + (py - ay) * aby) / (abx * abx + aby * aby));
            float qx = ax + abx * t - px;
            float qy = ay + aby * t - py;
            return Mathf.Sqrt(qx * qx + qy * qy);
        }

        /// <summary>Smooth value noise in 0..1.</summary>
        private static float Noise(float x, float y)
        {
            int ix = Mathf.FloorToInt(x);
            int iy = Mathf.FloorToInt(y);
            float fx = x - ix;
            float fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = PacmanToon.Hash(ix, iy);
            float b = PacmanToon.Hash(ix + 1, iy);
            float c = PacmanToon.Hash(ix, iy + 1);
            float d = PacmanToon.Hash(ix + 1, iy + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }
    }
}
