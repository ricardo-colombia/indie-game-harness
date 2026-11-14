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

        [Tooltip("Walk cycles per second while moving.")]
        [SerializeField] private float chompRate = 3.2f;

        private enum View { Side, Front, Back }

        /// <summary>The bones of one view that the walk cycle moves.</summary>
        private sealed class Rig
        {
            public Transform Upper;
            public Transform Head;
            public Transform Hat;
            public Transform Basket;
            public Transform[] Thigh = new Transform[2];
            public Transform[] Shin = new Transform[2];
            public Transform[] Arm = new Transform[2];
            public Transform[] Fore = new Transform[2];
            public Vector3[] ThighHome = new Vector3[2];
            public Vector3 UpperHome;
            public Vector3 HeadHome;
        }

        private PacmanGame game;
        private Vector2Int desired;
        private Transform body;
        private Transform farmer;
        private Transform[] views = new Transform[3];
        private Rig[] rigs = new Rig[3];
        private float gait;
        private float moveBlend;
        private View view = View.Side;
        private float facing = -1f;
        private float raise;
        private float phase;
        private float lastMouth = -1f;
        private bool dying;
        private float deathTimer;
        private float deathDuration = 1f;
        private float deathFrom;
        private GhostKind deathBy;
        private float deathAway = 1f;

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
            Prim(body, PacmanFarmerArt.Shadow(), new Color(0f, 0f, 0f, 0.4f), new Vector2(0f, -0.5f), Vector2.one, 0f, 4);
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

        private const float HipY = -0.14f;

        private static Transform Group(Transform parent, string name, Vector2 position)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            return go.transform;
        }

        /// <summary>Arm: the upper arm hangs from the shoulder and the forearm from its elbow.</summary>
        private static void BuildArm(Transform parent, Rig rig, int index, Vector2 shoulder, ref int o)
        {
            Transform arm = Prim(parent, PacmanFarmerArt.UpperArm(), Color.white, shoulder, Vector2.one, 0f, o++);
            Transform fore = Prim(arm, PacmanFarmerArt.Forearm(), Color.white, new Vector2(0f, -0.17f), Vector2.one, 0f, o++);
            rig.Arm[index] = arm;
            rig.Fore[index] = fore;
        }

        private static void BuildLeg(Transform parent, Rig rig, int index, Vector2 hip, bool front, ref int o)
        {
            Transform thigh = Prim(parent, PacmanFarmerArt.Thigh(), Color.white, hip, Vector2.one, 0f, o++);
            Transform shin = Prim(thigh, PacmanFarmerArt.Shin(front), Color.white, new Vector2(0f, -0.14f), Vector2.one, 0f, o++);
            rig.Thigh[index] = thigh;
            rig.Shin[index] = shin;
            rig.ThighHome[index] = thigh.localPosition;
        }

        private void BuildSide()
        {
            Transform t = NewView(View.Side);
            Rig rig = new Rig();
            int o = 0;
            // Everything above the hips hangs from one "upper" bone so the torso can lean and sway as he steps.
            BuildLeg(t, rig, 0, new Vector2(0f, HipY), false, ref o);
            Transform upper = Group(t, "Upper", new Vector2(0f, HipY));
            rig.Upper = upper;
            rig.UpperHome = upper.localPosition;
            // The upper bone's origin is the hips, so parts on it are placed relative to them.
            BuildArm(upper, rig, 0, new Vector2(0f, 0.28f), ref o);
            rig.Basket = Prim(upper, PacmanFarmerArt.BasketSide(), Color.white, new Vector2(-0.21f, 0.2f), Vector2.one, 0f, o++);
            Prim(upper, PacmanFarmerArt.TorsoSide(), Color.white, new Vector2(0f, 0.16f), Vector2.one, 0f, o++);
            BuildLeg(t, rig, 1, new Vector2(0f, HipY), false, ref o);
            Transform head = Group(upper, "Head", new Vector2(0.03f, 0.44f));
            rig.Head = head;
            rig.HeadHome = head.localPosition;
            Prim(head, PacmanFarmerArt.HeadSide(), Color.white, Vector2.zero, Vector2.one, 0f, o++);
            BuildArm(upper, rig, 1, new Vector2(0.02f, 0.28f), ref o);
            rig.Hat = Prim(head, PacmanFarmerArt.Hat(0.66f), Color.white, new Vector2(0f, 0.23f), Vector2.one, 0f, o++);
            rigs[(int)View.Side] = rig;
        }

        /// <summary>Front view (walking down: face and moustache) or back view (walking up: the canasto).</summary>
        private void BuildFront(bool back)
        {
            View v = back ? View.Back : View.Front;
            Transform t = NewView(v);
            Rig rig = new Rig();
            int o = 0;
            BuildLeg(t, rig, 0, new Vector2(-0.085f, HipY), true, ref o);
            BuildLeg(t, rig, 1, new Vector2(0.085f, HipY), true, ref o);
            Transform upper = Group(t, "Upper", new Vector2(0f, HipY));
            rig.Upper = upper;
            rig.UpperHome = upper.localPosition;
            if (!back)
            {
                // The basket peeks out behind his shoulders.
                rig.Basket = Prim(upper, PacmanFarmerArt.BasketRound(true), Color.white, new Vector2(0f, 0.3f), Vector2.one, 0f, o++);
            }
            BuildArm(upper, rig, 0, new Vector2(-0.21f, 0.29f), ref o);
            BuildArm(upper, rig, 1, new Vector2(0.21f, 0.29f), ref o);
            Prim(upper, PacmanFarmerArt.TorsoFront(back), Color.white, new Vector2(0f, 0.16f), Vector2.one, 0f, o++);
            if (back)
            {
                // The canasto covers his back.
                rig.Basket = Prim(upper, PacmanFarmerArt.BasketRound(false), Color.white, new Vector2(0f, 0.17f), Vector2.one, 0f, o++);
            }
            Transform head = Group(upper, "Head", new Vector2(0f, 0.44f));
            rig.Head = head;
            rig.HeadHome = head.localPosition;
            Prim(head, PacmanFarmerArt.HeadFront(back), Color.white, Vector2.zero, Vector2.one, 0f, o++);
            rig.Hat = Prim(head, PacmanFarmerArt.Hat(0.72f), Color.white, new Vector2(0f, 0.23f), Vector2.one, 0f, o++);
            rigs[(int)v] = rig;
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

        /// <summary>
        /// Starts the death animation, which depends on who caught him: bitten by the snake he jolts and drops, drained
        /// by the tax agent he deflates, shot by the guerrilla he is thrown back, bought out by the narco he sinks under cash.
        /// </summary>
        public void StartDeath(float duration, GhostKind by = GhostKind.Clyde, float away = 1f)
        {
            dying = true;
            deathTimer = 0f;
            deathDuration = Mathf.Max(0.01f, duration);
            deathFrom = MouthAngle;
            deathBy = by;
            deathAway = away;
        }

        /// <summary>The picker's death pose for the pest that caught him. t runs 0..1; the last fifth shrinks him away.</summary>
        private void AnimateDeath(float t)
        {
            float vanish = Mathf.Clamp01((t - 0.8f) / 0.2f);
            float fall = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.25f) / 0.45f));
            Vector3 pos = Vector3.zero;
            float angle = 0f;
            Vector3 scale = Vector3.one;
            switch (deathBy)
            {
                case GhostKind.Clyde:
                    // A jolt at the bite, then he drops sideways.
                    pos.x = t < 0.25f ? Mathf.Sin(t * 120f) * 0.03f : 0f;
                    scale = new Vector3(1f, 1f + (t < 0.25f ? 0.1f : 0f), 1f);
                    angle = -deathAway * 90f * fall;
                    pos.y = -0.22f * fall;
                    break;
                case GhostKind.Blinky:
                    // Drained: he gets thinner and flatter, swaying, until nothing is left.
                    float drain = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.8f));
                    scale = new Vector3(1f - 0.3f * drain, 1f - 0.5f * drain, 1f);
                    angle = Mathf.Sin(t * 14f) * 6f * drain;
                    pos.y = -0.25f * drain * 0.5f;
                    break;
                case GhostKind.Pinky:
                    // Thrown back by the shots, then down.
                    pos.x = deathAway * 0.45f * Mathf.Clamp01(t / 0.35f);
                    angle = -deathAway * 100f * fall;
                    pos.y = -0.2f * fall;
                    break;
                default:
                    // Buried: he sinks into the pile of cash.
                    scale = new Vector3(1f, 1f - 0.45f * t, 1f);
                    pos.y = -0.3f * t;
                    angle = Mathf.Sin(t * 10f) * 4f;
                    break;
            }
            farmer.localPosition = pos;
            farmer.localRotation = Quaternion.Euler(0f, 0f, angle);
            farmer.localScale = scale * Mathf.Max(0.0001f, 1f - vanish);
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
                AnimateDeath(t);
                return;
            }

            raise = Mathf.MoveTowards(raise, 0f, Time.deltaTime * 5f);
            bool moving = Dir != Vector2Int.zero;
            moveBlend = Mathf.MoveTowards(moveBlend, moving ? 1f : 0f, Time.deltaTime * 7f);
            if (moving)
            {
                gait = Mathf.Repeat(gait + Time.deltaTime * chompRate * 2f * Mathf.PI, 2f * Mathf.PI);
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

        /// <summary>
        /// Walk cycle driven by one continuous phase: the legs swing and bend at the knee, the arms swing against
        /// them, the body bobs twice per cycle, and the torso, head, hat and canasto follow a beat behind. The pose
        /// blends in and out with movement instead of snapping, and he breathes when standing.
        /// </summary>
        private void Animate()
        {
            Rig rig = rigs[(int)view];
            float amp = moveBlend;
            float p = gait;
            float breathe = Mathf.Sin(Time.time * 2.4f) * (1f - amp);
            float step = Mathf.Cos(p);
            farmer.localPosition = new Vector3(0f, amp * 0.034f * step * step + breathe * 0.004f, 0f);

            if (view == View.Side)
            {
                for (int i = 0; i < 2; i++)
                {
                    float pp = p + i * Mathf.PI;
                    float swing = Mathf.Sin(pp);
                    float lift = Mathf.Pow(Mathf.Max(0f, Mathf.Cos(pp)), 1.4f);
                    rig.Thigh[i].localRotation = Quaternion.Euler(0f, 0f, 31f * swing * amp);
                    rig.Shin[i].localRotation = Quaternion.Euler(0f, 0f, -52f * lift * amp);
                    // Arms swing against the leg on their own side; the elbow bends as the arm comes forward.
                    float armSwing = -swing * 24f * amp;
                    rig.Arm[i].localRotation = Quaternion.Euler(0f, 0f, armSwing + (i == 1 ? raise * 95f : 0f) - breathe * 1.5f);
                    rig.Fore[i].localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(8f + Mathf.Max(0f, armSwing) * 0.9f, 55f, i == 1 ? raise : 0f));
                }
                rig.Upper.localRotation = Quaternion.Euler(0f, 0f, (-3.5f + 1.8f * Mathf.Sin(p)) * amp);
                rig.Upper.localPosition = rig.UpperHome + new Vector3(0f, breathe * 0.004f, 0f);
                rig.Head.localRotation = Quaternion.Euler(0f, 0f, 2.2f * Mathf.Sin(p - 0.8f) * amp + breathe * 0.8f);
                rig.Head.localPosition = rig.HeadHome + new Vector3(0f, 0.008f * Mathf.Sin(2f * p + 0.7f) * amp, 0f);
                rig.Hat.localRotation = Quaternion.Euler(0f, 0f, 3.5f * Mathf.Sin(2f * p - 1.1f) * amp);
                rig.Basket.localRotation = Quaternion.Euler(0f, 0f, -5f * Mathf.Sin(p - 1f) * amp);
            }
            else
            {
                for (int i = 0; i < 2; i++)
                {
                    float pp = p + i * Mathf.PI;
                    float swing = Mathf.Sin(pp);
                    float lift = Mathf.Max(0f, swing);
                    // A foot lifts and the knee bends while the other plants.
                    rig.Thigh[i].localPosition = rig.ThighHome[i] + new Vector3(0f, 0.032f * lift * amp, 0f);
                    rig.Thigh[i].localRotation = Quaternion.Euler(0f, 0f, (i == 0 ? -1f : 1f) * 5f * lift * amp);
                    rig.Shin[i].localRotation = Quaternion.Euler(0f, 0f, (i == 0 ? 1f : -1f) * 12f * lift * amp);
                    rig.Shin[i].localScale = new Vector3(1f, 1f - 0.12f * lift * amp, 1f);
                    float outward = (i == 0 ? 1f : -1f);
                    float armSwing = swing * 12f * amp;
                    rig.Arm[i].localRotation = Quaternion.Euler(0f, 0f, outward * (4f - armSwing) + (raise > 0f ? outward * -raise * 80f : 0f));
                    rig.Fore[i].localRotation = Quaternion.Euler(0f, 0f, outward * (-6f - 8f * Mathf.Max(0f, -swing) * amp));
                }
                rig.Upper.localRotation = Quaternion.Euler(0f, 0f, -2.5f * Mathf.Sin(p) * amp);
                rig.Upper.localPosition = rig.UpperHome + new Vector3(0.012f * Mathf.Sin(p) * amp, breathe * 0.004f, 0f);
                rig.Head.localRotation = Quaternion.Euler(0f, 0f, 2f * Mathf.Sin(p - 0.6f) * amp + breathe * 0.8f);
                rig.Head.localPosition = rig.HeadHome + new Vector3(0f, 0.008f * Mathf.Sin(2f * p + 0.7f) * amp, 0f);
                rig.Hat.localRotation = Quaternion.Euler(0f, 0f, 3f * Mathf.Sin(2f * p - 1.1f) * amp);
                if (rig.Basket != null)
                {
                    rig.Basket.localRotation = Quaternion.Euler(0f, 0f, -3f * Mathf.Sin(p - 1f) * amp);
                }
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
