using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// Particle effects. Two pooled particle systems are created once and reused for every effect, so
    /// nothing allocates or is destroyed during play: sparks (small bursts) and rings (expanding circles).
    /// Colours are boosted past 1 so the bloom makes them glow. Purely cosmetic: never touches game state.
    /// </summary>
    public static class PacmanVfx
    {
        private const float Glow = 1.15f;

        private static ParticleSystem sparks;
        private static ParticleSystem rings;

        /// <summary>Emits a burst of sparks at a world position.</summary>
        public static void Burst(Vector3 position, Color colour, int count = 8)
        {
            Emit(EnsureSparks(), position, colour, count);
        }

        /// <summary>Big radial pop: a ring plus sparks. Used for eaten ghosts, the cherry and Pac-Man's end.</summary>
        public static void Pop(Vector3 position, Color colour, int sparkCount = 28)
        {
            Emit(EnsureSparks(), position, colour, sparkCount);
            Emit(EnsureRings(), position, colour, 36);
        }

        private static void Emit(ParticleSystem system, Vector3 position, Color colour, int count)
        {
            if (system == null)
            {
                return;
            }

            // In front of the board plane (the camera looks along +Z).
            system.transform.position = new Vector3(position.x, position.y, -1f);
            var emitParams = new ParticleSystem.EmitParams
            {
                startColor = new Color(colour.r * Glow, colour.g * Glow, colour.b * Glow, 1f),
            };
            system.Emit(emitParams, count);
        }

        private static ParticleSystem EnsureSparks()
        {
            if (sparks != null)
            {
                return sparks;
            }

            sparks = CreateSystem("EatSparks", 0.55f, 160);
            var main = sparks.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            main.gravityModifier = 0f;

            var shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.05f;

            var velocity = sparks.limitVelocityOverLifetime;
            velocity.enabled = true;
            velocity.dampen = 0.15f;
            velocity.limit = 1.5f;

            var size = sparks.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));
            sparks.Play();
            return sparks;
        }

        private static ParticleSystem EnsureRings()
        {
            if (rings != null)
            {
                return rings;
            }

            rings = CreateSystem("EatRing", 0.4f, 160);
            var main = rings.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(4.2f, 4.8f);
            main.startSize = 0.09f;

            var shape = rings.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.1f;

            var size = rings.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));
            rings.Play();
            return rings;
        }

        private static ParticleSystem CreateSystem(string name, float lifetime, int max)
        {
            GameObject host = new GameObject(name);
            ParticleSystem system = host.AddComponent<ParticleSystem>();
            // A new system starts playing on add; stop it before changing its settings.
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            // Looping keeps the system alive for every later Emit; emission is off, so it stays idle between bursts.
            main.loop = true;
            main.playOnAwake = false;
            main.duration = lifetime;
            main.startLifetime = lifetime;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;

            var emission = system.emission;
            emission.enabled = false;

            ParticleSystemRenderer rend = host.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = PacmanMaterials.Particle();
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.sortingOrder = 30;
            return system;
        }
    }
}
