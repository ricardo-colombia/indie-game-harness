using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// A light highland drizzle for the rainy afternoon levels: thin slanted streaks that fall across whatever the camera
    /// shows, whatever the zoom (sizes and speeds are relative to the view). The scenery sets how much rain there is
    /// and the drizzle fades in and out. A fixed pool of streaks is reused; nothing is allocated during play.
    /// </summary>
    public class PacmanRain : MonoBehaviour
    {
        private const int Streaks = 150;

        [Tooltip("Sideways drift of the rain: horizontal distance per unit of fall.")]
        [SerializeField] private float slant = 0.22f;
        [Tooltip("Strongest opacity of a streak.")]
        [SerializeField] private float opacity = 0.45f;
        [Tooltip("Seconds the rain takes to fade in or out.")]
        [SerializeField] private float fadeSeconds = 3f;

        private static Sprite streakSprite;

        private Transform[] streaks;
        private SpriteRenderer[] renderers;
        private float[] seedX;
        private float[] seedY;
        private float[] speed;
        private float amount;
        private float target;
        private Camera cam;

        /// <summary>Creates the rain under a parent; it starts dry.</summary>
        public static PacmanRain Build(Transform parent)
        {
            GameObject root = new GameObject("Rain");
            root.transform.SetParent(parent, false);
            PacmanRain self = root.AddComponent<PacmanRain>();
            EnsureSprite();
            self.streaks = new Transform[Streaks];
            self.renderers = new SpriteRenderer[Streaks];
            self.seedX = new float[Streaks];
            self.seedY = new float[Streaks];
            self.speed = new float[Streaks];
            for (int i = 0; i < Streaks; i++)
            {
                GameObject go = new GameObject("Streak");
                go.transform.SetParent(root.transform, false);
                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = streakSprite;
                sr.sortingOrder = 45;
                sr.color = new Color(0.82f, 0.9f, 1f, 0f);
                go.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan(self.slant) * Mathf.Rad2Deg);
                go.SetActive(false);
                self.streaks[i] = go.transform;
                self.renderers[i] = sr;
                self.seedX[i] = PacmanToon.Hash(i, 501);
                self.seedY[i] = PacmanToon.Hash(i, 503);
                self.speed[i] = Mathf.Lerp(0.9f, 1.5f, PacmanToon.Hash(i, 505));
            }
            return self;
        }

        /// <summary>Sets how much it rains: 0 dry, 1 steady drizzle.</summary>
        public void SetAmount(float value)
        {
            target = Mathf.Clamp01(value);
        }

        private void Update()
        {
            amount = Mathf.MoveTowards(amount, target, Time.unscaledDeltaTime / fadeSeconds);
            bool on = amount > 0.001f;
            if (cam == null)
            {
                cam = Camera.main;
            }
            if (!on || cam == null)
            {
                if (streaks[0].gameObject.activeSelf)
                {
                    foreach (Transform t in streaks)
                    {
                        t.gameObject.SetActive(false);
                    }
                }
                return;
            }

            float height = cam.orthographicSize;
            float width = height * cam.aspect;
            Vector3 centre = cam.transform.position;
            float length = height * 0.11f;
            float thickness = height * 0.0045f * 16f; // the sprite is 2 px wide at 32 px per unit
            int visible = Mathf.CeilToInt(Streaks * amount);
            for (int i = 0; i < Streaks; i++)
            {
                bool active = i < visible;
                if (streaks[i].gameObject.activeSelf != active)
                {
                    streaks[i].gameObject.SetActive(active);
                }
                if (!active)
                {
                    continue;
                }
                // fall: 0 at the top of the view, 1 at the bottom, then it wraps.
                float fall = Mathf.Repeat(seedY[i] + Time.time * speed[i] * 0.9f, 1f);
                float y = height * 1.15f - fall * height * 2.3f;
                float x = (seedX[i] * 2f - 1f) * width * 1.2f + fall * height * 2.3f * slant;
                streaks[i].position = new Vector3(centre.x + x, centre.y + y, 0f);
                streaks[i].localScale = new Vector3(thickness, length, 1f);
                float edge = Mathf.Clamp01(fall * 8f) * Mathf.Clamp01((1f - fall) * 8f);
                renderers[i].color = new Color(0.82f, 0.9f, 1f, opacity * amount * edge);
            }
        }

        private static void EnsureSprite()
        {
            if (streakSprite != null)
            {
                return;
            }
            // A vertical streak 1 unit tall: transparent at the top, brightest at the head (bottom).
            const int w = 2;
            const int h = 32;
            Color[] px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float a = Mathf.Pow(1f - y / (h - 1f), 1.6f);
                for (int x = 0; x < w; x++)
                {
                    px[y * w + x] = new Color(1f, 1f, 1f, a);
                }
            }
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels(px);
            tex.Apply();
            streakSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), h);
        }
    }
}
