using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// Life on the dirt paths: small grass tufts that sway in the breeze and pebbles, scattered in the corners of the
    /// open cells (never on the centre line where the beans lie). Sprites are painted pixel by pixel in code.
    /// </summary>
    public class PacmanGrass : MonoBehaviour
    {
        [Tooltip("Chance that an open cell gets a grass tuft in one of its corners.")]
        [SerializeField, Range(0f, 1f)] private float tuftChance = 0.45f;
        [Tooltip("Chance that an open cell gets a pebble.")]
        [SerializeField, Range(0f, 1f)] private float pebbleChance = 0.28f;
        [Tooltip("Sway of the tufts in degrees.")]
        [SerializeField] private float sway = 5f;

        private static Sprite[] tuftSprites;
        private static Sprite pebbleSprite;

        private Transform[] tufts;
        private float[] phases;

        /// <summary>Scatters tufts and pebbles over the open cells under a parent.</summary>
        public static PacmanGrass Build(Transform parent)
        {
            GameObject root = new GameObject("Grass");
            root.transform.SetParent(parent, false);
            PacmanGrass self = root.AddComponent<PacmanGrass>();
            EnsureSprites();

            var tuftList = new System.Collections.Generic.List<Transform>();
            var phaseList = new System.Collections.Generic.List<float>();
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
                    if (PacmanToon.Hash(id, 301) < self.tuftChance)
                    {
                        float cx = x + (PacmanToon.Hash(id, 303) < 0.5f ? -1f : 1f) * Mathf.Lerp(0.3f, 0.42f, PacmanToon.Hash(id, 305));
                        float cy = -y + (PacmanToon.Hash(id, 307) < 0.5f ? -1f : 1f) * Mathf.Lerp(0.28f, 0.42f, PacmanToon.Hash(id, 309));
                        GameObject go = new GameObject("Tuft");
                        go.transform.SetParent(root.transform, false);
                        go.transform.localPosition = new Vector3(cx, cy - 0.12f, 0f);
                        float size = Mathf.Lerp(0.42f, 0.62f, PacmanToon.Hash(id, 311));
                        go.transform.localScale = new Vector3(size * (PacmanToon.Hash(id, 313) < 0.5f ? -1f : 1f), size, 1f);
                        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                        sr.sprite = tuftSprites[Mathf.FloorToInt(PacmanToon.Hash(id, 315) * tuftSprites.Length) % tuftSprites.Length];
                        sr.sortingOrder = -3;
                        float v = Mathf.Lerp(0.8f, 1.1f, PacmanToon.Hash(id, 317));
                        sr.color = new Color(v, v, v * 0.9f, 1f);
                        tuftList.Add(go.transform);
                        phaseList.Add(PacmanToon.Hash(id, 319) * 6.283f);
                    }
                    if (PacmanToon.Hash(id, 321) < self.pebbleChance)
                    {
                        float px = x + Mathf.Lerp(-0.42f, 0.42f, PacmanToon.Hash(id, 323));
                        float py = -y + (PacmanToon.Hash(id, 325) < 0.5f ? -1f : 1f) * Mathf.Lerp(0.2f, 0.4f, PacmanToon.Hash(id, 327));
                        GameObject go = new GameObject("Pebble");
                        go.transform.SetParent(root.transform, false);
                        go.transform.localPosition = new Vector3(px, py, 0f);
                        float size = Mathf.Lerp(0.14f, 0.26f, PacmanToon.Hash(id, 329));
                        go.transform.localScale = new Vector3(size, size, 1f);
                        go.transform.localRotation = Quaternion.Euler(0f, 0f, PacmanToon.Hash(id, 331) * 180f);
                        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                        sr.sprite = pebbleSprite;
                        sr.sortingOrder = -3;
                        float v = Mathf.Lerp(0.75f, 1.15f, PacmanToon.Hash(id, 333));
                        sr.color = new Color(v, v * 0.97f, v * 0.92f, 1f);
                    }
                }
            }
            self.tufts = tuftList.ToArray();
            self.phases = phaseList.ToArray();
            return self;
        }

        private void Update()
        {
            float t = Time.time;
            for (int i = 0; i < tufts.Length; i++)
            {
                float wind = Mathf.Sin(t * 1.3f + phases[i]) * sway + Mathf.Sin(t * 0.37f + phases[i] * 0.5f) * sway * 0.5f;
                tufts[i].localRotation = Quaternion.Euler(0f, 0f, wind);
            }
        }

        private static void EnsureSprites()
        {
            if (tuftSprites == null)
            {
                tuftSprites = new[] { PaintTuft(1), PaintTuft(2), PaintTuft(3) };
            }
            if (pebbleSprite == null)
            {
                pebbleSprite = PaintPebble();
            }
        }

        private static Sprite PaintTuft(int seed)
        {
            const int size = 32;
            Color[] px = new Color[size * size];
            bool[] solid = new bool[px.Length];
            int blades = 5 + seed;
            for (int b = 0; b < blades; b++)
            {
                float u = (b + 0.5f) / blades - 0.5f;
                float lean = u * 0.9f + (PacmanToon.Hash(b, 400 + seed) - 0.5f) * 0.3f;
                float height = Mathf.Lerp(11f, 22f, PacmanToon.Hash(b, 410 + seed)) * (1f - Mathf.Abs(u) * 0.5f);
                float baseX = size * 0.5f + u * 9f;
                float shade = Mathf.Lerp(0.55f, 1f, PacmanToon.Hash(b, 420 + seed));
                for (float s = 0f; s <= height; s += 0.4f)
                {
                    float f = s / height;
                    float x = baseX + lean * s + lean * f * f * 4f;
                    float y = 2f + s;
                    float halfWidth = Mathf.Lerp(1.5f, 0.2f, f);
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        int ix = Mathf.RoundToInt(x) + dx;
                        int iy = Mathf.RoundToInt(y);
                        if (ix < 1 || ix >= size - 1 || iy < 1 || iy >= size - 1 || Mathf.Abs(ix - x) > halfWidth)
                        {
                            continue;
                        }
                        Color dark = new Color(0.16f, 0.36f, 0.14f);
                        Color light = new Color(0.62f, 0.76f, 0.26f);
                        Color c = Color.Lerp(dark, light, f) * shade;
                        c.a = 1f;
                        px[iy * size + ix] = c;
                        solid[iy * size + ix] = true;
                    }
                }
            }
            return Finish(px, solid, size, new Vector2(0.5f, 0.06f), 40f);
        }

        private static Sprite PaintPebble()
        {
            const int size = 16;
            Color[] px = new Color[size * size];
            bool[] solid = new bool[px.Length];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - 7.5f) / 5.6f;
                    float dy = (y - 7.5f) / 3.9f;
                    float d = dx * dx + dy * dy;
                    if (d > 1f)
                    {
                        continue;
                    }
                    float lit = Mathf.Clamp01(0.55f + (-dx * 0.25f + dy * 0.45f));
                    Color c = Color.Lerp(new Color(0.42f, 0.36f, 0.32f), new Color(0.78f, 0.72f, 0.64f), lit);
                    c.a = 1f;
                    px[y * size + x] = c;
                    solid[y * size + x] = true;
                }
            }
            return Finish(px, solid, size, new Vector2(0.5f, 0.5f), 16f);
        }

        private static Sprite Finish(Color[] px, bool[] solid, int size, Vector2 pivot, float pixelsPerUnit)
        {
            Color ink = new Color(0.09f, 0.05f, 0.03f, 0.85f);
            Color[] outlined = (Color[])px.Clone();
            for (int y = 1; y < size - 1; y++)
            {
                for (int x = 1; x < size - 1; x++)
                {
                    int i = y * size + x;
                    if (!solid[i] && (solid[i - 1] || solid[i + 1] || solid[i - size] || solid[i + size]))
                    {
                        outlined[i] = ink;
                    }
                }
            }
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels(outlined);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), pivot, pixelsPerUnit);
        }
    }
}
