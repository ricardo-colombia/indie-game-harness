using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>The footwear the farmer can wear. Each pair looks different and sounds different on the path.</summary>
    public enum Shoe
    {
        LeatherBoots,
        RubberBoots,
        Espadrilles,
        FlipFlops,
    }

    /// <summary>Names and colours of the shoes. Cycling order is the enum order.</summary>
    public static class PacmanShoes
    {
        public static readonly int Count = System.Enum.GetValues(typeof(Shoe)).Length;

        /// <summary>The shoe after this one, wrapping round.</summary>
        public static Shoe Next(Shoe shoe)
        {
            return (Shoe)(((int)shoe + 1) % Count);
        }

        /// <summary>Menu name, in Spanish like the rest of the UI.</summary>
        public static string Label(Shoe shoe)
        {
            switch (shoe)
            {
                case Shoe.RubberBoots: return "BOTAS PANTANERAS";
                case Shoe.Espadrilles: return "ALPARGATAS";
                case Shoe.FlipFlops: return "CHANCLAS";
                default: return "BOTAS DE CUERO";
            }
        }

        /// <summary>Colour of the shoe sprite on the farmer.</summary>
        public static Color Colour(Shoe shoe)
        {
            switch (shoe)
            {
                case Shoe.RubberBoots: return new Color(0.16f, 0.45f, 0.22f);
                case Shoe.Espadrilles: return new Color(0.86f, 0.76f, 0.52f);
                case Shoe.FlipFlops: return new Color(0.82f, 0.2f, 0.2f);
                default: return new Color(0.2f, 0.12f, 0.07f);
            }
        }
    }
}
