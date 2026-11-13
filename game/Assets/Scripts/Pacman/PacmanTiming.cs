using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// Splits a long frame into short simulation steps. Pac-Man and a ghost running at each other close
    /// about ten tiles a second, so on a slow phone one frame could carry them clean through each other
    /// without the collision check ever seeing them overlap.
    /// </summary>
    public static class PacmanTiming
    {
        /// <summary>Longest simulation step in seconds: a sixtieth, so actors move under a fifth of a tile.</summary>
        public const float MaxStep = 1f / 60f;

        /// <summary>Most steps one frame may be split into, so a long hitch cannot stall the game.</summary>
        public const int MaxSteps = 24;

        /// <summary>How many equal steps a frame of dt seconds is split into, and how long each one lasts.</summary>
        public static int Steps(float dt, out float step)
        {
            if (dt <= 0f)
            {
                step = 0f;
                return 0;
            }
            int count = Mathf.Clamp(Mathf.CeilToInt(dt / MaxStep - 1e-4f), 1, MaxSteps);
            step = dt / count;
            return count;
        }
    }
}
