using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// Boot prints in the dirt: while the object it sits on walks, it leaves alternating left and right prints that
    /// darken the earth and fade away over a few seconds, so the farmer's route across the finca can be read.
    /// A fixed pool is reused; nothing is allocated during play. Purely cosmetic: it only watches the position.
    /// </summary>
    public class PacmanTracks : MonoBehaviour
    {
        private const int Pool = 16;

        [Tooltip("Distance in tiles between two prints.")]
        [SerializeField] private float spacing = 0.5f;
        [Tooltip("How long a print lasts, in seconds.")]
        [SerializeField] private float life = 4.5f;
        [Tooltip("Offset of the feet from the object's centre, in tiles.")]
        [SerializeField] private Vector2 feet = new Vector2(0f, -0.3f);
        [Tooltip("How far left and right of the path centre each foot lands, in tiles.")]
        [SerializeField] private float stride = 0.11f;
        [Tooltip("Size of a print in tiles (height).")]
        [SerializeField] private float size = 0.32f;
        [Tooltip("Print colour; alpha is the strongest opacity.")]
        [SerializeField] private Color tint = new Color(0.08f, 0.04f, 0.02f, 0.6f);

        private static Sprite printSprite;

        private Transform[] prints;
        private SpriteRenderer[] renderers;
        private float[] age;
        private int next;
        private bool left;
        private Vector3 lastSpawn;
        private bool hasLast;

        private void Awake()
        {
            EnsureSprite();
            prints = new Transform[Pool];
            renderers = new SpriteRenderer[Pool];
            age = new float[Pool];
            for (int i = 0; i < Pool; i++)
            {
                GameObject go = new GameObject("Print");
                go.transform.SetParent(transform.parent, false);
                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = printSprite;
                sr.sortingOrder = -4;
                go.SetActive(false);
                prints[i] = go.transform;
                renderers[i] = sr;
                age[i] = -1f;
            }
        }

        private void OnDestroy()
        {
            if (prints == null)
            {
                return;
            }
            for (int i = 0; i < prints.Length; i++)
            {
                if (prints[i] != null)
                {
                    Destroy(prints[i].gameObject);
                }
            }
        }

        private void LateUpdate()
        {
            Vector3 p = transform.position;
            Vector3 delta = p - lastSpawn;
            if (!hasLast || delta.sqrMagnitude > 9f)
            {
                // First frame or a teleport (tunnel, restart): start over from here.
                lastSpawn = p;
                hasLast = true;
            }
            else if (delta.sqrMagnitude >= spacing * spacing)
            {
                Spawn(p, delta.normalized);
                lastSpawn = p;
            }

            for (int i = 0; i < prints.Length; i++)
            {
                if (age[i] < 0f)
                {
                    continue;
                }
                age[i] += Time.deltaTime;
                float t = age[i] / life;
                if (t >= 1f)
                {
                    age[i] = -1f;
                    prints[i].gameObject.SetActive(false);
                    continue;
                }
                Color c = tint;
                c.a = tint.a * Mathf.Clamp01(t * 12f) * (1f - t * t);
                renderers[i].color = c;
            }
        }

        private void Spawn(Vector3 at, Vector3 dir)
        {
            int i = next;
            next = (next + 1) % Pool;
            age[i] = 0f;
            left = !left;
            Vector3 side = new Vector3(-dir.y, dir.x, 0f) * (left ? stride : -stride);
            prints[i].gameObject.SetActive(true);
            prints[i].position = new Vector3(at.x + feet.x + side.x, at.y + feet.y + side.y, 0f);
            prints[i].rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
            prints[i].localScale = new Vector3(size * (left ? 1f : -1f), size, 1f);
            renderers[i].color = new Color(tint.r, tint.g, tint.b, 0f);
        }

        private static void EnsureSprite()
        {
            if (printSprite != null)
            {
                return;
            }
            // A boot sole facing up: a long ellipse for the sole with a narrower waist, and a rounder heel below it.
            const int w = 16;
            const int h = 28;
            Color[] px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = (x - 7.5f) / 6.2f;
                    float sole = dx * dx + Mathf.Pow((y - 17f) / 9.2f, 2f);
                    float waist = Mathf.Abs(y - 10f) < 2.5f ? 0.35f : 0f;
                    float soleDx = (x - 7.5f) / (6.2f - waist * 6f);
                    sole = soleDx * soleDx + Mathf.Pow((y - 17f) / 9.2f, 2f);
                    float hx = (x - 7.5f) / 4.8f;
                    float heel = hx * hx + Mathf.Pow((y - 5.5f) / 4.6f, 2f);
                    bool inside = sole <= 1f || heel <= 1f;
                    // A few tread bars cut into the sole make it read as a boot print rather than a blob.
                    bool tread = y > 11 && y < 25 && y % 4 == 0 && sole < 0.7f;
                    px[y * w + x] = inside && !tread ? Color.white : new Color(1f, 1f, 1f, 0f);
                }
            }
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels(px);
            tex.Apply();
            printSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), h);
        }
    }
}
