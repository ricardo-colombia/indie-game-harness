using NUnit.Framework;
using Studio.Pacman;

namespace Studio.Pacman.Tests
{
    public class PacmanStoryTests
    {
        [Test]
        public void EveryPanelHasTextAndTime()
        {
            Assert.AreEqual(7, PacmanStoryData.Panels.Length);
            foreach (StoryPanel panel in PacmanStoryData.Panels)
            {
                Assert.IsNotEmpty(panel.Title);
                Assert.IsNotEmpty(panel.Text);
                Assert.Greater(panel.Seconds, 2f);
                Assert.Less(panel.Text.Length, 140, "short captions read on a phone: " + panel.Title);
            }
        }

        [Test]
        public void IntroducesEachPestOnce()
        {
            StoryScene[] needed = { StoryScene.Snake, StoryScene.Taxes, StoryScene.Guerrilla, StoryScene.Boss };
            foreach (StoryScene scene in needed)
            {
                int count = 0;
                foreach (StoryPanel panel in PacmanStoryData.Panels)
                {
                    count += panel.Scene == scene ? 1 : 0;
                }
                Assert.AreEqual(1, count, scene.ToString());
            }
        }

        [Test]
        public void AdvancesByTimeAndFinishesAfterTheLastPanel()
        {
            PacmanStoryPlayer story = new PacmanStoryPlayer(PacmanStoryData.Panels);
            float total = 0f;
            foreach (StoryPanel panel in PacmanStoryData.Panels)
            {
                total += panel.Seconds;
            }
            Assert.AreEqual(0, story.Index);
            story.Tick(PacmanStoryData.Panels[0].Seconds + 0.01f);
            Assert.AreEqual(1, story.Index);
            Assert.IsFalse(story.Done);
            for (float t = 0f; t < total + 1f && !story.Done; t += 0.05f)
            {
                story.Tick(0.05f);
            }
            Assert.IsTrue(story.Done);
        }

        [Test]
        public void TapsAreIgnoredRightAfterAPanelAppears()
        {
            PacmanStoryPlayer story = new PacmanStoryPlayer(PacmanStoryData.Panels);
            story.Advance();
            Assert.AreEqual(0, story.Index, "a tap on the first instant does nothing");
            story.Tick(PacmanStoryPlayer.TapGuard + 0.01f);
            story.Advance();
            Assert.AreEqual(1, story.Index);
            story.Advance();
            Assert.AreEqual(1, story.Index, "the new panel is guarded again");
        }

        [Test]
        public void SkipFinishesAtOnce()
        {
            PacmanStoryPlayer story = new PacmanStoryPlayer(PacmanStoryData.Panels);
            story.Skip();
            Assert.IsTrue(story.Done);
            Assert.IsNull(story.Current);
        }
    }
}
