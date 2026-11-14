using System.Collections.Generic;
using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// The pest's look, drawn with sprites: a cartoon person whose arms and legs swing while the body bobs and
    /// leans, or a snake of rounded segments that wriggles along a travelling wave; per-character props, eyes that
    /// look where it is heading, the frightened "sprayed" blue face with a white flash before it ends, and eyes
    /// only while eaten. It reads the ghost and game state but never changes them.
    /// Characters by ghost slot: Blinky = the DIAN agent (suit, red tie, briefcase), Pinky = the guerrilla
    /// fighter (beret with a star, camouflage), Inky = the narco (sunglasses, gold chain), Clyde = the
    /// coral snake. All are played for laughs: no weapons, no gore.
    /// </summary>
    public class PacmanGhostView : MonoBehaviour
    {
        // Frightened pests are blue: the copper-sulphate spray (caldo bordeles) farmers use on coffee.
        private static readonly Color FrightenedBlue = new Color(0.1f, 0.18f, 0.9f);
        private static readonly Color FlashWhite = new Color(0.95f, 0.95f, 1f);
        private static readonly Color CoralRed = new Color(0.92f, 0.13f, 0.12f);
        private static readonly Color CoralYellow = new Color(1f, 0.86f, 0.18f);
        private static readonly Color CoralBlack = new Color(0.14f, 0.1f, 0.1f);
        private const int SnakeSegments = 16;

        /// <summary>A body part tinted by the pest's colour, scaled by a factor (pants darker than the shirt).</summary>
        private struct TintedPart
        {
            public SpriteRenderer Renderer;
            public float Factor;
        }

        [Tooltip("Walk cycles per second.")]
        [SerializeField] private float walkRate = 2.1f;
        [Tooltip("Walk cycles per second while frightened (they scurry).")]
        [SerializeField] private float scaredWalkRate = 3.4f;
        [Tooltip("Seconds of frightened time left when the ghost starts to flash.")]
        [SerializeField] private float flashSeconds = 2f;
        [Tooltip("Flashes per second near the end of fright.")]
        [SerializeField] private float flashRate = 4f;

        private Vector2 eyeSize = new Vector2(0.14f, 0.18f);
        private float pupilSize = 0.08f;
        private Vector2[] eyeSpots = { new Vector2(-0.085f, 0.23f), new Vector2(0.085f, 0.23f) };
        private bool snake;
        private Transform rig;
        private Transform[] shades = new Transform[0];
        private Vector2[] shadeBase = new Vector2[0];
        private Color baseColour;
        private SpriteRenderer body;
        private SpriteRenderer face;
        private readonly List<TintedPart> tinted = new List<TintedPart>();
        private readonly List<SpriteRenderer> marks = new List<SpriteRenderer>();
        private Transform[] legs = new Transform[2];
        private Transform[] arms = new Transform[2];
        private Transform[] segments;
        private SpriteRenderer[] segmentRenderers;
        private Color[] segmentColours;
        private Transform snakeHead;
        private Transform tongue;
        private SpriteRenderer glowSource;
        private Transform[] pupils;
        private SpriteRenderer[] pupilRenderers;
        private SpriteRenderer[] eyeRenderers;
        private PacmanGhost ghost;
        private PacmanGame game;
        private Vector2 look = Vector2.left;
        private float walk;
        private float lean;
        private float heading;
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
            walk = Random.value * 6.28f;
            rig = new GameObject("Rig").transform;
            rig.SetParent(transform, false);

            // A soft shadow on the ground, so the pests stand on the earth like the picker does.
            NewPart("Shadow", PacmanFarmerArt.Shadow(), new Vector2(0f, snake ? -0.22f : -0.42f), Vector2.one * 0.9f, 3, transform).color = new Color(0f, 0f, 0f, 0.35f);

            if (snake)
            {
                eyeSize = new Vector2(0.1f, 0.12f);
                pupilSize = 0.06f;
                eyeSpots = new[] { new Vector2(-0.075f, 0.04f), new Vector2(0.075f, 0.04f) };
                BuildSnake();
            }
            else
            {
                BuildPerson(kind, colour);
            }

            face = NewPart("Face", PacmanSprites.FrightFace, snake ? new Vector2(0f, 0.05f) : new Vector2(0f, 0.04f), Vector2.one * 0.8f, 8, snakeHead != null ? snakeHead : rig);
            eyeRenderers = new SpriteRenderer[2];
            pupilRenderers = new SpriteRenderer[2];
            pupils = new Transform[2];
            Transform eyeParent = snake ? snakeHead : rig;
            for (int i = 0; i < 2; i++)
            {
                eyeRenderers[i] = NewPart("Eye", PacmanPestArt.Eye(eyeSize.x, eyeSize.y), eyeSpots[i], Vector2.one, 8, eyeParent);
                pupilRenderers[i] = NewPart("Pupil", PacmanPestArt.Pupil(pupilSize), eyeSpots[i], Vector2.one, 9, eyeParent);
                pupils[i] = pupilRenderers[i].transform;
            }
        }

        // ---------------------------------------------------------------- person

        private void BuildPerson(GhostKind kind, Color colour)
        {
            body = NewPart("Torso", PacmanPestArt.PersonBody(), Vector2.zero, Vector2.one, 6, rig);
            body.color = colour;
            PacmanGlow.Attach(transform, colour, 2.2f, 0.2f, body);

            // Legs and arms hang from joints so they can swing; trousers a shade darker than the shirt.
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                SpriteRenderer leg = NewPart("Leg", PacmanPestArt.Leg(), new Vector2(side * 0.1f, -0.24f), Vector2.one, 4, rig);
                legs[i] = leg.transform;
                tinted.Add(new TintedPart { Renderer = leg, Factor = 0.62f });
                SpriteRenderer arm = NewPart("Arm", PacmanPestArt.Arm(), new Vector2(side * 0.27f, -0.02f), Vector2.one, 5, rig);
                arms[i] = arm.transform;
                tinted.Add(new TintedPart { Renderer = arm, Factor = 0.92f });
                marks.Add(NewPart("Hand", PacmanPestArt.Hand(), new Vector2(0f, -0.13f), Vector2.one, 6, arms[i]));
            }
            tinted.Add(new TintedPart { Renderer = body, Factor = 1f });

            BuildMarks(kind, colour);
        }

        /// <summary>The clothes and props that tell the three people apart besides their colour. Shown only in the normal look.</summary>
        private void BuildMarks(GhostKind kind, Color colour)
        {
            switch (kind)
            {
                case GhostKind.Blinky:
                    // DIAN agent: white shirt and red tie under a suit, side parting, and a briefcase of unpaid paperwork.
                    marks.Add(NewPart("ShirtTie", PacmanPestArt.ShirtAndTie(), new Vector2(0f, -0.08f), Vector2.one, 7, rig));
                    marks.Add(NewPart("Hair", PacmanPestArt.Hair(new Color(0.2f, 0.14f, 0.1f)), new Vector2(-0.02f, 0.37f), Vector2.one, 7, rig));
                    marks.Add(NewPart("Case", PacmanPestArt.Briefcase(), new Vector2(0.02f, -0.22f), Vector2.one, 7, arms[1]));
                    break;
                case GhostKind.Pinky:
                    // Guerrilla fighter: maroon beret with a yellow star, camouflage blotches, a bandana under the chin.
                    SpriteRenderer blotches = NewPart("Camo", PacmanPestArt.Blotches(), new Vector2(0f, -0.1f), Vector2.one, 7, rig);
                    blotches.color = new Color(colour.r * 0.45f, colour.g * 0.45f, colour.b * 0.45f);
                    marks.Add(blotches);
                    marks.Add(NewPart("Bandana", PacmanPestArt.Bandana(), new Vector2(0f, 0.04f), Vector2.one, 7, rig));
                    marks.Add(NewPart("Beret", PacmanPestArt.Beret(), new Vector2(0.03f, 0.4f), Vector2.one, 10, rig));
                    marks.Add(NewPart("Star", PacmanPestArt.Star(), new Vector2(-0.07f, 0.4f), Vector2.one, 11, rig));
                    break;
                default:
                    // Narco: dark sunglasses, moustache, thick gold chain and guayabera pleats.
                    marks.Add(NewPart("Pleats", PacmanPestArt.Pleats(), new Vector2(0f, -0.12f), Vector2.one, 7, rig));
                    marks.Add(NewPart("Moustache", PacmanPestArt.Moustache(), new Vector2(0f, 0.12f), Vector2.one, 9, rig));
                    marks.Add(NewPart("Chain", PacmanPestArt.Chain(), new Vector2(0f, 0.0f), Vector2.one, 8, rig));
                    shades = new Transform[1];
                    shadeBase = new[] { new Vector2(0f, 0.235f) };
                    SpriteRenderer lens = NewPart("Shades", PacmanPestArt.Shades(), shadeBase[0], Vector2.one, 10, rig);
                    shades[0] = lens.transform;
                    marks.Add(lens);
                    break;
            }
        }

        // ---------------------------------------------------------------- snake

        private void BuildSnake()
        {
            segments = new Transform[SnakeSegments];
            segmentRenderers = new SpriteRenderer[SnakeSegments];
            segmentColours = new Color[SnakeSegments];
            Color[] pattern = { CoralRed, CoralRed, CoralYellow, CoralBlack, CoralYellow };
            for (int i = 0; i < SnakeSegments; i++)
            {
                // Tail first so the head end overlaps it; the bands run red, yellow, black, yellow, red.
                SpriteRenderer seg = NewPart("Segment", PacmanPestArt.SnakeBall(), Vector2.zero, Vector2.one, 5 + (SnakeSegments - i) / 8, rig);
                seg.sortingOrder = 4 + (SnakeSegments - i);
                segments[i] = seg.transform;
                segmentRenderers[i] = seg;
                segmentColours[i] = pattern[i % pattern.Length];
                seg.color = segmentColours[i];
            }
            SpriteRenderer head = NewPart("SnakeHead", PacmanPestArt.SnakeHead(), new Vector2(0f, 0.3f), new Vector2(0.85f, 0.85f), 30, rig);
            snakeHead = head.transform;
            body = head;
            marks.Add(head);
            SpriteRenderer tongueRenderer = NewPart("Tongue", PacmanPestArt.Tongue(), new Vector2(0f, 0.17f), Vector2.one, 29, snakeHead);
            tongue = tongueRenderer.transform;
            marks.Add(tongueRenderer);

            glowSource = NewPart("GlowSource", null, Vector2.zero, Vector2.one, 0, transform);
            glowSource.color = CoralRed;
            PacmanGlow.Attach(transform, CoralRed, 2.2f, 0.2f, glowSource);
        }

        // ---------------------------------------------------------------- helpers

        private SpriteRenderer NewPart(string name, Sprite sprite, Vector2 position, Vector2 scale, int order, Transform parent)
        {
            GameObject part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }

        // ---------------------------------------------------------------- per frame

        private void Update()
        {
            if (rig == null)
            {
                return;
            }
            float dt = Time.deltaTime;

            // Look: 0 normal, 1 frightened blue, 2 frightened flash, 3 eaten (eyes only).
            int state = 0;
            bool moving = true;
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
                moving = ghost.Dir != Vector2Int.zero;
            }

            bool scared = state == 1 || state == 2;
            walk += dt * (scared ? scaredWalkRate : walkRate) * 2f * Mathf.PI * (moving ? 1f : 0.35f);
            if (snake)
            {
                AnimateSnake(dt, scared);
            }
            else
            {
                AnimatePerson(dt, scared);
            }

            PlacePupils();
            if (state != shownLook)
            {
                shownLook = state;
                Apply(state);
            }
        }

        /// <summary>Legs and arms swing against each other, the body bobs twice per step, squashes a little and leans into the move.</summary>
        private void AnimatePerson(float dt, bool scared)
        {
            float s = Mathf.Sin(walk);
            float swing = scared ? 42f : 32f;
            legs[0].localRotation = Quaternion.Euler(0f, 0f, swing * s);
            legs[1].localRotation = Quaternion.Euler(0f, 0f, -swing * s);
            arms[0].localRotation = Quaternion.Euler(0f, 0f, -swing * 0.85f * s + (scared ? 30f : 0f));
            arms[1].localRotation = Quaternion.Euler(0f, 0f, swing * 0.85f * s - (scared ? 30f : 0f));

            float bounce = 0.5f + 0.5f * Mathf.Cos(2f * walk);
            float squash = 0.028f * Mathf.Cos(2f * walk);
            rig.localPosition = new Vector3(0f, 0.03f * bounce, 0f);
            rig.localScale = new Vector3(1f + squash, 1f - squash, 1f);
            float targetLean = -look.x * 5f + 2.5f * s + (scared ? 4f * Mathf.Sin(Time.time * 38f) : 0f);
            lean = Mathf.Lerp(lean, targetLean, 1f - Mathf.Exp(-14f * dt));
            rig.localRotation = Quaternion.Euler(0f, 0f, lean);
        }

        /// <summary>A travelling wave runs down the chain of segments; the whole snake turns smoothly toward where it is going.</summary>
        private void AnimateSnake(float dt, bool scared)
        {
            float targetHeading = Mathf.Atan2(look.y, look.x) * Mathf.Rad2Deg - 90f;
            heading = Mathf.LerpAngle(heading, targetHeading, 1f - Mathf.Exp(-9f * dt));
            rig.localRotation = Quaternion.Euler(0f, 0f, heading);

            float amplitude = scared ? 0.2f : 0.15f;
            for (int i = 0; i < SnakeSegments; i++)
            {
                float t = i / (SnakeSegments - 1f);
                float y = Mathf.Lerp(0.14f, -0.46f, t);
                float x = amplitude * Mathf.Min(1f, t * 4f) * Mathf.Sin(t * 11f - walk * 1.25f);
                segments[i].localPosition = new Vector3(x, y, 0f);
                float radius = Mathf.Lerp(0.82f, 0.3f, Mathf.Pow(t, 1.15f));
                segments[i].localScale = new Vector3(radius, radius, 1f);
            }
            // The head sways against the neck, the tongue flicks in and out.
            float neck = amplitude * Mathf.Min(1f, 1f / 4f) * Mathf.Sin(0f - walk * 1.25f);
            snakeHead.localPosition = new Vector3(neck * 0.6f, 0.3f, 0f);
            snakeHead.localRotation = Quaternion.Euler(0f, 0f, 9f * Mathf.Cos(walk * 1.25f));
            float flick = Mathf.Max(0f, Mathf.Sin(Time.time * 7f));
            tongue.localScale = new Vector3(1f, 0.15f + 0.85f * flick * flick, 1f);
        }

        /// <summary>Slides the pupils toward the direction of travel (maze rows grow downward, the world's y grows up).</summary>
        private void PlacePupils()
        {
            Vector2 local = look;
            if (snake)
            {
                // The snake's head is turned toward its heading: express the look in the head's own frame.
                local = Quaternion.Inverse(rig.rotation) * (Vector3)look;
            }
            for (int i = 0; i < shades.Length; i++)
            {
                shades[i].localPosition = shadeBase[i] + new Vector2(local.x * 0.02f, local.y * 0.03f);
            }
            for (int i = 0; i < pupils.Length; i++)
            {
                pupils[i].localPosition = eyeSpots[i] + new Vector2(local.x * 0.03f, local.y * 0.04f);
            }
        }

        private void Apply(int state)
        {
            bool frightened = state == 1 || state == 2;
            Color tint = state == 1 ? FrightenedBlue : state == 2 ? FlashWhite : baseColour;

            if (snake)
            {
                for (int i = 0; i < SnakeSegments; i++)
                {
                    segmentRenderers[i].enabled = state != 3;
                    segmentRenderers[i].color = state == 0 ? segmentColours[i] : tint;
                }
                glowSource.enabled = state != 3;
                glowSource.color = state == 0 ? CoralRed : tint;
                snakeHead.GetComponent<SpriteRenderer>().color = frightened ? tint : Color.white;
            }

            foreach (TintedPart part in tinted)
            {
                part.Renderer.enabled = state != 3;
                float f = part.Factor;
                part.Renderer.color = new Color(tint.r * f, tint.g * f, tint.b * f, 1f);
            }
            foreach (SpriteRenderer mark in marks)
            {
                // A snake's head stays visible while frightened (tinted); clothes and props only in the normal look.
                mark.enabled = snake ? state != 3 : state == 0;
            }
            if (face != null)
            {
                face.enabled = frightened;
                face.color = state == 2 ? new Color(0.9f, 0.1f, 0.15f) : new Color(1f, 0.85f, 0.75f);
            }

            // Frightened pests have no round eyes: the face sprite carries its own.
            for (int i = 0; i < eyeRenderers.Length; i++)
            {
                eyeRenderers[i].enabled = !frightened;
                pupilRenderers[i].enabled = !frightened;
            }
        }
    }
}
