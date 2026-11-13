namespace Studio.Pacman
{
    public enum FruitKind
    {
        Cherry,
        Strawberry,
        Orange,
        Apple,
        Melon,
        Galaxian,
        Bell,
        Key,
    }

    /// <summary>The classic bonus-fruit table: which fruit a level offers and what it is worth (Dossier).</summary>
    public static class PacmanFruit
    {
        /// <summary>Where the fruit appears: on Pac-Man's start tile, below the ghost house.</summary>
        public const float SpawnX = 10f;
        public const int SpawnRow = 19;

        /// <summary>How many fruits one level offers.</summary>
        public const int PerLevel = 2;

        /// <summary>The fruit for a level (1-based). Levels past 13 all show the key.</summary>
        public static FruitKind For(int level)
        {
            switch (level)
            {
                case 1: return FruitKind.Cherry;
                case 2: return FruitKind.Strawberry;
                case 3:
                case 4: return FruitKind.Orange;
                case 5:
                case 6: return FruitKind.Apple;
                case 7:
                case 8: return FruitKind.Melon;
                case 9:
                case 10: return FruitKind.Galaxian;
                case 11:
                case 12: return FruitKind.Bell;
                default: return FruitKind.Key;
            }
        }

        public static int Points(FruitKind kind)
        {
            switch (kind)
            {
                case FruitKind.Cherry: return 100;
                case FruitKind.Strawberry: return 300;
                case FruitKind.Orange: return 500;
                case FruitKind.Apple: return 700;
                case FruitKind.Melon: return 1000;
                case FruitKind.Galaxian: return 2000;
                case FruitKind.Bell: return 3000;
                default: return 5000;
            }
        }
    }
}
