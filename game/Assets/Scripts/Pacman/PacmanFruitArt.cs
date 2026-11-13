using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>Draws each bonus fruit from a few tinted sprites (discs and bars), about one tile across.</summary>
    public static class PacmanFruitArt
    {
        private static readonly Color Red = new Color(0.92f, 0.08f, 0.12f);
        private static readonly Color Green = new Color(0.25f, 0.78f, 0.22f);
        private static readonly Color Brown = new Color(0.55f, 0.33f, 0.12f);
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
                case FruitKind.Cherry:
                    Bar(t, Green, new Vector2(-0.2f, -0.05f), new Vector2(0.06f, 0.42f), 0.05f, 4);
                    Bar(t, Green, new Vector2(0.2f, -0.08f), new Vector2(0.06f, 0.42f), 0.05f, 4);
                    Blob(t, Red, new Vector2(-0.2f, -0.2f), 0.46f, 5);
                    Blob(t, Red, new Vector2(0.2f, -0.23f), 0.46f, 5);
                    Blob(t, Shine, new Vector2(-0.29f, -0.1f), 0.1f, 6);
                    Blob(t, Shine, new Vector2(0.11f, -0.13f), 0.1f, 6);
                    break;

                case FruitKind.Strawberry:
                    Blob(t, Red, new Vector2(0f, -0.06f), new Vector2(0.74f, 0.82f), 5);
                    Blob(t, Green, new Vector2(0f, 0.34f), new Vector2(0.5f, 0.16f), 6);
                    Bar(t, Green, new Vector2(0f, 0.32f), new Vector2(0f, 0.46f), 0.05f, 6);
                    for (int i = 0; i < 6; i++)
                    {
                        float x = (i % 3 - 1) * 0.2f + (i / 3) * 0.1f - 0.05f;
                        float y = 0.14f - (i / 3) * 0.3f;
                        Blob(t, Shine, new Vector2(x, y), 0.07f, 6);
                    }
                    break;

                case FruitKind.Orange:
                    Blob(t, new Color(1f, 0.55f, 0.08f), new Vector2(0f, -0.06f), 0.78f, 5);
                    Blob(t, new Color(1f, 0.8f, 0.45f), new Vector2(-0.14f, 0.06f), 0.16f, 6);
                    Blob(t, Green, new Vector2(0.07f, 0.36f), new Vector2(0.24f, 0.14f), 6);
                    Bar(t, Brown, new Vector2(0f, 0.3f), new Vector2(0f, 0.42f), 0.05f, 6);
                    break;

                case FruitKind.Apple:
                    Blob(t, Red, new Vector2(-0.14f, -0.08f), 0.54f, 5);
                    Blob(t, Red, new Vector2(0.14f, -0.08f), 0.54f, 5);
                    Blob(t, Red, new Vector2(0f, -0.2f), 0.5f, 5);
                    Blob(t, Shine, new Vector2(-0.22f, 0.0f), 0.1f, 6);
                    Bar(t, Brown, new Vector2(0f, 0.2f), new Vector2(0.04f, 0.42f), 0.05f, 6);
                    Blob(t, Green, new Vector2(0.14f, 0.38f), new Vector2(0.22f, 0.11f), 6);
                    break;

                case FruitKind.Melon:
                    Blob(t, new Color(0.35f, 0.75f, 0.3f), new Vector2(0f, -0.06f), 0.8f, 5);
                    for (int i = 0; i < 7; i++)
                    {
                        float x = ((i * 37) % 5 - 2) * 0.13f;
                        float y = ((i * 53) % 5 - 3) * 0.13f;
                        Blob(t, new Color(0.7f, 0.95f, 0.6f), new Vector2(x, y), 0.09f, 6);
                    }
                    Bar(t, Brown, new Vector2(0f, 0.32f), new Vector2(0f, 0.44f), 0.05f, 6);
                    break;

                case FruitKind.Galaxian:
                    Blob(t, new Color(0.25f, 0.35f, 1f), new Vector2(0f, 0.12f), new Vector2(0.86f, 0.3f), 5);
                    Blob(t, new Color(0.25f, 0.35f, 1f), new Vector2(-0.38f, 0.28f), new Vector2(0.14f, 0.34f), 5);
                    Blob(t, new Color(0.25f, 0.35f, 1f), new Vector2(0.38f, 0.28f), new Vector2(0.14f, 0.34f), 5);
                    Blob(t, new Color(1f, 0.9f, 0.1f), new Vector2(0f, 0.0f), new Vector2(0.22f, 0.7f), 6);
                    Blob(t, Red, new Vector2(0f, 0.34f), 0.2f, 7);
                    break;

                case FruitKind.Bell:
                    Blob(t, new Color(1f, 0.85f, 0.1f), new Vector2(0f, 0.1f), new Vector2(0.66f, 0.7f), 5);
                    Blob(t, new Color(1f, 0.85f, 0.1f), new Vector2(0f, -0.26f), new Vector2(0.84f, 0.22f), 5);
                    Blob(t, new Color(0.4f, 0.95f, 1f), new Vector2(0f, -0.42f), 0.15f, 6);
                    Blob(t, Shine, new Vector2(-0.14f, 0.2f), new Vector2(0.1f, 0.2f), 6);
                    break;

                default:
                    Blob(t, new Color(0.4f, 0.95f, 1f), new Vector2(0f, 0.28f), 0.4f, 5);
                    Blob(t, new Color(0.012f, 0.016f, 0.05f), new Vector2(0f, 0.28f), 0.17f, 6);
                    Bar(t, new Color(0.9f, 0.95f, 1f), new Vector2(0f, 0.1f), new Vector2(0f, -0.42f), 0.1f, 5);
                    Bar(t, new Color(0.9f, 0.95f, 1f), new Vector2(0f, -0.3f), new Vector2(0.2f, -0.3f), 0.1f, 5);
                    Bar(t, new Color(0.9f, 0.95f, 1f), new Vector2(0f, -0.42f), new Vector2(0.2f, -0.42f), 0.1f, 5);
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
