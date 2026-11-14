using System.Collections.Generic;
using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// Path knowledge for the ghosts, pure and cached, so it can be tested without a scene.
    /// The old steering picked the neighbour nearest the target in a straight line. In a small maze that
    /// makes a ghost orbit a block forever without ever reaching a target on the other side of a wall.
    /// These helpers use real walking distance instead, and keep the original rules: no reversing, and no
    /// turning up at the four no-up junctions.
    /// </summary>
    public static class PacmanPaths
    {
        private static readonly Vector2Int[] Order = { PacmanMaze.Up, PacmanMaze.Left, PacmanMaze.Down, PacmanMaze.Right };

        private static readonly Dictionary<int, int[]> ChaseCache = new Dictionary<int, int[]>();
        private static readonly Dictionary<int, int[]> FreeCache = new Dictionary<int, int[]>();
        private static readonly List<Vector2Int> Scratch = new List<Vector2Int>(4);

        private static int TileIndex(Vector2Int t) => t.y * PacmanMaze.Width + t.x;

        private static int DirIndex(Vector2Int d)
        {
            for (int i = 0; i < Order.Length; i++)
            {
                if (Order[i] == d)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>True where a chasing or scattering ghost may stand: not a wall, not the ghost house.</summary>
        public static bool Walkable(Vector2Int t)
        {
            return !PacmanMaze.IsWall(t) && !PacmanMaze.IsGhostHouse(t);
        }

        /// <summary>
        /// The moves a ghost on a tile centre may make. Never back the way it came, never into a wall, only into
        /// the house when allowed, and never up at a no-up junction when the no-up rule applies.
        /// </summary>
        public static void LegalMoves(Vector2Int tile, Vector2Int current, bool mayEnterHouse, bool noUpRule, List<Vector2Int> moves)
        {
            moves.Clear();
            foreach (Vector2Int d in Order)
            {
                if (current != Vector2Int.zero && d == -current)
                {
                    continue;
                }
                Vector2Int next = tile + d;
                if (PacmanMaze.IsWall(next))
                {
                    continue;
                }
                if (!mayEnterHouse && PacmanMaze.IsGhostHouse(next))
                {
                    continue;
                }
                if (noUpRule && d == PacmanMaze.Up && UpForbidden(tile, current))
                {
                    continue;
                }
                moves.Add(d);
            }
        }

        /// <summary>
        /// True where the no-up rule bites: a no-up junction with some other way on. Where up is the only way
        /// forward (the corridor tiles of Pac-Man's start pocket) the rule would turn the corridor into a dead
        /// end and trap any ghost that wandered in, so it does not apply.
        /// </summary>
        public static bool UpForbidden(Vector2Int tile, Vector2Int current)
        {
            if (!PacmanMaze.IsNoUpTile(tile))
            {
                return false;
            }
            foreach (Vector2Int d in Order)
            {
                if (d == PacmanMaze.Up || (current != Vector2Int.zero && d == -current))
                {
                    continue;
                }
                Vector2Int next = tile + d;
                if (!PacmanMaze.IsWall(next) && !PacmanMaze.IsGhostHouse(next))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The walkable tile nearest to a target that may lie inside a wall or off the board.</summary>
        public static Vector2Int NearestWalkable(Vector2Int target)
        {
            if (target.y >= 0 && target.y < PacmanMaze.Height && target.x >= 0 && target.x < PacmanMaze.Width && Walkable(target))
            {
                return target;
            }

            Vector2Int best = target;
            int bestScore = int.MaxValue;
            for (int y = 0; y < PacmanMaze.Height; y++)
            {
                for (int x = 0; x < PacmanMaze.Width; x++)
                {
                    Vector2Int t = new Vector2Int(x, y);
                    if (!Walkable(t))
                    {
                        continue;
                    }
                    Vector2Int delta = t - target;
                    int score = delta.x * delta.x + delta.y * delta.y;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = t;
                    }
                }
            }
            return best;
        }

        /// <summary>
        /// Picks, among legal moves, the one that reaches the target tile in the fewest tiles while obeying the
        /// no-reverse and no-up rules. The target may be a wall or off the board, in which case the nearest
        /// walkable tile stands in. Returns zero when none of the moves can reach it.
        /// </summary>
        public static Vector2Int ChaseChoice(Vector2Int tile, List<Vector2Int> moves, Vector2Int target)
        {
            int[] distance = ChaseDistances(NearestWalkable(target));
            Vector2Int best = Vector2Int.zero;
            int bestDistance = int.MaxValue;
            foreach (Vector2Int d in moves)
            {
                Vector2Int next = PacmanMaze.Wrap(tile + d);
                int value = distance[TileIndex(next) * 4 + DirIndex(d)];
                if (value >= 0 && value < bestDistance)
                {
                    bestDistance = value;
                    best = d;
                }
            }
            return best;
        }

        /// <summary>One step of a chasing ghost from a tile, for tests: the legal moves, then <see cref="ChaseChoice"/>.</summary>
        public static Vector2Int ChaseStep(Vector2Int tile, Vector2Int current, Vector2Int target)
        {
            LegalMoves(tile, current, false, true, Scratch);
            if (Scratch.Count == 0)
            {
                return current != Vector2Int.zero && !PacmanMaze.IsWall(tile - current) ? -current : Vector2Int.zero;
            }
            if (Scratch.Count == 1)
            {
                return Scratch[0];
            }
            return ChaseChoice(tile, Scratch, target);
        }

        /// <summary>
        /// The step an eaten ghost (eyes) or a ghost leaving the house takes toward its goal. These may turn
        /// around and use the house, so the way is simply the shortest walk, which always arrives.
        /// </summary>
        public static Vector2Int HomeStep(Vector2Int tile, Vector2Int goal)
        {
            int[] distance = FreeDistances(goal);
            Vector2Int best = Vector2Int.zero;
            int bestDistance = int.MaxValue;
            foreach (Vector2Int d in Order)
            {
                Vector2Int next = PacmanMaze.Wrap(tile + d);
                if (PacmanMaze.IsWall(next))
                {
                    continue;
                }
                int value = distance[TileIndex(next)];
                if (value >= 0 && value < bestDistance)
                {
                    bestDistance = value;
                    best = d;
                }
            }
            return best;
        }

        /// <summary>Tiles to the goal along the shortest walk, ignoring the no-reverse rule. -1 where unreachable.</summary>
        private static int[] FreeDistances(Vector2Int goal)
        {
            int key = TileIndex(goal);
            if (FreeCache.TryGetValue(key, out int[] cached))
            {
                return cached;
            }

            int[] distance = new int[PacmanMaze.Width * PacmanMaze.Height];
            for (int i = 0; i < distance.Length; i++)
            {
                distance[i] = -1;
            }
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            distance[key] = 0;
            queue.Enqueue(goal);
            while (queue.Count > 0)
            {
                Vector2Int t = queue.Dequeue();
                foreach (Vector2Int d in Order)
                {
                    Vector2Int n = PacmanMaze.Wrap(t + d);
                    if (PacmanMaze.IsWall(n) || distance[TileIndex(n)] >= 0)
                    {
                        continue;
                    }
                    distance[TileIndex(n)] = distance[TileIndex(t)] + 1;
                    queue.Enqueue(n);
                }
            }
            FreeCache[key] = distance;
            return distance;
        }

        /// <summary>
        /// Tiles to the goal for a ghost standing on a tile having arrived heading a given way, under the
        /// no-reverse and no-up rules. Indexed by tile * 4 + heading; -1 where the goal cannot be reached.
        /// Built backwards from the goal, one breadth-first pass.
        /// </summary>
        private static int[] ChaseDistances(Vector2Int goal)
        {
            int key = TileIndex(goal);
            if (ChaseCache.TryGetValue(key, out int[] cached))
            {
                return cached;
            }

            int[] distance = new int[PacmanMaze.Width * PacmanMaze.Height * 4];
            for (int i = 0; i < distance.Length; i++)
            {
                distance[i] = -1;
            }

            Queue<int> queue = new Queue<int>();
            for (int h = 0; h < 4; h++)
            {
                distance[key * 4 + h] = 0;
                queue.Enqueue(key * 4 + h);
            }

            while (queue.Count > 0)
            {
                int state = queue.Dequeue();
                int tileIndex = state / 4;
                int heading = state % 4;
                Vector2Int move = Order[heading];
                Vector2Int arrived = new Vector2Int(tileIndex % PacmanMaze.Width, tileIndex / PacmanMaze.Width);
                Vector2Int from = PacmanMaze.Wrap(arrived - move);
                if (!Walkable(from))
                {
                    continue;
                }
                for (int previous = 0; previous < 4; previous++)
                {
                    if (Order[previous] == -move)
                    {
                        continue;
                    }
                    // No-up junctions forbid starting an upward move there, unless it is the only way on.
                    if (move == PacmanMaze.Up && UpForbidden(from, Order[previous]))
                    {
                        continue;
                    }
                    int predecessor = TileIndex(from) * 4 + previous;
                    if (distance[predecessor] >= 0)
                    {
                        continue;
                    }
                    distance[predecessor] = distance[state] + 1;
                    queue.Enqueue(predecessor);
                }
            }
            ChaseCache[key] = distance;
            return distance;
        }
    }
}
