using NUnit.Framework;
using UnityEngine;

namespace Studio.Pacman.Tests
{
    public class PacmanAudioTests
    {
        private static float[] Samples(AudioClip clip)
        {
            float[] data = new float[clip.samples * clip.channels];
            Assert.IsTrue(clip.GetData(data, 0), "clip data readable");
            return data;
        }

        private static void AssertClean(AudioClip clip, string name)
        {
            Assert.IsNotNull(clip, name);
            Assert.Greater(clip.length, 0.02f, name + " has length");
            float peak = 0f;
            foreach (float s in Samples(clip))
            {
                Assert.IsFalse(float.IsNaN(s) || float.IsInfinity(s), name + " has NaN/Inf");
                peak = Mathf.Max(peak, Mathf.Abs(s));
            }
            Assert.Greater(peak, 0.01f, name + " is not silent");
            Assert.LessOrEqual(peak, 1.0001f, name + " is not clipped");
        }

        [Test]
        public void Shoes_CycleThroughEveryPairAndComeBack()
        {
            Shoe shoe = Shoe.LeatherBoots;
            for (int i = 0; i < PacmanShoes.Count; i++)
            {
                shoe = PacmanShoes.Next(shoe);
            }
            Assert.AreEqual(Shoe.LeatherBoots, shoe);
        }

        [Test]
        public void Shoes_EachPairSoundsDifferent()
        {
            PacmanSfx sfx = new PacmanSfx();
            for (int a = 0; a < PacmanShoes.Count; a++)
            {
                for (int b = a + 1; b < PacmanShoes.Count; b++)
                {
                    CollectionAssert.AreNotEqual(
                        Samples(sfx.StepFor((Shoe)a, false)), Samples(sfx.StepFor((Shoe)b, false)),
                        (Shoe)a + " and " + (Shoe)b + " sound the same");
                }
                CollectionAssert.AreNotEqual(
                    Samples(sfx.StepFor((Shoe)a, false)), Samples(sfx.StepFor((Shoe)a, true)),
                    (Shoe)a + " left and right feet sound the same");
            }
        }

        [Test]
        public void Sfx_AllClipsAreAudibleAndFinite()
        {
            PacmanSfx sfx = new PacmanSfx();
            for (int shoe = 0; shoe < PacmanShoes.Count; shoe++)
            {
                AssertClean(sfx.StepFor((Shoe)shoe, false), "Step " + (Shoe)shoe + " a");
                AssertClean(sfx.StepFor((Shoe)shoe, true), "Step " + (Shoe)shoe + " b");
            }
            AssertClean(sfx.Pellet, "Pellet");
            AssertClean(sfx.Power, "Power");
            AssertClean(sfx.GhostEaten, "GhostEaten");
            AssertClean(sfx.Death, "Death");
            AssertClean(sfx.LevelClear, "LevelClear");
            AssertClean(sfx.Blip, "Blip");
            AssertClean(sfx.Select, "Select");
            AssertClean(sfx.Back, "Back");
            AssertClean(sfx.Cherry, "Cherry");
            AssertClean(sfx.CherryAppear, "CherryAppear");
            AssertClean(sfx.GameOver, "GameOver");
        }

        [Test]
        public void Sfx_GhostChainClipsExistAndClampOutOfRange()
        {
            PacmanSfx sfx = new PacmanSfx();
            Assert.IsNotNull(sfx.GhostEatenFor(1));
            Assert.IsNotNull(sfx.GhostEatenFor(4));
            // Out-of-range chains must clamp rather than throw.
            Assert.IsNotNull(sfx.GhostEatenFor(0));
            Assert.IsNotNull(sfx.GhostEatenFor(99));
        }

        [Test]
        public void Music_LoopsAreAudibleAndFinite()
        {
            PacmanMusic music = new PacmanMusic();
            AssertClean(music.Title, "Title");
            AssertClean(music.Chase, "Chase");
            AssertClean(music.Frightened, "Frightened");
            AssertClean(music.Intro, "Intro");
            Assert.Greater(music.Chase.length, 4f, "Chase is a real loop");
            Assert.Greater(music.Title.length, 4f, "Title is a real loop");
            Assert.AreNotSame(music.Title, music.Chase, "The menu has its own song");
        }
    }
}
