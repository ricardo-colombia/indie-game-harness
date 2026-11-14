using NUnit.Framework;

namespace Studio.Pacman.Tests
{
    public class PacmanMusicTests
    {
        [Test]
        public void ChaseLoopIsLongEnoughNotToBeNoticed()
        {
            // Game-design rule of thumb: a gameplay loop under about a minute becomes audible repetition within a few rounds.
            PacmanMusic music = new PacmanMusic();
            Assert.GreaterOrEqual(music.Chase.length, 60f);
            Assert.IsTrue(music.Chase.length < 180f, "A loop this long costs startup time on phones");
        }
    }
}
