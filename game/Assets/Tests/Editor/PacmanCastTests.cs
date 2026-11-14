using System.Collections.Generic;
using NUnit.Framework;

namespace Studio.Pacman.Tests
{
    public class PacmanCastTests
    {
        [Test]
        public void EveryMemberHasAFullSheet()
        {
            foreach (CastMember m in PacmanCast.Members)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(m.Name));
                Assert.IsFalse(string.IsNullOrWhiteSpace(m.Role), m.Name);
                Assert.IsFalse(string.IsNullOrWhiteSpace(m.Context), m.Name);
                Assert.IsFalse(string.IsNullOrWhiteSpace(m.Wants), m.Name);
                Assert.IsFalse(string.IsNullOrWhiteSpace(m.Lesson), m.Name);
                Assert.IsFalse(string.IsNullOrWhiteSpace(m.Tag), m.Name);
            }
        }

        [Test]
        public void TheFarmerAndTheFourPestsAreAllThere()
        {
            Assert.AreEqual(5, PacmanCast.Count);
            Assert.IsNull(PacmanCast.Members[0].Pest, "the farmer comes first");
            foreach (GhostKind kind in new[] { GhostKind.Blinky, GhostKind.Pinky, GhostKind.Inky, GhostKind.Clyde })
            {
                Assert.IsNotNull(PacmanCast.For(kind), kind.ToString());
            }
        }

        [Test]
        public void EachCharacterHasItsOwnPictureAndName()
        {
            var scenes = new HashSet<StoryScene>();
            var names = new HashSet<string>();
            foreach (CastMember m in PacmanCast.Members)
            {
                Assert.IsTrue(scenes.Add(m.Scene), "two characters share a picture: " + m.Name);
                Assert.IsTrue(names.Add(m.Name), "duplicate name " + m.Name);
                Assert.AreNotEqual(StoryScene.Dawn, m.Scene);
                Assert.AreNotEqual(StoryScene.Finale, m.Scene);
            }
        }

        [Test]
        public void NameTagsAreShortEnoughToReadAtAGlance()
        {
            foreach (CastMember m in PacmanCast.Members)
            {
                Assert.LessOrEqual(m.Tag.Length, 32, m.Name);
            }
        }

        [Test]
        public void TheCastMatchesThePrologue()
        {
            // The same people the comic introduces, in the same order after the farmer.
            var comic = new List<StoryScene>();
            foreach (StoryPanel p in PacmanStoryData.Panels)
            {
                comic.Add(p.Scene);
            }
            foreach (CastMember m in PacmanCast.Members)
            {
                Assert.Contains(m.Scene, comic, m.Name + " is not in the comic");
            }
        }

        [Test]
        public void WrapGoesBothWays()
        {
            Assert.AreEqual(0, PacmanCast.Wrap(PacmanCast.Count));
            Assert.AreEqual(PacmanCast.Count - 1, PacmanCast.Wrap(-1));
            Assert.AreEqual(2, PacmanCast.Wrap(2));
        }
    }
}
