using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// A few butterflies (yellow, orange and morpho blue) that wander lazily over the plantation. Each flaps by
    /// squeezing its wings and flies along a slow looping path, turning toward where it is going. One procedural
    /// sprite tinted per butterfly; small, sparse and below the characters.
    /// </summary>
    public class PacmanButterflies : MonoBehaviour
    {
        private const int Count = 4;

        [Tooltip("Size of a butterfly in tiles.")]
        [SerializeField] private float size = 0.7f;
        [Tooltip("Wing beats per second.")]
        [SerializeField] private float flap = 7f;

        private static Sprite wingSprite;

        private Transform[] bodies;
        private SpriteRenderer[] renderers;
        private Vector2[] centre;
        private Vector2[] radius;
        private float[] speed;
        private float[] phase;
        private Vector3[] last;

        /// <summary>Creates the butterflies over the maze under a parent.</summary>
        public static PacmanButterflies Build(Transform parent)
        {
            GameObject root = new GameObject("Butterflies");
            root.transform.SetParent(parent, false);
            PacmanButterflies self = root.AddComponent<PacmanButterflies>();
            EnsureSprite();
            self.bodies = new Transform[Count];
            self.renderers = new SpriteRenderer[Count];
            self.centre = new Vector2[Count];
            self.radius = new Vector2[Count];
            self.speed = new float[Count];
            self.phase = new float[Count];
            self.last = new Vector3[Count];
            Color[] tints =
            {
                new Color(1f, 0.86f, 0.25f), new Color(1f, 0.55f, 0.18f), new Color(0.3f, 0.6f, 1f), new Color(1f, 0.92f, 0.5f),
            };
            for (int i = 0; i < Count; i++)
            {
                GameObject go = new GameObject("Butterfly");
                go.transform.SetParent(root.transform, false);
                SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = wingSprite;
                renderer.sortingOrder = 3;
                renderer.color = tints[i % tints.Length];
                self.bodies[i] = go.transform;
                self.renderers[i] = renderer;
                self.centre[i] = new Vector2(
                    Mathf.Lerp(3f, PacmanMaze.Width - 3f, (i + PacmanToon.Hash(i, 51) * 0.7f) / Count),
                    -Mathf.Lerp(3f, PacmanMaze.Height - 3f, PacmanToon.Hash(i, 53)));
                self.radius[i] = new Vector2(Mathf.Lerp(3f, 7f, PacmanToon.Hash(i, 55)), Mathf.Lerp(2f, 5f, PacmanToon.Hash(i, 57)));
                self.speed[i] = Mathf.Lerp(0.18f, 0.32f, PacmanToon.Hash(i, 59));
                self.phase[i] = PacmanToon.Hash(i, 61) * 6.283f;
                self.last[i] = go.transform.localPosition;
            }
            return self;
        }

        private void Update()
        {
            if (bodies == null)
            {
                return;
            }
            for (int i = 0; i < bodies.Length; i++)
            {
                float t = Time.time * speed[i] + phase[i];
                // A slow figure-of-eight drift plus a small flutter.
                Vector3 p = new Vector3(
                    centre[i].x + Mathf.Sin(t) * radius[i].x + Mathf.Sin(t * 7f) * 0.12f,
                    centre[i].y + Mathf.Sin(t * 2f) * radius[i].y + Mathf.Cos(t * 9f) * 0.12f, 0f);
                Vector3 d = p - last[i];
                if (d.sqrMagnitude > 1e-6f)
                {
                    float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f;
                    bodies[i].localRotation = Quaternion.Euler(0f, 0f, angle);
                }
                last[i] = p;
                bodies[i].localPosition = p;
                float beat = 0.3f + 0.7f * Mathf.Abs(Mathf.Sin(Time.time * flap * 0.5f * 3.1416f + phase[i]));
                bodies[i].localScale = new Vector3(size * beat, size, 1f);
            }
        }

        private static void EnsureSprite()
        {
            if (wingSprite != null)
            {
                return;
            }
            const int w = 32;
            const int h = 32;
            Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "PacmanButterfly",
                hideFlags = HideFlags.DontSave,
            };
            Color[] pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w * 2f - 1f;  // -1..1 across the wings
                    float v = (y + 0.5f) / h * 2f - 1f;  // -1 tail .. 1 head
                    float au = Mathf.Abs(u);
                    // Upper wings: a larger lobe above the middle; lower wings: a smaller lobe below.
                    float upper = Ellipse(au - 0.5f, v - 0.28f, 0.48f, 0.5f);
                    float lower = Ellipse(au - 0.4f, v + 0.42f, 0.34f, 0.38f);
                    float wing = Mathf.Max(upper, lower);
                    float body = Ellipse(u, v + 0.05f, 0.07f, 0.62f);
                    Color c = Color.clear;
                    if (wing > 0f)
                    {
                        // White-ish wing (tinted per butterfly) with a dark rim and a light vein near the body.
                        float rim = Mathf.Clamp01(wing * 4f);
                        float lum = Mathf.Lerp(0.35f, 1f, rim) * (0.85f + 0.15f * (1f - au));
                        c = new Color(lum, lum, lum, Mathf.Clamp01(wing * 6f));
                    }
                    if (body > 0f)
                    {
                        float a = Mathf.Clamp01(body * 8f);
                        c = Color.Lerp(c, new Color(0.12f, 0.08f, 0.05f, 1f), a);
                        c.a = Mathf.Max(c.a, a);
                    }
                    pixels[y * w + x] = c;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false);
            wingSprite = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w, 0, SpriteMeshType.FullRect);
            wingSprite.hideFlags = HideFlags.DontSave;
        }

        /// <summary>Positive inside an ellipse of half-axes (a, b), growing toward the centre.</summary>
        private static float Ellipse(float x, float y, float a, float b)
        {
            return 1f - Mathf.Sqrt((x * x) / (a * a) + (y * y) / (b * b));
        }
    }
}
