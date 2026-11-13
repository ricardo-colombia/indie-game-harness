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
        private static Sprite square;
        private static Sprite frightFace;
        private static Sprite gridTile;
        private static readonly Sprite[] PacSprites = new Sprite[PacFrames + 1];
        private static readonly Sprite[] GhostSprites = new Sprite[2];

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

        /// <summary>The ghost silhouette: round dome, straight sides and a three-foot wavy hem (two ripple frames).</summary>
        public static Sprite Ghost(int frame)
        {
            frame &= 1;
            if (GhostSprites[frame] == null)
            {
                float phase = frame * Mathf.PI;
                GhostSprites[frame] = Make("GhostFrame" + frame, FrameSize, (x, y) =>
                {
                    if (y > 0f)
                    {
                        return BodyRadius - Mathf.Sqrt(x * x + y * y);
                    }
                    float theta = (x + BodyRadius) / (2f * BodyRadius) * 3f * 2f * Mathf.PI + phase;
                    float hem = -BodyRadius + 0.1f * (0.5f + 0.5f * Mathf.Cos(theta));
                    return Mathf.Min(BodyRadius - Mathf.Abs(x), y - hem);
                });
            }
            return GhostSprites[frame];
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

        /// <summary>One floor cell: dark navy with a faint line on two edges. Opaque, tiled behind the maze.</summary>
        public static Sprite GridTile
        {
            get
            {
                if (gridTile == null)
                {
                    const int n = 64;
                    Texture2D texture = new Texture2D(n, n, TextureFormat.RGBA32, false)
                    {
                        wrapMode = TextureWrapMode.Repeat,
                        filterMode = FilterMode.Bilinear,
                        name = "PacmanGrid",
                        hideFlags = HideFlags.DontSave,
                    };
                    Color baseColour = new Color(0.012f, 0.016f, 0.05f);
                    Color line = new Color(0.05f, 0.09f, 0.22f);
                    for (int y = 0; y < n; y++)
                    {
                        for (int x = 0; x < n; x++)
                        {
                            float dx = (x - n * 0.5f) / (n * 0.5f);
                            float dy = (y - n * 0.5f) / (n * 0.5f);
                            float glow = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy)) * 0.025f;
                            Color c = x < 2 || y < 2 ? line : baseColour + new Color(glow * 0.4f, glow * 0.6f, glow, 0f);
                            c.a = 1f;
                            texture.SetPixel(x, y, c);
                        }
                    }
                    texture.Apply(false);
                    gridTile = Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n, 0, SpriteMeshType.FullRect);
                    gridTile.hideFlags = HideFlags.DontSave;
                }
                return gridTile;
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
        private static Sprite Make(string name, int size, Func<float, float, float> inside)
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
                    pixels[py * size + px] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(coverage * 255f));
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
