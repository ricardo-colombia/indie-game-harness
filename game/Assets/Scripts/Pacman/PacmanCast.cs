namespace Studio.Pacman
{
    /// <summary>One character's sheet: who they are, what they want, and what they teach the player.</summary>
    public sealed class CastMember
    {
        public string Name;
        public string Role;
        /// <summary>Which prologue picture shows this character (the real rigs, filmed into a texture).</summary>
        public StoryScene Scene;
        /// <summary>Who they are and how you notice them.</summary>
        public string Context;
        public string Wants;
        /// <summary>What playing against them teaches.</summary>
        public string Lesson;
        /// <summary>The short line of the name tag shown the first time they come out in a game.</summary>
        public string Tag;
        /// <summary>The ghost slot they play, or null for the farmer.</summary>
        public GhostKind? Pest;
    }

    /// <summary>
    /// The cast of the game, as plain data (no scene access). Credible people with a reason to be on the farm, a
    /// look you recognise and something they teach: the farmer, and the four who come for what he has. The
    /// tone is a farm fable: nobody is a real organisation and nobody is a joke at the farmer's expense.
    /// </summary>
    public static class PacmanCast
    {
        public static readonly CastMember[] Members =
        {
            new CastMember
            {
                Name = "DON ELISEO", Role = "El campesino", Scene = StoryScene.Farmer, Pest = null,
                Context = "68 años, caficultor de la vereda. Ruana al hombro, sombrero de paja y botas pantaneras. Camina firme y se detiene en cada mata.",
                Wants = "Que esta cosecha pague la escuela de su nieta Lucía y no perder la tierra de su padre.",
                Lesson = "Todos quieren algo de él. Él solo quiere terminar.",
                Tag = "Quiere terminar la cosecha.",
            },
            new CastMember
            {
                Name = "EL DOCTOR", Role = "El señor de los impuestos", Scene = StoryScene.Taxes, Pest = GhostKind.Blinky,
                Context = "Funcionario de la zona, con carpeta, maletín y casco de moto. No es malo: es puntual e implacable. Se le oye venir.",
                Wants = "Su parte, con los papeles en regla.",
                Lesson = "Presión constante: se acelera al final. No dejes los granos difíciles para después.",
                Tag = "Viene por su parte.",
            },
            new CastMember
            {
                Name = "ALIAS EL FLACO", Role = "El guerrillero", Scene = StoryScene.Guerrilla, Pest = GhostKind.Pinky,
                Context = "Sale del monte con un megáfono. Cuando se callan los pájaros, ya está cerca.",
                Wants = "Colaboración para una guerra que Don Eliseo nunca pidió.",
                Lesson = "Te corta el camino: no te metas en pasillos sin salida.",
                Tag = "Pide colaboración.",
            },
            new CastMember
            {
                Name = "DON HERNÁN", Role = "El patrón", Scene = StoryScene.Boss, Pest = GhostKind.Inky,
                Context = "Llega despacio en camioneta polarizada, cuando ya hay cosecha que valga. Su peligro no es la amenaza: es la oferta.",
                Wants = "La tierra, para sembrar otra cosa.",
                Lesson = "Cierra por el otro lado: no te quedes entre dos.",
                Tag = "Quiere comprar la finca.",
            },
            new CastMember
            {
                Name = "LA CORAL", Role = "La culebra de la finca", Scene = StoryScene.Snake, Pest = GhostKind.Clyde,
                Context = "Vive bajo los cafetos y entre las piedras. No le tiene rencor a nadie: la finca también es suya.",
                Wants = "Que la dejen en paz.",
                Lesson = "Te sigue de lejos, pero si te acercas se retira. Respeta su territorio.",
                Tag = "Defiende su casa.",
            },
        };

        public static int Count => Members.Length;

        /// <summary>The sheet of the character that plays a ghost slot.</summary>
        public static CastMember For(GhostKind kind)
        {
            foreach (CastMember member in Members)
            {
                if (member.Pest == kind)
                {
                    return member;
                }
            }
            return null;
        }

        /// <summary>Index into <see cref="Members"/> with wrap-around in both directions.</summary>
        public static int Wrap(int index)
        {
            int n = Members.Length;
            return ((index % n) + n) % n;
        }
    }
}
