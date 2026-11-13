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
        public static readonly Vector2Int HouseCentre = new Vector2Int(9, 7);
        public static readonly Vector2Int HouseExit = new Vector2Int(7, 5);

        [SerializeField] private Transform model;

        private PacmanGame game;
        private GhostKind kind;
        private Vector2Int scatterCorner;
        private readonly List<Vector2Int> options = new List<Vector2Int>(4);

        public GhostKind Kind => kind;
        public GhostMode Mode { get; private set; }

        public void Init(PacmanGame owner, GhostKind ghostKind, Vector2Int corner, Transform ghostModel)
        {
            game = owner;
            kind = ghostKind;
            scatterCorner = corner;
            model = ghostModel;
            Mode = GhostMode.Scatter;
        }

        public void SetMode(GhostMode mode)
        {
            if (Mode == GhostMode.Eaten || Mode == GhostMode.Exiting)
            {
                return;
            }
            Mode = mode;
        }

        public void Frighten()
        {
            if (Mode == GhostMode.Scatter || Mode == GhostMode.Chase)
            {
                Mode = GhostMode.Frightened;
            }
        }

        public void Eat()
        {
            Mode = GhostMode.Eaten;
        }

        public void ResetTo(Vector2Int start, GhostMode mode)
        {
            Mode = mode;
            Place(start);
            FaceDirection(model);
        }

        public void Tick(float tiles)
        {
            Advance(tiles);
            FaceDirection(model);
        }

        protected override void OnTileReached(Vector2Int tile)
        {
            if (Mode == GhostMode.Eaten && tile == HouseCentre)
            {
                Mode = GhostMode.Exiting;
            }
            else if (Mode == GhostMode.Exiting && tile == HouseExit)
            {
                Mode = game != null ? game.GlobalMode : GhostMode.Scatter;
            }
        }

        protected override Vector2Int ChooseDirection(Vector2Int tile, Vector2Int current)
        {
            options.Clear();
            Vector2Int[] all = { PacmanMaze.Up, PacmanMaze.Left, PacmanMaze.Down, PacmanMaze.Right };
            foreach (Vector2Int d in all)
            {
                if (current != Vector2Int.zero && d == -current)
                {
                    continue;
                }
                if (!PacmanMaze.IsWall(tile + d))
                {
                    options.Add(d);
                }
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
                    return HouseCentre;
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
                    // Clyde chases while far away and retreats to his corner when close.
                    return d.sqrMagnitude > 64 ? pac : scatterCorner;
                default:
                    return pac;
            }
        }
    }
}
