namespace Studio.Pacman
{
    /// <summary>
    /// One row of the per-level difficulty table. Speeds are percentages of full speed
    /// (PacmanGame.fullSpeed tiles per second = 100%). Source: The Pac-Man Dossier, Chapter 2
    /// and Appendix A (https://pacman.holenet.info/), read as text.
    /// </summary>
    public struct LevelRule
    {
        public float PacmanPercent;
        public float PacmanFrightenedPercent;
        public float GhostPercent;
        public float GhostFrightenedPercent;
        /// <summary>Ghost speed while inside the tunnel tiles (normal mode only).</summary>
        public float GhostTunnelPercent;
        /// <summary>Seconds ghosts stay frightened. Zero means no frightened mode on this level.</summary>
        public float FrightenedSeconds;
        public int ElroyOnePellets;
        public float ElroyOnePercent;
        public int ElroyTwoPellets;
        public float ElroyTwoPercent;
        /// <summary>Alternating scatter / chase durations in seconds. The last value holds for the rest of the level.</summary>
        public float[] ModeSchedule;
    }

    public static class PacmanLevelTable
    {
        private const float Forever = 9999f;
        private const float OneFrame = 1f / 60f;

        // Scatter / chase schedules, by level group (Dossier, Chapter 2).
        private static readonly float[] ScheduleLevelOne = { 7f, 20f, 7f, 20f, 5f, 20f, 5f, Forever };
        private static readonly float[] ScheduleLevelTwoToFour = { 7f, 20f, 7f, 20f, 5f, 1033f, OneFrame, Forever };
        private static readonly float[] ScheduleLevelFiveUp = { 5f, 20f, 5f, 20f, 5f, 1037f, OneFrame, Forever };

        private static LevelRule Row(float pac, float pacFright, float ghost, float ghostFright, float ghostTunnel,
            float fright, int elroyOne, float elroyOneSpeed, int elroyTwo, float elroyTwoSpeed, float[] schedule)
        {
            return new LevelRule
            {
                PacmanPercent = pac,
                PacmanFrightenedPercent = pacFright,
                GhostPercent = ghost,
                GhostFrightenedPercent = ghostFright,
                GhostTunnelPercent = ghostTunnel,
                FrightenedSeconds = fright,
                ElroyOnePellets = elroyOne,
                ElroyOnePercent = elroyOneSpeed,
                ElroyTwoPellets = elroyTwo,
                ElroyTwoPercent = elroyTwoSpeed,
                ModeSchedule = schedule,
            };
        }

        /// <summary>Rows for levels 1 to 21. Level 21 and above reuse the last row.</summary>
        public static readonly LevelRule[] Rows =
        {
            //                 pac  pacF  ghost ghostF tunnel fright E1pel E1%   E2pel E2%    schedule
            Row(80, 90, 75, 50, 40, 6, 20, 80, 10, 85, ScheduleLevelOne),         // 1
            Row(90, 95, 85, 55, 45, 5, 30, 90, 15, 95, ScheduleLevelTwoToFour),   // 2
            Row(90, 95, 85, 55, 45, 4, 40, 90, 20, 95, ScheduleLevelTwoToFour),   // 3
            Row(90, 95, 85, 55, 45, 3, 40, 90, 20, 95, ScheduleLevelTwoToFour),   // 4
            Row(100, 100, 95, 60, 50, 2, 40, 100, 20, 105, ScheduleLevelFiveUp),  // 5
            Row(100, 100, 95, 60, 50, 5, 50, 100, 25, 105, ScheduleLevelFiveUp),  // 6
            Row(100, 100, 95, 60, 50, 2, 50, 100, 25, 105, ScheduleLevelFiveUp),  // 7
            Row(100, 100, 95, 60, 50, 2, 50, 100, 25, 105, ScheduleLevelFiveUp),  // 8
            Row(100, 100, 95, 60, 50, 1, 60, 100, 30, 105, ScheduleLevelFiveUp),  // 9
            Row(100, 100, 95, 60, 50, 5, 60, 100, 30, 105, ScheduleLevelFiveUp),  // 10
            Row(100, 100, 95, 60, 50, 2, 60, 100, 30, 105, ScheduleLevelFiveUp),  // 11
            Row(100, 100, 95, 60, 50, 1, 80, 100, 40, 105, ScheduleLevelFiveUp),  // 12
            Row(100, 100, 95, 60, 50, 1, 80, 100, 40, 105, ScheduleLevelFiveUp),  // 13
            Row(100, 100, 95, 60, 50, 3, 80, 100, 40, 105, ScheduleLevelFiveUp),  // 14
            Row(100, 100, 95, 60, 50, 1, 100, 100, 50, 105, ScheduleLevelFiveUp), // 15
            Row(100, 100, 95, 60, 50, 1, 100, 100, 50, 105, ScheduleLevelFiveUp), // 16
            Row(100, 0, 95, 0, 50, 0, 100, 100, 50, 105, ScheduleLevelFiveUp),    // 17: no frightened mode
            Row(100, 100, 95, 60, 50, 1, 100, 100, 50, 105, ScheduleLevelFiveUp), // 18
            Row(90, 0, 95, 0, 50, 0, 120, 100, 60, 105, ScheduleLevelFiveUp),     // 19
            Row(90, 0, 95, 0, 50, 0, 120, 100, 60, 105, ScheduleLevelFiveUp),     // 20
            Row(90, 0, 95, 0, 50, 0, 120, 100, 60, 105, ScheduleLevelFiveUp),     // 21+
        };

        /// <summary>The rule for a 1-based level number. Levels past the table use the last row.</summary>
        public static LevelRule For(int level)
        {
            int index = System.Math.Max(1, System.Math.Min(level, Rows.Length)) - 1;
            return Rows[index];
        }
    }
}
