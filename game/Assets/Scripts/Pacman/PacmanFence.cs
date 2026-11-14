using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// A rustic wooden finca fence around the board: posts and rails painted once in code and repeated along the
    /// margin tiles. The sides leave a gap at the tunnel row, where the farmer walks out of the plantation.
    /// </summary>
    public static class PacmanFence
    {
        private const int N = 32;

        /// <summary>Builds the fence ring under a parent, behind the bushes.</summary>
        public static void Build(Transform parent)
        {
            Sprite horizontal = Paint(true);
            Sprite vertical = Paint(false);
            GameObject root = new GameObject("Fence");
            root.transform.SetParent(parent, false);

            int w = PacmanMaze.Width;
            int h = PacmanMaze.Height;
            float top = 1.05f;
            float bottom = -(h - 1) - 1.05f;
            float left = -1.05f;
            float right = w - 1 + 1.05f;
            for (int x = -1; x <= w; x++)
            {
                Place(root.transform, horizontal, x, top);
                Place(root.transform, horizontal, x, bottom);
            }
            for (int y = 0; y < h; y++)
            {
                if (y == PacmanMaze.TunnelRow)
                {
                    continue;
                }
                Place(root.transform, vertical, left, -y);
                Place(root.transform, vertical, right, -y);
            }
        }

        private static void Place(Transform parent, Sprite sprite, float x, float y)
        {
            GameObject piece = new GameObject("Fence");
            piece.transform.SetParent(parent, false);
            piece.transform.position = new Vector3(x, y, 0f);
            SpriteRenderer renderer = piece.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = -6;
        }

        /// <summary>One tile of fence: a post with two rails (horizontal run) or a post with a rail going up and down (vertical run).</summary>
        private static Sprite Paint(bool horizontal)
        {
            Color ink = new Color(0.1f, 0.06f, 0.035f);
            Color wood = new Color(0.43f, 0.28f, 0.15f);
            Color light = new Color(0.58f, 0.41f, 0.23f);
            Color[] px = new Color[N * N];
            for (int i = 0; i < px.Length; i++)
            {
                px[i] = new Color(0f, 0f, 0f, 0f);
            }

            // Shapes in pixel boxes: rails first, then the post over them.
            if (horizontal)
            {
                Box(px, 0, 10, N, 13, wood, light, ink, 1);
                Box(px, 0, 19, N, 22, wood, light, ink, 2);
                Box(px, 12, 4, 20, 28, wood, light, ink, 3);
            }
            else
            {
                Box(px, 14, 0, 18, N, wood, light, ink, 4);
                Box(px, 10, 12, 22, 20, wood, light, ink, 5);
            }

            Texture2D texture = new Texture2D(N, N, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "PacmanFence",
                hideFlags = HideFlags.DontSave,
            };
            texture.SetPixels(px);
            texture.Apply(false);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N, 0, SpriteMeshType.FullRect);
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        /// <summary>A plank with an ink rim, a lit top edge and wood grain. The box is [x0,x1) by [y0,y1).</summary>
        private static void Box(Color[] px, int x0, int y0, int x1, int y1, Color wood, Color light, Color ink, int seed)
        {
            for (int y = y0; y < y1; y++)
            {
                for (int x = Mathf.Max(0, x0); x < Mathf.Min(N, x1); x++)
                {
                    bool rimY = y == y0 || y == y1 - 1;
                    bool rimX = (x == x0 && x0 > 0) || (x == x1 - 1 && x1 < N);
                    Color c;
                    if (rimY || rimX)
                    {
                        c = ink;
                    }
                    else
                    {
                        float grain = PacmanToon.Hash(x / 4, y + seed * 17);
                        c = Color.Lerp(wood, light, y == y1 - 2 ? 0.8f : grain * 0.35f);
                    }
                    px[y * N + x] = c;
                }
            }
        }
    }
}
