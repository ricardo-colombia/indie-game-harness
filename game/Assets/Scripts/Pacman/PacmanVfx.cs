using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// Eating sparks. One pooled particle system is created once and reused for every burst,
    /// so eating never allocates or destroys objects. Purely cosmetic: it never touches game state.
    /// </summary>
    public static class PacmanVfx
    {
        private const float Lifetime = 0.45f;

        private static ParticleSystem system;

        /// <summary>Emits a burst of sparks at a world position.</summary>
        public static void Burst(Vector3 position, Color colour, int count = 8)
        {
            EnsureSystem();
            if (system == null)
            {
                return;
            }

            system.transform.position = position;
            var emitParams = new ParticleSystem.EmitParams
            {
                startColor = colour,
            };
            system.Emit(emitParams, count);
        }

        private static void EnsureSystem()
        {
            if (system != null)
            {
                return;
            }

            GameObject host = new GameObject("EatSparks");
            system = host.AddComponent<ParticleSystem>();
            // A new system starts playing on add; stop it before changing its settings.
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            // Configure once; the module values are read when the system starts.
            var main = system.main;
            // Looping keeps the system alive for every later Emit; emission is off, so it stays idle between bursts.
            main.loop = true;
            main.playOnAwake = false;
            main.duration = Lifetime;
            main.startLifetime = Lifetime;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;

            var emission = system.emission;
            emission.enabled = false;

            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;

            var rend = host.GetComponent<ParticleSystemRenderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Sprites/Default");
            rend.sharedMaterial = new Material(shader);

            system.Play();
        }
    }
}
