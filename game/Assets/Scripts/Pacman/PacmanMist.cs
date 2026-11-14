using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// Mountain mist: a few big, soft, pale clouds that drift slowly across the plantation, above the bushes and
    /// below the beans and characters, so the finca feels like a cool highland morning. The cloud texture is painted
    /// once in code (layered noise with a soft falloff); there are no assets.
    /// </summary>
    public class PacmanMist : MonoBehaviour
    {
        private const int Clouds = 7;

        [Tooltip("Highest opacity of a cloud (0..1). Kept low so the maze always stays readable.")]
        [SerializeField] private float opacity = 0.1f;
        [Tooltip("Drift speed range in tiles per second.")]
        [SerializeField] private Vector2 speed = new Vector2(0.18f, 0.42f);

        private static Sprite cloudSprite;

        private Transform[] clouds;
        private float[] speeds;
        private float[] bobPhase;
        private float[] baseY;
        private float left;
        private float right;

        /// <summary>Creates the drifting clouds over the maze under a parent.</summary>
        public static PacmanMist Build(Transform parent)
        {
            GameObject root = new GameObject("Mist");
            root.transform.SetParent(parent, false);
            PacmanMist self = root.AddComponent<PacmanMist>();
            EnsureSprite();

            self.left = -9f;
            self.right = PacmanMaze.Width + 8f;
            self.clouds = new Transform[Clouds];
            self.speeds = new float[Clouds];
            self.bobPhase = new float[Clouds];
            self.baseY = new float[Clouds];
            for (int i = 0; i < Clouds; i++)
            {
                GameObject cloud = new GameObject("Cloud");
                cloud.transform.SetParent(root.transform, false);
                float width = Mathf.Lerp(9f, 15f, PacmanToon.Hash(i, 3));
                float height = width * Mathf.Lerp(0.28f, 0.45f, PacmanToon.Hash(i, 5));
                cloud.transform.localScale = new Vector3(width / 8f, height / 4f, 1f); // the sprite is 8 x 4 units
                float y = -Mathf.Lerp(1.5f, PacmanMaze.Height - 1.5f, (i + PacmanToon.Hash(i, 7) * 0.6f) / Clouds);
                float x = Mathf.Lerp(self.left, self.right, PacmanToon.Hash(i, 9));
                cloud.transform.localPosition = new Vector3(x, y, 0f);
                SpriteRenderer renderer = cloud.AddComponent<SpriteRenderer>();
                renderer.sprite = cloudSprite;
                renderer.sortingOrder = -1;
                renderer.color = new Color(0.86f, 0.93f, 0.9f, self.opacity * Mathf.Lerp(0.6f, 1f, PacmanToon.Hash(i, 11)));
                self.clouds[i] = cloud.transform;
                self.speeds[i] = Mathf.Lerp(self.speed.x, self.speed.y, PacmanToon.Hash(i, 13)) * (PacmanToon.Hash(i, 15) < 0.25f ? -1f : 1f);
                self.bobPhase[i] = PacmanToon.Hash(i, 17) * 6.283f;
                self.baseY[i] = y;
            }
            return self;
        }

        private void Update()
        {
            if (clouds == null)
            {
                return;
            }
            for (int i = 0; i < clouds.Length; i++)
            {
                Vector3 p = clouds[i].localPosition;
                p.x += speeds[i] * Time.deltaTime;
                if (p.x > right)
                {
                    p.x = left;
                }
                else if (p.x < left)
                {
                    p.x = right;
                }
                p.y = baseY[i] + Mathf.Sin(Time.time * 0.2f + bobPhase[i]) * 0.35f;
                clouds[i].localPosition = p;
            }
        }

        private static void EnsureSprite()
        {
            if (cloudSprite != null)
            {
                return;
            }
            const int w = 128;
            const int h = 64;
            Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "PacmanMist",
                hideFlags = HideFlags.DontSave,
            };
            Color[] pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w * 2f - 1f;
                    float v = (y + 0.5f) / h * 2f - 1f;
                    float falloff = Mathf.Clamp01(1f - (u * u + v * v * 1.1f));
                    falloff = falloff * falloff * (3f - 2f * falloff);
                    float n = Noise(x * 0.05f, y * 0.09f) * 0.75f + Noise(x * 0.12f, y * 0.2f) * 0.25f;
                    float a = Mathf.Clamp01(falloff * falloff * (0.5f + n * 0.6f));
                    pixels[y * w + x] = new Color(1f, 1f, 1f, a);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false);
            cloudSprite = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f, 0, SpriteMeshType.FullRect);
            cloudSprite.hideFlags = HideFlags.DontSave;
        }

        private static float Noise(float x, float y)
        {
            int ix = Mathf.FloorToInt(x);
            int iy = Mathf.FloorToInt(y);
            float fx = x - ix;
            float fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            return Mathf.Lerp(
                Mathf.Lerp(PacmanToon.Hash(ix, iy), PacmanToon.Hash(ix + 1, iy), fx),
                Mathf.Lerp(PacmanToon.Hash(ix, iy + 1), PacmanToon.Hash(ix + 1, iy + 1), fx), fy);
        }
    }
}
