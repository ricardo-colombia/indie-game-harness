using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// Sunbeams: a few long, soft, slanted shafts of light that slip through the canopy from the upper left and
    /// breathe slowly. They are tinted by the time of day (warm at dawn and dusk, white at noon, faint moon-blue at
    /// night) and kept very faint so the maze stays readable. One procedural sprite, no assets.
    /// </summary>
    public class PacmanRays : MonoBehaviour
    {
        private const int Beams = 5;

        [Tooltip("Highest opacity of a beam (0..1).")]
        [SerializeField] private float opacity = 0.11f;
        [Tooltip("Angle of the beams from vertical, in degrees (positive leans to the right going down).")]
        [SerializeField] private float lean = 28f;

        private static Sprite beamSprite;

        private SpriteRenderer[] beams;
        private float[] phase;
        private float[] strength;
        private Color tint = Color.white;

        /// <summary>Creates the beams over the maze under a parent.</summary>
        public static PacmanRays Build(Transform parent)
        {
            GameObject root = new GameObject("Sunbeams");
            root.transform.SetParent(parent, false);
            PacmanRays self = root.AddComponent<PacmanRays>();
            EnsureSprite();
            self.beams = new SpriteRenderer[Beams];
            self.phase = new float[Beams];
            self.strength = new float[Beams];
            float width = PacmanMaze.Width;
            float height = PacmanMaze.Height;
            for (int i = 0; i < Beams; i++)
            {
                GameObject beam = new GameObject("Beam");
                beam.transform.SetParent(root.transform, false);
                float x = Mathf.Lerp(-3f, width + 3f, (i + PacmanToon.Hash(i, 21) * 0.7f) / Beams);
                beam.transform.localPosition = new Vector3(x, -height * 0.5f, 0f);
                beam.transform.localRotation = Quaternion.Euler(0f, 0f, self.lean);
                float w = Mathf.Lerp(2.4f, 5.2f, PacmanToon.Hash(i, 23));
                beam.transform.localScale = new Vector3(w, height * 1.15f, 1f);
                SpriteRenderer renderer = beam.AddComponent<SpriteRenderer>();
                renderer.sprite = beamSprite;
                renderer.sortingOrder = -1;
                self.beams[i] = renderer;
                self.phase[i] = PacmanToon.Hash(i, 25) * 6.283f;
                self.strength[i] = Mathf.Lerp(0.55f, 1f, PacmanToon.Hash(i, 27));
            }
            return self;
        }

        /// <summary>Sets the colour of the light (alpha is the overall strength for the time of day).</summary>
        public void SetTint(Color colour)
        {
            tint = colour;
        }

        private void Update()
        {
            if (beams == null)
            {
                return;
            }
            for (int i = 0; i < beams.Length; i++)
            {
                float breathe = 0.65f + 0.35f * Mathf.Sin(Time.time * 0.35f + phase[i]);
                Color c = tint;
                c.a = opacity * tint.a * strength[i] * breathe;
                beams[i].color = c;
            }
        }

        private static void EnsureSprite()
        {
            if (beamSprite != null)
            {
                return;
            }
            const int w = 32;
            const int h = 128;
            Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "PacmanBeam",
                hideFlags = HideFlags.DontSave,
            };
            Color[] pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = y / (h - 1f);
                // Brighter toward the top (the source), fading out at both ends so no beam has a visible edge.
                float along = Mathf.SmoothStep(0f, 1f, v / 0.3f) * Mathf.SmoothStep(0f, 1f, (1f - v) / 0.12f) * (0.45f + 0.55f * v);
                for (int x = 0; x < w; x++)
                {
                    float u = Mathf.Abs((x + 0.5f) / w * 2f - 1f);
                    float across = 1f - Mathf.SmoothStep(0f, 1f, u);
                    pixels[y * w + x] = new Color(1f, 1f, 1f, across * across * along);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false);
            beamSprite = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f, 0, SpriteMeshType.FullRect);
            beamSprite.hideFlags = HideFlags.DontSave;
        }
    }
}
