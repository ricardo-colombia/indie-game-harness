using NUnit.Framework;
using Studio.Pacman;
using UnityEngine;

namespace Studio.Pacman.Tests
{
    public class PacmanHitBoxTests
    {
        private static readonly Vector2 Box = new Vector2(0.6f, 0.9f);

        [Test]
        public void SameSpot_Overlaps()
        {
            Assert.IsTrue(PacmanHitBox.Overlap(Vector2.zero, Box, Vector2.zero, Box));
        }

        [Test]
        public void SideBySideCloserThanWidth_Overlaps()
        {
            Assert.IsTrue(PacmanHitBox.Overlap(Vector2.zero, Box, new Vector2(0.55f, 0f), Box));
        }

        [Test]
        public void SideBySideFartherThanWidth_DoesNotOverlap()
        {
            Assert.IsFalse(PacmanHitBox.Overlap(Vector2.zero, Box, new Vector2(0.65f, 0f), Box));
        }

        [Test]
        public void AdjacentRowsOneTileApart_DoNotOverlap()
        {
            Assert.IsFalse(PacmanHitBox.Overlap(Vector2.zero, Box, new Vector2(0f, 1f), Box));
        }

        [Test]
        public void TallBoxReachesPastACentrePointDistance()
        {
            // 0.8 tiles apart vertically: farther than the old 0.6 point distance, still touching because the boxes are tall.
            Assert.IsTrue(PacmanHitBox.Overlap(Vector2.zero, Box, new Vector2(0f, 0.8f), Box));
        }
    }
}
