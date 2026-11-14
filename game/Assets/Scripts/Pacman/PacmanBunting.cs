using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// Festive bunting along the top fence: a rope that sags between the posts in scallops, hung with
    /// yellow, blue and red pennants (the colours of the Colombian flag) that flutter in the breeze.
    /// One procedural pennant sprite tinted per flag; the rope is stretched white squares.
    /// </summary>
    public class PacmanBunting : MonoBehaviour
    {
        [Tooltip("Distance in tiles between the posts the rope is tied to.")]
        [SerializeField] private float span = 4f;
        [Tooltip("How far the rope sags between posts, in tiles.")]
        [SerializeField] private float sag = 0.3f;
        [Tooltip("Pennant width in tiles.")]
        [SerializeField] private float flagWidth = 0.34f;
        [Tooltip("Sway of the pennants in degrees.")]
        [SerializeField] private float flutter = 9f;

        private static Sprite flagSprite;
        private static Sprite squareSprite;

        private Transform[] flags;
        private float[] phase;

        /// <summary>Creates the bunting along the top of the maze under a parent.</summary>
        public static PacmanBunting Build(Transform parent)
        {
            GameObject root = new GameObject("Bunting");
            root.transform.SetParent(parent, false);
            PacmanBunting self = root.AddComponent<PacmanBunting>();
            EnsureSprites();

            float left = -1f;
            float right = PacmanMaze.Width;
            float ropeY = 1.42f;
            System.Collections.Generic.List<Transform> list = new System.Collections.Generic.List<Transform>();
            System.Collections.Generic.List<float> phases = new System.Collections.Generic.List<float>();
            Color[] colours = { new Color(1f, 0.82f, 0.16f), new Color(0.15f, 0.35f, 0.8f), new Color(0.82f, 0.13f, 0.17f) };
            Color rope = new Color(0.22f, 0.13f, 0.07f);
            int flagIndex = 0;
            for (float x0 = left; x0 < right - 0.01f; x0 += self.span)
            {
                float x1 = Mathf.Min(x0 + self.span, right);
                float len = x1 - x0;
                // The rope: a parabola sagging in the middle, drawn as short stretched segments.
                const int steps = 14;
                Vector2 prev = Curve(x0, ropeY, len, self.sag, 0f);
                for (int s = 1; s <= steps; s++)
                {
                    Vector2 next = Curve(x0, ropeY, len, self.sag, s / (float)steps);
                    Vector2 mid = (prev + next) * 0.5f;
                    Vector2 d = next - prev;
                    GameObject seg = new GameObject("Rope");
                    seg.transform.SetParent(root.transform, false);
                    seg.transform.localPosition = new Vector3(mid.x, mid.y, 0f);
                    seg.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                    seg.transform.localScale = new Vector3(d.magnitude + 0.01f, 0.035f, 1f);
                    SpriteRenderer sr = seg.AddComponent<SpriteRenderer>();
                    sr.sprite = squareSprite;
                    sr.sortingOrder = -2;
                    sr.color = rope;
                    prev = next;
                }
                // The pennants, evenly spaced along the sagging rope.
                int count = Mathf.Max(1, Mathf.RoundToInt(len / (self.flagWidth * 1.55f)));
                for (int k = 0; k < count; k++)
                {
                    float t = (k + 0.5f) / count;
                    Vector2 p = Curve(x0, ropeY, len, self.sag, t);
                    GameObject flag = new GameObject("Pennant");
                    flag.transform.SetParent(root.transform, false);
                    flag.transform.localPosition = new Vector3(p.x, p.y - 0.01f, 0f);
                    flag.transform.localScale = new Vector3(self.flagWidth, self.flagWidth * 1.2f, 1f);
                    SpriteRenderer sr = flag.AddComponent<SpriteRenderer>();
                    sr.sprite = flagSprite;
                    sr.sortingOrder = -2;
                    sr.color = colours[flagIndex % colours.Length];
                    flagIndex++;
                    list.Add(flag.transform);
                    phases.Add(PacmanToon.Hash(flagIndex, 71) * 6.283f);
                }
            }
            self.flags = list.ToArray();
            self.phase = phases.ToArray();
            return self;
        }

        private static Vector2 Curve(float x0, float y, float len, float sag, float t)
        {
            return new Vector2(x0 + len * t, y - sag * 4f * t * (1f - t));
        }

        private void Update()
        {
            if (flags == null)
            {
                return;
            }
            for (int i = 0; i < flags.Length; i++)
            {
                float a = Mathf.Sin(Time.time * 1.8f + phase[i]) * flutter + Mathf.Sin(Time.time * 0.6f + i * 0.3f) * flutter * 0.5f;
                flags[i].localRotation = Quaternion.Euler(0f, 0f, a);
            }
        }

        private static void EnsureSprites()
        {
            if (squareSprite == null)
            {
                Texture2D t = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave, name = "PacmanSquare" };
                t.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
                t.Apply(false);
                squareSprite = Sprite.Create(t, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f, 0, SpriteMeshType.FullRect);
                squareSprite.hideFlags = HideFlags.DontSave;
            }
            if (flagSprite != null)
            {
                return;
            }
            const int w = 24;
            const int h = 32;
            Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "PacmanPennant",
                hideFlags = HideFlags.DontSave,
            };
            Color[] pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h; // 0 at the tip (bottom), 1 at the rope (top)
                float half = v * 0.5f;
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w - 0.5f;
                    float edge = Mathf.Clamp01((half - Mathf.Abs(u)) * w * 0.7f);
                    if (edge <= 0f)
                    {
                        pixels[y * w + x] = Color.clear;
                        continue;
                    }
                    float rim = Mathf.Clamp01((half - Mathf.Abs(u)) / 0.06f);
                    float lum = Mathf.Lerp(0.6f, 1f, rim) * (0.9f + 0.1f * v);
                    pixels[y * w + x] = new Color(lum, lum, lum, edge);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false);
            // Pivot at the top middle so the pennant swings from the rope.
            flagSprite = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 1f), w, 0, SpriteMeshType.FullRect);
            flagSprite.hideFlags = HideFlags.DontSave;
        }
    }
}
