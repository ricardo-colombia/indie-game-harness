using System;
using System.Collections.Generic;

namespace Studio.Pacman
{
    /// <summary>When the farmer says something: while he walks, or in reaction to what just happened.</summary>
    public enum SayKind { Walk, Power, EatPest, Hurt, Clear }

    /// <summary>
    /// What Don Eliseo says out loud: Colombian campesino sayings and dry farm humour. Plain data, no scene access.
    /// The humour is his own and warm (the joke is the situation, never the farmer or anyone who works the land).
    /// </summary>
    public static class PacmanSayings
    {
        /// <summary>Longest line the speech bubble is designed for.</summary>
        public const int MaxLength = 54;

        private static readonly string[] Walk =
        {
            "Camarón que se duerme, se lo lleva la corriente.",
            "Grano a grano llena la gallina el buche.",
            "Al que madruga, Dios le ayuda.",
            "Más vale pájaro en mano que ciento volando.",
            "Despacio, que voy de afán.",
            "Del dicho al hecho hay mucho trecho.",
            "¡Ave María, qué solazo!",
            "Mijo, el café no se recoge solo.",
            "El que tiene tienda, que la atienda.",
            "No hay mal que por bien no venga.",
            "Al mal tiempo, buena cara.",
            "Más vale tarde que nunca.",
            "Cuando el río suena, piedras lleva.",
            "Más sabe el diablo por viejo que por diablo.",
            "Barriga llena, corazón contento.",
            "¿Y mi tinto? Que se me enfría el alma.",
            "Esta mata sí está cargadita, bendito sea Dios.",
            "Esto es pan comido... con aguapanela.",
            "Con tinto y arepa no hay cosecha que se atragante.",
            "Mis botas y yo: inseparables.",
            "Quién quita que este año sí llueva a tiempo.",
            "En casa del cafetero, tinto de ayer.",
        };

        private static readonly string[] Power =
        {
            "¡Un tintico y quedo como nuevo!",
            "¡Eso sí es combustible!",
            "¡Ahora sí, a respetar al viejo!",
            "¡Café bien cargado, pa' que sepan!",
            "¡Pa' que se les quite lo atrevido!",
        };

        private static readonly string[] EatPest =
        {
            "¡Pa' que respeten!",
            "¡Eso, a mí no me pisa nadie!",
            "¡Ni tan fácil, mijo!",
            "¡Se lo dije!",
            "¡Que le sirva de lección!",
        };

        private static readonly string[] Hurt =
        {
            "¡Ay, mi madre!",
            "¡Quién me manda!",
            "¡Uy, no! Me cogieron.",
            "¡Ni modo, otra vez!",
            "¡Ay, Dios mío!",
        };

        private static readonly string[] Clear =
        {
            "¡Cosecha buena, gracias a Dios!",
            "¡Pa' la escuela de Lucía!",
            "¡Eso se merece un tinto!",
            "¡Otro día de trabajo bien hecho!",
        };

        /// <summary>All the lines for one kind of moment.</summary>
        public static IReadOnlyList<string> Pool(SayKind kind)
        {
            switch (kind)
            {
                case SayKind.Power: return Power;
                case SayKind.EatPest: return EatPest;
                case SayKind.Hurt: return Hurt;
                case SayKind.Clear: return Clear;
                default: return Walk;
            }
        }

        /// <summary>
        /// Draws lines like a shuffled bag: every line is said once before any repeats, and the first line of a new
        /// round is never the one just said. Uses its own random generator, so it never disturbs the game's.
        /// </summary>
        public sealed class Bag
        {
            private readonly string[] lines;
            private readonly Random random;
            private readonly List<string> left = new List<string>();
            private string last;

            public Bag(IReadOnlyList<string> pool, Random rng)
            {
                lines = new string[pool.Count];
                for (int i = 0; i < lines.Length; i++)
                {
                    lines[i] = pool[i];
                }
                random = rng;
            }

            public string Next()
            {
                if (left.Count == 0)
                {
                    left.AddRange(lines);
                    // Fisher-Yates.
                    for (int i = left.Count - 1; i > 0; i--)
                    {
                        int j = random.Next(i + 1);
                        string swap = left[i];
                        left[i] = left[j];
                        left[j] = swap;
                    }
                    if (lines.Length > 1 && left[left.Count - 1] == last)
                    {
                        // The next line is taken from the end of the list: move the repeat out of that spot.
                        string swap = left[0];
                        left[0] = left[left.Count - 1];
                        left[left.Count - 1] = swap;
                    }
                }
                string line = left[left.Count - 1];
                left.RemoveAt(left.Count - 1);
                last = line;
                return line;
            }
        }
    }
}
