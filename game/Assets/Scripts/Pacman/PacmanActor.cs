using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// Moves an actor tile to tile through the maze. Movement is continuous between tile
    /// centres; direction changes are only decided when an actor reaches a tile centre.
    /// </summary>
    public abstract class PacmanActor : MonoBehaviour
    {
        public Vector2Int Tile { get; protected set; }
        public Vector2Int Dir { get; protected set; }

        /// <summary>0..1 progress from Tile toward Tile + Dir.</summary>
        public float Progress { get; protected set; }

        public void Place(Vector2Int tile)
        {
            Tile = tile;
            Dir = Vector2Int.zero;
            Progress = 0f;
            UpdateTransform();
        }

        /// <summary>Choose the next direction when standing on a tile centre. Zero means stop.</summary>
        protected abstract Vector2Int ChooseDirection(Vector2Int tile, Vector2Int current);

        protected virtual void OnTileReached(Vector2Int tile)
        {
        }

        /// <summary>Cells this actor cannot enter. Walls by default.</summary>
        protected virtual bool Blocked(Vector2Int tile)
        {
            return PacmanMaze.IsWall(tile);
        }

        /// <summary>
        /// Turns the actor around in place, keeping its world position. Used for the forced
        /// 180-degree turn when the global ghost mode changes.
        /// </summary>
        protected void Reverse()
        {
            if (Dir == Vector2Int.zero)
            {
                return;
            }

            if (Progress > 0f)
            {
                // Mid-tile: the position is Tile + Progress * Dir. Re-express it as
                // (Tile + Dir) + (1 - Progress) * (-Dir), which is the same point.
                Tile = PacmanMaze.Wrap(Tile + Dir);
                Progress = 1f - Progress;
            }
            Dir = -Dir;
            UpdateTransform();
        }

        protected void Advance(float tiles)
        {
            float remaining = tiles;
            if (Dir == Vector2Int.zero && Progress == 0f)
            {
                Dir = ChooseDirection(Tile, Dir);
            }

            int guard = 0;
            while (remaining > 0f && Dir != Vector2Int.zero && guard++ < 64)
            {
                Vector2Int next = Tile + Dir;
                if (Blocked(next))
                {
                    Dir = Vector2Int.zero;
                    break;
                }

                float step = Mathf.Min(1f - Progress, remaining);
                Progress += step;
                remaining -= step;

                if (Progress >= 1f - 1e-4f)
                {
                    Tile = PacmanMaze.Wrap(next);
                    Progress = 0f;
                    OnTileReached(Tile);
                    Dir = ChooseDirection(Tile, Dir);
                }
            }

            UpdateTransform();
        }

        protected void UpdateTransform()
        {
            Vector3 from = PacmanMaze.ToWorld(Tile);
            transform.position = Dir == Vector2Int.zero
                ? from
                : Vector3.Lerp(from, PacmanMaze.ToWorld(Tile + Dir), Progress);
        }

        [Tooltip("Extra yaw (degrees) that makes the imported model's front face its movement direction.")]
        [SerializeField] private float modelYawOffset = 180f;

        /// <summary>Extra yaw in degrees so the model's front faces its movement direction.</summary>
        protected virtual float ModelYawOffset => modelYawOffset;

        /// <summary>Turn the model so its front points along the current direction.</summary>
        protected void FaceDirection(Transform model)
        {
            if (model == null || Dir == Vector2Int.zero)
            {
                return;
            }
            Vector3 forward = new Vector3(Dir.x, 0f, -Dir.y);
            float yaw = Mathf.Atan2(forward.z, forward.x) * Mathf.Rad2Deg;
            model.localRotation = Quaternion.Euler(0f, -yaw + ModelYawOffset, 0f);
        }
    }
}
