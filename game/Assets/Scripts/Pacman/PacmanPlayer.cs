using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// Player-controlled Pac-Man. The body is a 2D sprite: a disc with a wedge cut out, swapped between
    /// pre-rendered mouth angles so it chomps, and rotated to point along the movement direction.
    /// </summary>
    public class PacmanPlayer : PacmanActor
    {
        private const float MinMouth = 0.04f;
        private const float MaxMouth = 0.72f;

        private static readonly Color Yellow = new Color(1f, 0.85f, 0.05f);

        [Tooltip("Mouth open/close cycles per second while moving.")]
        [SerializeField] private float chompRate = 3.2f;

        [Tooltip("How far past a junction centre, in tiles, a late turn is still accepted. 0 turns it off.")]
        [SerializeField, Range(0f, 0.5f)] private float turnForgiveness = 0.3f;

        private PacmanGame game;
        private Vector2Int desired;
        private bool freshRequest;
        private Transform body;
        private SpriteRenderer sprite;
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
            sprite.color = Yellow;
            sprite.sortingOrder = 5;
            PacmanGlow.Attach(transform, Yellow, 3.2f, 0.22f);

            ResetFacing();
            SetMouth(0.5f);
        }

        /// <summary>Turns Pac-Man to face left (the classic start) so he never starts looking up or down.</summary>
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
                freshRequest = direction != desired;
                desired = direction;
            }
        }

        /// <summary>A new game or life starts with no buffered input, so an old swipe cannot steer the first step.</summary>
        public override void Place(Vector2Int tile)
        {
            base.Place(tile);
            desired = Vector2Int.zero;
            freshRequest = false;
        }

        public void Tick(float tiles)
        {
            ApplyFreshRequest();
            Advance(tiles);
            FaceDirection(body);
        }

        /// <summary>
        /// Two forgiving rules for a new request. Reversing is instant, even between tiles, as in the arcade.
        /// A turn asked for just after the junction centre was passed is still taken, by stepping back to it.
        /// </summary>
        private void ApplyFreshRequest()
        {
            if (!freshRequest)
            {
                return;
            }
            freshRequest = false;
            if (Dir == Vector2Int.zero || desired == Dir)
            {
                return;
            }

            if (desired == -Dir)
            {
                // Reverse() keeps the world position; the tile behind is always open since we came from it.
                if (!Blocked(Tile - Dir) || Progress > 0f)
                {
                    Reverse();
                }
                return;
            }

            if (Progress >= 0f && Progress <= turnForgiveness && !Blocked(Tile + desired))
            {
                SnapTurn(desired);
            }
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
