using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>Draws each bonus item (coffee cherry, banana, panela, a cup of tinto...) from a few tinted sprites (discs and bars), about one tile across.</summary>
    public static class PacmanFruitArt
    {
        private static readonly Color Red = new Color(0.92f, 0.08f, 0.12f);
        private static readonly Color Green = new Color(0.25f, 0.78f, 0.22f);
        private static readonly Color Brown = new Color(0.55f, 0.33f, 0.12f);
        private static readonly Color Banana = new Color(1f, 0.85f, 0.15f);
        private static readonly Color Shine = new Color(1f, 0.82f, 0.82f);

        /// <summary>Builds the fruit under a parent and returns its root. Sorted above pellets, below Pac-Man.</summary>
        public static Transform Build(Transform parent, FruitKind kind, float scale)
        {
            GameObject root = new GameObject("Fruit_" + kind);
            root.transform.SetParent(parent, false);
            root.transform.localScale = Vector3.one * scale;
            Transform t = root.transform;

            switch (kind)
            {
                case FruitKind.CoffeeCherry:
                    // Two ripe cherries on a twig with a leaf.
                    Bar(t, Green, new Vector2(-0.2f, -0.05f), new Vector2(0.02f, 0.4f), 0.05f, 4);
                    Bar(t, Green, new Vector2(0.2f, -0.08f), new Vector2(0.02f, 0.4f), 0.05f, 4);
                    Blob(t, Green, new Vector2(0.2f, 0.4f), new Vector2(0.34f, 0.15f), 4);
                    Blob(t, Red, new Vector2(-0.2f, -0.2f), 0.46f, 5);
                    Blob(t, Red, new Vector2(0.2f, -0.23f), 0.46f, 5);
                    Blob(t, Shine, new Vector2(-0.29f, -0.1f), 0.1f, 6);
                    Blob(t, Shine, new Vector2(0.11f, -0.13f), 0.1f, 6);
                    break;

                case FruitKind.Banana:
                    // A curved hand of banana built from three arcs of discs.
                    for (int i = 0; i < 9; i++)
                    {
                        float a = Mathf.Lerp(-0.9f, 0.9f, i / 8f);
                        Vector2 p = new Vector2(Mathf.Sin(a) * 0.42f, -Mathf.Cos(a) * 0.3f + 0.18f);
                        Blob(t, Banana, p, 0.3f, 5);
                    }
                    Blob(t, Brown, new Vector2(-0.38f, 0.04f), 0.12f, 6);
                    Blob(t, Brown, new Vector2(0.38f, 0.04f), 0.12f, 6);
                    Blob(t, Green, new Vector2(0f, 0.38f), new Vector2(0.4f, 0.1f), 6);
                    break;

                case FruitKind.Arepa:
                    // An arepa: a round corn cake, golden, with grill marks.
                    Blob(t, new Color(0.82f, 0.58f, 0.24f), new Vector2(0f, 0f), 0.82f, 5);
                    Blob(t, new Color(0.97f, 0.82f, 0.46f), new Vector2(0f, 0.02f), 0.72f, 6);
                    Bar(t, new Color(0.45f, 0.26f, 0.08f), new Vector2(-0.26f, -0.12f), new Vector2(0.12f, 0.24f), 0.06f, 7);
                    Bar(t, new Color(0.45f, 0.26f, 0.08f), new Vector2(-0.14f, -0.2f), new Vector2(0.24f, 0.14f), 0.06f, 7);
                    Bar(t, new Color(0.45f, 0.26f, 0.08f), new Vector2(-0.34f, 0.0f), new Vector2(0.0f, 0.32f), 0.06f, 7);
                    break;

                case FruitKind.Panela:
                    // A block of unrefined cane sugar, with a lighter top face.
                    Part(t, PacmanSprites.Square, new Color(0.62f, 0.34f, 0.08f), new Vector2(0f, -0.05f), new Vector3(0.78f, 0.52f, 1f), 0f, 5);
                    Part(t, PacmanSprites.Square, new Color(0.86f, 0.55f, 0.16f), new Vector2(0f, 0.08f), new Vector3(0.78f, 0.22f, 1f), 0f, 6);
                    Part(t, PacmanSprites.Square, new Color(0.45f, 0.24f, 0.05f), new Vector2(0f, -0.17f), new Vector3(0.78f, 0.04f, 1f), 0f, 6);
                    Blob(t, new Color(1f, 0.8f, 0.45f), new Vector2(-0.22f, 0.08f), 0.08f, 7);
                    break;

                case FruitKind.Tinto:
                    // A steaming cup of black coffee, side view.
                    Part(t, PacmanSprites.Square, new Color(0.96f, 0.94f, 0.88f), new Vector2(-0.04f, -0.12f), new Vector3(0.58f, 0.42f, 1f), 0f, 5);
                    Blob(t, new Color(0.96f, 0.94f, 0.88f), new Vector2(-0.04f, -0.32f), new Vector2(0.58f, 0.2f), 5);
                    Blob(t, new Color(0.96f, 0.94f, 0.88f), new Vector2(0.32f, -0.1f), 0.3f, 4);
                    Blob(t, new Color(0.012f, 0.016f, 0.012f), new Vector2(0.32f, -0.1f), 0.14f, 5);
                    Blob(t, new Color(0.2f, 0.1f, 0.05f), new Vector2(-0.04f, 0.09f), new Vector2(0.58f, 0.14f), 6);
                    Bar(t, new Color(1f, 1f, 1f, 0.7f), new Vector2(-0.14f, 0.2f), new Vector2(-0.08f, 0.44f), 0.05f, 7);
                    Bar(t, new Color(1f, 1f, 1f, 0.7f), new Vector2(0.06f, 0.2f), new Vector2(0.12f, 0.44f), 0.05f, 7);
                    break;

                case FruitKind.Sack:
                    // A burlap sack of beans, tied at the neck.
                    Blob(t, new Color(0.78f, 0.6f, 0.34f), new Vector2(0f, -0.12f), new Vector2(0.78f, 0.7f), 5);
                    Blob(t, new Color(0.78f, 0.6f, 0.34f), new Vector2(0f, 0.22f), new Vector2(0.34f, 0.4f), 5);
                    Bar(t, new Color(0.35f, 0.2f, 0.08f), new Vector2(-0.16f, 0.14f), new Vector2(0.16f, 0.14f), 0.08f, 6);
                    Blob(t, new Color(0.9f, 0.76f, 0.5f), new Vector2(-0.18f, -0.02f), new Vector2(0.14f, 0.3f), 6);
                    // The Colombian flag stripes down the front, as on a "Café de Colombia" sack.
                    Part(t, PacmanSprites.Square, new Color(0.99f, 0.82f, 0.09f), new Vector2(-0.07f, -0.14f), new Vector3(0.09f, 0.5f, 1f), 0f, 7);
                    Part(t, PacmanSprites.Square, new Color(0.0f, 0.22f, 0.65f), new Vector2(0.02f, -0.14f), new Vector3(0.09f, 0.5f, 1f), 0f, 7);
                    Part(t, PacmanSprites.Square, new Color(0.81f, 0.07f, 0.17f), new Vector2(0.11f, -0.14f), new Vector3(0.09f, 0.5f, 1f), 0f, 7);
                    break;

                case FruitKind.Sombrero:
                    // The farmer's own hat: wide brim, tall crown, dark band.
                    Blob(t, new Color(0.95f, 0.88f, 0.68f), new Vector2(0f, -0.14f), new Vector2(0.98f, 0.3f), 5);
                    Blob(t, new Color(0.98f, 0.93f, 0.78f), new Vector2(0f, 0.1f), new Vector2(0.52f, 0.5f), 5);
                    Part(t, PacmanSprites.Square, new Color(0.12f, 0.08f, 0.06f), new Vector2(0f, -0.02f), new Vector3(0.52f, 0.1f, 1f), 0f, 6);
                    Blob(t, new Color(1f, 0.97f, 0.88f), new Vector2(-0.1f, 0.2f), new Vector2(0.14f, 0.16f), 6);
                    // Sombrero vueltiao: black and white weave on the brim edge.
                    for (int i = 0; i < 5; i++)
                    {
                        Part(t, PacmanSprites.Square, new Color(0.1f, 0.07f, 0.05f), new Vector2(-0.36f + i * 0.18f, -0.26f), new Vector3(0.07f, 0.06f, 1f), 0f, 6);
                    }
                    break;

                default:
                    // The golden bean: a big gold coffee bean with its crease.
                    Blob(t, new Color(1f, 0.82f, 0.18f), new Vector2(0f, -0.02f), new Vector2(0.62f, 0.86f), 5);
                    Blob(t, new Color(1f, 0.95f, 0.6f), new Vector2(-0.12f, 0.14f), new Vector2(0.14f, 0.28f), 6);
                    Bar(t, new Color(0.55f, 0.32f, 0.04f), new Vector2(0f, 0.34f), new Vector2(0.05f, -0.34f), 0.07f, 6);
                    break;
            }
            return t;
        }

        private static void Blob(Transform parent, Color colour, Vector2 position, float diameter, int order)
        {
            Blob(parent, colour, position, new Vector2(diameter, diameter), order);
        }

        private static void Blob(Transform parent, Color colour, Vector2 position, Vector2 size, int order)
        {
            Part(parent, PacmanSprites.Disc, colour, position, new Vector3(size.x, size.y, 1f), 0f, order);
        }

        /// <summary>A straight bar between two points, as a stretched square.</summary>
        private static void Bar(Transform parent, Color colour, Vector2 from, Vector2 to, float width, int order)
        {
            Vector2 delta = to - from;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - 90f;
            Part(parent, PacmanSprites.Square, colour, (from + to) * 0.5f, new Vector3(width, delta.magnitude, 1f), angle, order);
        }

        private static void Part(Transform parent, Sprite sprite, Color colour, Vector2 position, Vector3 scale, float angle, int order)
        {
            GameObject part = new GameObject("Part");
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            part.transform.localScale = scale;
            SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = colour;
            renderer.sortingOrder = order;
        }
    }
}
