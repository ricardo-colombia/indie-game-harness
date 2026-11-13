using NUnit.Framework;
using UnityEngine;

namespace Studio.Pacman.Tests
{
    public class PacmanRulesTests
    {
        [Test]
        public void FruitTable_FollowsTheClassicSequence()
        {
            Assert.AreEqual(FruitKind.CoffeeCherry, PacmanFruit.For(1));
            Assert.AreEqual(FruitKind.Banana, PacmanFruit.For(2));
            Assert.AreEqual(FruitKind.Arepa, PacmanFruit.For(3));
            Assert.AreEqual(FruitKind.Arepa, PacmanFruit.For(4));
            Assert.AreEqual(FruitKind.Panela, PacmanFruit.For(6));
            Assert.AreEqual(FruitKind.Tinto, PacmanFruit.For(8));
            Assert.AreEqual(FruitKind.Sack, PacmanFruit.For(10));
            Assert.AreEqual(FruitKind.Sombrero, PacmanFruit.For(12));
            Assert.AreEqual(FruitKind.GoldenBean, PacmanFruit.For(13));
            Assert.AreEqual(FruitKind.GoldenBean, PacmanFruit.For(99));
        }

        [Test]
        public void FruitPoints_MatchTheTable()
        {
            Assert.AreEqual(100, PacmanFruit.Points(FruitKind.CoffeeCherry));
            Assert.AreEqual(300, PacmanFruit.Points(FruitKind.Banana));
            Assert.AreEqual(500, PacmanFruit.Points(FruitKind.Arepa));
            Assert.AreEqual(700, PacmanFruit.Points(FruitKind.Panela));
            Assert.AreEqual(1000, PacmanFruit.Points(FruitKind.Tinto));
            Assert.AreEqual(2000, PacmanFruit.Points(FruitKind.Sack));
            Assert.AreEqual(3000, PacmanFruit.Points(FruitKind.Sombrero));
            Assert.AreEqual(5000, PacmanFruit.Points(FruitKind.GoldenBean));
        }

        [Test]
        public void FruitSpot_IsAnOpenCorridorBelowTheHouse()
        {
            Assert.IsFalse(PacmanMaze.IsWall(new Vector2Int(Mathf.FloorToInt(PacmanFruit.SpawnX), PacmanFruit.SpawnRow)));
            Assert.IsFalse(PacmanMaze.IsWall(new Vector2Int(Mathf.CeilToInt(PacmanFruit.SpawnX), PacmanFruit.SpawnRow)));
        }

        [Test]
        public void NoUpTiles_AreTheFourJunctionsAndOpen()
        {
            Vector2Int[] tiles = { new Vector2Int(7, 7), new Vector2Int(13, 7), new Vector2Int(9, 18), new Vector2Int(11, 18) };
            foreach (Vector2Int t in tiles)
            {
                Assert.IsTrue(PacmanMaze.IsNoUpTile(t), t.ToString());
                Assert.IsFalse(PacmanMaze.IsWall(t), t.ToString());
            }
            Assert.IsFalse(PacmanMaze.IsNoUpTile(new Vector2Int(10, 7)));
            Assert.IsFalse(PacmanMaze.IsNoUpTile(new Vector2Int(3, 7)));
        }

        [Test]
        public void Release_Level1_PinkyAtOnceThenInkyThenClyde()
        {
            PacmanGhostRelease r = new PacmanGhostRelease();
            r.Reset(1, false);
            r.Tick(PacmanGhostRelease.ReleaseGap + 0.01f);
            Assert.IsTrue(r.IsReleased(0));
            Assert.IsTrue(r.IsReleased(1), "Pinky has a limit of 0");
            Assert.IsFalse(r.IsReleased(2));
            Assert.IsFalse(r.IsReleased(3));

            for (int i = 0; i < 29; i++)
            {
                r.OnDot();
            }
            Assert.IsFalse(r.IsReleased(2));
            r.OnDot();
            Assert.IsTrue(r.IsReleased(2), "Inky leaves on the 30th dot");
            Assert.IsFalse(r.IsReleased(3));

            for (int i = 0; i < 59; i++)
            {
                r.OnDot();
            }
            Assert.IsFalse(r.IsReleased(3));
            r.Tick(PacmanGhostRelease.ReleaseGap + 0.01f);
            r.OnDot();
            Assert.IsTrue(r.IsReleased(3), "Clyde leaves after 60 more dots on his own counter");
        }

        [Test]
        public void Release_Level2_InkyOutAtOnceClydeAt50()
        {
            PacmanGhostRelease r = new PacmanGhostRelease();
            r.Reset(2, false);
            r.Tick(PacmanGhostRelease.ReleaseGap + 0.01f);
            Assert.IsTrue(r.IsReleased(1));
            Assert.IsTrue(r.IsReleased(2));
            Assert.IsFalse(r.IsReleased(3));
            r.Tick(PacmanGhostRelease.ReleaseGap + 0.01f);
            for (int i = 0; i < 50; i++)
            {
                r.OnDot();
            }
            Assert.IsTrue(r.IsReleased(3));
        }

        [Test]
        public void Release_Level3_EveryoneLeavesOneAfterAnotherWithAGap()
        {
            PacmanGhostRelease r = new PacmanGhostRelease();
            r.Reset(3, false);
            Assert.IsTrue(r.IsReleased(1));
            Assert.IsFalse(r.IsReleased(2), "the gap keeps ghosts from leaving on top of each other");
            r.Tick(PacmanGhostRelease.ReleaseGap + 0.01f);
            Assert.IsTrue(r.IsReleased(2));
            Assert.IsFalse(r.IsReleased(3));
            r.Tick(PacmanGhostRelease.ReleaseGap + 0.01f);
            Assert.IsTrue(r.IsReleased(3));
        }

        [Test]
        public void Release_NoDotsEatenForTooLong_SendsNextGhostOut()
        {
            PacmanGhostRelease r = new PacmanGhostRelease();
            r.Reset(1, false);
            r.Tick(3.9f);
            Assert.IsFalse(r.IsReleased(2));
            r.Tick(0.2f);
            Assert.IsTrue(r.IsReleased(2), "level 1 waits 4 seconds");

            PacmanGhostRelease late = new PacmanGhostRelease();
            late.Reset(5, false);
            late.Tick(3.1f);
            Assert.IsTrue(late.IsReleased(2), "level 5 and up wait 3 seconds");
        }

        [Test]
        public void Release_EatingADotRestartsTheIdleTimer()
        {
            PacmanGhostRelease r = new PacmanGhostRelease();
            r.Reset(1, false);
            r.Tick(3.5f);
            r.OnDot();
            r.Tick(3.5f);
            Assert.IsFalse(r.IsReleased(2));
        }

        [Test]
        public void Release_AfterDeath_UsesTheGlobalCounter()
        {
            PacmanGhostRelease r = new PacmanGhostRelease();
            r.Reset(1, true);
            Assert.IsTrue(r.GlobalActive);
            Assert.IsFalse(r.IsReleased(1), "Pinky now waits for 7 dots");
            for (int i = 0; i < 6; i++)
            {
                r.OnDot();
            }
            Assert.IsFalse(r.IsReleased(1));
            r.OnDot();
            Assert.IsTrue(r.IsReleased(1));

            for (int i = 0; i < 9; i++)
            {
                r.OnDot();
            }
            r.Tick(PacmanGhostRelease.ReleaseGap + 0.01f);
            r.OnDot();
            Assert.IsTrue(r.IsReleased(2), "Inky at 17");
            Assert.IsFalse(r.IsReleased(3));

            r.Tick(PacmanGhostRelease.ReleaseGap + 0.01f);
            for (int i = 0; i < 15; i++)
            {
                r.OnDot();
            }
            Assert.IsTrue(r.IsReleased(3), "Clyde at 32");
            Assert.IsFalse(r.GlobalActive, "personal counters take over again");
        }

        [Test]
        public void Release_PersonalCountersSurviveADeath()
        {
            PacmanGhostRelease r = new PacmanGhostRelease();
            r.Reset(1, false);
            for (int i = 0; i < 12; i++)
            {
                r.OnDot();
            }
            Assert.AreEqual(12, r.PersonalCount(2));
            r.Reset(1, true);
            Assert.AreEqual(12, r.PersonalCount(2));
            r.Reset(1, false);
            Assert.AreEqual(0, r.PersonalCount(2), "a new level starts them over");
        }
    }
}
