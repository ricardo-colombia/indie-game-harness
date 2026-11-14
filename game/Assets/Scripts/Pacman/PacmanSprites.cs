using System;
using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// Every sprite the game draws, generated at runtime from signed-distance shapes, so there are no art
    /// files to import. All sprites are white with an alpha mask and are tinted by their SpriteRenderer,
    /// except the floor grid tile. Each one is exactly one world unit wide (one maze tile).
    /// </summary>
    public static class PacmanSprites
    {
        /// <summary>Number of mouth-opening steps between closed and fully open (half-angle 0..pi).</summary>
        public const int PacFrames = 48;

        /// <summary>Body radius in tiles, for Pac-Man and the ghosts.</summary>
        public const float BodyRadius = 0.46f;

        private const int FrameSize = 64;

        private static Sprite disc;
        private static Sprite bean;
        private static Sprite square;
        private static Sprite frightFace;
        private static Sprite gridTile;
        private static Sprite mountain;
        private static Sprite mountainVintage;
        private static Sprite gridTileVintage;
        private static readonly Sprite[] PacSprites = new Sprite[PacFrames + 1];

        /// <summary>A soft-edged disc filling its one-unit square. Used for pellets, eyes and fruit.</summary>
        public static Sprite Disc
        {
            get
            {
                if (disc == null)
                {
                    disc = Make("PacDisc", 32, (x, y) => 0.5f - Mathf.Sqrt(x * x + y * y));
                }
                return disc;
            }
        }

        /// <summary>A coffee bean: an upright ellipse (0.6 wide, 0.92 tall) in its one-unit square. The crease is a separate dark bar.</summary>
        public static Sprite Bean
        {
            get
            {
                if (bean == null)
                {
                    bean = Make("PacBean", 48, (x, y) => (1f - Mathf.Sqrt((x * x) / (0.3f * 0.3f) + (y * y) / (0.46f * 0.46f))) * 0.3f);
                }
                return bean;
            }
        }

        /// <summary>A solid one-unit square, scaled for bars and stems.</summary>
        public static Sprite Square
        {
            get
            {
                if (square == null)
                {
                    square = Make("PacSquare", 4, (x, y) => 1f);
                }
                return square;
            }
        }

        /// <summary>Pac-Man with the mouth opened to a half-angle in radians (0 closed .. pi gone). Mouth points +X.</summary>
        public static Sprite Pac(float halfAngle)
        {
            int index = Mathf.Clamp(Mathf.RoundToInt(halfAngle / Mathf.PI * PacFrames), 0, PacFrames);
            if (PacSprites[index] == null)
            {
                float half = index * Mathf.PI / PacFrames;
                PacSprites[index] = Make("PacFrame" + index, FrameSize, (x, y) =>
                {
                    float len = Mathf.Sqrt(x * x + y * y);
                    float sd = BodyRadius - len;
                    if (half > 0.001f)
                    {
                        // Outside the mouth wedge, measured as arc length so the edge stays antialiased.
                        float angle = Mathf.Abs(Mathf.Atan2(y, x));
                        sd = Mathf.Min(sd, (angle - half) * Mathf.Max(len, 0.02f));
                    }
                    return sd;
                });
            }
            return PacSprites[index];
        }

        /// <summary>Positive inside a rounded box centred on the origin of its own frame.</summary>
        private static float RoundBox(float x, float y, float halfWidth, float halfHeight, float radius)
        {
            float qx = Mathf.Abs(x) - halfWidth + radius;
            float qy = Mathf.Abs(y) - halfHeight + radius;
            float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
            return radius - (outside + Mathf.Min(Mathf.Max(qx, qy), 0f));
        }

        /// <summary>Distance from a point to the line segment a-b.</summary>
        private static float SegmentDistance(float x, float y, Vector2 a, Vector2 b)
        {
            Vector2 p = new Vector2(x, y) - a;
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p, ab) / ab.sqrMagnitude);
            return (p - ab * t).magnitude;
        }

        /// <summary>A mountain silhouette filling its one-unit square: a peak in the middle sloping to the base corners.</summary>
        public static Sprite Mountain
        {
            get
            {
                bool vintage = PacmanToon.IsVintage;
                Sprite cached = vintage ? mountainVintage : mountain;
                if (cached == null)
                {
                    Func<float, float> ridgeAt = x => -0.5f + Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(x) * 2f), 0.85f) + 0.025f * Mathf.Sin(x * 37f);
                    Func<float, float, float> inside = (x, y) => Mathf.Min((ridgeAt(x) - y) * 0.7f, y + 0.5f);
                    if (vintage)
                    {
                        // Engraved slopes: slanted hatching that thickens toward the base, an ink line along the ridge and paper grain.
                        cached = Make("PacMountainV", 128, inside, (x, y, px, py) =>
                        {
                            float below = ridgeAt(x) - y;
                            float shade = 0.92f + (PacmanToon.Hash(px, py) - 0.5f) * 0.1f;
                            float depth = Mathf.Clamp01(below * 1.6f);
                            if (Mathf.Repeat(px * 0.6f + py, 5f) < 0.8f + depth * 1.6f)
                            {
                                shade *= 0.72f;
                            }
                            if (below < 0.012f)
                            {
                                shade *= 0.55f;
                            }
                            return shade;
                        });
                        mountainVintage = cached;
                    }
                    else
                    {
                        cached = Make("PacMountain", 128, inside);
                        mountain = cached;
                    }
                }
                return cached;
            }
        }

        /// <summary>The frightened face: two square eyes and a zigzag mouth.</summary>
        public static Sprite FrightFace
        {
            get
            {
                if (frightFace == null)
                {
                    frightFace = Make("GhostFrightFace", FrameSize, (x, y) =>
                    {
                        float eyes = -BoxDistance(Mathf.Abs(x) - 0.15f, y - 0.1f, 0.055f, 0.055f);
                        float u = x / 0.1f;
                        float tri = Mathf.Abs(Mathf.Repeat(u, 2f) - 1f) * 2f - 1f;
                        float centre = -0.14f + 0.06f * tri;
                        float mouth = Mathf.Min(0.3f - Mathf.Abs(x), 0.028f - Mathf.Abs(y - centre));
                        return Mathf.Max(eyes, mouth);
                    });
                }
                return frightFace;
            }
        }

        /// <summary>One floor cell of the farm path: dark earth with a faint furrow line on two edges. Opaque, tiled behind the maze.</summary>
        public static Sprite GridTile
        {
            get
            {
                bool vintage = PacmanToon.IsVintage;
                if ((vintage ? gridTileVintage : gridTile) == null)
                {
                    const int n = 64;
                    Texture2D texture = new Texture2D(n, n, TextureFormat.RGBA32, false)
                    {
                        wrapMode = TextureWrapMode.Repeat,
                        filterMode = FilterMode.Bilinear,
                        name = "PacmanGrid",
                        hideFlags = HideFlags.DontSave,
                    };
                    Color baseColour = new Color(0.085f, 0.055f, 0.035f);
                    Color line = new Color(0.14f, 0.09f, 0.055f);
                    for (int y = 0; y < n; y++)
                    {
                        for (int x = 0; x < n; x++)
                        {
                            float dx = (x - n * 0.5f) / (n * 0.5f);
                            float dy = (y - n * 0.5f) / (n * 0.5f);
                            float glow = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy)) * 0.025f;
                            // Fixed pseudo-random speckle so the earth is not flat.
                            float grit = ((x * 73856093 ^ y * 19349663) & 255) / 255f;
                            Color c = x < 2 || y < 2 ? line : baseColour + new Color(glow * 0.8f, glow * 0.5f, glow * 0.2f, 0f) + Color.white * (grit * grit * 0.03f);
                            c.a = 1f;
                            texture.SetPixel(x, y, c);
                        }
                    }
                    if (vintage)
                    {
                        // Etched earth: short slanted ink ticks scattered over the tile (wrapping, so the tiling stays seamless).
                        Color tick = new Color(0.2f, 0.125f, 0.075f);
                        for (int k = 0; k < 26; k++)
                        {
                            int tx = Mathf.FloorToInt(PacmanToon.Hash(k, 11) * n);
                            int ty = Mathf.FloorToInt(PacmanToon.Hash(k, 29) * n);
                            int len = 3 + Mathf.FloorToInt(PacmanToon.Hash(k, 47) * 4f);
                            for (int t = 0; t < len; t++)
                            {
                                texture.SetPixel((tx + t) % n, (ty + t) % n, tick);
                            }
                        }
                    }
                    texture.Apply(false);
                    Sprite made = Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n, 0, SpriteMeshType.FullRect);
                    made.hideFlags = HideFlags.DontSave;
                    if (vintage)
                    {
                        gridTileVintage = made;
                    }
                    else
                    {
                        gridTile = made;
                    }
                }
                return vintage ? gridTileVintage : gridTile;
            }
        }

        /// <summary>Signed distance to a box centred on the origin: negative inside.</summary>
        private static float BoxDistance(float x, float y, float halfWidth, float halfHeight)
        {
            float dx = Mathf.Abs(x) - halfWidth;
            float dy = Mathf.Abs(y) - halfHeight;
            return Mathf.Max(dx, dy);
        }

        /// <summary>
        /// Rasterises a signed-distance function (positive inside, in tile units, origin at the sprite centre)
        /// into a white sprite whose alpha is the antialiased coverage.
        /// </summary>
        private static Sprite Make(string name, int size, Func<float, float, float> inside, Func<float, float, int, int, float> shade = null)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = name,
                hideFlags = HideFlags.DontSave,
            };
            Color32[] pixels = new Color32[size * size];
            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    float x = (px + 0.5f) / size - 0.5f;
                    float y = (py + 0.5f) / size - 0.5f;
                    float coverage = Mathf.Clamp01(0.5f + inside(x, y) * size);
                    byte grey = shade == null ? (byte)255 : (byte)Mathf.RoundToInt(Mathf.Clamp01(shade(x, y, px, py)) * 255f);
                    pixels[py * size + px] = new Color32(grey, grey, grey, (byte)Mathf.RoundToInt(coverage * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }
}
