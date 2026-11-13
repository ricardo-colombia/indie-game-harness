using System.Collections.Generic;
using UnityEngine;

namespace Studio.Pacman
{
    public enum GhostKind { Blinky, Pinky, Inky, Clyde }

    public enum GhostMode { Scatter, Chase, Frightened, Eaten, Exiting }

    /// <summary>
    /// Ghost AI on the tile grid. Ghosts never reverse unless they are boxed in,
    /// and pick the neighbour that brings them closest to their target.
    /// </summary>
    public class PacmanGhost : PacmanActor
    {
        // Where an eaten ghost returns to, and where ghosts leave the house.
        public static readonly Vector2Int HouseCentre = PacmanMaze.HouseCentre;
        public static readonly Vector2Int HouseExit = PacmanMaze.HouseExit;


        private PacmanGame game;
        private GhostKind kind;
        private Vector2Int scatterCorner;
        private readonly List<Vector2Int> options = new List<Vector2Int>(4);

        public GhostKind Kind => kind;
        public GhostMode Mode { get; private set; }

        // A ghost still inside the house when a power pellet is eaten turns blue too. It keeps leaving the
        // house (Exiting) and becomes a normal frightened ghost the moment it steps out.
        private bool scaredInHouse;

        // An eaten ghost first runs to the tile above the gate, then drops through it to the house centre.
        private bool droppingIn;

        /// <summary>True while the ghost is blue and edible, including ghosts still inside the house.</summary>
        public bool IsScared => Mode == GhostMode.Frightened || (Mode == GhostMode.Exiting && scaredInHouse);

        public void Init(PacmanGame owner, GhostKind ghostKind, Vector2Int corner)
        {
            game = owner;
            kind = ghostKind;
            scatterCorner = corner;
            Mode = GhostMode.Scatter;
        }

        /// <summary>
        /// Switches the global mode. The original forces a 180-degree turn on scatter/chase switches,
        /// and on entering frightened; it does not turn ghosts back when frightened ends.
        /// </summary>
        public void SetMode(GhostMode mode)
        {
            if (Mode == GhostMode.Eaten || Mode == GhostMode.Exiting)
            {
                return;
            }
            if (IsGlobalMode(Mode) && mode != Mode)
            {
                Reverse();
            }
            Mode = mode;
        }

        /// <summary>Turns the ghost blue. Eaten ghosts (eyes on their way home) are not affected.</summary>
        public void Frighten()
        {
            if (IsGlobalMode(Mode))
            {
                Reverse();
                Mode = GhostMode.Frightened;
            }
            else if (Mode == GhostMode.Exiting)
            {
                scaredInHouse = true;
            }
        }

        /// <summary>Ends the blue state of a ghost that is still leaving the house.</summary>
        public void CalmInHouse()
        {
            scaredInHouse = false;
        }

        private static bool IsGlobalMode(GhostMode mode)
        {
            return mode == GhostMode.Scatter || mode == GhostMode.Chase;
        }

        public void Eat()
        {
            Mode = GhostMode.Eaten;
            scaredInHouse = false;
            droppingIn = false;
        }

        public void ResetTo(Vector2Int start, GhostMode mode)
        {
            Mode = mode;
            scaredInHouse = false;
            droppingIn = false;
            Place(start);
        }

        public void Tick(float tiles)
        {
            Advance(tiles);
        }

        protected override void OnTileReached(Vector2Int tile)
        {
            if (Mode == GhostMode.Eaten && tile == HouseExit)
            {
                droppingIn = true;
            }
            else if (Mode == GhostMode.Eaten && tile == HouseCentre)
            {
                Mode = GhostMode.Exiting;
                droppingIn = false;
            }
            else if (Mode == GhostMode.Exiting && tile == HouseExit)
            {
                // A ghost that was blue when it left the house stays blue while fright lasts.
                bool stillScared = scaredInHouse && game != null && game.FrightenedActive;
                Mode = stillScared ? GhostMode.Frightened : (game != null ? game.GlobalMode : GhostMode.Scatter);
                scaredInHouse = false;
            }
        }

        // Distance, in tiles, from every open tile to a target. Eyes follow it downhill, so they always get home
        // (the classic "aim at the target" rule gets stuck in this maze's loops).
        private static readonly System.Collections.Generic.Dictionary<Vector2Int, int[,]> DistanceFields =
            new System.Collections.Generic.Dictionary<Vector2Int, int[,]>();

        private static int[,] DistanceTo(Vector2Int target)
        {
            if (DistanceFields.TryGetValue(target, out int[,] cached))
            {
                return cached;
            }
            int[,] dist = new int[PacmanMaze.Width, PacmanMaze.Height];
            for (int y = 0; y < PacmanMaze.Height; y++)
            {
                for (int x = 0; x < PacmanMaze.Width; x++)
                {
                    dist[x, y] = int.MaxValue;
                }
            }
            var queue = new System.Collections.Generic.Queue<Vector2Int>();
            dist[target.x, target.y] = 0;
            queue.Enqueue(target);
            Vector2Int[] all = { PacmanMaze.Up, PacmanMaze.Left, PacmanMaze.Down, PacmanMaze.Right };
            while (queue.Count > 0)
            {
                Vector2Int at = queue.Dequeue();
                foreach (Vector2Int d in all)
                {
                    Vector2Int n = PacmanMaze.Wrap(at + d);
                    if (n.x < 0 || n.y < 0 || n.x >= PacmanMaze.Width || n.y >= PacmanMaze.Height || PacmanMaze.IsWall(n) || dist[n.x, n.y] != int.MaxValue)
                    {
                        continue;
                    }
                    dist[n.x, n.y] = dist[at.x, at.y] + 1;
                    queue.Enqueue(n);
                }
            }
            DistanceFields[target] = dist;
            return dist;
        }

        /// <summary>The step that gets eyes closest to their target. Eyes may turn back; they take the shortest way.</summary>
        private Vector2Int HomewardStep(Vector2Int tile)
        {
            int[,] dist = DistanceTo(CurrentTarget());
            Vector2Int[] all = { PacmanMaze.Up, PacmanMaze.Left, PacmanMaze.Down, PacmanMaze.Right };
            Vector2Int best = Vector2Int.zero;
            int bestDistance = int.MaxValue;
            foreach (Vector2Int d in all)
            {
                Vector2Int n = PacmanMaze.Wrap(tile + d);
                if (n.x < 0 || n.y < 0 || n.x >= PacmanMaze.Width || n.y >= PacmanMaze.Height || PacmanMaze.IsWall(n))
                {
                    continue;
                }
                if (dist[n.x, n.y] < bestDistance)
                {
                    bestDistance = dist[n.x, n.y];
                    best = d;
                }
            }
            return best;
        }

        protected override Vector2Int ChooseDirection(Vector2Int tile, Vector2Int current)
        {
            if (Mode == GhostMode.Eaten)
            {
                return HomewardStep(tile);
            }
            options.Clear();
            Vector2Int[] all = { PacmanMaze.Up, PacmanMaze.Left, PacmanMaze.Down, PacmanMaze.Right };
            foreach (Vector2Int d in all)
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
                // Only ghosts that are leaving the house, or eyes going home, may use the gate.
                bool mayEnterHouse = Mode == GhostMode.Eaten || Mode == GhostMode.Exiting;
                if (!mayEnterHouse && PacmanMaze.IsGhostHouse(next))
                {
                    continue;
                }
                // Chasing and scattering ghosts may not turn up at the four classic no-up junctions.
                if (d == PacmanMaze.Up && (Mode == GhostMode.Scatter || Mode == GhostMode.Chase)
                    && PacmanMaze.IsNoUpTile(tile))
                {
                    continue;
                }
                options.Add(d);
            }

            if (options.Count == 0)
            {
                // Dead end: the only legal move is back the way we came.
                if (current != Vector2Int.zero && !PacmanMaze.IsWall(tile - current))
                {
                    return -current;
                }
                return Vector2Int.zero;
            }

            if (options.Count == 1)
            {
                return options[0];
            }

            if (Mode == GhostMode.Frightened)
            {
                return options[Random.Range(0, options.Count)];
            }

            Vector2Int target = CurrentTarget();
            Vector2Int best = options[0];
            long bestScore = long.MaxValue;
            foreach (Vector2Int d in options)
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

        private Vector2Int CurrentTarget()
        {
            switch (Mode)
            {
                case GhostMode.Eaten:
                    return droppingIn ? HouseCentre : HouseExit;
                case GhostMode.Exiting:
                    return HouseExit;
                case GhostMode.Chase:
                    return ChaseTarget();
                default:
                    return scatterCorner;
            }
        }

        /// <summary>
        /// Inky's target: a point two tiles ahead of Pac-Man, reflected through Blinky so the
        /// vector from Blinky to that point is doubled.
        /// </summary>
        public static Vector2Int InkyTarget(Vector2Int pac, Vector2Int pacDir, Vector2Int blinky)
        {
            Vector2Int pivot = pac + pacDir * 2;
            return pivot + (pivot - blinky);
        }

        private Vector2Int ChaseTarget()
        {
            if (game == null)
            {
                return scatterCorner;
            }

            Vector2Int pac = game.PacmanTile;
            Vector2Int pacDir = game.PacmanDir;
            switch (kind)
            {
                case GhostKind.Pinky:
                    return pac + pacDir * 4;
                case GhostKind.Inky:
                    return InkyTarget(pac, pacDir, game.GhostTile(GhostKind.Blinky));
                case GhostKind.Clyde:
                    Vector2Int d = pac - Tile;
                    // Clyde chases at eight tiles or more and retreats to his corner when closer (Dossier).
                    return d.sqrMagnitude >= 64 ? pac : scatterCorner;
                default:
                    return pac;
            }
        }
    }
}
