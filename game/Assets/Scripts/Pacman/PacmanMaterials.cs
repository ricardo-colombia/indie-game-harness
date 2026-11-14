using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// One place that makes the game's materials, so look changes happen here rather than in every builder.
    /// Everything uses URP Lit (already in the project); glow comes from emission plus the bloom volume.
    /// </summary>
    public static class PacmanMaterials
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
        private static readonly int Metallic = Shader.PropertyToID("_Metallic");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        private static Shader LitShader()
        {
            return Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        }

        /// <summary>A lit surface. emission is an HDR colour added on top; values above 1 bloom.</summary>
        public static Material Lit(Color colour, float smoothness = 0.35f, float metallic = 0f, Color? emission = null)
        {
            Material material = new Material(LitShader());
            SetColour(material, colour);
            material.SetFloat(Smoothness, smoothness);
            material.SetFloat(Metallic, metallic);
            if (emission.HasValue)
            {
                SetEmission(material, emission.Value);
            }
            return material;
        }

        /// <summary>A flat glowing colour: dark base, strong emission. Used for neon trim.</summary>
        public static Material Neon(Color colour, float intensity = 2.5f)
        {
            Material material = Lit(colour * 0.1f, 0f, 0f, colour * intensity);
            return material;
        }

        public static void SetColour(Material material, Color colour)
        {
            if (material.HasProperty(BaseColor))
            {
                material.SetColor(BaseColor, colour);
            }
            if (material.HasProperty(ColorId))
            {
                material.SetColor(ColorId, colour);
            }
        }

        public static void SetEmission(Material material, Color emission)
        {
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            if (material.HasProperty(EmissionColor))
            {
                material.SetColor(EmissionColor, emission);
            }
        }

        /// <summary>Material for the additive-looking particle effects.</summary>
        public static Material Particle()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Sprites/Default");
            return new Material(shader);
        }
    }
}
