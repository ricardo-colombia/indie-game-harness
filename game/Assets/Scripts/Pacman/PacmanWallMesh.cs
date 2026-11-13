using System.Collections.Generic;
using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// Builds the whole maze wall as one mesh, like the arcade walls: outer corners are rounded and
    /// inner corners get a filleted join. Cosmetic only; collision still uses the tile grid.
    /// </summary>
    public static class PacmanWallMesh
    {
        private const float Height = 1f;
        private const int ArcSegments = 6;

        /// <summary>Corner offsets in tile units, walked in order around each tile.</summary>
        private static readonly Vector2Int[] Corners =
        {
            new Vector2Int(1, 1),
            new Vector2Int(-1, 1),
            new Vector2Int(-1, -1),
            new Vector2Int(1, -1),
        };

        public static Mesh Build(IList<Vector2Int> walls, float radius)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();

            foreach (Vector2Int tile in walls)
            {
                Vector3 centre = PacmanMaze.ToWorld(tile);
                centre.y = 0f;
                AddPlate(tile, centre, radius, vertices, normals, triangles);
                AddFillets(tile, centre, radius, vertices, normals, triangles);
            }

            Mesh mesh = new Mesh { name = "PacmanWalls", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// The tile's top and sides. A corner is rounded only when both neighbours beside it are open,
        /// so walls that join up stay flush.
        /// </summary>
        private static void AddPlate(Vector2Int tile, Vector3 centre, float r, List<Vector3> v, List<Vector3> n, List<int> t)
        {
            var outline = new List<Vector2>();
            foreach (Vector2Int corner in Corners)
            {
                int dx = corner.x;
                int dy = corner.y;
                bool wallBeside = PacmanMaze.IsWall(tile + new Vector2Int(dx, 0))
                    || PacmanMaze.IsWall(tile + new Vector2Int(0, dy));
                if (wallBeside)
                {
                    outline.Add(new Vector2(dx * 0.5f, dy * 0.5f));
                    continue;
                }

                // Quarter arc inside the corner, from the x-side to the y-side.
                Vector2 arcCentre = new Vector2(dx * (0.5f - r), dy * (0.5f - r));
                float a0 = Mathf.Atan2(0f, dx) * Mathf.Rad2Deg;
                float a1 = Mathf.Atan2(dy, 0f) * Mathf.Rad2Deg;
                float sweep = Mathf.DeltaAngle(a0, a1) * Mathf.Deg2Rad;
                float start = a0 * Mathf.Deg2Rad;
                for (int i = 0; i <= ArcSegments; i++)
                {
                    float a = start + sweep * i / ArcSegments;
                    outline.Add(arcCentre + r * new Vector2(Mathf.Cos(a), Mathf.Sin(a)));
                }
            }

            Vector3 up = Vector3.up;
            for (int i = 0; i < outline.Count; i++)
            {
                Vector2 a = outline[i];
                Vector2 b = outline[(i + 1) % outline.Count];
                Vector3 a0 = centre + new Vector3(a.x, 0f, -a.y);
                Vector3 b0 = centre + new Vector3(b.x, 0f, -b.y);
                Vector3 a1 = a0 + Vector3.up * Height;
                Vector3 b1 = b0 + Vector3.up * Height;

                AddTri(centre + Vector3.up * Height, a1, b1, up, v, n, t);

                // Outward side normal: the outline surrounds the tile centre.
                Vector3 side = new Vector3((a.x + b.x) * 0.5f, 0f, -(a.y + b.y) * 0.5f).normalized;
                AddTri(a0, b0, b1, side, v, n, t);
                AddTri(a0, b1, a1, side, v, n, t);
            }
        }

        /// <summary>
        /// Where two walls meet at a corner with an open diagonal, fill the concave gap with a fillet
        /// so the join curves the way the original walls do.
        /// </summary>
        private static void AddFillets(Vector2Int tile, Vector3 centre, float r, List<Vector3> v, List<Vector3> n, List<int> t)
        {
            foreach (Vector2Int corner in Corners)
            {
                int dx = corner.x;
                int dy = corner.y;
                bool bothSides = PacmanMaze.IsWall(tile + new Vector2Int(dx, 0))
                    && PacmanMaze.IsWall(tile + new Vector2Int(0, dy));
                if (!bothSides || PacmanMaze.IsWall(tile + new Vector2Int(dx, dy)))
                {
                    continue;
                }

                // Region = square at the corner minus a disc centred on M. It is star-shaped from C.
                Vector2 c = new Vector2(dx * 0.5f, dy * 0.5f);
                Vector2 m = c + new Vector2(dx * r, dy * r);
                Vector2 pointA = c + new Vector2(dx * r, 0f);

                float a0 = Mathf.Atan2(-dy, 0f) * Mathf.Rad2Deg;
                float a1 = Mathf.Atan2(0f, -dx) * Mathf.Rad2Deg;
                float sweep = Mathf.DeltaAngle(a0, a1) * Mathf.Deg2Rad;
                float start = a0 * Mathf.Deg2Rad;

                var arc = new List<Vector2>();
                for (int i = 0; i <= ArcSegments; i++)
                {
                    float a = start + sweep * i / ArcSegments;
                    arc.Add(m + r * new Vector2(Mathf.Cos(a), Mathf.Sin(a)));
                }

                Vector3 up = Vector3.up;
                Vector3 cw = centre + new Vector3(c.x, 0f, -c.y);
                for (int i = 0; i < arc.Count - 1; i++)
                {
                    Vector3 p = centre + new Vector3(arc[i].x, 0f, -arc[i].y);
                    Vector3 q = centre + new Vector3(arc[i + 1].x, 0f, -arc[i + 1].y);
                    AddTri(cw + Vector3.up * Height, p + Vector3.up * Height, q + Vector3.up * Height, up, v, n, t);

                    // The arc is the only exposed side; the straight edges touch walls.
                    Vector2 mid = (arc[i] + arc[i + 1]) * 0.5f;
                    Vector3 side = new Vector3(m.x - mid.x, 0f, -(m.y - mid.y)).normalized;
                    Vector3 p1 = p + Vector3.up * Height;
                    Vector3 q1 = q + Vector3.up * Height;
                    AddTri(p, q, q1, side, v, n, t);
                    AddTri(p, q1, p1, side, v, n, t);
                }
            }
        }

        /// <summary>
        /// Adds one triangle with unshared vertices and the given normal. The winding is chosen so the
        /// front face points the same way as the normal.
        /// </summary>
        private static void AddTri(Vector3 a, Vector3 b, Vector3 c, Vector3 normal, List<Vector3> v, List<Vector3> n, List<int> t)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0f)
            {
                Vector3 swap = b;
                b = c;
                c = swap;
            }

            int first = v.Count;
            v.Add(a);
            v.Add(b);
            v.Add(c);
            n.Add(normal);
            n.Add(normal);
            n.Add(normal);
            t.Add(first);
            t.Add(first + 1);
            t.Add(first + 2);
        }
    }
}
