using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// Turns a finger drag into steering. Each time the finger has travelled the threshold distance the
    /// dominant axis becomes a direction and the anchor moves to the finger, so a single drag can turn
    /// several times. Works in screen pixels (y grows upward) and knows nothing about the game.
    /// </summary>
    public class PacmanSwipe
    {
        private Vector2 anchor;
        private bool active;

        /// <summary>Pixels the finger must travel before it counts as a swipe.</summary>
        public float Threshold { get; set; } = 40f;

        public bool Active => active;

        public void Begin(Vector2 position)
        {
            anchor = position;
            active = true;
        }

        public void End()
        {
            active = false;
        }

        /// <summary>
        /// Feeds the finger position. Returns a unit step in screen space (x right, y up), or zero while the
        /// finger has not moved far enough.
        /// </summary>
        public Vector2Int Move(Vector2 position)
        {
            if (!active)
            {
                return Vector2Int.zero;
            }

            Vector2 delta = position - anchor;
            if (delta.sqrMagnitude < Threshold * Threshold)
            {
                return Vector2Int.zero;
            }

            anchor = position;
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            {
                return new Vector2Int(delta.x > 0f ? 1 : -1, 0);
            }
            return new Vector2Int(0, delta.y > 0f ? 1 : -1);
        }
    }
}
