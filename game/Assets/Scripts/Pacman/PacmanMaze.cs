using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// Static maze data and grid helpers.
    /// Legend: '#' wall, '.' pellet, 'o' power pellet, ' ' empty floor, '-' ghost house gate,
    /// 'h' empty ghost house floor, 'G' ghost start inside the house, 'P' Pac-Man start.
    /// The row TunnelRow wraps left/right. The house is a closed box with one gate on top; only
    /// ghosts may use it.
    /// The layout is the arcade original, 28 x 31 with 244 pellets. The original puts the gate, the house
    /// and the starts on half tiles; here they sit on the tile to their left (the gate is two tiles wide).
    /// Tile (x, y): x grows to the right, y grows downward (row index).
    /// </summary>
    public static class PacmanMaze
    {
        /// <summary>The row that wraps left/right. It runs through the middle of the ghost house's row, with the side tunnels at both ends.</summary>
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

        /// <summary>
        /// The four junctions (two above the ghost house, two above Pac-Man's start) where chasing or scattering
        /// ghosts may not turn upward. The original has them so the ghosts' routes stay predictable.
        /// </summary>
        public static bool IsNoUpTile(Vector2Int t)
        {
            return (t.y == 11 || t.y == 23) && (t.x == 12 || t.x == 15);
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
            return new Vector3(t.x, -t.y, 0f);
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
