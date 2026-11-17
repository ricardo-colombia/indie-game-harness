using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>Plain rectangle overlap for the touch rules: what you see touching is what counts, not the distance between two centre points.</summary>
    public static class PacmanHitBox
    {
        /// <summary>True when the box of size <paramref name="sizeA"/> centred at <paramref name="a"/> overlaps the box of size <paramref name="sizeB"/> centred at <paramref name="b"/>. Boxes that only share an edge do not overlap.</summary>
        public static bool Overlap(Vector2 a, Vector2 sizeA, Vector2 b, Vector2 sizeB)
        {
            return Mathf.Abs(a.x - b.x) < (sizeA.x + sizeB.x) * 0.5f
                && Mathf.Abs(a.y - b.y) < (sizeA.y + sizeB.y) * 0.5f;
        }
    }
}
