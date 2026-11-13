using System.Collections.Generic;
using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// Builds the maze walls as one mesh of blocks with flat tops and rounded plan corners. A corner is
    /// rounded only where it is exposed, so joined walls stay flush. Cosmetic only; collision still uses
    /// the tile grid.
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
                AddBlock(tile, centre, radius, vertices, normals, triangles);
            }

            Mesh mesh = new Mesh { name = "PacmanWalls", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// A thin glowing strip that follows every exposed wall edge on top of the walls, drawn as one mesh.
        /// Edges shared with a neighbouring wall are skipped, so the trim traces the outline of the maze.
        /// </summary>
        public static Mesh BuildTrim(IList<Vector2Int> walls, float radius, float width, float inset)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();

            foreach (Vector2Int tile in walls)
            {
                Vector3 centre = PacmanMaze.ToWorld(tile);
                centre.y = 0f;
                var outline = new List<Vector2>();
                var cornerOf = new List<int>();
                BuildOutline(tile, radius, outline, cornerOf);

                for (int i = 0; i < outline.Count; i++)
                {
                    int j = (i + 1) % outline.Count;
                    bool exposed;
                    if (cornerOf[i] == cornerOf[j])
                    {
                        // Inside a rounded corner: always exposed.
                        exposed = true;
                    }
                    else
                    {
                        // A straight side. Side k runs from corner k to corner k+1.
                        Vector2Int side = SideOf(cornerOf[i]);
                        exposed = !PacmanMaze.IsWall(tile + side);
                    }
                    if (!exposed)
                    {
                        continue;
                    }

                    Vector2 a = outline[i];
                    Vector2 b = outline[j];
                    Vector2 along = (b - a).normalized;
                    // In plan the tile centre is the origin, so inward is toward it.
                    Vector2 mid = (a + b) * 0.5f;
                    Vector2 inward = -new Vector2(mid.x, mid.y).normalized;
                    if (cornerOf[i] != cornerOf[j])
                    {
                        // On a straight side the inward direction is perpendicular to the edge.
                        inward = new Vector2(-along.y, along.x);
                        if (Vector2.Dot(inward, -mid) < 0f)
                        {
                            inward = -inward;
                        }
                    }
                    Vector2 outerA = a - inward * (inset - width * 0.5f);
                    Vector2 outerB = b - inward * (inset - width * 0.5f);
                    Vector2 innerA = a - inward * (inset + width * 0.5f);
                    Vector2 innerB = b - inward * (inset + width * 0.5f);
                    float y = Height + 0.004f;
                    AddTri(Point(centre, outerA, y), Point(centre, outerB, y), Point(centre, innerB, y), Vector3.up, vertices, normals, triangles);
                    AddTri(Point(centre, outerA, y), Point(centre, innerB, y), Point(centre, innerA, y), Vector3.up, vertices, normals, triangles);
                }
            }

            Mesh mesh = new Mesh { name = "PacmanWallTrim", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>The neighbour direction across the straight side that starts at the given corner.</summary>
        private static Vector2Int SideOf(int corner)
        {
            switch (corner)
            {
                case 0: return new Vector2Int(0, 1);
                case 1: return new Vector2Int(-1, 0);
                case 2: return new Vector2Int(0, -1);
                default: return new Vector2Int(1, 0);
            }
        }

        /// <summary>One tile as a block: flat top, vertical sides, rounded exposed plan corners.</summary>
        private static void AddBlock(Vector2Int tile, Vector3 centre, float r, List<Vector3> v, List<Vector3> n, List<int> t)
        {
            var outline = BuildOutline(tile, r);
            Vector3 up = Vector3.up;
            Vector3 topCentre = centre + up * Height;

            for (int i = 0; i < outline.Count; i++)
            {
                Vector2 a = outline[i];
                Vector2 b = outline[(i + 1) % outline.Count];
                Vector3 side = new Vector3((a.x + b.x) * 0.5f, 0f, -(a.y + b.y) * 0.5f).normalized;

                Vector3 a0 = Point(centre, a, 0f);
                Vector3 b0 = Point(centre, b, 0f);
                Vector3 a1 = Point(centre, a, Height);
                Vector3 b1 = Point(centre, b, Height);

                AddTri(a0, b0, b1, side, v, n, t);
                AddTri(a0, b1, a1, side, v, n, t);
                AddTri(topCentre, a1, b1, up, v, n, t);
            }
        }

        /// <summary>Plan outline in tile units, rounded at corners that have no wall beside them.</summary>
        private static List<Vector2> BuildOutline(Vector2Int tile, float r)
        {
            var outline = new List<Vector2>();
            BuildOutline(tile, r, outline, new List<int>());
            return outline;
        }

        /// <summary>Same outline, also recording which of the four corners each point belongs to.</summary>
        private static void BuildOutline(Vector2Int tile, float r, List<Vector2> outline, List<int> cornerOf)
        {
            for (int corner = 0; corner < Corners.Length; corner++)
            {
                int dx = Corners[corner].x;
                int dy = Corners[corner].y;
                bool wallBeside = PacmanMaze.IsWall(tile + new Vector2Int(dx, 0))
                    || PacmanMaze.IsWall(tile + new Vector2Int(0, dy));
                if (wallBeside)
                {
                    outline.Add(new Vector2(dx * 0.5f, dy * 0.5f));
                    cornerOf.Add(corner);
                    continue;
                }

                // Quarter arc inside the corner, from the x-side to the y-side.
                Vector2 arcCentre = new Vector2(dx * (0.5f - r), dy * (0.5f - r));
                float a0 = Mathf.Atan2(0f, dx) * Mathf.Rad2Deg;
                float a1 = Mathf.Atan2(dy, 0f) * Mathf.Rad2Deg;
                if (dx * dy < 0)
                {
                    // The outline walks the corners in order, so the arc must start on the side shared with the previous corner.
                    (a0, a1) = (a1, a0);
                }
                float sweep = Mathf.DeltaAngle(a0, a1) * Mathf.Deg2Rad;
                float start = a0 * Mathf.Deg2Rad;
                for (int i = 0; i <= ArcSegments; i++)
                {
                    float a = start + sweep * i / ArcSegments;
                    outline.Add(arcCentre + r * new Vector2(Mathf.Cos(a), Mathf.Sin(a)));
                    cornerOf.Add(corner);
                }
            }
        }

        /// <summary>Maps a plan point in tile units to world space at a height. Tile y grows toward -z.</summary>
        private static Vector3 Point(Vector3 centre, Vector2 plan, float y)
        {
            return centre + new Vector3(plan.x, y, -plan.y);
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
