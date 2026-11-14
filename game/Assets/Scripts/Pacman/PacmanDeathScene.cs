using System.Collections.Generic;
using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// What kills the picker depends on who catches him, so the death says something about the pest: the coral snake
    /// lunges and bites, the DIAN agent bleeds him of his money, the guerrilla opens fire, and the narco buries him
    /// under cash for the farm. It only adds visuals around the moment of the catch; the rules do not change.
    /// </summary>
    public class PacmanDeathScene : MonoBehaviour
    {
        private struct Flyer
        {
            public Transform Transform;
            public Vector3 From;
            public Vector3 To;
            public float Start;
            public float Length;
            public float Arc;
            public float Spin;
        }

        private GhostKind kind;
        private Transform killer;
        private Transform victim;
        private float duration;
        private float time;
        private float nextSpawn;
        private int shots;
        private Vector3 killerHome;
        private readonly List<Flyer> flyers = new List<Flyer>();

        /// <summary>Starts the scene for one death. It removes itself, and everything it made, when the death is over.</summary>
        public static void Play(GhostKind byKind, Transform by, Transform on, float length)
        {
            GameObject go = new GameObject("DeathScene");
            PacmanDeathScene scene = go.AddComponent<PacmanDeathScene>();
            scene.kind = byKind;
            scene.killer = by;
            scene.victim = on;
            scene.duration = length;
            scene.killerHome = by.position;
            Vector3 up = Vector3.up * 0.9f;
            switch (byKind)
            {
                case GhostKind.Blinky:
                    PacmanUi.Show(by.position + up, "IMPUESTOS", new Color(1f, 0.85f, 0.3f));
                    break;
                case GhostKind.Inky:
                    PacmanUi.Show(by.position + up, "UNA OFERTA…", new Color(0.85f, 0.9f, 0.7f));
                    break;
            }
        }

        private void Update()
        {
            time += Time.deltaTime;
            if (killer == null || victim == null || time > duration + 0.2f)
            {
                Finish();
                return;
            }

            Vector3 to = victim.position - killer.position;
            Vector3 dir = to.sqrMagnitude > 0.0001f ? to.normalized : Vector3.right;
            switch (kind)
            {
                case GhostKind.Clyde:
                    Bite(dir);
                    break;
                case GhostKind.Blinky:
                    Taxes();
                    break;
                case GhostKind.Pinky:
                    Shoot(dir);
                    break;
                default:
                    Cash();
                    break;
            }
            Fly();
        }

        /// <summary>The snake coils back, strikes at the picker and returns, leaving two green fang flashes.</summary>
        private void Bite(Vector3 dir)
        {
            float u = Mathf.Clamp01((time - 0.1f) / 0.35f);
            float strike = Mathf.Sin(u * Mathf.PI);
            killer.position = killerHome + dir * (0.35f * strike - 0.08f * Mathf.Sin(Mathf.Clamp01(time / 0.1f) * Mathf.PI));
            if (shots == 0 && u >= 0.5f)
            {
                shots = 1;
                Vector3 bite = victim.position + Vector3.up * 0.05f;
                PacmanVfx.Pop(bite + Vector3.left * 0.06f, new Color(0.55f, 1f, 0.3f), 10);
                PacmanVfx.Pop(bite + Vector3.right * 0.06f, new Color(0.55f, 1f, 0.3f), 10);
                PacmanUi.Show(killer.position + Vector3.up * 0.9f, "¡MORDIDA!", new Color(0.6f, 1f, 0.35f));
            }
        }

        /// <summary>Banknotes and coins fly out of the picker into the agent's briefcase until nothing is left.</summary>
        private void Taxes()
        {
            if (time >= nextSpawn && time < duration * 0.85f)
            {
                nextSpawn = time + 0.07f;
                bool coin = Random.value < 0.3f;
                Vector3 from = victim.position + new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(-0.1f, 0.3f), 0f);
                AddFlyer(from, killer.position + Vector3.up * 0.1f, 0.55f, coin ? Coin() : Bill(), 0.6f);
            }
        }

        /// <summary>Three shots: a muzzle flash at the guerrilla, a tracer to the picker and a hit puff.</summary>
        private void Shoot(Vector3 dir)
        {
            float[] at = { 0.15f, 0.5f, 0.85f };
            if (shots < at.Length && time >= at[shots])
            {
                shots++;
                Vector3 muzzle = killer.position + dir * 0.35f + Vector3.up * 0.05f;
                PacmanVfx.Pop(muzzle, new Color(1f, 0.9f, 0.35f), 12);
                PacmanUi.Show(killer.position + Vector3.up * 0.9f, "¡PUM!", new Color(1f, 0.55f, 0.25f));
                Transform tracer = Bill(true);
                tracer.localScale = new Vector3(0.3f, 0.05f, 1f);
                tracer.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                AddFlyer(muzzle, victim.position, 0.1f, tracer, 0f, false);
                PacmanVfx.Pop(victim.position + Vector3.up * 0.05f, new Color(1f, 0.6f, 0.2f), 10);
            }
            // Recoil on every shot.
            float recoil = 0f;
            for (int i = 0; i < at.Length; i++)
            {
                float d = time - at[i];
                if (d >= 0f && d < 0.12f)
                {
                    recoil = Mathf.Sin(d / 0.12f * Mathf.PI) * 0.1f;
                }
            }
            killer.position = killerHome - dir * recoil;
        }

        /// <summary>A few notes drift down on the picker: a quiet offer, not a deluge.</summary>
        private void Cash()
        {
            if (time >= nextSpawn && time < duration * 0.9f)
            {
                nextSpawn = time + 0.14f;
                Vector3 top = victim.position + new Vector3(Random.Range(-0.5f, 0.5f), 1.1f, 0f);
                AddFlyer(top, victim.position + new Vector3(Random.Range(-0.15f, 0.15f), -0.3f, 0f), 0.6f, Bill(), 0f);
            }
        }

        // ---------------------------------------------------------------- flyers

        private Transform Bill(bool tracer = false)
        {
            GameObject go = new GameObject(tracer ? "Tracer" : "Bill");
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(0.3f, 0.17f, 1f);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PacmanSprites.Square;
            sr.sortingOrder = 60;
            sr.color = tracer ? new Color(1f, 0.92f, 0.4f) : new Color(0.5f, 0.78f, 0.42f);
            if (!tracer)
            {
                GameObject band = new GameObject("Band");
                band.transform.SetParent(go.transform, false);
                band.transform.localScale = new Vector3(0.35f, 0.5f, 1f);
                SpriteRenderer b = band.AddComponent<SpriteRenderer>();
                b.sprite = PacmanSprites.Square;
                b.sortingOrder = 61;
                b.color = new Color(0.25f, 0.45f, 0.22f);
            }
            return go.transform;
        }

        private Transform Coin()
        {
            GameObject go = new GameObject("Coin");
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * 0.18f;
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PacmanSprites.Disc;
            sr.sortingOrder = 60;
            sr.color = new Color(1f, 0.82f, 0.2f);
            return go.transform;
        }

        private void AddFlyer(Vector3 from, Vector3 to, float length, Transform t, float arc, bool spin = true)
        {
            t.position = from;
            flyers.Add(new Flyer { Transform = t, From = from, To = to, Start = time, Length = length, Arc = arc, Spin = spin ? Random.Range(-540f, 540f) : 0f });
        }

        private void Fly()
        {
            for (int i = flyers.Count - 1; i >= 0; i--)
            {
                Flyer f = flyers[i];
                float u = (time - f.Start) / f.Length;
                if (u >= 1f)
                {
                    Destroy(f.Transform.gameObject);
                    flyers.RemoveAt(i);
                    continue;
                }
                Vector3 p = Vector3.Lerp(f.From, f.To, u);
                p.y += Mathf.Sin(u * Mathf.PI) * f.Arc;
                f.Transform.position = p;
                if (f.Spin != 0f)
                {
                    f.Transform.rotation = Quaternion.Euler(0f, 0f, f.Spin * u);
                }
            }
        }

        private void Finish()
        {
            if (killer != null && (kind == GhostKind.Clyde || kind == GhostKind.Pinky))
            {
                killer.position = killerHome;
            }
            Destroy(gameObject);
        }
    }
}
