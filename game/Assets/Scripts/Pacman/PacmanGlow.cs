using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// A soft halo that follows an actor and tints the dark board around it, like a lantern.
    /// It can mirror a sprite's colour (ghosts change colour when frightened) and breathes slowly.
    /// Purely visual: it never reads or writes game state.
    /// </summary>
    public class PacmanGlow : MonoBehaviour
    {
        private static Sprite halo;

        [Tooltip("Halo radius in tiles.")]
        [SerializeField] private float radius = 2.5f;
        [Tooltip("Peak opacity of the halo, 0..1.")]
        [SerializeField] private float strength = 0.2f;
        [Tooltip("Breathing of the halo, as a fraction of its strength.")]
        [SerializeField] private float breathe = 0.2f;
        [Tooltip("Breaths per second.")]
        [SerializeField] private float rate = 0.8f;

        private SpriteRenderer halo2;
        private SpriteRenderer source;
        private Color fixedColour = Color.white;
        private float phase;

        /// <summary>Adds a halo under a parent. When source is given the halo mirrors its colour and visibility.</summary>
        public static PacmanGlow Attach(Transform parent, Color colour, float radius, float strength, SpriteRenderer source = null)
        {
            GameObject go = new GameObject("Glow");
            go.transform.SetParent(parent, false);
            PacmanGlow glow = go.AddComponent<PacmanGlow>();
            glow.radius = radius;
            glow.strength = strength;
            glow.fixedColour = colour;
            glow.source = source;
            glow.phase = Random.value * 6.28f;
            glow.halo2 = go.AddComponent<SpriteRenderer>();
            glow.halo2.sprite = Halo();
            glow.halo2.sortingOrder = 3;
            go.transform.localScale = Vector3.one * radius * 2f;
            return glow;
        }

        private static Sprite Halo()
        {
            if (halo == null)
            {
                const int size = 64;
                Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "PacHalo",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                Color32[] pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f) / size - 0.5f;
                        float dy = (y + 0.5f) / size - 0.5f;
                        float t = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 2f);
                        byte a = (byte)(Mathf.RoundToInt(255f * t * t * t));
                        pixels[y * size + x] = new Color32(255, 255, 255, a);
                    }
                }
                tex.SetPixels32(pixels);
                tex.Apply(false, true);
                halo = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
            }
            return halo;
        }

        private void LateUpdate()
        {
            Color c = source != null ? source.color : fixedColour;
            bool visible = source == null || source.enabled;
            float pulse = 1f + breathe * Mathf.Sin(Time.time * rate * 6.28f + phase);
            c.a = visible ? strength * pulse : 0f;
            halo2.color = c;
        }
    }
}
