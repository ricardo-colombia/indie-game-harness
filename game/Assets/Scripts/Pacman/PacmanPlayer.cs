using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// Player-controlled Pac-Man. The body is a flat pie mesh built at runtime so the mouth can open
    /// and close smoothly and always points along the movement direction.
    /// </summary>
    public class PacmanPlayer : PacmanActor
    {
        private const int LonSegments = 40;
        private const int LatSegments = 16;
        private const float Radius = 0.42f;
        private const float MinMouth = 0.04f;
        private const float MaxMouth = 0.72f;

        [Tooltip("Mouth open/close cycles per second while moving.")]
        [SerializeField] private float chompRate = 3.2f;

        private PacmanGame game;
        private Vector2Int desired;
        private Transform body;
        private Mesh mesh;
        private float phase;
        private float lastMouth = -1f;
        private Transform eye;
        private Vector2Int face = new Vector2Int(-1, 0);
        private bool dying;
        private float deathTimer;
        private float deathDuration = 1f;
        private float deathFrom;

        public Vector2Int Desired => desired;

        /// <summary>Half-angle of the open mouth in radians. Probe for the chomp animation.</summary>
        public float MouthAngle { get; private set; }

        /// <summary>Set by the game when a pellet is eaten, to snap the mouth wide open for a moment.</summary>
        public void Gobble()
        {
            phase = Mathf.PI * 0.5f;
        }

        protected override float ModelYawOffset => 0f;

        public void Init(PacmanGame owner, Material material)
        {
            game = owner;

            GameObject bodyObject = new GameObject("Body");
            bodyObject.transform.SetParent(transform, false);
            body = bodyObject.transform;

            mesh = new Mesh { name = "PacmanPie" };
            mesh.MarkDynamic();
            MeshFilter filter = bodyObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer meshRenderer = bodyObject.AddComponent<MeshRenderer>();
            // Submesh 0 is the yellow skin, submesh 1 the dark inside of the mouth.
            Material inside = PacmanMaterials.Lit(new Color(0.45f, 0.08f, 0.02f), 0.1f);
            meshRenderer.sharedMaterials = new[] { material, inside };

            // Seen in profile, like the classic sprite: a single eye. PlaceEye keeps it on the upper side of
            // the screen when moving left or right (and on the right side when moving up or down).
            eye = AddEye(new Vector3(0.1f, 0.34f, 0.2f));

            SetMouth(0.5f);
        }

        private Transform AddEye(Vector3 localPosition)
        {
            GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eye.name = "Eye";
            eye.transform.SetParent(body, false);
            eye.transform.localPosition = localPosition;
            eye.transform.localScale = Vector3.one * 0.17f;
            Destroy(eye.GetComponent<Collider>());
            eye.GetComponent<Renderer>().sharedMaterial = PacmanMaterials.Lit(Color.white, 0.8f);

            GameObject pupil = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pupil.name = "Pupil";
            pupil.transform.SetParent(eye.transform, false);
            pupil.transform.localPosition = new Vector3(0.28f, 0.1f, 0f);
            pupil.transform.localScale = Vector3.one * 0.55f;
            Destroy(pupil.GetComponent<Collider>());
            pupil.GetComponent<Renderer>().sharedMaterial = PacmanMaterials.Lit(new Color(0.03f, 0.03f, 0.05f), 0.9f);
            return eye.transform;
        }

        private void PlaceEye()
        {
            if (eye == null)
            {
                return;
            }
            if (Dir != Vector2Int.zero)
            {
                face = Dir;
            }
            Vector3 side = face.x != 0 ? Vector3.forward : Vector3.right;
            Vector3 lateral = body.InverseTransformDirection(side);
            lateral.y = 0f;
            eye.localPosition = new Vector3(0.1f, 0.34f, 0f) + lateral.normalized * 0.2f;
        }

        /// <summary>Turns Pac-Man to face left (the classic start) so he never starts looking up or down.</summary>
        public void ResetFacing()
        {
            face = new Vector2Int(-1, 0);
            if (body != null)
            {
                body.localRotation = Quaternion.Euler(0f, 180f + ModelYawOffset, 0f);
                PlaceEye();
            }
        }

        public void Request(Vector2Int direction)
        {
            if (direction != Vector2Int.zero)
            {
                desired = direction;
            }
        }

        public void Tick(float tiles)
        {
            Advance(tiles);
            FaceDirection(body);
            PlaceEye();
        }

        /// <summary>Starts the death animation: the mouth opens until the body collapses to a line.</summary>
        public void StartDeath(float duration)
        {
            dying = true;
            deathTimer = 0f;
            deathDuration = Mathf.Max(0.01f, duration);
            deathFrom = MouthAngle;
        }

        /// <summary>Leaves the death animation and restores the normal chomp.</summary>
        public void CancelDeath()
        {
            dying = false;
            deathTimer = 0f;
        }

        private void Update()
        {
            if (body == null)
            {
                return;
            }

            if (dying)
            {
                // Ease the mouth open to a full collapse, so the body sinks to a line over the death time.
                deathTimer += Time.deltaTime;
                float t = Mathf.Clamp01(deathTimer / deathDuration);
                float eased = t * t * (3f - 2f * t);
                SetMouth(Mathf.Lerp(deathFrom, Mathf.PI - 0.01f, eased));
                return;
            }

            if (Dir != Vector2Int.zero)
            {
                // Keep cycling: the mouth opens and closes once per chompRate cycle while moving.
                phase = Mathf.Repeat(phase + Time.deltaTime * chompRate * 2f * Mathf.PI, 2f * Mathf.PI);
                float open = 0.5f + 0.5f * Mathf.Sin(phase);
                SetMouth(Mathf.Lerp(MinMouth, MaxMouth, open * open));
            }
            else
            {
                phase = 0f;
                SetMouth(Mathf.Lerp(MinMouth, 0.3f, 0.5f));
            }
        }

        protected override bool Blocked(Vector2Int tile)
        {
            return PacmanMaze.IsWall(tile) || PacmanMaze.IsGhostHouse(tile);
        }

        protected override Vector2Int ChooseDirection(Vector2Int tile, Vector2Int current)
        {
            if (desired != Vector2Int.zero && !Blocked(tile + desired))
            {
                return desired;
            }
            if (current != Vector2Int.zero && !Blocked(tile + current))
            {
                return current;
            }
            return Vector2Int.zero;
        }

        protected override void OnTileReached(Vector2Int tile)
        {
            if (game != null)
            {
                game.OnPlayerTile(tile);
            }
        }

        private void SetMouth(float halfAngle)
        {
            if (mesh == null || Mathf.Approximately(halfAngle, lastMouth))
            {
                return;
            }
            lastMouth = halfAngle;
            MouthAngle = halfAngle;
            BuildPie(mesh, halfAngle);
        }

        /// <summary>
        /// A ball with a wedge cut out. The open mouth points along +X; submesh 0 is the skin, submesh 1 the
        /// two flat faces inside the mouth. Rebuilt whenever the mouth angle changes.
        /// </summary>
        private static void BuildPie(Mesh target, float halfMouth)
        {
            int rows = LatSegments + 1;
            int cols = LonSegments + 1;
            var vertices = new System.Collections.Generic.List<Vector3>(rows * cols + rows * 4);
            var normals = new System.Collections.Generic.List<Vector3>(rows * cols + rows * 4);
            var skin = new System.Collections.Generic.List<int>(LatSegments * LonSegments * 6);
            var cut = new System.Collections.Generic.List<int>(LatSegments * 12);

            float lonStart = halfMouth;
            float lonEnd = 2f * Mathf.PI - halfMouth;
            for (int r = 0; r < rows; r++)
            {
                float lat = Mathf.Lerp(-Mathf.PI * 0.5f, Mathf.PI * 0.5f, r / (float)LatSegments);
                for (int c = 0; c < cols; c++)
                {
                    float lon = Mathf.Lerp(lonStart, lonEnd, c / (float)LonSegments);
                    Vector3 n = new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));
                    vertices.Add(n * Radius);
                    normals.Add(n);
                }
            }
            for (int r = 0; r < LatSegments; r++)
            {
                for (int c = 0; c < LonSegments; c++)
                {
                    int a = r * cols + c;
                    int b = a + 1;
                    int d = a + cols;
                    int e = d + 1;
                    // Wound so the outside faces out.
                    skin.Add(a); skin.Add(b); skin.Add(d);
                    skin.Add(b); skin.Add(e); skin.Add(d);
                }
            }

            // The two mouth faces: strips from the vertical axis out to the skin edge at each cut angle.
            AddMouthFace(vertices, normals, cut, lonStart, true);
            AddMouthFace(vertices, normals, cut, lonEnd, false);

            target.Clear();
            target.SetVertices(vertices);
            target.SetNormals(normals);
            target.subMeshCount = 2;
            target.SetTriangles(skin, 0);
            target.SetTriangles(cut, 1);
            target.RecalculateBounds();
        }

        private static void AddMouthFace(System.Collections.Generic.List<Vector3> v, System.Collections.Generic.List<Vector3> n,
            System.Collections.Generic.List<int> t, float lon, bool upperEdge)
        {
            // The face looks into the gap: toward smaller angles on the upper edge, larger on the lower one.
            Vector3 radial = new Vector3(Mathf.Cos(lon), 0f, Mathf.Sin(lon));
            Vector3 tangent = new Vector3(-Mathf.Sin(lon), 0f, Mathf.Cos(lon));
            Vector3 normal = upperEdge ? -tangent : tangent;
            int first = v.Count;
            for (int r = 0; r <= LatSegments; r++)
            {
                float lat = Mathf.Lerp(-Mathf.PI * 0.5f, Mathf.PI * 0.5f, r / (float)LatSegments);
                float y = Mathf.Sin(lat) * Radius;
                float reach = Mathf.Cos(lat) * Radius;
                v.Add(new Vector3(0f, y, 0f));
                n.Add(normal);
                v.Add(radial * reach + new Vector3(0f, y, 0f));
                n.Add(normal);
            }
            for (int r = 0; r < LatSegments; r++)
            {
                int a = first + r * 2;
                int b = a + 1;
                int c = a + 2;
                int d = a + 3;
                if (upperEdge)
                {
                    t.Add(a); t.Add(c); t.Add(b);
                    t.Add(b); t.Add(c); t.Add(d);
                }
                else
                {
                    t.Add(a); t.Add(b); t.Add(c);
                    t.Add(b); t.Add(d); t.Add(c);
                }
            }
        }
    }
}
