using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// Static maze data and grid helpers.
    /// Legend: '#' wall, '.' pellet, 'o' power pellet, ' ' empty floor, '-' ghost house gate,
    /// 'h' empty ghost house floor, 'G' ghost start inside the house, 'P' Pac-Man start.
    /// The row TunnelRow wraps left/right. The house is a closed box with one two-tile gate on
    /// top; only ghosts may use it.
    /// Tile (x, y): x grows to the right, y grows downward (row index).
    /// </summary>
    public static class PacmanMaze
    {
        /// <summary>The classic 28 x 31 Pac-Man board, tile for tile (244 pellets, 4 of them power pellets).</summary>
        public const int TunnelRow = 14;

        /// <summary>Tile above the gate. Ghosts leave the house to here, and eaten ghosts return here first.</summary>
        public static readonly Vector2Int HouseExit = new Vector2Int(13, 11);

        /// <summary>Middle of the house. Eaten ghosts drop in here and are revived.</summary>
        public static readonly Vector2Int HouseCentre = new Vector2Int(13, 14);

        /// <summary>Where Blinky starts: outside the house, just above the gate.</summary>
        public static readonly Vector2Int BlinkyStart = new Vector2Int(13, 11);

        public static readonly string[] Rows =
        {
            "############################",
            "#............##............#",
            "#.####.#####.##.#####.####.#",
            "#o####.#####.##.#####.####o#",
            "#.####.#####.##.#####.####.#",
            "#..........................#",
            "#.####.##.########.##.####.#",
            "#.####.##.########.##.####.#",
            "#......##....##....##......#",
            "######.##### ## #####.######",
            "######.##### ## #####.######",
            "######.##          ##.######",
            "######.## ###--### ##.######",
            "######.## #hhhhhh# ##.######",
            "      .   #GhGhGh#   .      ",
            "######.## #hhhhhh# ##.######",
            "######.## ######## ##.######",
            "######.##          ##.######",
            "######.## ######## ##.######",
            "######.## ######## ##.######",
            "#............##............#",
            "#.####.#####.##.#####.####.#",
            "#.####.#####.##.#####.####.#",
            "#o..##.......P .......##..o#",
            "###.##.##.########.##.##.###",
            "###.##.##.########.##.##.###",
            "#......##....##....##......#",
            "#.##########.##.##########.#",
            "#.##########.##.##########.#",
            "#..........................#",
            "############################"
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
            return c == 'h' || c == 'G' || c == '-';
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
