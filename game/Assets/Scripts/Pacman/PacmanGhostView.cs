using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// The pest's look, drawn with sprites: an insect silhouette whose legs scuttle in two frames, per-species
    /// markings, eyes that look where it is heading, the frightened "sprayed" blue face with a white flash
    /// before it ends, and eyes only while eaten. It reads the ghost and game state but never changes them.
    /// Species by ghost slot: Blinky = broca (borer beetle), Pinky = roya (rust fungus), Inky = minador
    /// (leaf miner), Clyde = cochinilla (mealybug).
    /// </summary>
    public class PacmanGhostView : MonoBehaviour
    {
        // Frightened pests are blue: the copper-sulphate spray (caldo bordeles) farmers use on coffee.
        private static readonly Color FrightenedBlue = new Color(0.1f, 0.18f, 0.9f);
        private static readonly Color FlashWhite = new Color(0.95f, 0.95f, 1f);
        private static readonly Color Pupil = new Color(0.12f, 0.06f, 0.03f);
        private static readonly Vector2 EyeSize = new Vector2(0.22f, 0.28f);
        private static readonly Vector2[] EyeSpots = { new Vector2(-0.15f, 0.1f), new Vector2(0.15f, 0.1f) };

        [Tooltip("Hem ripple frames per second.")]
        [SerializeField] private float rippleRate = 6f;
        [Tooltip("Seconds of frightened time left when the ghost starts to flash.")]
        [SerializeField] private float flashSeconds = 2f;
        [Tooltip("Flashes per second near the end of fright.")]
        [SerializeField] private float flashRate = 4f;

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
            body = gameObject.AddComponent<SpriteRenderer>();
            body.sprite = PacmanSprites.Ghost(0);
            body.color = colour;
            body.sortingOrder = 6;
            PacmanGlow.Attach(transform, colour, 2.2f, 0.2f, body);

            marks = BuildMarks(kind, colour);
            face = NewPart("Face", PacmanSprites.FrightFace, Vector2.zero, Vector2.one, 7);
            eyeRenderers = new SpriteRenderer[2];
            pupils = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                eyeRenderers[i] = NewPart("Eye", PacmanSprites.Disc, EyeSpots[i], EyeSize, 7);
                SpriteRenderer pupil = NewPart("Pupil", PacmanSprites.Disc, EyeSpots[i], Vector2.one * 0.12f, 8);
                pupil.color = Pupil;
                pupils[i] = pupil.transform;
            }
        }

        /// <summary>The markings that tell the four pests apart besides their colour. Shown only in the normal look.</summary>
        private SpriteRenderer[] BuildMarks(GhostKind kind, Color colour)
        {
            Color dark = new Color(colour.r * 0.45f, colour.g * 0.45f, colour.b * 0.45f);
            Color light = Color.Lerp(colour, Color.white, 0.6f);
            var list = new System.Collections.Generic.List<SpriteRenderer>();
            switch (kind)
            {
                case GhostKind.Blinky:
                    // Borer beetle: a shell seam and two wing-case dots.
                    list.Add(NewPart("Seam", PacmanSprites.Square, new Vector2(0f, -0.14f), new Vector2(0.03f, 0.4f), 6, dark));
                    list.Add(NewPart("Dot", PacmanSprites.Disc, new Vector2(-0.12f, -0.18f), Vector2.one * 0.1f, 6, dark));
                    list.Add(NewPart("Dot", PacmanSprites.Disc, new Vector2(0.12f, -0.18f), Vector2.one * 0.1f, 6, dark));
                    break;
                case GhostKind.Pinky:
                    // Rust: orange spores scattered over the back.
                    list.Add(NewPart("Spot", PacmanSprites.Disc, new Vector2(-0.14f, -0.12f), Vector2.one * 0.12f, 6, dark));
                    list.Add(NewPart("Spot", PacmanSprites.Disc, new Vector2(0.13f, -0.06f), Vector2.one * 0.09f, 6, dark));
                    list.Add(NewPart("Spot", PacmanSprites.Disc, new Vector2(0.02f, -0.27f), Vector2.one * 0.11f, 6, dark));
                    break;
                case GhostKind.Inky:
                    // Leaf miner: the winding tunnel it leaves, drawn as two bands.
                    list.Add(NewPart("Band", PacmanSprites.Square, new Vector2(0f, -0.1f), new Vector2(0.5f, 0.035f), 6, dark));
                    list.Add(NewPart("Band", PacmanSprites.Square, new Vector2(0f, -0.24f), new Vector2(0.4f, 0.035f), 6, dark));
                    break;
                default:
                    // Mealybug: cottony tufts around its edge.
                    list.Add(NewPart("Tuft", PacmanSprites.Disc, new Vector2(-0.22f, -0.1f), Vector2.one * 0.16f, 6, light));
                    list.Add(NewPart("Tuft", PacmanSprites.Disc, new Vector2(0.22f, -0.1f), Vector2.one * 0.16f, 6, light));
                    list.Add(NewPart("Tuft", PacmanSprites.Disc, new Vector2(-0.1f, -0.3f), Vector2.one * 0.15f, 6, light));
                    list.Add(NewPart("Tuft", PacmanSprites.Disc, new Vector2(0.1f, -0.3f), Vector2.one * 0.15f, 6, light));
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
                body.sprite = PacmanSprites.Ghost(frame);
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
            for (int i = 0; i < pupils.Length; i++)
            {
                pupils[i].localPosition = EyeSpots[i] + new Vector2(look.x * 0.05f, look.y * 0.07f);
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
