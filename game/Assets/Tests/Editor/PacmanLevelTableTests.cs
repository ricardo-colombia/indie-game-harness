using NUnit.Framework;
using Studio.Pacman;

namespace Studio.Pacman.Tests
{
    public class PacmanLevelTableTests
    {
        [Test]
        public void Level1_MatchesDossierRow()
        {
            LevelRule r = PacmanLevelTable.For(1);
            Assert.AreEqual(80f, r.PacmanPercent);
            Assert.AreEqual(75f, r.GhostPercent);
            Assert.AreEqual(50f, r.GhostFrightenedPercent);
            Assert.AreEqual(6f, r.FrightenedSeconds);
            Assert.AreEqual(20, r.ElroyOnePellets);
            Assert.AreEqual(80f, r.ElroyOnePercent);
            Assert.AreEqual(10, r.ElroyTwoPellets);
            Assert.AreEqual(85f, r.ElroyTwoPercent);
        }

        [Test]
        public void GhostTunnelSpeed_StepsUpWithLevelGroup()
        {
            Assert.AreEqual(40f, PacmanLevelTable.For(1).GhostTunnelPercent);
            Assert.AreEqual(45f, PacmanLevelTable.For(3).GhostTunnelPercent);
            Assert.AreEqual(50f, PacmanLevelTable.For(5).GhostTunnelPercent);
        }

        [Test]
        public void Level9_HasOneSecondFrightened()
        {
            Assert.AreEqual(1f, PacmanLevelTable.For(9).FrightenedSeconds);
        }

        [Test]
        public void Level17_HasNoFrightenedMode()
        {
            Assert.AreEqual(0f, PacmanLevelTable.For(17).FrightenedSeconds);
            Assert.AreEqual(0f, PacmanLevelTable.For(19).FrightenedSeconds);
        }

        [Test]
        public void Levels21AndUp_ReuseTheLastRow()
        {
            LevelRule last = PacmanLevelTable.For(21);
            LevelRule later = PacmanLevelTable.For(40);
            Assert.AreEqual(last.PacmanPercent, later.PacmanPercent);
            Assert.AreEqual(last.ElroyOnePellets, later.ElroyOnePellets);
            Assert.AreEqual(last.ElroyTwoPercent, later.ElroyTwoPercent);
            Assert.AreEqual(90f, last.PacmanPercent);
        }

        [Test]
        public void LevelBelowOne_ClampsToLevel1()
        {
            Assert.AreEqual(PacmanLevelTable.For(1).PacmanPercent, PacmanLevelTable.For(0).PacmanPercent);
        }

        [Test]
        public void ModeSchedules_AreEightEntriesAndEndInChase()
        {
            for (int level = 1; level <= PacmanLevelTable.Rows.Length; level++)
            {
                float[] schedule = PacmanLevelTable.For(level).ModeSchedule;
                Assert.AreEqual(8, schedule.Length, $"level {level}");
                Assert.Greater(schedule[7], 9000f, $"level {level} must end in an open-ended chase");
            }
        }

        [Test]
        public void Level5_OpensWithFiveSecondScatter()
        {
            Assert.AreEqual(5f, PacmanLevelTable.For(5).ModeSchedule[0]);
            Assert.AreEqual(7f, PacmanLevelTable.For(1).ModeSchedule[0]);
        }

        [Test]
        public void ElroyOne_NeverBelowElroyTwoThreshold()
        {
            for (int level = 1; level <= PacmanLevelTable.Rows.Length; level++)
            {
                LevelRule r = PacmanLevelTable.For(level);
                Assert.GreaterOrEqual(r.ElroyOnePellets, r.ElroyTwoPellets, $"level {level}");
                Assert.GreaterOrEqual(r.ElroyTwoPercent, r.ElroyOnePercent, $"level {level}");
            }
        }
    }
}
