using UnityEngine;
using Toon = Studio.Pacman.PacmanToon;

namespace Studio.Pacman
{
    /// <summary>
    /// The sprites of the coffee picker, painted with the shared toon style: head, hat, torso, canasto and the
    /// two-part arms and legs the walk cycle bends. Every part is drawn in world units (1 = one tile) and faces +X
    /// in profile; limb sprites have their pivot at the joint they hang from.
    /// </summary>
    public static class PacmanFarmerArt
    {
        public static readonly Color Shirt = new Color(0.97f, 0.95f, 0.9f);
        public static readonly Color Jeans = new Color(0.27f, 0.41f, 0.68f);
        public static readonly Color Skin = new Color(0.82f, 0.56f, 0.37f);
        public static readonly Color Boot = new Color(0.3f, 0.17f, 0.1f);
        public static readonly Color Straw = new Color(0.94f, 0.8f, 0.5f);
        public static readonly Color StrawDark = new Color(0.78f, 0.6f, 0.32f);
        public static readonly Color Weave = new Color(0.74f, 0.47f, 0.3f);
        public static readonly Color WeaveDark = new Color(0.52f, 0.31f, 0.18f);
        public static readonly Color Strap = new Color(0.5f, 0.3f, 0.17f);
        public static readonly Color Cherry = new Color(0.9f, 0.08f, 0.12f);
        private static readonly Color Dark = new Color(0.16f, 0.09f, 0.06f);
        private static readonly Color Gold = new Color(0.98f, 0.8f, 0.25f);

        // ---------------------------------------------------------------- head

        /// <summary>Head in profile facing +X: nose, ear, eye, moustache and a little hair at the back.</summary>
        public static Sprite HeadSide()
        {
            return Toon.Make("FarmerHeadSide", 0.4f, 0.38f,
                (x, y) => Toon.Blend(Toon.Circle(x, y, 0f, 0f, 0.14f), Toon.Circle(x, y, 0.135f, -0.025f, 0.042f), 0.03f),
                Skin,
                (x, y) =>
                {
                    Color c = Skin;
                    c = Toon.Over(c, Dark, Toon.Mask(Toon.Circle(x, y, -0.04f, 0.075f, 0.1f), x < -0.01f && y > 0.01f));
                    c = Toon.Over(c, Toon.Shade(Skin, 0.78f), Toon.Circle(x, y, -0.035f, -0.01f, 0.032f));
                    c = Toon.Over(c, Dark, Toon.Circle(x, y, 0.07f, 0.03f, 0.017f));
                    c = Toon.Over(c, Color.white, Toon.Circle(x + 0.005f, y - 0.006f, 0.07f, 0.03f, 0.006f));
                    c = Toon.Over(c, Dark, Toon.Ellipse(x, y, 0.105f, -0.062f, 0.066f, 0.02f));
                    c = Toon.Over(c, new Color(0.9f, 0.45f, 0.4f), Toon.Circle(x, y, 0.06f, -0.04f, 0.018f) * 0.5f);
                    return c;
                });
        }

        /// <summary>Head seen from the front (walking down) or from behind (walking up: hair, no face).</summary>
        public static Sprite HeadFront(bool back)
        {
            return Toon.Make(back ? "FarmerHeadBack" : "FarmerHeadFront", 0.4f, 0.38f,
                (x, y) => Toon.Blend(Toon.Circle(x, y, 0f, 0f, 0.14f), Toon.Blend(Toon.Circle(x, y, -0.14f, -0.01f, 0.032f), Toon.Circle(x, y, 0.14f, -0.01f, 0.032f), 0.01f), 0.02f),
                Skin,
                (x, y) =>
                {
                    Color c = Skin;
                    if (back)
                    {
                        return Toon.Over(c, Dark, Toon.Mask(Toon.Circle(x, y, 0f, 0.04f, 0.145f), y > -0.045f));
                    }
                    c = Toon.Over(c, Dark, Toon.Mask(Toon.Circle(x, y, 0f, 0.15f, 0.12f), y > 0.075f) * 0.8f);
                    c = Toon.Over(c, Dark, Toon.Circle(x, y, -0.052f, 0.02f, 0.017f));
                    c = Toon.Over(c, Dark, Toon.Circle(x, y, 0.052f, 0.02f, 0.017f));
                    c = Toon.Over(c, Color.white, Toon.Circle(x, y, -0.047f, 0.026f, 0.006f));
                    c = Toon.Over(c, Color.white, Toon.Circle(x, y, 0.057f, 0.026f, 0.006f));
                    c = Toon.Over(c, Toon.Shade(Skin, 0.82f), Toon.Ellipse(x, y, 0f, -0.018f, 0.02f, 0.025f));
                    c = Toon.Over(c, Dark, Toon.Ellipse(x, y, 0f, -0.06f, 0.075f, 0.02f));
                    return c;
                });
        }

        // ---------------------------------------------------------------- hat

        /// <summary>Straw hat (sombrero aguadeño): wide brim, domed crown, dark band and woven texture.</summary>
        public static Sprite Hat(float brim)
        {
            return Toon.Make("FarmerHat" + Mathf.RoundToInt(brim * 100f), brim + 0.06f, 0.42f,
                (x, y) => Toon.Blend(Toon.Ellipse(x, y, 0f, -0.1f, brim * 0.5f, 0.085f), Toon.Mask(Toon.Ellipse(x, y, -0.01f, 0.01f, 0.17f, 0.15f), y > -0.12f), 0.02f),
                Straw,
                (x, y) =>
                {
                    bool crown = Mathf.Abs(x + 0.01f) < 0.17f && y > -0.1f;
                    float weave = 0.5f + 0.5f * Mathf.Sin((x * 90f) + (y * 26f));
                    Color c = Color.Lerp(crown ? Straw : Toon.Shade(StrawDark, 1.12f), crown ? Toon.Shade(Straw, 0.88f) : StrawDark, weave * 0.4f);
                    if (!crown && y < -0.115f)
                    {
                        c = Toon.Shade(c, 0.86f);
                    }
                    Color band = new Color(0.36f, 0.12f, 0.09f);
                    c = Toon.Over(c, band, Toon.Mask(Toon.Box(x, y, -0.01f, -0.075f, 0.18f, 0.028f, 0.005f), crown || Mathf.Abs(x) < 0.19f));
                    return c;
                });
        }

        // ---------------------------------------------------------------- torso and canasto

        public static Sprite TorsoSide()
        {
            return Toon.Make("FarmerTorsoSide", 0.42f, 0.54f,
                (x, y) => Toon.Box(x, y, 0f, 0f, 0.15f, 0.23f, 0.1f),
                Shirt,
                (x, y) =>
                {
                    Color c = Shirt;
                    c = Toon.Over(c, Strap, Capsule2(x, y, new Vector2(0.1f, 0.22f), new Vector2(-0.12f, -0.08f), 0.024f));
                    c = Toon.Over(c, Dark, Toon.Box(x, y, 0f, -0.18f, 0.2f, 0.036f, 0.005f));
                    c = Toon.Over(c, Gold, Toon.Box(x, y, 0.08f, -0.18f, 0.026f, 0.03f, 0.005f));
                    c = Toon.Over(c, Toon.Shade(Shirt, 0.86f), Toon.Capsule(x, y, new Vector2(-0.02f, 0.12f), new Vector2(0f, -0.05f), 0.006f, 0.006f));
                    return c;
                });
        }

        public static Sprite TorsoFront(bool back)
        {
            return Toon.Make(back ? "FarmerTorsoBack" : "FarmerTorsoFront", 0.5f, 0.54f,
                (x, y) => Toon.Box(x, y, 0f, 0f, 0.19f, 0.23f, 0.1f),
                Shirt,
                (x, y) =>
                {
                    Color c = Shirt;
                    if (!back)
                    {
                        c = Toon.Over(c, Skin, (y - 0.12f) * 4f - Mathf.Abs(x) * 5f > 0f ? 0.02f : -1f);
                        c = Toon.Over(c, Strap, Capsule2(x, y, new Vector2(-0.1f, 0.23f), new Vector2(-0.09f, -0.1f), 0.022f));
                        c = Toon.Over(c, Strap, Capsule2(x, y, new Vector2(0.1f, 0.23f), new Vector2(0.09f, -0.1f), 0.022f));
                        c = Toon.Over(c, new Color(0.75f, 0.73f, 0.68f), Toon.Circle(x, y, 0f, 0.05f, 0.012f));
                        c = Toon.Over(c, new Color(0.75f, 0.73f, 0.68f), Toon.Circle(x, y, 0f, -0.04f, 0.012f));
                    }
                    c = Toon.Over(c, Dark, Toon.Box(x, y, 0f, -0.18f, 0.25f, 0.036f, 0.005f));
                    c = Toon.Over(c, Gold, Toon.Box(x, y, 0f, -0.18f, 0.03f, 0.03f, 0.005f));
                    return c;
                });
        }

        /// <summary>The canasto seen from the side: a woven basket brimming with red cherries, strapped to his back.</summary>
        public static Sprite BasketSide()
        {
            return Toon.Make("FarmerBasketSide", 0.4f, 0.52f,
                (x, y) =>
                {
                    float body = Toon.Box(x, y, 0f, -0.07f, 0.14f, 0.17f, 0.08f);
                    float rim = Toon.Ellipse(x, y, 0f, 0.1f, 0.155f, 0.05f);
                    float fruit = Mathf.Max(Toon.Circle(x, y, -0.07f, 0.145f, 0.05f), Mathf.Max(Toon.Circle(x, y, 0.02f, 0.16f, 0.05f), Toon.Circle(x, y, 0.09f, 0.14f, 0.045f)));
                    return Toon.Blend(Toon.Blend(body, rim, 0.02f), fruit, 0.02f);
                },
                Weave,
                (x, y) =>
                {
                    float cell = Mathf.Abs(Mathf.Sin(x * 70f) * Mathf.Sin(y * 70f));
                    Color c = Color.Lerp(Weave, WeaveDark, 0.3f * (1f - cell));
                    c = Toon.Over(c, WeaveDark, Toon.Ellipse(x, y, 0f, 0.1f, 0.15f, 0.045f));
                    c = Toon.Over(c, Toon.Shade(WeaveDark, 0.6f), Toon.Ellipse(x, y, 0f, 0.105f, 0.12f, 0.03f));
                    c = Toon.Over(c, Cherry, Toon.Circle(x, y, -0.07f, 0.145f, 0.046f));
                    c = Toon.Over(c, Cherry, Toon.Circle(x, y, 0.02f, 0.16f, 0.046f));
                    c = Toon.Over(c, Toon.Shade(Cherry, 1.05f), Toon.Circle(x, y, 0.09f, 0.14f, 0.041f));
                    c = Toon.Over(c, new Color(1f, 0.8f, 0.8f), Toon.Circle(x, y, -0.085f, 0.158f, 0.011f));
                    c = Toon.Over(c, new Color(1f, 0.8f, 0.8f), Toon.Circle(x, y, 0.005f, 0.173f, 0.011f));
                    return c;
                });
        }

        /// <summary>The canasto seen from behind (it covers his back) or peeking over his shoulders from the front.</summary>
        public static Sprite BasketRound(bool wide)
        {
            return Toon.Make(wide ? "FarmerBasketFront" : "FarmerBasketBack", wide ? 0.58f : 0.5f, wide ? 0.4f : 0.5f,
                (x, y) => wide ? Toon.Ellipse(x, y, 0f, -0.02f, 0.27f, 0.17f) : Toon.Circle(x, y, 0f, 0f, 0.23f),
                Weave,
                (x, y) =>
                {
                    float cell = Mathf.Abs(Mathf.Sin(x * 70f) * Mathf.Sin(y * 70f));
                    Color c = Color.Lerp(Weave, WeaveDark, 0.3f * (1f - cell));
                    if (!wide)
                    {
                        c = Toon.Over(c, Toon.Shade(WeaveDark, 0.6f), Toon.Circle(x, y, 0f, 0.01f, 0.165f));
                        Vector2[] spots = { new Vector2(-0.07f, 0.06f), new Vector2(0.06f, 0.05f), new Vector2(0f, -0.03f), new Vector2(-0.07f, -0.07f), new Vector2(0.07f, -0.06f) };
                        foreach (Vector2 s in spots)
                        {
                            c = Toon.Over(c, Cherry, Toon.Circle(x, y, s.x, s.y, 0.05f));
                            c = Toon.Over(c, new Color(1f, 0.8f, 0.8f), Toon.Circle(x, y, s.x - 0.015f, s.y + 0.02f, 0.012f));
                        }
                    }
                    else
                    {
                        c = Toon.Over(c, Toon.Shade(WeaveDark, 0.7f), Toon.Ellipse(x, y, 0f, 0.06f, 0.24f, 0.08f));
                        for (int i = 0; i < 5; i++)
                        {
                            float cx = -0.16f + i * 0.08f;
                            c = Toon.Over(c, Cherry, Toon.Circle(x, y, cx, 0.1f + (i % 2) * 0.015f, 0.04f));
                            c = Toon.Over(c, new Color(1f, 0.8f, 0.8f), Toon.Circle(x, y, cx - 0.012f, 0.115f + (i % 2) * 0.015f, 0.01f));
                        }
                    }
                    return c;
                });
        }

        // ---------------------------------------------------------------- limbs

        /// <summary>Upper arm (sleeve) hanging from the shoulder joint at the sprite's pivot.</summary>
        public static Sprite UpperArm()
        {
            return Toon.Make("FarmerUpperArm", 0.16f, 0.26f,
                (x, y) => Toon.Capsule(x, y, new Vector2(0f, 0.08f), new Vector2(0f, -0.09f), 0.056f, 0.05f),
                Shirt, null, 0.05f, 0.017f, new Vector2(0.5f, 0.5f + 0.08f / 0.26f));
        }

        /// <summary>Forearm and hand: bare skin with the sleeve cuff rolled up at the elbow.</summary>
        public static Sprite Forearm()
        {
            return Toon.Make("FarmerForearm", 0.16f, 0.26f,
                (x, y) => Mathf.Max(Toon.Capsule(x, y, new Vector2(0f, 0.08f), new Vector2(0f, -0.07f), 0.05f, 0.04f), Toon.Circle(x, y, 0f, -0.1f, 0.05f)),
                Skin,
                (x, y) => Toon.Over(Skin, Shirt, (y - 0.035f)), 0.045f, 0.017f, new Vector2(0.5f, 0.5f + 0.08f / 0.26f));
        }

        /// <summary>Thigh in jeans, pivot at the hip.</summary>
        public static Sprite Thigh()
        {
            return Toon.Make("FarmerThigh", 0.2f, 0.26f,
                (x, y) => Toon.Capsule(x, y, new Vector2(0f, 0.09f), new Vector2(0f, -0.05f), 0.072f, 0.06f),
                Jeans,
                (x, y) => Toon.Over(Jeans, Toon.Shade(Jeans, 1.2f), Toon.Box(x, y, 0.028f, 0f, 0.006f, 0.2f, 0.003f) * 0.6f),
                0.06f, 0.017f, new Vector2(0.5f, 0.5f + 0.09f / 0.26f));
        }

        /// <summary>Shin and boot, pivot at the knee. The side-view boot points forward (+X); the front boot is symmetrical.</summary>
        public static Sprite Shin(bool front, Color? bootColour = null)
        {
            Color boot = bootColour ?? Boot;
            return Toon.Make(front ? "FarmerShinFront" : "FarmerShinSide", 0.26f, 0.28f,
                (x, y) =>
                {
                    float leg = Toon.Capsule(x, y, new Vector2(0f, 0.1f), new Vector2(0f, -0.05f), 0.062f, 0.05f);
                    float bootShape = front
                        ? Toon.Box(x, y, 0f, -0.095f, 0.065f, 0.042f, 0.03f)
                        : Toon.Capsule(x, y, new Vector2(-0.02f, -0.092f), new Vector2(0.08f, -0.092f), 0.045f, 0.04f);
                    return Toon.Blend(leg, bootShape, 0.015f);
                },
                Jeans,
                (x, y) =>
                {
                    Color c = Jeans;
                    c = Toon.Over(c, boot, -0.065f - y);
                    c = Toon.Over(c, Toon.Shade(boot, 0.45f), -0.118f - y);
                    return c;
                },
                0.05f, 0.017f, new Vector2(0.5f, 0.5f + 0.1f / 0.28f));
        }

        /// <summary>A soft oval shadow on the ground.</summary>
        public static Sprite Shadow()
        {
            return Toon.Make("FarmerShadow", 0.7f, 0.2f,
                (x, y) => Toon.Ellipse(x, y, 0f, 0f, 0.34f, 0.09f),
                new Color(0f, 0f, 0f, 1f), null, 0.09f, 0f);
        }

        private static float Capsule2(float x, float y, Vector2 a, Vector2 b, float r)
        {
            return Toon.Capsule(x, y, a, b, r, r);
        }
    }
}
