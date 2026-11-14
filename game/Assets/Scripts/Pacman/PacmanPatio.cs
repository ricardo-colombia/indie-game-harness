using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// The ghost house as a coffee drying patio (patio de secado): terracotta tiles with joints and a low wooden
    /// kerb, with coffee beans spread out to dry. Painted once into a single sprite over the house cells; the gate
    /// and the characters are drawn by other systems above it.
    /// </summary>
    public static class PacmanPatio
    {
        private const int CellPixels = 28;

        /// <summary>Creates the patio sprite over the ghost house as a child of the parent.</summary>
        public static void Build(Transform parent)
        {
            int minX = int.MaxValue;
            int maxX = int.MinValue;
            int minY = int.MaxValue;
            int maxY = int.MinValue;
            for (int y = 0; y < PacmanMaze.Height; y++)
            {
                for (int x = 0; x < PacmanMaze.Width; x++)
                {
                    char c = PacmanMaze.Cell(x, y);
                    if (c == 'h')
                    {
                        minX = Mathf.Min(minX, x);
                        maxX = Mathf.Max(maxX, x);
                        minY = Mathf.Min(minY, y);
                        maxY = Mathf.Max(maxY, y);
                    }
                }
            }
            if (minX > maxX)
            {
                return;
            }

            int cellsW = maxX - minX + 1;
            int cellsH = maxY - minY + 1;
            int tw = cellsW * CellPixels;
            int th = cellsH * CellPixels;
            Texture2D texture = new Texture2D(tw, th, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "PacmanPatio",
                hideFlags = HideFlags.DontSave,
            };
            bool vintage = PacmanToon.IsVintage;
            Color clay = new Color(0.58f, 0.33f, 0.2f);
            Color clayLight = new Color(0.68f, 0.42f, 0.26f);
            Color joint = new Color(0.27f, 0.15f, 0.09f);
            Color wood = new Color(0.3f, 0.18f, 0.09f);
            Color bean = new Color(0.36f, 0.16f, 0.08f);
            const float tile = 0.5f; // a tile is half a cell
            Color[] pixels = new Color[tw * th];
            for (int py = 0; py < th; py++)
            {
                for (int px = 0; px < tw; px++)
                {
                    float u = (px + 0.5f) / CellPixels; // cells from the left edge
                    float v = (py + 0.5f) / CellPixels;
                    float edge = Mathf.Min(Mathf.Min(u, cellsW - u), Mathf.Min(v, cellsH - v));
                    int ti = Mathf.FloorToInt(u / tile);
                    int tj = Mathf.FloorToInt(v / tile);
                    float tileShade = PacmanToon.Hash(ti + 3, tj + 7);
                    Color col = Color.Lerp(clay, clayLight, tileShade * 0.8f);
                    float grit = PacmanToon.Hash(px, py);
                    col *= 0.94f + grit * 0.12f;
                    // Tile joints.
                    float fu = Mathf.Repeat(u, tile);
                    float fv = Mathf.Repeat(v, tile);
                    float j = Mathf.Min(Mathf.Min(fu, tile - fu), Mathf.Min(fv, tile - fv));
                    if (j < 0.022f)
                    {
                        col = Color.Lerp(col, joint, 0.55f);
                    }
                    else if (vintage && Mathf.Repeat((px + py) * 0.3f, 1f) < 0.1f)
                    {
                        col *= 0.88f;
                    }
                    // Coffee beans drying in loose drifts, thicker toward the middle of each tile row.
                    float cluster = PacmanToon.Hash(Mathf.FloorToInt(u * 2.2f), Mathf.FloorToInt(v * 2.2f) + 40);
                    if (cluster > 0.35f)
                    {
                        float cx = Mathf.Floor(u * 9f) + 0.5f;
                        float cy = Mathf.Floor(v * 9f) + 0.5f;
                        float seed = PacmanToon.Hash(Mathf.FloorToInt(cx) + 11, Mathf.FloorToInt(cy) + 23);
                        if (seed > 0.3f)
                        {
                            float du = (u * 9f - cx) * 1f;
                            float dv = (v * 9f - cy) * 1.5f;
                            if (du * du + dv * dv < 0.26f)
                            {
                                col = Color.Lerp(bean, new Color(0.55f, 0.25f, 0.12f), seed);
                            }
                        }
                    }
                    // Wooden kerb around the patio, with a darker line where it meets the tiles.
                    float kerb = 0.13f;
                    if (edge < kerb)
                    {
                        float grain = 0.88f + 0.12f * Mathf.Sin((px + py * 0.2f) * 0.9f);
                        col = wood * grain;
                        if (edge > kerb - 0.03f)
                        {
                            col = Color.Lerp(col, joint, 0.7f);
                        }
                    }
                    else if (edge < kerb + 0.15f)
                    {
                        col = Color.Lerp(col, joint, (1f - (edge - kerb) / 0.15f) * 0.45f);
                    }
                    pixels[py * tw + px] = new Color(col.r, col.g, col.b, 1f);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, tw, th), new Vector2(0.5f, 0.5f), CellPixels, 0, SpriteMeshType.FullRect);
            sprite.hideFlags = HideFlags.DontSave;

            GameObject go = new GameObject("Patio");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3((minX + maxX) * 0.5f, -(minY + maxY) * 0.5f, 0f);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = -8;
        }
    }
}
