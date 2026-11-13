using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// The pest's look, drawn with sprites: a walking cartoon person or a wriggling snake, per-character
    /// props, eyes that look where it is heading, the frightened "sprayed" blue face with a white flash
    /// before it ends, and eyes only while eaten. It reads the ghost and game state but never changes them.
    /// Characters by ghost slot: Blinky = the DIAN agent (suit, red tie, briefcase), Pinky = the guerrilla
    /// fighter (beret with a star, camouflage), Inky = the narco (sunglasses, gold chain), Clyde = the
    /// coral snake. All are played for laughs: no weapons, no gore.
    /// </summary>
    public class PacmanGhostView : MonoBehaviour
    {
        // Frightened pests are blue: the copper-sulphate spray (caldo bordeles) farmers use on coffee.
        private static readonly Color FrightenedBlue = new Color(0.1f, 0.18f, 0.9f);
        private static readonly Color FlashWhite = new Color(0.95f, 0.95f, 1f);
        private static readonly Color Pupil = new Color(0.12f, 0.06f, 0.03f);

        [Tooltip("Hem ripple frames per second.")]
        [SerializeField] private float rippleRate = 6f;
        [Tooltip("Seconds of frightened time left when the ghost starts to flash.")]
        [SerializeField] private float flashSeconds = 2f;
        [Tooltip("Flashes per second near the end of fright.")]
        [SerializeField] private float flashRate = 4f;

        private Vector2 eyeSize = new Vector2(0.14f, 0.18f);
        private float pupilSize = 0.08f;
        private Vector2[] eyeSpots = { new Vector2(-0.085f, 0.23f), new Vector2(0.085f, 0.23f) };
        private bool snake;
        private Transform[] shades = new Transform[0];
        private Vector2[] shadeBase = new Vector2[0];
        private Color baseColour;
        private SpriteRenderer body;
        private SpriteRenderer face;
        private SpriteRenderer[] marks;
        private Transform[] pupils;
        private SpriteRenderer[] eyeRenderers;
        private PacmanGhost ghost;
        private PacmanGame game;
        private Vector2 look = Vector2.left;
        private int shownFrame = -1;
        private int shownLook = -1;

        /// <summary>Builds a ghost body with its eyes and returns the transform that holds the view.</summary>
        public static Transform Create(Transform parent, Color colour, GhostKind kind)
        {
            GameObject root = new GameObject("Body");
            root.transform.SetParent(parent, false);
            PacmanGhostView view = root.AddComponent<PacmanGhostView>();
            view.Build(colour, kind);
            return root.transform;
        }

        public void Bind(PacmanGhost owner, PacmanGame owningGame)
        {
            ghost = owner;
            game = owningGame;
        }

        private void Build(Color colour, GhostKind kind)
        {
            baseColour = colour;
            snake = kind == GhostKind.Clyde;
            if (snake)
            {
                eyeSize = new Vector2(0.11f, 0.13f);
                pupilSize = 0.06f;
                eyeSpots = new[] { new Vector2(-0.085f, 0.3f), new Vector2(0.085f, 0.3f) };
            }
            body = gameObject.AddComponent<SpriteRenderer>();
            body.sprite = Frame(0);
            body.color = colour;
            body.sortingOrder = 6;
            PacmanGlow.Attach(transform, colour, 2.2f, 0.2f, body);

            marks = BuildMarks(kind, colour);
            face = NewPart("Face", PacmanSprites.FrightFace, new Vector2(0f, 0.04f), Vector2.one * 0.8f, 7);
            eyeRenderers = new SpriteRenderer[2];
            pupils = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                eyeRenderers[i] = NewPart("Eye", PacmanSprites.Disc, eyeSpots[i], eyeSize, 7);
                SpriteRenderer pupil = NewPart("Pupil", PacmanSprites.Disc, eyeSpots[i], Vector2.one * pupilSize, 8);
                pupil.color = Pupil;
                pupils[i] = pupil.transform;
            }
        }

        private Sprite Frame(int frame)
        {
            return snake ? PacmanSprites.Snake(frame) : PacmanSprites.Person(frame);
        }

        /// <summary>The clothes and props that tell the four pests apart besides their colour. Shown only in the normal look.</summary>
        private SpriteRenderer[] BuildMarks(GhostKind kind, Color colour)
        {
            Color dark = new Color(colour.r * 0.4f, colour.g * 0.4f, colour.b * 0.4f);
            var list = new System.Collections.Generic.List<SpriteRenderer>();
            Sprite square = PacmanSprites.Square;
            Sprite disc = PacmanSprites.Disc;
            switch (kind)
            {
                case GhostKind.Blinky:
                    // DIAN agent: white shirt, red tie, dark lapels, side parting, and a briefcase of unpaid paperwork.
                    list.Add(NewPart("Shirt", square, new Vector2(0f, -0.04f), new Vector2(0.15f, 0.26f), 6, Color.white));
                    list.Add(NewPart("Tie", square, new Vector2(0f, -0.1f), new Vector2(0.055f, 0.22f), 7, new Color(0.85f, 0.1f, 0.12f)));
                    list.Add(NewPart("Lapel", square, new Vector2(-0.12f, -0.1f), new Vector2(0.035f, 0.26f), 6, dark));
                    list.Add(NewPart("Lapel", square, new Vector2(0.12f, -0.1f), new Vector2(0.035f, 0.26f), 6, dark));
                    list.Add(NewPart("Hair", disc, new Vector2(-0.02f, 0.37f), new Vector2(0.3f, 0.1f), 6, new Color(0.18f, 0.13f, 0.1f)));
                    list.Add(NewPart("Case", square, new Vector2(0.33f, -0.3f), new Vector2(0.2f, 0.15f), 7, new Color(0.5f, 0.28f, 0.12f)));
                    list.Add(NewPart("Handle", square, new Vector2(0.33f, -0.2f), new Vector2(0.09f, 0.03f), 7, new Color(0.2f, 0.1f, 0.05f)));
                    break;
                case GhostKind.Pinky:
                    // Guerrilla fighter: maroon beret with a yellow star, camouflage blotches, a bandana under the chin.
                    list.Add(NewPart("Blotch", disc, new Vector2(-0.14f, -0.06f), new Vector2(0.14f, 0.11f), 6, dark));
                    list.Add(NewPart("Blotch", disc, new Vector2(0.12f, -0.16f), new Vector2(0.16f, 0.1f), 6, dark));
                    list.Add(NewPart("Blotch", disc, new Vector2(-0.05f, -0.28f), new Vector2(0.12f, 0.08f), 6, dark));
                    list.Add(NewPart("Bandana", square, new Vector2(0f, 0.04f), new Vector2(0.3f, 0.05f), 6, new Color(0.85f, 0.2f, 0.15f)));
                    list.Add(NewPart("Beret", disc, new Vector2(0.03f, 0.38f), new Vector2(0.5f, 0.2f), 8, new Color(0.5f, 0.1f, 0.08f)));
                    list.Add(NewPart("Star", disc, new Vector2(-0.07f, 0.4f), Vector2.one * 0.07f, 9, new Color(1f, 0.85f, 0.15f)));
                    break;
                case GhostKind.Inky:
                    // Narco: dark sunglasses, moustache, thick gold chain and guayabera pleats.
                    list.Add(NewPart("Pleat", square, new Vector2(-0.1f, -0.14f), new Vector2(0.02f, 0.3f), 6, new Color(0.7f, 0.58f, 0.3f)));
                    list.Add(NewPart("Pleat", square, new Vector2(0.1f, -0.14f), new Vector2(0.02f, 0.3f), 6, new Color(0.7f, 0.58f, 0.3f)));
                    list.Add(NewPart("Moustache", square, new Vector2(0f, 0.12f), new Vector2(0.14f, 0.03f), 8, new Color(0.1f, 0.06f, 0.04f)));
                    Color gold = new Color(1f, 0.8f, 0.1f);
                    list.Add(NewPart("Chain", disc, new Vector2(-0.1f, 0.0f), Vector2.one * 0.07f, 7, gold));
                    list.Add(NewPart("Chain", disc, new Vector2(0f, -0.04f), Vector2.one * 0.09f, 7, gold));
                    list.Add(NewPart("Chain", disc, new Vector2(0.1f, 0.0f), Vector2.one * 0.07f, 7, gold));
                    Color lens = new Color(0.05f, 0.05f, 0.08f);
                    shades = new Transform[3];
                    shadeBase = new[] { new Vector2(-0.085f, 0.23f), new Vector2(0.085f, 0.23f), new Vector2(0f, 0.245f) };
                    SpriteRenderer left = NewPart("Shade", square, shadeBase[0], new Vector2(0.15f, 0.1f), 9, lens);
                    SpriteRenderer right = NewPart("Shade", square, shadeBase[1], new Vector2(0.15f, 0.1f), 9, lens);
                    SpriteRenderer bridge = NewPart("Bridge", square, shadeBase[2], new Vector2(0.06f, 0.025f), 9, lens);
                    shades[0] = left.transform;
                    shades[1] = right.transform;
                    shades[2] = bridge.transform;
                    list.Add(left);
                    list.Add(right);
                    list.Add(bridge);
                    break;
                default:
                    // Coral snake: red and yellow bands across the body, a forked tongue.
                    Color band = new Color(1f, 0.85f, 0.15f);
                    list.Add(NewPart("Band", square, new Vector2(0.0f, -0.04f), new Vector2(0.2f, 0.035f), 6, band));
                    list.Add(NewPart("Band", square, new Vector2(0.1f, -0.2f), new Vector2(0.17f, 0.035f), 6, band));
                    list.Add(NewPart("Band", square, new Vector2(-0.12f, -0.33f), new Vector2(0.13f, 0.03f), 6, band));
                    list.Add(NewPart("Head", disc, new Vector2(0f, 0.3f), new Vector2(0.38f, 0.28f), 6, new Color(0.12f, 0.1f, 0.1f)));
                    list.Add(NewPart("Tongue", square, new Vector2(0f, 0.47f), new Vector2(0.025f, 0.1f), 6, new Color(0.95f, 0.1f, 0.25f)));
                    list.Add(NewPart("Fork", square, new Vector2(-0.025f, 0.53f), new Vector2(0.025f, 0.04f), 6, new Color(0.95f, 0.1f, 0.25f)));
                    list.Add(NewPart("Fork", square, new Vector2(0.025f, 0.53f), new Vector2(0.025f, 0.04f), 6, new Color(0.95f, 0.1f, 0.25f)));
                    break;
            }
            return list.ToArray();
        }

        private SpriteRenderer NewPart(string name, Sprite sprite, Vector2 position, Vector2 scale, int order, Color colour)
        {
            SpriteRenderer renderer = NewPart(name, sprite, position, scale, order);
            renderer.color = colour;
            return renderer;
        }

        private SpriteRenderer NewPart(string name, Sprite sprite, Vector2 position, Vector2 scale, int order)
        {
            GameObject part = new GameObject(name);
            part.transform.SetParent(transform, false);
            part.transform.localPosition = position;
            part.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }

        private void Update()
        {
            if (body == null)
            {
                return;
            }

            int frame = Mathf.FloorToInt(Time.time * rippleRate) & 1;
            if (frame != shownFrame)
            {
                shownFrame = frame;
                body.sprite = Frame(frame);
            }

            // Look: 0 normal, 1 frightened blue, 2 frightened flash, 3 eaten (eyes only).
            int state = 0;
            if (ghost != null)
            {
                if (ghost.Mode == GhostMode.Eaten)
                {
                    state = 3;
                }
                else if (ghost.IsScared)
                {
                    bool flashing = game != null && game.FrightenedRemaining < flashSeconds
                        && Mathf.FloorToInt(Time.time * flashRate * 2f) % 2 == 0;
                    state = flashing ? 2 : 1;
                }
                if (ghost.Dir != Vector2Int.zero)
                {
                    look = new Vector2(ghost.Dir.x, -ghost.Dir.y);
                }
            }

            PlacePupils();
            if (state != shownLook)
            {
                shownLook = state;
                Apply(state);
            }
        }

        /// <summary>Slides the pupils toward the direction of travel (maze rows grow downward, the world's y grows up).</summary>
        private void PlacePupils()
        {
            for (int i = 0; i < shades.Length; i++)
            {
                shades[i].localPosition = shadeBase[i] + new Vector2(look.x * 0.02f, look.y * 0.03f);
            }
            for (int i = 0; i < pupils.Length; i++)
            {
                pupils[i].localPosition = eyeSpots[i] + new Vector2(look.x * 0.03f, look.y * 0.04f);
            }
        }

        private void Apply(int state)
        {
            bool frightened = state == 1 || state == 2;
            body.enabled = state != 3;
            face.enabled = frightened;
            foreach (SpriteRenderer mark in marks)
            {
                mark.enabled = state == 0;
            }
            face.color = state == 2 ? new Color(0.9f, 0.1f, 0.15f) : new Color(1f, 0.85f, 0.75f);
            body.color = state == 1 ? FrightenedBlue : state == 2 ? FlashWhite : baseColour;

            // Frightened ghosts have no round eyes: the face sprite carries its own.
            for (int i = 0; i < eyeRenderers.Length; i++)
            {
                eyeRenderers[i].enabled = !frightened;
                pupils[i].GetComponent<SpriteRenderer>().enabled = !frightened;
            }
        }
    }
}
