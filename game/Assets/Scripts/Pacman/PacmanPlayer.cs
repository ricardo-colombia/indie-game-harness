using UnityEngine;
using UnityEngine.Rendering;

namespace Studio.Pacman
{
    /// <summary>
    /// Player-controlled coffee picker: a campesino with a straw hat, white shirt and canasto, seen in profile and
    /// full body. He walks (legs and arms swing), faces the way he moves, and shows his front or back when going
    /// down or up. Gameplay is the same as the arcade original: only the look changed.
    /// </summary>
    public class PacmanPlayer : PacmanActor
    {
        private const float MinMouth = 0.04f;
        private const float MaxMouth = 0.72f;
        private const float BodyScale = 1.15f;

        private static readonly Color Shirt = new Color(0.96f, 0.95f, 0.9f);
        private static readonly Color Sleeve = new Color(0.88f, 0.87f, 0.82f);
        private static readonly Color Jeans = new Color(0.25f, 0.38f, 0.62f);
        private static readonly Color Skin = new Color(0.78f, 0.52f, 0.34f);
        private static readonly Color Dark = new Color(0.12f, 0.08f, 0.06f);
        private static readonly Color Boot = new Color(0.2f, 0.12f, 0.07f);
        private static readonly Color BrimStraw = new Color(0.86f, 0.7f, 0.42f);
        private static readonly Color CrownStraw = new Color(0.94f, 0.82f, 0.55f);
        private static readonly Color BasketColour = new Color(0.7f, 0.45f, 0.4f);
        private static readonly Color Cherry = new Color(0.88f, 0.1f, 0.14f);

        [Tooltip("Walk cycles per second while moving.")]
        [SerializeField] private float chompRate = 3.2f;

        private enum View { Side, Front, Back }

        private PacmanGame game;
        private Vector2Int desired;
        private Transform body;
        private Transform farmer;
        private Transform[] views = new Transform[3];
        private Transform[][] legs = new Transform[3][];
        private Transform[][] arms = new Transform[3][];
        private View view = View.Side;
        private float facing = -1f;
        private float raise;
        private float phase;
        private float lastMouth = -1f;
        private bool dying;
        private float deathTimer;
        private float deathDuration = 1f;
        private float deathFrom;

        public Vector2Int Desired => desired;

        /// <summary>Walk-cycle openness in radians (0.04..0.72). Probe for the picker's step animation.</summary>
        public float MouthAngle { get; private set; }

        /// <summary>Set by the game when a bean is eaten: the picker reaches out an arm to grab it.</summary>
        public void Gobble()
        {
            raise = 1f;
        }

        public void Init(PacmanGame owner)
        {
            game = owner;

            GameObject bodyObject = new GameObject("Body");
            bodyObject.transform.SetParent(transform, false);
            body = bodyObject.transform;
            body.localScale = Vector3.one * BodyScale;
            BuildFarmer();
            PacmanGlow.Attach(transform, new Color(1f, 0.85f, 0.45f), 3.2f, 0.2f);

            ResetFacing();
            SetMouth(0.5f);
        }

        // ---------------------------------------------------------------- build

        /// <summary>
        /// A campesino seen from the side, full body: straw hat, white shirt, jeans, boots and a canasto on his
        /// back. Walking away or towards the camera swaps to a back or front view. 1 unit = 1 tile, feet at -0.5.
        /// </summary>
        private void BuildFarmer()
        {
            GameObject root = new GameObject("Farmer");
            root.transform.SetParent(body, false);
            farmer = root.transform;
            SortingGroup group = root.AddComponent<SortingGroup>();
            group.sortingOrder = 5;

            BuildSide();
            BuildFront(false);
            BuildFront(true);
            SetView(View.Side);
        }

        private Transform NewView(View v)
        {
            GameObject go = new GameObject(v + "View");
            go.transform.SetParent(farmer, false);
            views[(int)v] = go.transform;
            return go.transform;
        }

        private void BuildSide()
        {
            Transform t = NewView(View.Side);
            int o = 0;
            Transform backArm = Limb(t, new Vector2(0f, 0.14f), Sleeve, 0.27f, 0.1f, ref o, true);
            Transform backLeg = Limb(t, new Vector2(0f, -0.14f), new Color(0.2f, 0.3f, 0.5f), 0.34f, 0.14f, ref o, false);
            // Canasto on the back: a woven basket full of cherries.
            Prim(t, PacmanSprites.Disc, BasketColour, new Vector2(-0.22f, -0.02f), new Vector2(0.3f, 0.38f), 0f, o++);
            Prim(t, PacmanSprites.Disc, new Color(0.32f, 0.18f, 0.1f), new Vector2(-0.22f, 0.16f), new Vector2(0.28f, 0.1f), 0f, o++);
            Prim(t, PacmanSprites.Disc, Cherry, new Vector2(-0.27f, 0.18f), new Vector2(0.09f, 0.09f), 0f, o++);
            Prim(t, PacmanSprites.Disc, Cherry, new Vector2(-0.18f, 0.19f), new Vector2(0.09f, 0.09f), 0f, o++);
            Prim(t, PacmanSprites.Disc, Cherry, new Vector2(-0.22f, 0.21f), new Vector2(0.08f, 0.08f), 0f, o++);
            // Torso, belt and the strap of the basket.
            Prim(t, PacmanSprites.Disc, Shirt, new Vector2(0f, 0.02f), new Vector2(0.34f, 0.44f), 0f, o++);
            Prim(t, PacmanSprites.Square, Dark, new Vector2(0f, -0.14f), new Vector2(0.28f, 0.04f), 0f, o++);
            Prim(t, PacmanSprites.Square, new Color(0.5f, 0.3f, 0.2f), new Vector2(-0.06f, 0.05f), new Vector2(0.05f, 0.4f), 25f, o++);
            Transform frontLeg = Limb(t, new Vector2(0f, -0.14f), Jeans, 0.34f, 0.14f, ref o, false);
            // Head in profile: skin, nose, eye and moustache.
            Prim(t, PacmanSprites.Disc, Skin, new Vector2(0.04f, 0.3f), new Vector2(0.27f, 0.27f), 0f, o++);
            Prim(t, PacmanSprites.Disc, Skin, new Vector2(0.17f, 0.28f), new Vector2(0.08f, 0.08f), 0f, o++);
            Prim(t, PacmanSprites.Disc, Dark, new Vector2(0.11f, 0.33f), new Vector2(0.045f, 0.045f), 0f, o++);
            Prim(t, PacmanSprites.Disc, Dark, new Vector2(0.13f, 0.235f), new Vector2(0.12f, 0.045f), 0f, o++);
            Transform frontArm = Limb(t, new Vector2(0.02f, 0.14f), Shirt, 0.27f, 0.11f, ref o, true);
            AddHat(t, new Vector2(0.03f, 0f), 0.66f, ref o);
            legs[(int)View.Side] = new[] { backLeg, frontLeg };
            arms[(int)View.Side] = new[] { backArm, frontArm };
        }

        /// <summary>Front view (walking down: face and moustache) or back view (walking up: the canasto).</summary>
        private void BuildFront(bool back)
        {
            View v = back ? View.Back : View.Front;
            Transform t = NewView(v);
            int o = 0;
            if (!back)
            {
                // The basket peeks out behind his shoulders.
                Prim(t, PacmanSprites.Disc, BasketColour, new Vector2(0f, 0.12f), new Vector2(0.52f, 0.34f), 0f, o++);
            }
            Transform legL = Limb(t, new Vector2(-0.08f, -0.14f), Jeans, 0.34f, 0.14f, ref o, false);
            Transform legR = Limb(t, new Vector2(0.08f, -0.14f), Jeans, 0.34f, 0.14f, ref o, false);
            Transform armL = Limb(t, new Vector2(-0.2f, 0.15f), Shirt, 0.27f, 0.1f, ref o, true);
            Transform armR = Limb(t, new Vector2(0.2f, 0.15f), Shirt, 0.27f, 0.1f, ref o, true);
            Prim(t, PacmanSprites.Disc, Shirt, new Vector2(0f, 0.02f), new Vector2(0.4f, 0.44f), 0f, o++);
            Prim(t, PacmanSprites.Square, Dark, new Vector2(0f, -0.14f), new Vector2(0.34f, 0.04f), 0f, o++);
            Prim(t, PacmanSprites.Disc, Skin, new Vector2(0f, 0.3f), new Vector2(0.28f, 0.28f), 0f, o++);
            if (!back)
            {
                Prim(t, PacmanSprites.Disc, Dark, new Vector2(-0.06f, 0.32f), new Vector2(0.045f, 0.045f), 0f, o++);
                Prim(t, PacmanSprites.Disc, Dark, new Vector2(0.06f, 0.32f), new Vector2(0.045f, 0.045f), 0f, o++);
                Prim(t, PacmanSprites.Disc, Dark, new Vector2(0f, 0.24f), new Vector2(0.15f, 0.045f), 0f, o++);
            }
            else
            {
                // The canasto covers his back; hair shows under the hat.
                Prim(t, PacmanSprites.Disc, Dark, new Vector2(0f, 0.33f), new Vector2(0.28f, 0.2f), 0f, o++);
                Prim(t, PacmanSprites.Disc, BasketColour, new Vector2(0f, 0.0f), new Vector2(0.44f, 0.44f), 0f, o++);
                Prim(t, PacmanSprites.Disc, new Color(0.32f, 0.18f, 0.1f), new Vector2(0f, 0.0f), new Vector2(0.34f, 0.34f), 0f, o++);
                Prim(t, PacmanSprites.Disc, Cherry, new Vector2(-0.07f, 0.04f), new Vector2(0.09f, 0.09f), 0f, o++);
                Prim(t, PacmanSprites.Disc, Cherry, new Vector2(0.06f, 0.0f), new Vector2(0.09f, 0.09f), 0f, o++);
                Prim(t, PacmanSprites.Disc, Cherry, new Vector2(0f, -0.07f), new Vector2(0.08f, 0.08f), 0f, o++);
            }
            AddHat(t, Vector2.zero, 0.72f, ref o);
            legs[(int)v] = new[] { legL, legR };
            arms[(int)v] = new[] { armL, armR };
        }

        private static void AddHat(Transform t, Vector2 offset, float brim, ref int o)
        {
            Prim(t, PacmanSprites.Disc, BrimStraw, offset + new Vector2(0f, 0.43f), new Vector2(brim, 0.15f), 0f, o++);
            Prim(t, PacmanSprites.Disc, CrownStraw, offset + new Vector2(-0.02f, 0.51f), new Vector2(0.34f, 0.3f), 0f, o++);
            Prim(t, PacmanSprites.Square, Dark, offset + new Vector2(-0.02f, 0.44f), new Vector2(0.32f, 0.07f), 0f, o++);
        }

        /// <summary>An arm or leg: a pivot at the joint with a bar hanging down from it and a hand or boot at the end.</summary>
        private static Transform Limb(Transform parent, Vector2 joint, Color colour, float length, float width, ref int order, bool arm)
        {
            GameObject pivot = new GameObject(arm ? "Arm" : "Leg");
            pivot.transform.SetParent(parent, false);
            pivot.transform.localPosition = joint;
            Prim(pivot.transform, PacmanSprites.Square, colour, new Vector2(0f, -length * 0.5f), new Vector2(width, length), 0f, order++);
            if (arm)
            {
                Prim(pivot.transform, PacmanSprites.Disc, Skin, new Vector2(0f, -length), new Vector2(0.11f, 0.11f), 0f, order++);
            }
            else
            {
                Prim(pivot.transform, PacmanSprites.Disc, Boot, new Vector2(0.03f, -length), new Vector2(0.2f, 0.1f), 0f, order++);
            }
            return pivot.transform;
        }

        private static Transform Prim(Transform parent, Sprite sprite, Color colour, Vector2 position, Vector2 size, float angle, int order)
        {
            GameObject part = new GameObject("Part");
            part.transform.SetParent(parent, false);
            part.transform.localPosition = new Vector3(position.x, position.y, 0f);
            part.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            part.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = colour;
            renderer.sortingOrder = order;
            return part.transform;
        }

        private void SetView(View v)
        {
            view = v;
            for (int i = 0; i < views.Length; i++)
            {
                if (views[i] != null)
                {
                    views[i].gameObject.SetActive(i == (int)v);
                }
            }
        }

        /// <summary>Turns the picker to face left (the classic start) in profile.</summary>
        public void ResetFacing()
        {
            facing = -1f;
            if (farmer != null)
            {
                SetView(View.Side);
                views[(int)View.Side].localScale = new Vector3(facing, 1f, 1f);
            }
        }

        public void Request(Vector2Int direction)
        {
            if (direction != Vector2Int.zero)
            {
                desired = direction;
            }
        }

        public void Tick(float tiles)
        {
            Advance(tiles);
            if (Dir.x != 0)
            {
                facing = Dir.x > 0 ? 1f : -1f;
                if (view != View.Side)
                {
                    SetView(View.Side);
                }
            }
            else if (Dir.y != 0)
            {
                View wanted = Dir.y > 0 ? View.Front : View.Back;
                if (view != wanted)
                {
                    SetView(wanted);
                }
            }
            views[(int)View.Side].localScale = new Vector3(facing, 1f, 1f);
        }

        /// <summary>Starts the death animation: the picker spins and shrinks away.</summary>
        public void StartDeath(float duration)
        {
            dying = true;
            deathTimer = 0f;
            deathDuration = Mathf.Max(0.01f, duration);
            deathFrom = MouthAngle;
        }

        /// <summary>Leaves the death animation and restores the normal walk.</summary>
        public void CancelDeath()
        {
            dying = false;
            deathTimer = 0f;
            if (farmer != null)
            {
                farmer.localScale = Vector3.one;
                farmer.localRotation = Quaternion.identity;
                farmer.localPosition = Vector3.zero;
            }
        }

        private void Update()
        {
            if (body == null || farmer == null)
            {
                return;
            }

            if (dying)
            {
                deathTimer += Time.deltaTime;
                float t = Mathf.Clamp01(deathTimer / deathDuration);
                float eased = t * t * (3f - 2f * t);
                SetMouth(Mathf.Lerp(deathFrom, Mathf.PI, eased));
                farmer.localRotation = Quaternion.Euler(0f, 0f, eased * 540f);
                farmer.localScale = Vector3.one * Mathf.Max(0.0001f, 1f - eased);
                return;
            }

            raise = Mathf.MoveTowards(raise, 0f, Time.deltaTime * 6f);
            if (Dir != Vector2Int.zero)
            {
                phase = Mathf.Repeat(phase + Time.deltaTime * chompRate * 2f * Mathf.PI, 2f * Mathf.PI);
                float open = 0.5f + 0.5f * Mathf.Sin(phase);
                SetMouth(Mathf.Lerp(MinMouth, MaxMouth, open * open));
            }
            else
            {
                phase = 0f;
                SetMouth(Mathf.Lerp(MinMouth, 0.3f, 0.5f));
            }
            Animate();
        }

        /// <summary>Walk cycle: legs and arms swing, the body bobs. A grabbed bean raises the front arm.</summary>
        private void Animate()
        {
            bool moving = Dir != Vector2Int.zero;
            float s = moving ? Mathf.Sin(phase) : 0f;
            farmer.localPosition = new Vector3(0f, moving ? Mathf.Abs(s) * 0.035f : 0f, 0f);
            Transform[] l = legs[(int)view];
            Transform[] a = arms[(int)view];
            if (view == View.Side)
            {
                l[0].localRotation = Quaternion.Euler(0f, 0f, 38f * s);
                l[1].localRotation = Quaternion.Euler(0f, 0f, -38f * s);
                a[0].localRotation = Quaternion.Euler(0f, 0f, -30f * s);
                a[1].localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(30f * s, 100f, raise));
            }
            else
            {
                // Facing the camera or away from it: a foot lifts alternately and the arms swing in and out.
                l[0].localScale = new Vector3(1f, 1f - 0.2f * Mathf.Max(0f, s), 1f);
                l[1].localScale = new Vector3(1f, 1f - 0.2f * Mathf.Max(0f, -s), 1f);
                a[0].localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-10f * s, -75f, raise));
                a[1].localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(10f * s, 75f, raise));
            }
        }

        protected override bool Blocked(Vector2Int tile)
        {
            return PacmanMaze.IsWall(tile) || PacmanMaze.IsGhostHouse(tile);
        }

        protected override Vector2Int ChooseDirection(Vector2Int tile, Vector2Int current)
        {
            if (desired != Vector2Int.zero && !Blocked(tile + desired))
            {
                return desired;
            }
            if (current != Vector2Int.zero && !Blocked(tile + current))
            {
                return current;
            }
            return Vector2Int.zero;
        }

        protected override void OnTileReached(Vector2Int tile)
        {
            if (game != null)
            {
                game.OnPlayerTile(tile);
            }
        }

        private void SetMouth(float amount)
        {
            if (Mathf.Approximately(amount, lastMouth))
            {
                return;
            }
            lastMouth = amount;
            MouthAngle = amount;
        }
    }
}
