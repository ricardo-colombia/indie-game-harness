using System;

namespace Studio.Pacman
{
    /// <summary>
    /// When the ghosts waiting in the house may leave, by the classic dot-counter rules (Dossier). Each waiting
    /// ghost has a personal counter of dots eaten while it is the next in line (Pinky, then Inky, then Clyde).
    /// After Pac-Man loses a life a single global counter takes over until Clyde leaves. If no dot is eaten
    /// for a few seconds the next ghost in line leaves anyway. Pure logic: it knows nothing of Unity.
    /// </summary>
    public class PacmanGhostRelease
    {
        /// <summary>Ghost indices 1..3 are Pinky, Inky and Clyde. Blinky (0) is never held in the house.</summary>
        public const int HouseGhosts = 3;

        /// <summary>Seconds between two ghosts leaving, so they don't leave on top of each other.</summary>
        public const float ReleaseGap = 0.6f;

        private static readonly int[] GlobalLimits = { 7, 17, 32 };

        private readonly int[] personal = new int[HouseGhosts + 1];
        private readonly bool[] released = new bool[HouseGhosts + 1];
        private int level = 1;
        private bool globalActive;
        private int global;
        private float idle;
        private float gap;

        /// <summary>True while the global counter (used after a death) is in charge.</summary>
        public bool GlobalActive => globalActive;
        public int GlobalCount => global;

        /// <summary>Dots a ghost (1..3) must see eaten before it leaves, on its own counter.</summary>
        public static int PersonalLimit(int level, int ghost)
        {
            if (level <= 1)
            {
                return ghost == 1 ? 0 : ghost == 2 ? 30 : 60;
            }
            if (level == 2)
            {
                return ghost == 3 ? 50 : 0;
            }
            return 0;
        }

        /// <summary>Seconds without eating a dot before the next ghost is sent out.</summary>
        public static float IdleLimit(int level)
        {
            return level <= 4 ? 4f : 3f;
        }

        /// <summary>
        /// Puts every house ghost back in the house. A new level (or game) clears the personal counters; after a
        /// death they are kept and the global counter starts from zero.
        /// </summary>
        public void Reset(int newLevel, bool afterDeath)
        {
            level = Math.Max(1, newLevel);
            Array.Clear(released, 0, released.Length);
            released[0] = true;
            idle = 0f;
            gap = 0f;
            global = 0;
            globalActive = afterDeath;
            if (!afterDeath)
            {
                Array.Clear(personal, 0, personal.Length);
            }
            Evaluate();
        }

        public bool IsReleased(int ghost)
        {
            return ghost <= 0 || (ghost <= HouseGhosts && released[ghost]);
        }

        /// <summary>Personal counter of a house ghost (1..3). Probe for tests.</summary>
        public int PersonalCount(int ghost)
        {
            return ghost >= 1 && ghost <= HouseGhosts ? personal[ghost] : 0;
        }

        /// <summary>Call when Pac-Man eats a dot or a power pellet.</summary>
        public void OnDot()
        {
            idle = 0f;
            int waiting = NextWaiting();
            if (globalActive)
            {
                global++;
            }
            else if (waiting > 0)
            {
                personal[waiting]++;
            }
            Evaluate();
        }

        /// <summary>Advances the idle and spacing timers by dt seconds of play.</summary>
        public void Tick(float dt)
        {
            idle += dt;
            if (gap > 0f)
            {
                gap -= dt;
            }

            if (idle >= IdleLimit(level) && NextWaiting() > 0)
            {
                idle = 0f;
                ReleaseNext();
                return;
            }
            Evaluate();
        }

        private int NextWaiting()
        {
            for (int i = 1; i <= HouseGhosts; i++)
            {
                if (!released[i])
                {
                    return i;
                }
            }
            return 0;
        }

        private void ReleaseNext()
        {
            int waiting = NextWaiting();
            if (waiting == 0)
            {
                return;
            }
            released[waiting] = true;
            gap = ReleaseGap;
            // Clyde leaving while the global counter has reached its last stage hands control back to
            // the personal counters.
            if (globalActive && waiting == HouseGhosts && global == GlobalLimits[HouseGhosts - 1])
            {
                globalActive = false;
            }
        }

        /// <summary>Releases the next ghost in line when its counter allows it (one per call, spaced by the gap).</summary>
        private void Evaluate()
        {
            int waiting = NextWaiting();
            if (waiting == 0 || gap > 0f)
            {
                return;
            }

            bool ready = globalActive
                ? global >= GlobalLimits[waiting - 1]
                : personal[waiting] >= PersonalLimit(level, waiting);
            if (ready)
            {
                ReleaseNext();
            }
        }
    }
}
