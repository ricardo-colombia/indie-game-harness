using System.Collections.Generic;
using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// Builds a ghost silhouette at runtime: a rounded dome, a straight skirt and a scalloped hem,
    /// with two eyes on the front (+Z, the direction the ghost faces). Cosmetic only.
    /// </summary>
    public static class PacmanGhostMesh
    {
        private const float Radius = 0.4f;
        private const int Segments = 24;
        private const int DomeRings = 8;
        private const int BodyRings = 3;

        /// <summary>Creates the ghost model under a parent and returns its transform.</summary>
        public static Transform Create(Transform parent, Color colour)
        {
            GameObject model = new GameObject("Model");
            model.transform.SetParent(parent, false);

            MeshFilter filter = model.AddComponent<MeshFilter>();
            filter.sharedMesh = Build();
            MeshRenderer renderer = model.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = MakeMaterial(colour);

            // Eyes: white sclera with a blue pupil, looking forward.
            AddEye(model.transform, new Vector3(-0.14f, 0.1f, 0.34f));
            AddEye(model.transform, new Vector3(0.14f, 0.1f, 0.34f));
            return model.transform;
        }

        /// <summary>Lathe-style body: dome on top, straight skirt, hem with three scallops.</summary>
        public static Mesh Build()
        {
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();

            // Dome: from the top pole down to the equator.
            for (int row = 0; row <= DomeRings; row++)
            {
                float theta = Mathf.PI * 0.5f - Mathf.PI * 0.5f * row / DomeRings;
                float r = Radius * Mathf.Cos(theta);
                float y = Radius * Mathf.Sin(theta);
                AddRing(vertices, r, y, null);
            }

            // Skirt: straight down, with the bottom edge scalloped by angle.
            for (int row = 1; row <= BodyRings; row++)
            {
                float t = row / (float)BodyRings;
                AddRing(vertices, Radius, 0f, t);
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
                    // Two triangles per quad, wound so normals face outward.
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

        // Dome rows use the fixed y and radius. Skirt rows (t != null) go from y=0 down to the hem.
        private static void AddRing(List<Vector3> vertices, float radius, float y, float? skirtT)
        {
            for (int s = 0; s < Segments; s++)
            {
                float angle = Mathf.PI * 2f * s / Segments;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                float height = y;
                if (skirtT.HasValue)
                {
                    height = Mathf.Lerp(0f, Hem(angle), skirtT.Value);
                }
                vertices.Add(new Vector3(x, height, z));
            }
        }

        // Three scallops around the hem: lowest at each scallop tip.
        private static float Hem(float angle)
        {
            return -0.22f - 0.1f * Mathf.Abs(Mathf.Cos(angle * 3f));
        }

        private static void AddEye(Transform parent, Vector3 localPosition)
        {
            GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eye.name = "Eye";
            eye.transform.SetParent(parent, false);
            eye.transform.localPosition = localPosition;
            eye.transform.localScale = Vector3.one * 0.16f;
            Object.Destroy(eye.GetComponent<Collider>());
            eye.GetComponent<Renderer>().sharedMaterial = MakeMaterial(Color.white);

            GameObject pupil = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pupil.name = "Pupil";
            pupil.transform.SetParent(eye.transform, false);
            pupil.transform.localPosition = new Vector3(0f, 0f, 0.38f);
            pupil.transform.localScale = Vector3.one * 0.5f;
            Object.Destroy(pupil.GetComponent<Collider>());
            pupil.GetComponent<Renderer>().sharedMaterial = MakeMaterial(new Color(0.1f, 0.2f, 0.8f));
        }

        private static Material MakeMaterial(Color colour)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new Material(shader);
            material.color = colour;
            return material;
        }
    }
}
