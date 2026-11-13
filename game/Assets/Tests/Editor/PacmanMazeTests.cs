using System.Collections.Generic;
using NUnit.Framework;
using Studio.Pacman;
using UnityEngine;

namespace Studio.Pacman.Tests
{
    public class PacmanMazeTests
    {
        [Test]
        public void Layout_IsRectangular()
        {
            foreach (string row in PacmanMaze.Rows)
            {
                Assert.AreEqual(PacmanMaze.Width, row.Length);
            }
        }

        [Test]
        public void Layout_IsMirrorSymmetric()
        {
            foreach (string row in PacmanMaze.Rows)
            {
                char[] reversed = row.ToCharArray();
                System.Array.Reverse(reversed);
                Assert.AreEqual(row, new string(reversed));
            }
        }

        [Test]
        public void Layout_HasOnePlayerStartAndThreeGhostSpawns()
        {
            int players = 0, ghosts = 0;
            foreach (string row in PacmanMaze.Rows)
            {
                foreach (char c in row)
                {
                    if (c == 'P') players++;
                    if (c == 'G') ghosts++;
                }
            }
            Assert.AreEqual(1, players);
            Assert.AreEqual(3, ghosts);
        }

        [Test]
        public void Layout_HasFourPowerPellets()
        {
            int power = 0;
            foreach (string row in PacmanMaze.Rows)
            {
                foreach (char c in row)
                {
                    if (c == 'o') power++;
                }
            }
            Assert.AreEqual(4, power);
        }

        [Test]
        public void Layout_EveryPelletIsReachableFromPlayerStart()
        {
            Vector2Int start = PacmanMaze.Find('P');
            Assert.AreNotEqual(new Vector2Int(-1, -1), start);

            var seen = new HashSet<Vector2Int> { start };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            Vector2Int[] dirs = { PacmanMaze.Up, PacmanMaze.Down, PacmanMaze.Left, PacmanMaze.Right };

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                foreach (Vector2Int d in dirs)
                {
                    Vector2Int next = PacmanMaze.Wrap(current + d);
                    if (PacmanMaze.IsWall(next) || seen.Contains(next))
                    {
                        continue;
                    }
                    seen.Add(next);
                    queue.Enqueue(next);
                }
            }

            for (int y = 0; y < PacmanMaze.Height; y++)
            {
                for (int x = 0; x < PacmanMaze.Width; x++)
                {
                    char c = PacmanMaze.Cell(x, y);
                    if (c == '.' || c == 'o')
                    {
                        Assert.IsTrue(seen.Contains(new Vector2Int(x, y)), $"Pellet at ({x},{y}) is unreachable");
                    }
                }
            }
        }

        [Test]
        public void Wrap_OnlyWrapsTunnelRow()
        {
            Assert.AreEqual(new Vector2Int(0, PacmanMaze.TunnelRow), PacmanMaze.Wrap(new Vector2Int(PacmanMaze.Width, PacmanMaze.TunnelRow)));
            Assert.AreEqual(new Vector2Int(PacmanMaze.Width - 1, PacmanMaze.TunnelRow), PacmanMaze.Wrap(new Vector2Int(-1, PacmanMaze.TunnelRow)));
            Assert.AreEqual(new Vector2Int(-1, 3), PacmanMaze.Wrap(new Vector2Int(-1, 3)));
        }

        [Test]
        public void IsGhostHouse_CoversHouseInteriorNotCorridors()
        {
            Assert.IsTrue(PacmanMaze.IsGhostHouse(new Vector2Int(9, 7)));
            Assert.IsTrue(PacmanMaze.IsGhostHouse(new Vector2Int(9, 9)));
            Assert.IsFalse(PacmanMaze.IsGhostHouse(new Vector2Int(9, 5)));
            Assert.IsFalse(PacmanMaze.IsGhostHouse(new Vector2Int(9, PacmanMaze.TunnelRow)));
            Assert.IsFalse(PacmanMaze.IsGhostHouse(new Vector2Int(1, 7)));
        }

        [Test]
        public void IsWall_TreatsOutOfBoundsAsWallOffTunnel()
        {
            Assert.IsTrue(PacmanMaze.IsWall(new Vector2Int(-1, 3)));
            Assert.IsTrue(PacmanMaze.IsWall(new Vector2Int(0, -1)));
            Assert.IsFalse(PacmanMaze.IsWall(new Vector2Int(-1, PacmanMaze.TunnelRow)));
        }
    }
}
