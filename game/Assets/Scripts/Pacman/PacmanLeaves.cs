using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// Falling leaves: a handful of small coffee leaves that drift down across the plantation, swaying and
    /// tumbling, so the still maze always has a little life in it. One procedural leaf sprite tinted per leaf;
    /// they stay below the beans and the characters and are small and sparse so they never read as gameplay.
    /// </summary>
    public class PacmanLeaves : MonoBehaviour
    {
        private const int Count = 14;

        [Tooltip("Fall speed range in tiles per second.")]
        [SerializeField] private Vector2 fall = new Vector2(0.45f, 0.9f);
        [Tooltip("How far a leaf sways sideways, in tiles.")]
        [SerializeField] private float sway = 0.8f;
        [Tooltip("Size of a leaf in tiles.")]
        [SerializeField] private float size = 0.62f;

        private static Sprite leafSprite;

        private Transform[] leaves;
        private float[] speed;
        private float[] phase;
        private float[] baseX;
        private float[] spin;
        private float top;
        private float bottom;

        /// <summary>Creates the falling leaves over the maze under a parent.</summary>
        public static PacmanLeaves Build(Transform parent)
        {
            GameObject root = new GameObject("Leaves");
            root.transform.SetParent(parent, false);
            PacmanLeaves self = root.AddComponent<PacmanLeaves>();
            EnsureSprite();
            self.top = 2f;
            self.bottom = -PacmanMaze.Height - 2f;
            self.leaves = new Transform[Count];
            self.speed = new float[Count];
            self.phase = new float[Count];
            self.baseX = new float[Count];
            self.spin = new float[Count];
            Color[] tints =
            {
                new Color(0.55f, 0.85f, 0.35f), new Color(0.4f, 0.75f, 0.3f), new Color(0.78f, 0.85f, 0.3f),
                new Color(0.9f, 0.7f, 0.25f), new Color(0.35f, 0.65f, 0.4f),
            };
            for (int i = 0; i < Count; i++)
            {
                GameObject leaf = new GameObject("Leaf");
                leaf.transform.SetParent(root.transform, false);
                float s = self.size * Mathf.Lerp(0.7f, 1.2f, PacmanToon.Hash(i, 31));
                leaf.transform.localScale = new Vector3(s, s, 1f);
                SpriteRenderer renderer = leaf.AddComponent<SpriteRenderer>();
                renderer.sprite = leafSprite;
                renderer.sortingOrder = 0;
                Color c = tints[Mathf.Min(tints.Length - 1, (int)(PacmanToon.Hash(i, 33) * tints.Length))];
                c.a = 0.75f;
                renderer.color = c;
                self.leaves[i] = leaf.transform;
                self.speed[i] = Mathf.Lerp(self.fall.x, self.fall.y, PacmanToon.Hash(i, 35));
                self.phase[i] = PacmanToon.Hash(i, 37) * 6.283f;
                self.baseX[i] = Mathf.Lerp(-1f, PacmanMaze.Width + 1f, (i + PacmanToon.Hash(i, 39) * 0.8f) / Count);
                self.spin[i] = Mathf.Lerp(-90f, 90f, PacmanToon.Hash(i, 41));
                // Spread the starting heights so they do not fall as one curtain.
                Vector3 p = leaf.transform.localPosition;
                p.y = Mathf.Lerp(self.top, self.bottom, PacmanToon.Hash(i, 43));
                leaf.transform.localPosition = p;
            }
            return self;
        }

        private void Update()
        {
            if (leaves == null)
            {
                return;
            }
            for (int i = 0; i < leaves.Length; i++)
            {
                Vector3 p = leaves[i].localPosition;
                p.y -= speed[i] * Time.deltaTime;
                if (p.y < bottom)
                {
                    p.y = top;
                }
                p.x = baseX[i] + Mathf.Sin(Time.time * 0.9f + phase[i]) * sway;
                leaves[i].localPosition = p;
                // Tumble: rotate and flip slowly, like a leaf catching the air.
                float flip = Mathf.Cos(Time.time * 1.3f + phase[i]);
                Vector3 s = leaves[i].localScale;
                s.x = Mathf.Sign(s.y) * Mathf.Abs(s.y) * (0.35f + 0.65f * Mathf.Abs(flip));
                leaves[i].localScale = s;
                leaves[i].localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 0.7f + phase[i]) * 55f + spin[i] * 0.2f);
            }
        }

        private static void EnsureSprite()
        {
            if (leafSprite != null)
            {
                return;
            }
            const int w = 24;
            const int h = 48;
            Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "PacmanLeaf",
                hideFlags = HideFlags.DontSave,
            };
            Color[] pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                float half = Mathf.Sin(v * Mathf.PI) * 0.46f * (1f - 0.25f * v);
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w - 0.5f;
                    float edge = Mathf.Clamp01((half - Mathf.Abs(u)) * w * 0.6f);
                    if (edge <= 0f)
                    {
                        pixels[y * w + x] = Color.clear;
                        continue;
                    }
                    // White-ish body (the tint multiplies it): a darker rim, a light midrib, one side a little shaded.
                    float shade = 0.78f + 0.22f * (1f - (u + 0.5f));
                    float rim = Mathf.Clamp01((half - Mathf.Abs(u)) / 0.07f);
                    float rib = Mathf.Abs(u) < 0.025f ? 1.18f : 1f;
                    float lum = Mathf.Clamp01(Mathf.Lerp(0.55f, 1f, rim) * shade * rib);
                    pixels[y * w + x] = new Color(lum, lum, lum, edge);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false);
            leafSprite = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), h, 0, SpriteMeshType.FullRect);
            leafSprite.hideFlags = HideFlags.DontSave;
        }
    }
}
