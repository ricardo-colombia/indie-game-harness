using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// Night-time fireflies drifting over the coffee rows: small warm dots that wander in slow loops and blink.
    /// The scenery sets how many glow (none by day); they fade in and out. A fixed pool is reused.
    /// </summary>
    public class PacmanFireflies : MonoBehaviour
    {
        private const int Count = 34;

        [Tooltip("Size of a firefly glow in tiles.")]
        [SerializeField] private float size = 0.5f;
        [Tooltip("How far a firefly wanders from its home spot, in tiles.")]
        [SerializeField] private float wander = 1.6f;
        [Tooltip("Seconds the fireflies take to fade in or out.")]
        [SerializeField] private float fadeSeconds = 2.5f;

        private Transform[] flies;
        private SpriteRenderer[] renderers;
        private Vector2[] home;
        private float amount;
        private float target;

        /// <summary>Creates the fireflies under a parent; they start dark.</summary>
        public static PacmanFireflies Build(Transform parent)
        {
            GameObject root = new GameObject("Fireflies");
            root.transform.SetParent(parent, false);
            PacmanFireflies self = root.AddComponent<PacmanFireflies>();
            self.flies = new Transform[Count];
            self.renderers = new SpriteRenderer[Count];
            self.home = new Vector2[Count];
            for (int i = 0; i < Count; i++)
            {
                GameObject go = new GameObject("Firefly");
                go.transform.SetParent(root.transform, false);
                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = PacmanSprites.SoftDisc;
                sr.sortingOrder = 30; // over the bushes, under the weather and the interface
                sr.color = new Color(1f, 0.95f, 0.45f, 0f);
                go.SetActive(false);
                self.flies[i] = go.transform;
                self.renderers[i] = sr;
                self.home[i] = new Vector2(
                    Mathf.Lerp(-1f, PacmanMaze.Width, PacmanToon.Hash(i, 601)),
                    -Mathf.Lerp(-1f, PacmanMaze.Height, PacmanToon.Hash(i, 603)));
            }
            return self;
        }

        /// <summary>Sets how many fireflies glow: 0 none, 1 all.</summary>
        public void SetAmount(float value)
        {
            target = Mathf.Clamp01(value);
        }

        private void Update()
        {
            amount = Mathf.MoveTowards(amount, target, Time.unscaledDeltaTime / fadeSeconds);
            bool on = amount > 0.001f;
            if (!on)
            {
                if (flies[0].gameObject.activeSelf)
                {
                    foreach (Transform t in flies)
                    {
                        t.gameObject.SetActive(false);
                    }
                }
                return;
            }
            float time = Time.time;
            for (int i = 0; i < Count; i++)
            {
                if (!flies[i].gameObject.activeSelf)
                {
                    flies[i].gameObject.SetActive(true);
                }
                float p = PacmanToon.Hash(i, 605) * 20f;
                float speed = Mathf.Lerp(0.18f, 0.4f, PacmanToon.Hash(i, 607));
                Vector2 at = home[i] + new Vector2(
                    Mathf.Sin(time * speed + p) * wander + Mathf.Sin(time * speed * 2.3f + p * 1.7f) * wander * 0.3f,
                    Mathf.Cos(time * speed * 0.9f + p * 1.3f) * wander * 0.8f);
                flies[i].position = new Vector3(at.x, at.y, 0f);
                float blink = Mathf.Pow(Mathf.Clamp01(Mathf.Sin(time * Mathf.Lerp(0.8f, 1.6f, PacmanToon.Hash(i, 609)) + p) * 0.5f + 0.5f), 2.2f);
                float s = size * (0.7f + 0.5f * blink);
                flies[i].localScale = new Vector3(s, s, 1f);
                renderers[i].color = new Color(1f, 0.95f, 0.45f, (0.15f + 0.85f * blink) * amount);
            }
        }
    }
}
