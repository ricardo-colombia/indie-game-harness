using System.Collections.Generic;
using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// Builds a ghost at runtime: a rounded dome, a straight skirt and a scalloped hem that ripples between
    /// two frames. The eyes sit on the front (+Z, the direction the ghost faces) and on top of the dome so
    /// the straight-down camera sees them. PacmanGhostView drives the look. Cosmetic only.
    /// </summary>
    public static class PacmanGhostMesh
    {
        private const float Radius = 0.4f;
        private const int Segments = 32;
        private const int DomeRings = 10;
        private const int BodyRings = 4;
        private const int Scallops = 5;

        private static readonly Mesh[] Frames = new Mesh[2];

        /// <summary>Creates the ghost model under a parent and returns its transform.</summary>
        public static Transform Create(Transform parent, Color colour)
        {
            GameObject model = new GameObject("Model");
            model.transform.SetParent(parent, false);

            model.AddComponent<MeshFilter>().sharedMesh = Frame(0);
            MeshRenderer body = model.AddComponent<MeshRenderer>();
            Material material = PacmanMaterials.Lit(colour, 0.55f, 0f, colour * 0.25f);
            body.sharedMaterial = material;

            PacmanGhostView view = model.AddComponent<PacmanGhostView>();
            // Seen in profile: one eye, which PacmanGhostView keeps on the upper (or right) side of the screen.
            Transform eye = AddEye(model.transform, new Vector3(0f, 0.27f, 0.13f), out Renderer pupil);
            Renderer mouth = AddFrightenedMouth(model.transform);
            view.Setup(colour, body, material, new[] { eye }, new[] { pupil }, mouth);
            return model.transform;
        }

        /// <summary>One of the two hem frames. They differ by half a scallop, so swapping reads as rippling.</summary>
        public static Mesh Frame(int index)
        {
            index &= 1;
            if (Frames[index] == null)
            {
                Frames[index] = Build(index * Mathf.PI);
            }
            return Frames[index];
        }

        /// <summary>Lathe-style body: dome on top, straight skirt, scalloped hem.</summary>
        public static Mesh Build(float hemPhase)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();

            // Dome: from the top pole down to the equator.
            for (int row = 0; row <= DomeRings; row++)
            {
                float theta = Mathf.PI * 0.5f - Mathf.PI * 0.5f * row / DomeRings;
                AddRing(vertices, Radius * Mathf.Cos(theta), Radius * Mathf.Sin(theta), null, hemPhase);
            }

            // Skirt: straight down, with the bottom edge scalloped by angle.
            for (int row = 1; row <= BodyRings; row++)
            {
                AddRing(vertices, Radius, 0f, row / (float)BodyRings, hemPhase);
            }

            int rowCount = DomeRings + 1 + BodyRings;
            for (int row = 0; row < rowCount - 1; row++)
            {
                for (int s = 0; s < Segments; s++)
                {
                    int next = (s + 1) % Segments;
                    int a = row * Segments + s;
                    int b = row * Segments + next;
                    int c = (row + 1) * Segments + next;
                    int d = (row + 1) * Segments + s;
                    triangles.Add(a); triangles.Add(b); triangles.Add(d);
                    triangles.Add(b); triangles.Add(c); triangles.Add(d);
                }
            }

            Mesh mesh = new Mesh { name = "PacmanGhost" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddRing(List<Vector3> vertices, float radius, float y, float? skirtT, float hemPhase)
        {
            for (int s = 0; s < Segments; s++)
            {
                float angle = Mathf.PI * 2f * s / Segments;
                float height = y;
                if (skirtT.HasValue)
                {
                    height = Mathf.Lerp(0f, Hem(angle, hemPhase), skirtT.Value);
                }
                vertices.Add(new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius));
            }
        }

        // Five soft scallops around the hem; hemPhase shifts them by half a scallop between frames.
        private static float Hem(float angle, float hemPhase)
        {
            float wave = 0.5f + 0.5f * Mathf.Cos(angle * Scallops + hemPhase);
            return -0.24f - 0.09f * wave;
        }

        private static Transform AddEye(Transform parent, Vector3 localPosition, out Renderer pupilRenderer)
        {
            GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eye.name = "Eye";
            eye.transform.SetParent(parent, false);
            eye.transform.localPosition = localPosition;
            eye.transform.localScale = new Vector3(0.27f, 0.31f, 0.27f);
            Object.Destroy(eye.GetComponent<Collider>());
            eye.GetComponent<Renderer>().sharedMaterial = PacmanMaterials.Lit(Color.white, 0.8f, 0f, new Color(0.35f, 0.35f, 0.4f));

            GameObject pupil = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pupil.name = "Pupil";
            pupil.transform.SetParent(eye.transform, false);
            pupil.transform.localPosition = new Vector3(0f, 0f, 0.3f);
            pupil.transform.localScale = Vector3.one * 0.55f;
            Object.Destroy(pupil.GetComponent<Collider>());
            pupilRenderer = pupil.GetComponent<Renderer>();
            pupilRenderer.sharedMaterial = PacmanMaterials.Lit(new Color(0.1f, 0.2f, 0.85f), 0.9f);
            return eye.transform;
        }

        /// <summary>The wavy frightened mouth: a thin zigzag lying on the dome, behind the eyes.</summary>
        private static Renderer AddFrightenedMouth(Transform parent)
        {
            const int points = 8;
            const float halfWidth = 0.22f;
            const float width = 0.035f;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i < points; i++)
            {
                float x = Mathf.Lerp(-halfWidth, halfWidth, i / (float)(points - 1));
                float z = -0.06f + (i % 2 == 0 ? 0.045f : -0.045f);
                float y = Mathf.Sqrt(Mathf.Max(0f, Radius * Radius - x * x - z * z)) + 0.004f;
                vertices.Add(new Vector3(x, y, z - width));
                vertices.Add(new Vector3(x, y, z + width));
            }
            for (int i = 0; i < points - 1; i++)
            {
                int a = i * 2;
                triangles.Add(a); triangles.Add(a + 1); triangles.Add(a + 2);
                triangles.Add(a + 1); triangles.Add(a + 3); triangles.Add(a + 2);
                triangles.Add(a); triangles.Add(a + 2); triangles.Add(a + 1);
                triangles.Add(a + 1); triangles.Add(a + 2); triangles.Add(a + 3);
            }
            Mesh mesh = new Mesh { name = "FrightenedMouth" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();

            GameObject mouth = new GameObject("FrightenedMouth");
            mouth.transform.SetParent(parent, false);
            mouth.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = mouth.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = PacmanMaterials.Lit(new Color(1f, 0.85f, 0.75f), 0.2f, 0f, new Color(1f, 0.8f, 0.7f) * 0.8f);
            renderer.enabled = false;
            return renderer;
        }
    }
}
