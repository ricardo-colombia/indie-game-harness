namespace Studio.Pacman
{
    /// <summary>
    /// The farm the farmer is harvesting at a given level. Plain names a coffee grower in the Colombian
    /// highlands would really give a finca; the list repeats when the levels run past its end.
    /// </summary>
    public static class PacmanFarms
    {
        private static readonly string[] Names =
        {
            "Finca La Esmeralda",
            "Hacienda El Cafetal",
            "Finca Los Geranios",
            "Finca El Paraíso",
            "Hacienda La Primavera",
            "Finca Las Margaritas",
            "Finca Buenavista",
            "Hacienda Santa Ana",
            "Finca La Aurora",
            "Finca El Mirador",
            "Finca Villa Rosa",
            "Hacienda Los Naranjos",
            "Finca El Recuerdo",
            "Finca La Palma",
            "Finca Alto Bonito",
            "Hacienda San José",
        };

        /// <summary>Number of different names before they start over.</summary>
        public static int Count => Names.Length;

        /// <summary>Name of the farm for a level (levels count from 1). Out-of-range levels wrap around.</summary>
        public static string NameFor(int level)
        {
            int index = (level - 1) % Names.Length;
            if (index < 0)
            {
                index += Names.Length;
            }
            return Names[index];
        }
    }
}
