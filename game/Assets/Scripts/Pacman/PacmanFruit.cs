namespace Studio.Pacman
{
    public enum FruitKind
    {
        CoffeeCherry,
        Banana,
        Arepa,
        Panela,
        Tinto,
        Sack,
        Sombrero,
        GoldenBean,
    }

    /// <summary>The bonus table (same slots and points as the arcade fruit): which treat a level offers and what it is worth.
    /// Coffee-farm skin: cherry, banana from the shade trees, orange, panela, a cup of tinto, a sack of beans,
    /// the farmer's sombrero and, from level 13, the golden bean.</summary>
    public static class PacmanFruit
    {
        /// <summary>Where the fruit appears: in the corridor right below the pest house, between two tiles, as in the arcade.</summary>
        public const float SpawnX = 13.5f;
        public const int SpawnRow = 17;

        /// <summary>How many fruits one level offers.</summary>
        public const int PerLevel = 2;

        /// <summary>The fruit for a level (1-based). Levels past 13 all show the golden bean.</summary>
        public static FruitKind For(int level)
        {
            switch (level)
            {
                case 1: return FruitKind.CoffeeCherry;
                case 2: return FruitKind.Banana;
                case 3:
                case 4: return FruitKind.Arepa;
                case 5:
                case 6: return FruitKind.Panela;
                case 7:
                case 8: return FruitKind.Tinto;
                case 9:
                case 10: return FruitKind.Sack;
                case 11:
                case 12: return FruitKind.Sombrero;
                default: return FruitKind.GoldenBean;
            }
        }

        public static int Points(FruitKind kind)
        {
            switch (kind)
            {
                case FruitKind.CoffeeCherry: return 100;
                case FruitKind.Banana: return 300;
                case FruitKind.Arepa: return 500;
                case FruitKind.Panela: return 700;
                case FruitKind.Tinto: return 1000;
                case FruitKind.Sack: return 2000;
                case FruitKind.Sombrero: return 3000;
                default: return 5000;
            }
        }
    }
}
