using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// Static maze data and grid helpers.
    /// Legend: '#' wall, '.' pellet, 'o' power pellet, ' ' empty floor,
    /// 'P' Pac-Man start, 'G' ghost house start. The row TunnelRow wraps left/right.
    /// Tile (x, y): x grows to the right, y grows downward (row index).
    /// </summary>
    public static class PacmanMaze
    {
        public const int TunnelRow = 8;

        public static readonly string[] Rows =
        {
            "###################",
            "#o.......#.......o#",
            "#.###.##.#.##.###.#",
            "#.#.....#.#.....#.#",
            "#.#.###.#.#.###.#.#",
            "#.................#",
            "###.#.#.###.#.#.###",
            "#.#...# GGG #...#.#",
            ".........P.........",
            "#.#...#     #...#.#",
            "###.#.#.###.#.#.###",
            "#.................#",
            "#.#.###.#.#.###.#.#",
            "#.#.....#.#.....#.#",
            "#.###.##.#.##.###.#",
            "#o.......#.......o#",
            "###################",
        };

        public static int Width => Rows[0].Length;
        public static int Height => Rows.Length;

        public static readonly Vector2Int Up = new Vector2Int(0, -1);
        public static readonly Vector2Int Down = new Vector2Int(0, 1);
        public static readonly Vector2Int Left = new Vector2Int(-1, 0);
        public static readonly Vector2Int Right = new Vector2Int(1, 0);

        public static char Cell(int x, int y)
        {
            return Rows[y][x];
        }

        /// <summary>Wraps x on the tunnel row; other rows are returned unchanged.</summary>
        public static Vector2Int Wrap(Vector2Int t)
        {
            if (t.y == TunnelRow)
            {
                t.x = ((t.x % Width) + Width) % Width;
            }
            return t;
        }

        public static bool IsWall(Vector2Int t)
        {
            t = Wrap(t);
            if (t.y < 0 || t.y >= Height || t.x < 0 || t.x >= Width)
            {
                return true;
            }
            return Rows[t.y][t.x] == '#';
        }

        /// <summary>True for the ghost house interior and its gate. Pac-Man may not enter it.</summary>
        public static bool IsGhostHouse(Vector2Int t)
        {
            t = Wrap(t);
            if (t.y < 0 || t.y >= Height || t.x < 0 || t.x >= Width)
            {
                return false;
            }
            char c = Rows[t.y][t.x];
            return c == ' ' || c == 'G';
        }

        public static Vector3 ToWorld(Vector2Int t)
        {
            return new Vector3(t.x, 0f, -t.y);
        }

        /// <summary>Returns the first tile holding the given legend character, or (-1,-1).</summary>
        public static Vector2Int Find(char c)
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (Rows[y][x] == c)
                    {
                        return new Vector2Int(x, y);
                    }
                }
            }
            return new Vector2Int(-1, -1);
        }
    }
}
