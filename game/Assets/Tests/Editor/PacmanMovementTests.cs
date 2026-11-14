using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Studio.Pacman.Tests
{
    /// <summary>
    /// Adversarial tests for ghost and player movement: they look for states from which the steering
    /// never arrives, input that is dropped or stale, and frames long enough to skip a collision.
    /// </summary>
    public class PacmanMovementTests
    {
        private static readonly Vector2Int[] Order = { PacmanMaze.Up, PacmanMaze.Left, PacmanMaze.Down, PacmanMaze.Right };
        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<Vector2Int> moves = new List<Vector2Int>(4);

        [TearDown]
        public void Clean()
        {
            foreach (GameObject g in spawned)
            {
                Object.DestroyImmediate(g);
            }
            spawned.Clear();
        }

        private T Spawn<T>() where T : Component
        {
            GameObject g = new GameObject(typeof(T).Name);
            spawned.Add(g);
            return g.AddComponent<T>();
        }

        private static List<Vector2Int> WalkableTiles()
        {
            List<Vector2Int> tiles = new List<Vector2Int>();
            for (int y = 0; y < PacmanMaze.Height; y++)
            {
                for (int x = 0; x < PacmanMaze.Width; x++)
                {
                    Vector2Int t = new Vector2Int(x, y);
                    if (PacmanPaths.Walkable(t))
                    {
                        tiles.Add(t);
                    }
                }
            }
            return tiles;
        }

        [Test]
        public void EveryOpenTileCanBeReachedFromTheGate()
        {
            for (int y = 0; y < PacmanMaze.Height; y++)
            {
                for (int x = 0; x < PacmanMaze.Width; x++)
                {
                    Vector2Int t = new Vector2Int(x, y);
                    if (PacmanMaze.IsWall(t) || t == PacmanMaze.HouseExit)
                    {
                        continue;
                    }
                    Assert.AreNotEqual(Vector2Int.zero, PacmanPaths.HomeStep(t, PacmanMaze.HouseExit), "No way from " + t + " to the gate");
                }
            }
        }

        [Test]
        public void EyesFromAnyTileReachTheHouseAndLeaveAgain()
        {
            PacmanGhost ghost = Spawn<PacmanGhost>();
            ghost.Init(null, GhostKind.Blinky, new Vector2Int(1, 1));
            List<string> stuck = new List<string>();
            for (int y = 0; y < PacmanMaze.Height; y++)
            {
                for (int x = 0; x < PacmanMaze.Width; x++)
                {
                    Vector2Int t = new Vector2Int(x, y);
                    if (PacmanMaze.IsWall(t))
                    {
                        continue;
                    }
                    ghost.ResetTo(t, GhostMode.Chase);
                    ghost.Eat();
                    bool reachedHouse = false;
                    bool left = false;
                    for (int i = 0; i < 4000 && !left; i++)
                    {
                        ghost.Tick(0.1f);
                        reachedHouse |= ghost.Mode == GhostMode.Exiting;
                        left = ghost.Mode == GhostMode.Scatter;
                    }
                    if (!reachedHouse || !left)
                    {
                        stuck.Add(t.ToString());
                    }
                }
            }
            Assert.IsEmpty(stuck, "Eaten ghosts never got home from: " + string.Join(" ", stuck));
        }

        /// <summary>Follows the steering from a start tile and counts the steps to reach the target. -1 when it has no way, -2 when it wanders past the limit.</summary>
        private int Walk(System.Func<Vector2Int, Vector2Int, Vector2Int, Vector2Int> step, Vector2Int start, Vector2Int target, int limit)
        {
            Vector2Int tile = start;
            Vector2Int heading = Vector2Int.zero;
            for (int i = 0; i < limit; i++)
            {
                if (tile == target)
                {
                    return i;
                }
                Vector2Int d = step(tile, heading, target);
                if (d == Vector2Int.zero)
                {
                    return -1;
                }
                tile = PacmanMaze.Wrap(tile + d);
                heading = d;
            }
            return -2;
        }

        /// <summary>The old rule: of the legal moves, the one that ends nearest the target in a straight line.</summary>
        private Vector2Int Euclid(Vector2Int tile, Vector2Int current, Vector2Int target)
        {
            PacmanPaths.LegalMoves(tile, current, false, true, moves);
            if (moves.Count == 0)
            {
                return -current;
            }
            Vector2Int best = moves[0];
            long bestScore = long.MaxValue;
            foreach (Vector2Int d in moves)
            {
                Vector2Int delta = tile + d - target;
                long score = (long)delta.x * delta.x + (long)delta.y * delta.y;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = d;
                }
            }
            return best;
        }

        [Test]
        public void ChasingByPathReachesTargetsFarMoreOftenThanStraightLine()
        {
            List<Vector2Int> tiles = WalkableTiles();
            int cases = 0;
            int pathLoops = 0;
            int pathNoWay = 0;
            int euclidFailures = 0;
            foreach (Vector2Int target in tiles)
            {
                foreach (Vector2Int start in tiles)
                {
                    cases++;
                    int result = Walk(PacmanPaths.ChaseStep, start, target, 150);
                    if (result == -2)
                    {
                        pathLoops++;
                    }
                    else if (result == -1)
                    {
                        pathNoWay++;
                    }
                    if (Walk(Euclid, start, target, 150) < 0)
                    {
                        euclidFailures++;
                    }
                }
            }
            Debug.Log("Chase reach out of " + cases + ": path loops " + pathLoops + ", path no way " + pathNoWay + ", straight line fails " + euclidFailures);
            Assert.AreEqual(0, pathLoops, "Path chase wandered without arriving where a route exists");
            Assert.AreEqual(0, pathNoWay, "Some start/target pairs have no legal route at all");
            Assert.Less(pathNoWay + pathLoops, euclidFailures / 4, "Path chase should fail far less often than straight-line chase");
        }

        [Test]
        public void ChaseNeverReversesOrBreaksTheJunctionRules()
        {
            List<Vector2Int> tiles = WalkableTiles();
            foreach (Vector2Int start in tiles)
            {
                foreach (Vector2Int heading in Order)
                {
                    foreach (Vector2Int target in new[] { new Vector2Int(13, 11), new Vector2Int(1, 1), new Vector2Int(26, 29), new Vector2Int(-3, -3) })
                    {
                        Vector2Int d = PacmanPaths.ChaseStep(start, heading, target);
                        if (d == Vector2Int.zero)
                        {
                            continue;
                        }
                        Vector2Int next = start + d;
                        Assert.IsFalse(PacmanMaze.IsWall(next), "Walked into a wall from " + start);
                        Assert.IsFalse(PacmanMaze.IsGhostHouse(next), "Chased into the house from " + start);
                        Assert.IsFalse(d == PacmanMaze.Up && PacmanPaths.UpForbidden(start, heading), "Turned up at a no-up tile " + start);
                        if (d == -heading)
                        {
                            // Only a dead end may turn a chaser round.
                            PacmanPaths.LegalMoves(start, heading, false, true, moves);
                            Assert.AreEqual(0, moves.Count, "Reversed at " + start + " with other ways open");
                        }
                    }
                }
            }
        }

        [Test]
        public void NoTileIsADeadEndForAChasingGhost()
        {
            // The no-up rule once turned the two corridor tiles of Pac-Man's start pocket into dead ends,
            // so a ghost that wandered in bounced between them for ever.
            foreach (Vector2Int tile in WalkableTiles())
            {
                foreach (Vector2Int heading in Order)
                {
                    if (!PacmanPaths.Walkable(tile - heading))
                    {
                        continue;
                    }
                    PacmanPaths.LegalMoves(tile, heading, false, true, moves);
                    Assert.IsNotEmpty(moves, "Ghost arriving at " + tile + " heading " + heading + " has nowhere to go");
                }
            }
        }

        [Test]
        public void TargetsInsideWallsOrOffBoardStillPullTheGhost()
        {
            Vector2Int snapped = PacmanPaths.NearestWalkable(new Vector2Int(-5, -5));
            Assert.IsTrue(PacmanPaths.Walkable(snapped));
            Assert.AreEqual(new Vector2Int(13, 11), PacmanPaths.NearestWalkable(new Vector2Int(13, 11)));
            Assert.AreNotEqual(Vector2Int.zero, PacmanPaths.ChaseStep(new Vector2Int(1, 21), Vector2Int.left, new Vector2Int(40, -40)));
        }

        [Test]
        public void FramesAreSplitIntoSmallEqualSteps()
        {
            Assert.AreEqual(0, PacmanTiming.Steps(0f, out _));
            Assert.AreEqual(1, PacmanTiming.Steps(1f / 60f, out float one));
            Assert.AreEqual(1f / 60f, one, 1e-6f);
            int n = PacmanTiming.Steps(0.1f, out float step);
            Assert.AreEqual(6, n);
            Assert.AreEqual(0.1f, n * step, 1e-5f);
            n = PacmanTiming.Steps(30f, out step);
            Assert.AreEqual(PacmanTiming.MaxSteps, n);
            Assert.AreEqual(30f, n * step, 1e-3f);
        }

        [Test]
        public void ActorsRunningHeadOnCannotPassThroughEachOtherInOneFrame()
        {
            // Closing speed is about 0.75 tiles/s * 2 per ... use the game's top: ~11 tiles/s each way.
            float closing = 22f;
            foreach (float dt in new[] { 0.016f, 0.05f, 0.1f, 0.25f, 0.5f })
            {
                PacmanTiming.Steps(dt, out float step);
                Assert.Less(closing * step, 0.6f, "A step of " + step + " s could skip a 0.6-tile touch at dt " + dt);
            }
        }

        private Vector2Int FindTJunction(out Vector2Int arrivalSide)
        {
            foreach (Vector2Int t in WalkableTiles())
            {
                Vector2Int left = t + Vector2Int.left;
                Vector2Int right = t + Vector2Int.right;
                Vector2Int up = t + PacmanMaze.Up;
                if (t.y != PacmanMaze.TunnelRow && PacmanPaths.Walkable(left) && PacmanPaths.Walkable(right) && PacmanPaths.Walkable(up))
                {
                    arrivalSide = left;
                    return t;
                }
            }
            arrivalSide = Vector2Int.zero;
            return Vector2Int.zero;
        }

        [Test]
        public void ReversingIsInstantEvenBetweenTiles()
        {
            Vector2Int junction = FindTJunction(out Vector2Int start);
            PacmanPlayer player = Spawn<PacmanPlayer>();
            player.Place(start);
            player.Request(Vector2Int.right);
            player.Tick(0.5f);
            Assert.AreEqual(Vector2Int.right, player.Dir);
            float before = player.transform.position.x;

            player.Request(Vector2Int.left);
            player.Tick(0.1f);
            Assert.AreEqual(Vector2Int.left, player.Dir, "Reverse should not wait for the next tile");
            Assert.Less(player.transform.position.x, before, "Pac-Man should be heading back already");
            Assert.AreNotEqual(Vector2Int.zero, junction);
        }

        [Test]
        public void ATurnAskedJustAfterTheJunctionIsStillTaken()
        {
            Vector2Int junction = FindTJunction(out Vector2Int start);
            PacmanPlayer player = Spawn<PacmanPlayer>();
            player.Place(start);
            player.Request(Vector2Int.right);
            player.Tick(1.2f);
            Assert.AreEqual(junction, player.Tile);

            player.Request(PacmanMaze.Up);
            player.Tick(0.05f);
            Assert.AreEqual(PacmanMaze.Up, player.Dir, "A turn 0.2 tiles late should still count");
            Assert.AreEqual(junction, player.Tile);
        }

        [Test]
        public void ATurnMuchTooLateIsNotTaken()
        {
            Vector2Int junction = FindTJunction(out Vector2Int start);
            PacmanPlayer player = Spawn<PacmanPlayer>();
            player.Place(start);
            player.Request(Vector2Int.right);
            player.Tick(1.7f);
            player.Request(PacmanMaze.Up);
            player.Tick(0.01f);
            Assert.AreNotEqual(PacmanMaze.Up, player.Dir);
            Assert.AreNotEqual(Vector2Int.zero, junction);
        }

        [Test]
        public void PlacingThePlayerForgetsOldInput()
        {
            PacmanPlayer player = Spawn<PacmanPlayer>();
            player.Place(PacmanMaze.Find('P'));
            player.Request(Vector2Int.left);
            Assert.AreEqual(Vector2Int.left, player.Desired);
            player.Place(PacmanMaze.Find('P'));
            Assert.AreEqual(Vector2Int.zero, player.Desired);
        }

        [Test]
        public void RandomInputNeverPutsActorsInWallsOrTearsThem()
        {
            Random.InitState(1234);
            PacmanPlayer player = Spawn<PacmanPlayer>();
            player.Place(PacmanMaze.Find('P'));
            PacmanGhost ghost = Spawn<PacmanGhost>();
            ghost.Init(null, GhostKind.Pinky, new Vector2Int(1, 1));
            ghost.ResetTo(PacmanMaze.Find('P'), GhostMode.Chase);
            Vector2Int[] dirs = { PacmanMaze.Up, PacmanMaze.Down, PacmanMaze.Left, PacmanMaze.Right };
            for (int i = 0; i < 20000; i++)
            {
                if (Random.value < 0.2f)
                {
                    player.Request(dirs[Random.Range(0, 4)]);
                }
                float tiles = Random.Range(0f, 0.9f);
                Vector3 before = player.transform.position;
                player.Tick(tiles);
                ghost.Tick(tiles);
                Assert.IsFalse(PacmanMaze.IsWall(player.Tile), "Player in a wall at step " + i);
                Assert.IsFalse(PacmanMaze.IsWall(ghost.Tile), "Ghost in a wall at step " + i);
                Assert.IsFalse(PacmanMaze.IsGhostHouse(player.Tile), "Player in the house at step " + i);
                float jump = Vector3.Distance(before, player.transform.position);
                bool wrapped = jump > PacmanMaze.Width - 2;
                Assert.IsTrue(wrapped || jump <= tiles + 1.01f, "Player jumped " + jump + " tiles at step " + i);
            }
        }
    }
}
