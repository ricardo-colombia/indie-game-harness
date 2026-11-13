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
            // The start markers (P, G) sit on single tiles, so they are ignored here.
            foreach (string row in PacmanMaze.Rows)
            {
                string plain = row.Replace('P', ' ').Replace('G', 'h');
                char[] reversed = plain.ToCharArray();
                System.Array.Reverse(reversed);
                Assert.AreEqual(plain, new string(reversed));
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
        public void Layout_IsTheClassicBoard()
        {
            // 28 x 31 with 240 pellets and 4 power pellets: the arcade original, tile for tile.
            Assert.AreEqual(28, PacmanMaze.Width);
            Assert.AreEqual(31, PacmanMaze.Height);
            int pellets = 0, power = 0;
            foreach (string row in PacmanMaze.Rows)
            {
                foreach (char c in row)
                {
                    if (c == '.') pellets++;
                    if (c == 'o') power++;
                }
            }
            Assert.AreEqual(240, pellets);
            Assert.AreEqual(4, power);
        }

        [Test]
        public void GhostHouse_IsClosedExceptForTheGate()
        {
            // Every walkable cell next to a house cell must be another house cell or the tile above the gate.
            Vector2Int[] dirs = { PacmanMaze.Up, PacmanMaze.Down, PacmanMaze.Left, PacmanMaze.Right };
            for (int y = 0; y < PacmanMaze.Height; y++)
            {
                for (int x = 0; x < PacmanMaze.Width; x++)
                {
                    Vector2Int tile = new Vector2Int(x, y);
                    if (!PacmanMaze.IsGhostHouse(tile))
                    {
                        continue;
                    }
                    foreach (Vector2Int d in dirs)
                    {
                        Vector2Int n = tile + d;
                        bool leak = !PacmanMaze.IsWall(n) && !PacmanMaze.IsGhostHouse(n);
                        if (leak)
                        {
                            Assert.AreEqual('-', PacmanMaze.Cell(x, y), $"House cell ({x},{y}) opens onto ({n.x},{n.y}) but is not the gate");
                            Assert.AreEqual(PacmanMaze.Up, d, "The gate may only open upward");
                        }
                    }
                }
            }
        }

        [Test]
        public void GhostMarkers_AreWhereTheGameExpects()
        {
            Assert.IsTrue(PacmanMaze.IsGhostHouse(PacmanMaze.HouseCentre));
            Assert.IsFalse(PacmanMaze.IsGhostHouse(PacmanMaze.HouseExit));
            Assert.IsFalse(PacmanMaze.IsWall(PacmanMaze.HouseExit));
            Assert.IsFalse(PacmanMaze.IsWall(PacmanMaze.BlinkyStart));
            Assert.AreEqual(PacmanMaze.TunnelRow, 14);
            Assert.AreEqual('P', PacmanMaze.Cell(PacmanMaze.Find('P').x, PacmanMaze.Find('P').y));
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
                    if (PacmanMaze.IsWall(next) || PacmanMaze.IsGhostHouse(next) || seen.Contains(next))
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
            Assert.IsTrue(PacmanMaze.IsGhostHouse(new Vector2Int(13, 14)));
            Assert.IsTrue(PacmanMaze.IsGhostHouse(new Vector2Int(13, 12)), "the gate belongs to the house");
            Assert.IsFalse(PacmanMaze.IsGhostHouse(new Vector2Int(13, 11)));
            Assert.IsFalse(PacmanMaze.IsGhostHouse(new Vector2Int(9, PacmanMaze.TunnelRow)));
            Assert.IsFalse(PacmanMaze.IsGhostHouse(new Vector2Int(1, 7)));
        }

        [Test]
        public void InkyTarget_DoublesTheVectorFromBlinkyThroughTheTwoTileAheadPoint()
        {
            // Pac-Man at (5,5) moving right: pivot (7,5). Blinky at (3,5): vector (4,0), target (11,5).
            Assert.AreEqual(new Vector2Int(11, 5), PacmanGhost.InkyTarget(new Vector2Int(5, 5), PacmanMaze.Right, new Vector2Int(3, 5)));
            // Blinky sitting on the pivot: target is the pivot itself.
            Assert.AreEqual(new Vector2Int(7, 5), PacmanGhost.InkyTarget(new Vector2Int(5, 5), PacmanMaze.Right, new Vector2Int(7, 5)));
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
