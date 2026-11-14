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

        private const int Ripples = 44;

        private static Sprite streakSprite;
        private static Sprite rippleSprite;
        private static Sprite puddleSprite;
        private SpriteRenderer[] puddles;
        [Tooltip("Chance that an open cell holds a puddle while it rains.")]
        [SerializeField, Range(0f, 1f)] private float puddleChance = 0.2f;
        private Transform[] ripples;
        private SpriteRenderer[] rippleRenderers;

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
            var puddleList = new System.Collections.Generic.List<SpriteRenderer>();
            for (int y = 0; y < PacmanMaze.Height; y++)
            {
                for (int x = 0; x < PacmanMaze.Width; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    if (PacmanMaze.IsWall(cell) || PacmanMaze.IsGhostHouse(cell))
                    {
                        continue;
                    }
                    int id = y * 64 + x;
                    if (PacmanToon.Hash(id, 541) > self.puddleChance)
                    {
                        continue;
                    }
                    GameObject pg = new GameObject("Puddle");
                    pg.transform.SetParent(root.transform, false);
                    SpriteRenderer pr = pg.AddComponent<SpriteRenderer>();
                    pr.sprite = puddleSprite;
                    pr.sortingOrder = -4;
                    pr.color = new Color(1f, 1f, 1f, 0f);
                    float w = Mathf.Lerp(0.6f, 1.0f, PacmanToon.Hash(id, 543));
                    pg.transform.position = new Vector3(
                        x + (PacmanToon.Hash(id, 545) - 0.5f) * 0.5f,
                        -y + (PacmanToon.Hash(id, 547) - 0.5f) * 0.5f - 0.18f, 0f);
                    pg.transform.localScale = new Vector3(w, w, 1f);
                    pg.SetActive(false);
                    puddleList.Add(pr);
                }
            }
            self.puddles = puddleList.ToArray();

            self.ripples = new Transform[Ripples];
            self.rippleRenderers = new SpriteRenderer[Ripples];
            for (int i = 0; i < Ripples; i++)
            {
                GameObject go = new GameObject("Ripple");
                go.transform.SetParent(root.transform, false);
                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = rippleSprite;
                sr.sortingOrder = -4; // on the ground, above the dirt fill and under the bushes and walkers
                sr.color = new Color(0.85f, 0.93f, 1f, 0f);
                go.SetActive(false);
                self.ripples[i] = go.transform;
                self.rippleRenderers[i] = sr;
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
                    foreach (Transform t in ripples)
                    {
                        t.gameObject.SetActive(false);
                    }
                    foreach (SpriteRenderer pd in puddles)
                    {
                        pd.gameObject.SetActive(false);
                    }
                }
                return;
            }

            float height = cam.orthographicSize;
            float width = height * cam.aspect;
            Vector3 centre = cam.transform.position;
            float length = height * 0.11f;
            float thickness = height * 0.0045f * 16f; // the sprite is 2 px wide at 32 px per unit
            UpdateRipples(height, width, centre);
            float wet = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((amount - 0.15f) / 0.85f));
            foreach (SpriteRenderer pd in puddles)
            {
                if (!pd.gameObject.activeSelf)
                {
                    pd.gameObject.SetActive(true);
                }
                pd.color = new Color(1f, 1f, 1f, wet);
            }
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

        // Rings that spread on the wet ground and fade, each reborn somewhere new in the view when it ends.
        private void UpdateRipples(float height, float width, Vector3 centre)
        {
            int visible = Mathf.CeilToInt(Ripples * amount);
            for (int i = 0; i < Ripples; i++)
            {
                bool active = i < visible;
                if (ripples[i].gameObject.activeSelf != active)
                {
                    ripples[i].gameObject.SetActive(active);
                }
                if (!active)
                {
                    continue;
                }
                float life = 0.9f + 0.6f * PacmanToon.Hash(i, 521);
                float t = Time.time / life + PacmanToon.Hash(i, 523) * 10f;
                int cycle = Mathf.FloorToInt(t);
                float age = t - cycle;
                float px = PacmanToon.Hash(i * 31 + cycle, 527) * 2f - 1f;
                float py = PacmanToon.Hash(i * 31 + cycle, 529) * 2f - 1f;
                ripples[i].position = new Vector3(centre.x + px * width * 0.95f, centre.y + py * height * 0.95f, 0f);
                float size = height * (0.02f + 0.075f * age);
                ripples[i].localScale = new Vector3(size, size * 0.5f, 1f);
                float a = Mathf.Clamp01(age * 10f) * (1f - age) * (1f - age);
                rippleRenderers[i].color = new Color(0.85f, 0.93f, 1f, 0.9f * amount * a);
            }
        }

        private static void EnsureSprite()
        {
            if (puddleSprite == null)
            {
                // A shallow puddle: dark wet ground with a pale sky reflection along its upper rim, soft at the edge.
                const int pw = 48;
                const int ph = 24;
                Color[] pp = new Color[pw * ph];
                for (int y = 0; y < ph; y++)
                {
                    for (int x = 0; x < pw; x++)
                    {
                        float dx = (x + 0.5f) / pw * 2f - 1f;
                        float dy = (y + 0.5f) / ph * 2f - 1f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float edge = Mathf.Clamp01((1f - d) / 0.18f);
                        float sky = Mathf.Clamp01(dy * 0.9f + 0.1f) * Mathf.Clamp01((1f - d) / 0.5f);
                        Color c = Color.Lerp(new Color(0.1f, 0.12f, 0.17f), new Color(0.62f, 0.72f, 0.88f), sky * 0.75f);
                        c.a = edge * Mathf.Lerp(0.6f, 0.8f, sky);
                        pp[y * pw + x] = c;
                    }
                }
                Texture2D pt = new Texture2D(pw, ph, TextureFormat.RGBA32, false);
                pt.filterMode = FilterMode.Bilinear;
                pt.wrapMode = TextureWrapMode.Clamp;
                pt.SetPixels(pp);
                pt.Apply();
                puddleSprite = Sprite.Create(pt, new Rect(0, 0, pw, ph), new Vector2(0.5f, 0.5f), pw);
            }
            if (rippleSprite == null)
            {
                // A thin ring, 1 unit wide, squashed vertically by the caller to read as lying on the ground.
                const int n = 32;
                Color[] rp = new Color[n * n];
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + 0.5f) / n * 2f - 1f;
                        float dy = (y + 0.5f) / n * 2f - 1f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.8f) / 0.14f);
                        rp[y * n + x] = new Color(1f, 1f, 1f, a);
                    }
                }
                Texture2D rt = new Texture2D(n, n, TextureFormat.RGBA32, false);
                rt.filterMode = FilterMode.Bilinear;
                rt.wrapMode = TextureWrapMode.Clamp;
                rt.SetPixels(rp);
                rt.Apply();
                rippleSprite = Sprite.Create(rt, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            }
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
