using UnityEngine;
using Toon = Studio.Pacman.PacmanToon;

namespace Studio.Pacman
{
    /// <summary>
    /// Sprites of the four pests in the shared toon style: the person body, jointed limbs, the clothes and props
    /// that tell them apart, the eyes and the segments of the snake. Body parts are white and tinted by the pest's
    /// colour; clothes and props carry their own colours.
    /// </summary>
    public static class PacmanPestArt
    {
        private static readonly Color Dark = new Color(0.16f, 0.09f, 0.06f);
        private static readonly Color Gold = new Color(1f, 0.8f, 0.15f);

        // ---------------------------------------------------------------- person

        /// <summary>Head and torso in one piece, origin at the pest's centre. White: tinted by the renderer.</summary>
        public static Sprite PersonBody()
        {
            return Toon.Make("PestBody", 0.66f, 0.9f,
                (x, y) => Toon.Blend(Toon.Circle(x, y, 0f, 0.22f, 0.2f), Toon.Box(x, y, 0f, -0.1f, 0.26f, 0.2f, 0.1f), 0.06f),
                Color.white,
                (x, y) => Toon.Over(Color.white, Toon.Shade(Color.white, 0.82f), Toon.Mask(Toon.Box(x, y, 0f, 0.05f, 0.14f, 0.03f, 0.02f), Mathf.Abs(x) < 0.15f)),
                0.09f);
        }

        /// <summary>Arm hanging from the shoulder (pivot at the top).</summary>
        public static Sprite Arm()
        {
            return Toon.Make("PestArm", 0.18f, 0.32f,
                (x, y) => Toon.Capsule(x, y, new Vector2(0f, 0.1f), new Vector2(0f, -0.12f), 0.058f, 0.05f),
                Color.white, null, 0.05f, 0.017f, new Vector2(0.5f, 0.5f + 0.1f / 0.32f));
        }

        /// <summary>Leg with a foot pointing forward, pivot at the hip.</summary>
        public static Sprite Leg()
        {
            return Toon.Make("PestLeg", 0.28f, 0.3f,
                (x, y) => Toon.Blend(Toon.Capsule(x, y, new Vector2(0f, 0.1f), new Vector2(0f, -0.07f), 0.078f, 0.062f), Toon.Capsule(x, y, new Vector2(-0.01f, -0.1f), new Vector2(0.07f, -0.1f), 0.046f, 0.042f), 0.02f),
                Color.white, null, 0.06f, 0.017f, new Vector2(0.5f, 0.5f + 0.1f / 0.3f));
        }

        /// <summary>A hand to put at the end of an arm, in skin colour.</summary>
        public static Sprite Hand()
        {
            return Toon.Make("PestHand", 0.14f, 0.14f, (x, y) => Toon.Circle(x, y, 0f, 0f, 0.055f), new Color(0.88f, 0.62f, 0.42f), null, 0.05f, 0.016f);
        }

        // ---------------------------------------------------------------- DIAN agent

        public static Sprite ShirtAndTie()
        {
            Color red = new Color(0.85f, 0.1f, 0.12f);
            return Toon.Make("PestShirtTie", 0.3f, 0.42f,
                (x, y) => Toon.Box(x, y, 0f, 0f, 0.1f, 0.18f, 0.025f),
                Color.white,
                (x, y) =>
                {
                    Color c = Color.white;
                    c = Toon.Over(c, red, Toon.Box(x, y, 0f, -0.03f, 0.03f, 0.12f, 0.012f));
                    c = Toon.Over(c, Toon.Shade(red, 0.75f), Toon.Box(x, y, 0f, 0.13f, 0.04f, 0.032f, 0.01f));
                    c = Toon.Over(c, Toon.Shade(Color.white, 0.8f), Toon.Mask(Toon.Box(x, y, 0f, 0.15f, 0.1f, 0.02f, 0.005f), Mathf.Abs(x) > 0.045f));
                    return c;
                }, 0.05f);
        }

        public static Sprite Hair(Color colour)
        {
            return Toon.Make("PestHair", 0.44f, 0.18f,
                (x, y) => Toon.Ellipse(x, y, 0f, 0f, 0.2f, 0.07f), colour, null, 0.05f, 0.015f);
        }

        public static Sprite Briefcase()
        {
            Color leather = new Color(0.55f, 0.3f, 0.13f);
            return Toon.Make("PestBriefcase", 0.3f, 0.28f,
                (x, y) => Mathf.Max(Toon.Box(x, y, 0f, -0.02f, 0.12f, 0.085f, 0.02f), Toon.Cut(Toon.Box(x, y, 0f, 0.085f, 0.055f, 0.04f, 0.02f), Toon.Box(x, y, 0f, 0.08f, 0.035f, 0.025f, 0.01f))),
                leather,
                (x, y) =>
                {
                    Color c = leather;
                    c = Toon.Over(c, Toon.Shade(leather, 0.7f), Toon.Box(x, y, 0f, 0.02f, 0.13f, 0.01f, 0.003f));
                    c = Toon.Over(c, Gold, Toon.Box(x, y, 0f, 0.02f, 0.025f, 0.02f, 0.004f));
                    return c;
                }, 0.05f);
        }

        // ---------------------------------------------------------------- guerrilla

        public static Sprite Blotches()
        {
            return Toon.Make("PestBlotches", 0.56f, 0.38f,
                (x, y) => Mathf.Max(Toon.Ellipse(x, y, -0.15f, 0.07f, 0.11f, 0.07f), Mathf.Max(Toon.Ellipse(x, y, 0.12f, -0.06f, 0.12f, 0.07f), Toon.Ellipse(x, y, -0.05f, -0.12f, 0.09f, 0.05f))),
                Color.white, null, 0.04f, 0f);
        }

        public static Sprite Bandana()
        {
            Color red = new Color(0.86f, 0.2f, 0.14f);
            return Toon.Make("PestBandana", 0.36f, 0.14f,
                (x, y) => Toon.Blend(Toon.Box(x, y, 0f, 0f, 0.16f, 0.034f, 0.012f), Toon.Ellipse(x, y, 0.13f, -0.03f, 0.04f, 0.05f), 0.02f),
                red, null, 0.04f, 0.014f);
        }

        public static Sprite Beret()
        {
            Color maroon = new Color(0.55f, 0.1f, 0.09f);
            return Toon.Make("PestBeret", 0.56f, 0.26f,
                (x, y) => Toon.Blend(Toon.Ellipse(x, y, 0f, -0.02f, 0.26f, 0.1f), Toon.Circle(x, y, 0.03f, 0.09f, 0.03f), 0.02f),
                maroon, null, 0.06f, 0.016f);
        }

        public static Sprite Star()
        {
            return Toon.Make("PestStar", 0.18f, 0.18f,
                (x, y) =>
                {
                    float angle = Mathf.Atan2(y, x);
                    float spike = Mathf.Pow(Mathf.Abs(Mathf.Cos(2.5f * (angle - Mathf.PI * 0.5f))), 1.4f);
                    float radius = Mathf.Lerp(0.03f, 0.082f, spike);
                    return (radius - Mathf.Sqrt(x * x + y * y)) * 0.8f;
                },
                Gold, null, 0.03f, 0.012f);
        }

        // ---------------------------------------------------------------- narco

        public static Sprite Pleats()
        {
            Color tan = new Color(0.72f, 0.58f, 0.3f);
            return Toon.Make("PestPleats", 0.34f, 0.36f,
                (x, y) => Mathf.Max(Toon.Box(x, y, -0.1f, 0f, 0.013f, 0.15f, 0.005f), Toon.Box(x, y, 0.1f, 0f, 0.013f, 0.15f, 0.005f)),
                tan, null, 0.02f, 0.008f);
        }

        public static Sprite Shades()
        {
            Color lens = new Color(0.07f, 0.07f, 0.11f);
            return Toon.Make("PestShades", 0.42f, 0.16f,
                (x, y) => Mathf.Max(Toon.Box(x, y, -0.09f, 0f, 0.08f, 0.055f, 0.03f), Mathf.Max(Toon.Box(x, y, 0.09f, 0f, 0.08f, 0.055f, 0.03f), Toon.Box(x, y, 0f, 0.015f, 0.04f, 0.012f, 0.005f))),
                lens,
                (x, y) =>
                {
                    Color c = lens;
                    c = Toon.Over(c, new Color(0.7f, 0.78f, 0.95f), Toon.Capsule(x, y, new Vector2(-0.12f, 0.02f), new Vector2(-0.085f, 0.035f), 0.009f, 0.009f));
                    c = Toon.Over(c, new Color(0.7f, 0.78f, 0.95f), Toon.Capsule(x, y, new Vector2(0.06f, 0.02f), new Vector2(0.095f, 0.035f), 0.009f, 0.009f));
                    return c;
                }, 0.03f, 0.013f);
        }

        public static Sprite Moustache()
        {
            return Toon.Make("PestMoustache", 0.24f, 0.1f,
                (x, y) => Toon.Blend(Toon.Ellipse(x, y, -0.052f, 0f, 0.065f, 0.022f), Toon.Ellipse(x, y, 0.052f, 0f, 0.065f, 0.022f), 0.015f),
                new Color(0.12f, 0.07f, 0.04f), null, 0.03f, 0f);
        }

        public static Sprite Chain()
        {
            return Toon.Make("PestChain", 0.36f, 0.2f,
                (x, y) =>
                {
                    float best = -1f;
                    for (int i = 0; i < 7; i++)
                    {
                        float cx = -0.135f + i * 0.045f;
                        float cy = -0.05f + cx * cx * 5f;
                        best = Mathf.Max(best, Toon.Circle(x, y, cx, cy, 0.028f));
                    }
                    return best;
                },
                Gold, null, 0.025f, 0.011f);
        }

        // ---------------------------------------------------------------- eyes

        public static Sprite Eye(float width, float height)
        {
            return Toon.Make("PestEye" + Mathf.RoundToInt(width * 100f) + "x" + Mathf.RoundToInt(height * 100f), width + 0.04f, height + 0.04f,
                (x, y) => Toon.Ellipse(x, y, 0f, 0f, width * 0.5f, height * 0.5f), new Color(0.98f, 0.98f, 0.95f), null, 0.05f, 0.013f);
        }

        public static Sprite Pupil(float size)
        {
            return Toon.Make("PestPupil" + Mathf.RoundToInt(size * 100f), size + 0.02f, size + 0.02f,
                (x, y) => Toon.Circle(x, y, 0f, 0f, size * 0.5f),
                Toon.Ink,
                (x, y) => Toon.Over(Toon.Ink, Color.white, Toon.Circle(x, y, -size * 0.17f, size * 0.2f, size * 0.15f)), 0.1f, 0f);
        }

        // ---------------------------------------------------------------- snake

        /// <summary>A body segment of the snake: a round, shaded ball (white, tinted per band).</summary>
        public static Sprite SnakeBall()
        {
            return Toon.Make("PestSnakeBall", 0.3f, 0.3f, (x, y) => Toon.Circle(x, y, 0f, 0f, 0.14f), Color.white, null, 0.12f, 0.016f);
        }

        /// <summary>The snake's head, pointing up: a rounded wedge with a yellow collar band, nostrils and two big yellow eyes with slit pupils.</summary>
        public static Sprite SnakeHead()
        {
            Color skin = new Color(0.3f, 0.2f, 0.17f);
            return Toon.Make("PestSnakeHead", 0.44f, 0.44f,
                (x, y) => Toon.Blend(Toon.Ellipse(x, y, 0f, -0.02f, 0.19f, 0.17f), Toon.Ellipse(x, y, 0f, 0.1f, 0.12f, 0.12f), 0.05f),
                skin,
                (x, y) =>
                {
                    Color c = skin;
                    c = Toon.Over(c, new Color(0.95f, 0.75f, 0.15f), Toon.Box(x, y, 0f, -0.15f, 0.3f, 0.03f, 0.01f));
                    c = Toon.Over(c, new Color(0.95f, 0.15f, 0.2f), Toon.Circle(x, y, 0f, 0.2f, 0.05f));
                    for (int side = -1; side <= 1; side += 2)
                    {
                        c = Toon.Over(c, new Color(1f, 0.9f, 0.2f), Toon.Circle(x, y, side * 0.085f, 0.04f, 0.058f));
                        c = Toon.Over(c, new Color(0.05f, 0.03f, 0.03f), Toon.Box(x, y, side * 0.085f, 0.04f, 0.012f, 0.045f, 0.004f));
                    }
                    c = Toon.Over(c, new Color(0.1f, 0.06f, 0.05f), Toon.Circle(x, y, -0.03f, 0.14f, 0.012f));
                    c = Toon.Over(c, new Color(0.1f, 0.06f, 0.05f), Toon.Circle(x, y, 0.03f, 0.14f, 0.012f));
                    return c;
                }, 0.08f);
        }

        public static Sprite Tongue()
        {
            Color red = new Color(0.95f, 0.1f, 0.25f);
            return Toon.Make("PestTongue", 0.16f, 0.22f,
                (x, y) => Mathf.Max(Toon.Capsule(x, y, new Vector2(0f, -0.1f), new Vector2(0f, 0.02f), 0.013f, 0.013f),
                    Mathf.Max(Toon.Capsule(x, y, new Vector2(0f, 0.02f), new Vector2(-0.04f, 0.1f), 0.012f, 0.008f), Toon.Capsule(x, y, new Vector2(0f, 0.02f), new Vector2(0.04f, 0.1f), 0.012f, 0.008f))),
                red, null, 0.02f, 0.007f, new Vector2(0.5f, 0.045f));
        }
    }
}
