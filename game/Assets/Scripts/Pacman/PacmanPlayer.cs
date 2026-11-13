using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// Player-controlled coffee picker (a campesino in a sombrero and poncho, seen from above). The hat brim is
    /// a 2D sprite: a disc with a wedge cut out, swapped between pre-rendered angles so the picker "gobbles" the
    /// cherries, and rotated to point along the movement direction. Under the wedge you see the farmer's face
    /// and moustache. Gameplay is the same as the arcade original: only the look changed.
    /// </summary>
    public class PacmanPlayer : PacmanActor
    {
        private const float MinMouth = 0.04f;
        private const float MaxMouth = 0.72f;

        private static readonly Color Straw = new Color(0.96f, 0.86f, 0.58f);
        private static readonly Color HatBand = new Color(0.12f, 0.08f, 0.06f);
        private static readonly Color Poncho = new Color(0.96f, 0.95f, 0.9f);
        private static readonly Color Skin = new Color(0.76f, 0.5f, 0.32f);
        private const float BodyScale = 0.95f;

        [Tooltip("Mouth open/close cycles per second while moving.")]
        [SerializeField] private float chompRate = 3.2f;

        private PacmanGame game;
        private Vector2Int desired;
        private Transform body;
        private SpriteRenderer sprite;
        private Transform decor;
        private float phase;
        private float lastMouth = -1f;
        private bool dying;
        private float deathTimer;
        private float deathDuration = 1f;
        private float deathFrom;

        public Vector2Int Desired => desired;

        /// <summary>Half-angle of the open mouth in radians. Probe for the chomp animation.</summary>
        public float MouthAngle { get; private set; }

        /// <summary>Set by the game when a pellet is eaten, to snap the mouth wide open for a moment.</summary>
        public void Gobble()
        {
            phase = Mathf.PI * 0.5f;
        }

        public void Init(PacmanGame owner)
        {
            game = owner;

            GameObject bodyObject = new GameObject("Body");
            bodyObject.transform.SetParent(transform, false);
            body = bodyObject.transform;
            sprite = bodyObject.AddComponent<SpriteRenderer>();
            sprite.color = Straw;
            sprite.sortingOrder = 5;
            body.localScale = Vector3.one * BodyScale;
            BuildFarmer();
            PacmanGlow.Attach(transform, new Color(1f, 0.85f, 0.45f), 3.2f, 0.2f);

            ResetFacing();
            SetMouth(0.5f);
        }

        /// <summary>Poncho, face and moustache under the hat, and the crown and band on top. Rotates with the body.</summary>
        private void BuildFarmer()
        {
            GameObject decorObject = new GameObject("Farmer");
            decorObject.transform.SetParent(body, false);
            decor = decorObject.transform;
            // Canasto (the woven picking basket) carried on the back, behind the hat.
            Part("Basket", new Color(0.62f, 0.38f, 0.28f), new Vector2(-0.5f, 0f), new Vector2(0.46f, 0.62f), 3, 0f);
            Part("BasketInside", new Color(0.3f, 0.16f, 0.1f), new Vector2(-0.52f, 0f), new Vector2(0.3f, 0.46f), 3, -0.005f);
            Part("BasketCherries", new Color(0.88f, 0.1f, 0.14f), new Vector2(-0.52f, 0.06f), new Vector2(0.18f, 0.2f), 4, -0.01f);
            // Shoulders: a white shirt wider than the hat, with a basket strap across it.
            Part("Poncho", Poncho, new Vector2(-0.04f, 0f), new Vector2(0.6f, 1.05f), 2, 0f);
            Part("PonchoStripe", new Color(0.55f, 0.34f, 0.2f), new Vector2(-0.14f, 0f), new Vector2(0.1f, 1.03f), 2, -0.005f);
            // Face peeks out of the wedge when the mouth opens: skin, two dark eyes and a moustache.
            Part("Face", Skin, new Vector2(0.28f, 0f), new Vector2(0.34f, 0.34f), 3, 0f);
            Part("Eye", HatBand, new Vector2(0.36f, 0.07f), new Vector2(0.05f, 0.05f), 4, 0f);
            Part("Eye", HatBand, new Vector2(0.36f, -0.07f), new Vector2(0.05f, 0.05f), 4, 0f);
            Part("Moustache", HatBand, new Vector2(0.43f, 0f), new Vector2(0.07f, 0.24f), 4, 0f);
            // Crown and band on top of the brim (the sombrero aguadeno: white with a black band).
            Part("HatBand", HatBand, Vector2.zero, new Vector2(0.52f, 0.52f), 5, -0.01f);
            Part("HatCrown", new Color(0.99f, 0.95f, 0.82f), Vector2.zero, new Vector2(0.38f, 0.38f), 5, -0.02f);
        }

        private void Part(string name, Color colour, Vector2 position, Vector2 size, int order, float z)
        {
            GameObject part = new GameObject(name);
            part.transform.SetParent(decor, false);
            part.transform.localPosition = new Vector3(position.x, position.y, z);
            part.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = PacmanSprites.Disc;
            renderer.color = colour;
            renderer.sortingOrder = order;
        }

        /// <summary>Turns the picker to face left (the classic start) so he never starts looking up or down.</summary>
        public void ResetFacing()
        {
            if (body != null)
            {
                body.localRotation = Quaternion.Euler(0f, 0f, 180f);
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
            FaceDirection(body);
        }

        /// <summary>Starts the death animation: the mouth opens until the body collapses to nothing.</summary>
        public void StartDeath(float duration)
        {
            dying = true;
            deathTimer = 0f;
            deathDuration = Mathf.Max(0.01f, duration);
            deathFrom = MouthAngle;
        }

        /// <summary>Leaves the death animation and restores the normal chomp.</summary>
        public void CancelDeath()
        {
            dying = false;
            deathTimer = 0f;
            if (decor != null)
            {
                decor.localScale = Vector3.one;
            }
        }

        private void Update()
        {
            if (body == null)
            {
                return;
            }

            if (dying)
            {
                // Ease the mouth open to a full collapse, so the body shrinks away over the death time.
                deathTimer += Time.deltaTime;
                float t = Mathf.Clamp01(deathTimer / deathDuration);
                float eased = t * t * (3f - 2f * t);
                SetMouth(Mathf.Lerp(deathFrom, Mathf.PI, eased));
                // The poncho, face and hat shrink away with the brim.
                decor.localScale = Vector3.one * Mathf.Max(0.0001f, 1f - eased);
                return;
            }

            if (Dir != Vector2Int.zero)
            {
                // Keep cycling: the mouth opens and closes once per chompRate cycle while moving.
                phase = Mathf.Repeat(phase + Time.deltaTime * chompRate * 2f * Mathf.PI, 2f * Mathf.PI);
                float open = 0.5f + 0.5f * Mathf.Sin(phase);
                SetMouth(Mathf.Lerp(MinMouth, MaxMouth, open * open));
            }
            else
            {
                phase = 0f;
                SetMouth(Mathf.Lerp(MinMouth, 0.3f, 0.5f));
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

        private void SetMouth(float halfAngle)
        {
            if (sprite == null || Mathf.Approximately(halfAngle, lastMouth))
            {
                return;
            }
            lastMouth = halfAngle;
            MouthAngle = halfAngle;
            sprite.sprite = PacmanSprites.Pac(halfAngle);
        }
    }
}
