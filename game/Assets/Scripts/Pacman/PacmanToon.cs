using System;
using System.Collections.Generic;
using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// The one art style of the game. Every character and prop sprite is a signed-distance shape that is lit
    /// from the top-left (a soft inflated look), given the same warm dark outline and, where useful, painted
    /// with details by a colour function. Sprites made white can be tinted by their renderer; the outline stays
    /// dark under any tint. Units are tiles (1 = one maze cell), the sprite's origin is its centre.
    /// </summary>
    public static class PacmanToon
    {
        /// <summary>Warm near-black used for outlines and the darkest accents everywhere.</summary>
        public static readonly Color Ink = new Color(0.12f, 0.065f, 0.04f);

        /// <summary>Texture pixels per tile for character parts.</summary>
        public const float Ppu = 96f;

        private static readonly Vector3 Light = new Vector3(-0.45f, 0.65f, 0.6f).normalized;
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>Returns a colour for a point of the shape; used to paint details inside it.</summary>
        public delegate Color Paint(float x, float y);

        /// <summary>
        /// Builds (or returns the cached) sprite of a shape. <paramref name="sd"/> is positive inside, in tiles.
        /// <paramref name="paint"/> optionally colours the interior; otherwise it is <paramref name="baseColour"/>.
        /// </summary>
        public static Sprite Make(string name, float width, float height, Func<float, float, float> sd, Color baseColour,
            Paint paint = null, float bulge = 0.07f, float outline = 0.017f, Vector2? pivot = null, float ppu = Ppu)
        {
            bool vintage = IsVintage;
            string key = vintage ? name + "|v" : name;
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null)
            {
                return cached;
            }

            int w = Mathf.Max(4, Mathf.CeilToInt(width * ppu));
            int h = Mathf.Max(4, Mathf.CeilToInt(height * ppu));
            float unitX = width / w;
            float unitY = height / h;
            float px = 1f / ppu;
            float e = 0.4f * px;
            Color32[] pixels = new Color32[w * h];
            for (int j = 0; j < h; j++)
            {
                for (int i = 0; i < w; i++)
                {
                    float x = (i + 0.5f) * unitX - width * 0.5f;
                    float y = (j + 0.5f) * unitY - height * 0.5f;
                    float d = sd(x, y);
                    float cover = Mathf.Clamp01(0.5f + d * ppu);
                    if (cover <= 0f)
                    {
                        pixels[j * w + i] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    float gx = sd(x + e, y) - sd(x - e, y);
                    float gy = sd(x, y + e) - sd(x, y - e);
                    float len = Mathf.Sqrt(gx * gx + gy * gy) + 1e-6f;
                    float depth = Mathf.Clamp01(d / bulge);
                    float edge = Mathf.Sqrt(Mathf.Max(0f, 1f - depth * depth));
                    Vector3 normal = new Vector3(-gx / len * edge, -gy / len * edge, depth);
                    float lit = Mathf.Clamp(Vector3.Dot(normal, Light), -0.2f, 1f);
                    float shade = 0.66f + 0.4f * lit;

                    Color c = paint != null ? paint(x, y) : baseColour;
                    if (vintage)
                    {
                        // Engraved-label look: three flat inks, line hatching in the shadows, paper grain, muted colour.
                        float band = 0.68f + 0.2f * Smooth(0.02f, 0.14f, lit) + 0.17f * Smooth(0.48f, 0.6f, lit);
                        float hatch = 0f;
                        if (lit < 0.12f)
                        {
                            hatch += Mathf.Repeat(i + j, 7f) < 2.1f ? 1f : 0f;
                        }
                        if (lit < -0.02f)
                        {
                            hatch += Mathf.Repeat(i - j, 7f) < 2.1f ? 1f : 0f;
                        }
                        float grain = (Hash(i, j) - 0.5f) * 0.07f;
                        shade = band * (1f - 0.26f * Mathf.Min(1f, hatch)) + grain;
                        float grey = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                        c = Color.Lerp(new Color(grey, grey * 0.97f, grey * 0.9f), c, 0.8f);
                    }
                    Color shaded = new Color(Mathf.Min(1f, c.r * shade), Mathf.Min(1f, c.g * shade), Mathf.Min(1f, c.b * shade), 1f);
                    // A warm bounce light on the lower rim keeps the dark side from going muddy.
                    float rim = Mathf.Clamp01(-normal.y * 0.5f) * (1f - depth) * 0.12f;
                    shaded.r += rim;
                    shaded.g += rim * 0.6f;
                    float ink = Mathf.Clamp01(0.5f + (outline - d) * ppu);
                    Color final = Color.Lerp(shaded, vintage ? VintageInk : Ink, ink);
                    pixels[j * w + i] = new Color32(
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(final.r) * 255f),
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(final.g) * 255f),
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(final.b) * 255f),
                        (byte)Mathf.RoundToInt(cover * 255f));
                }
            }

            Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = name,
                hideFlags = HideFlags.DontSave,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, w, h), pivot ?? new Vector2(0.5f, 0.5f), w / width, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>True when the active art theme is the engraved vintage look: procedural sprites then get that finish too.</summary>
        public static bool IsVintage => PacmanTheme.Current == PacmanTheme.Vintage;

        /// <summary>Outline colour of the vintage look: the dark brown ink of an engraved label.</summary>
        public static readonly Color VintageInk = new Color(0.2f, 0.105f, 0.065f);

        /// <summary>Smoothstep between two edges.</summary>
        public static float Smooth(float a, float b, float v)
        {
            float t = Mathf.Clamp01((v - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        /// <summary>A repeatable pseudo-random number in 0..1 from a pixel position.</summary>
        public static float Hash(int i, int j)
        {
            unchecked
            {
                uint h = (uint)(i * 374761393 + j * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }
        }

        // ---------------------------------------------------------------- shapes (positive inside)

        public static float Circle(float x, float y, float cx, float cy, float r)
        {
            float dx = x - cx;
            float dy = y - cy;
            return r - Mathf.Sqrt(dx * dx + dy * dy);
        }

        public static float Ellipse(float x, float y, float cx, float cy, float rx, float ry)
        {
            float dx = (x - cx) / rx;
            float dy = (y - cy) / ry;
            return (1f - Mathf.Sqrt(dx * dx + dy * dy)) * Mathf.Min(rx, ry);
        }

        public static float Box(float x, float y, float cx, float cy, float halfW, float halfH, float radius)
        {
            float qx = Mathf.Abs(x - cx) - halfW + radius;
            float qy = Mathf.Abs(y - cy) - halfH + radius;
            float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
            return radius - (outside + Mathf.Min(Mathf.Max(qx, qy), 0f));
        }

        /// <summary>A capsule from a to b whose radius goes from ra to rb.</summary>
        public static float Capsule(float x, float y, Vector2 a, Vector2 b, float ra, float rb)
        {
            Vector2 p = new Vector2(x, y) - a;
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return Mathf.Lerp(ra, rb, t) - (p - ab * t).magnitude;
        }

        /// <summary>Union of two shapes, with the seam rounded off by k.</summary>
        public static float Blend(float a, float b, float k)
        {
            float h = Mathf.Clamp01(0.5f + 0.5f * (a - b) / k);
            return Mathf.Lerp(b, a, h) + k * h * (1f - h);
        }

        /// <summary>Shape a with shape b cut out of it.</summary>
        public static float Cut(float a, float b)
        {
            return Mathf.Min(a, -b);
        }

        /// <summary>Paints colour <paramref name="over"/> on <paramref name="under"/> where the shape distance d is positive (antialiased).</summary>
        public static Color Over(Color under, Color over, float d)
        {
            return Color.Lerp(under, over, Mathf.Clamp01(0.5f + d * Ppu));
        }

        /// <summary>Keeps a shape distance only where a condition holds; elsewhere it is far outside (never a sign flip, which would fill the outside).</summary>
        public static float Mask(float d, bool on)
        {
            return on ? d : -1f;
        }

        /// <summary>A darker or lighter version of a colour, for hand-picked shading accents.</summary>
        public static Color Shade(Color c, float factor)
        {
            return new Color(Mathf.Clamp01(c.r * factor), Mathf.Clamp01(c.g * factor), Mathf.Clamp01(c.b * factor), c.a);
        }
    }
}
