using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// Footstep dust: while the object it sits on moves, small tan puffs rise at its feet and fade, so the farmer
    /// kicks up the dry earth of the finca. A fixed pool of puffs is reused; nothing is allocated during play.
    /// Purely cosmetic and read-only: it only watches the position.
    /// </summary>
    public class PacmanDust : MonoBehaviour
    {
        private const int Pool = 10;

        [Tooltip("Distance in tiles between two puffs.")]
        [SerializeField] private float spacing = 0.42f;
        [Tooltip("How long a puff lives, in seconds.")]
        [SerializeField] private float life = 0.55f;
        [Tooltip("Offset of the feet from the object's centre, in tiles.")]
        [SerializeField] private Vector2 feet = new Vector2(0f, -0.3f);
        [Tooltip("Dust colour; alpha is the strongest opacity.")]
        [SerializeField] private Color dust = new Color(0.86f, 0.7f, 0.5f, 0.75f);

        private static Sprite puffSprite;

        private Transform[] puffs;
        private SpriteRenderer[] renderers;
        private float[] age;
        private Vector2[] drift;
        private int next;
        private Vector3 lastSpawn;
        private bool hasLast;

        private void Awake()
        {
            EnsureSprite();
            puffs = new Transform[Pool];
            renderers = new SpriteRenderer[Pool];
            age = new float[Pool];
            drift = new Vector2[Pool];
            for (int i = 0; i < Pool; i++)
            {
                GameObject go = new GameObject("Puff");
                go.transform.SetParent(transform.parent, false);
                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = puffSprite;
                sr.sortingOrder = 2;
                go.SetActive(false);
                puffs[i] = go.transform;
                renderers[i] = sr;
                age[i] = -1f;
            }
        }

        private void OnDestroy()
        {
            if (puffs == null)
            {
                return;
            }
            for (int i = 0; i < puffs.Length; i++)
            {
                if (puffs[i] != null)
                {
                    Destroy(puffs[i].gameObject);
                }
            }
        }

        private void LateUpdate()
        {
            Vector3 p = transform.position;
            if (!hasLast || (p - lastSpawn).sqrMagnitude > 9f)
            {
                // First frame or a teleport (tunnel, restart): start over from here.
                lastSpawn = p;
                hasLast = true;
            }
            else if ((p - lastSpawn).sqrMagnitude >= spacing * spacing)
            {
                Spawn(p);
                lastSpawn = p;
            }

            for (int i = 0; i < puffs.Length; i++)
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
                    puffs[i].gameObject.SetActive(false);
                    continue;
                }
                float scale = Mathf.Lerp(0.2f, 0.65f, 1f - (1f - t) * (1f - t));
                puffs[i].localScale = new Vector3(scale, scale, 1f);
                puffs[i].position += new Vector3(drift[i].x, drift[i].y, 0f) * Time.deltaTime;
                Color c = dust;
                c.a = dust.a * (1f - t) * Mathf.Clamp01(t * 8f + 0.3f);
                renderers[i].color = c;
            }
        }

        private void Spawn(Vector3 at)
        {
            int i = next;
            next = (next + 1) % Pool;
            age[i] = 0f;
            puffs[i].gameObject.SetActive(true);
            puffs[i].position = new Vector3(at.x + feet.x + Random.Range(-0.06f, 0.06f), at.y + feet.y + Random.Range(-0.04f, 0.04f), 0f);
            puffs[i].localScale = new Vector3(0.18f, 0.18f, 1f);
            drift[i] = new Vector2(Random.Range(-0.1f, 0.1f), Random.Range(0.12f, 0.28f));
            renderers[i].color = dust;
        }

        private static void EnsureSprite()
        {
            if (puffSprite != null)
            {
                return;
            }
            const int n = 32;
            Texture2D texture = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "PacmanDust",
                hideFlags = HideFlags.DontSave,
            };
            Color[] pixels = new Color[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f;
                    float v = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float lump = 0.75f + 0.25f * Mathf.Sin(u * 7f + v * 5f);
                    float a = Mathf.Clamp01(1f - r / lump);
                    a = a * a * (3f - 2f * a);
                    pixels[y * n + x] = new Color(1f, 1f, 1f, a);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false);
            puffSprite = Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n, 0, SpriteMeshType.FullRect);
            puffSprite.hideFlags = HideFlags.DontSave;
        }
    }
}
