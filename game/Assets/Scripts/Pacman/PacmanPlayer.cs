using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// Player-controlled Pac-Man. The body is a flat pie mesh built at runtime so the mouth can open
    /// and close smoothly and always points along the movement direction.
    /// </summary>
    public class PacmanPlayer : PacmanActor
    {
        private const int ArcSegments = 36;
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
            meshRenderer.sharedMaterial = material;

            SetMouth(0.5f);
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
                phase += Time.deltaTime * chompRate * 2f * Mathf.PI;
                float open = 0.5f + 0.5f * Mathf.Sin(phase);
                SetMouth(Mathf.Lerp(MinMouth, MaxMouth, open * open));
            }
            else
            {
                phase = 0f;
                SetMouth(Mathf.Lerp(MinMouth, 0.3f, 0.5f));
            }

            if (phase > Mathf.PI * 0.5f && Dir != Vector2Int.zero)
            {
                // Mouth wide open after gobbling: stay open until the phase drifts back.
                phase = Mathf.Min(phase, Mathf.PI * 0.5f + 0.001f);
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

        /// <summary>Fan of triangles around the origin; the open mouth points along +X.</summary>
        private static void BuildPie(Mesh target, float halfMouth)
        {
            Vector3[] vertices = new Vector3[ArcSegments + 2];
            Vector3[] normals = new Vector3[ArcSegments + 2];
            int[] triangles = new int[ArcSegments * 3 * 2];
            vertices[0] = Vector3.zero;
            normals[0] = Vector3.up;

            float start = halfMouth;
            float end = 2f * Mathf.PI - halfMouth;
            for (int i = 0; i <= ArcSegments; i++)
            {
                float a = Mathf.Lerp(start, end, i / (float)ArcSegments);
                vertices[i + 1] = new Vector3(Mathf.Cos(a) * Radius, 0f, Mathf.Sin(a) * Radius);
                normals[i + 1] = Vector3.up;
            }

            int t = 0;
            for (int i = 0; i < ArcSegments; i++)
            {
                // Front face (up) and back face (down) so the disc reads from both sides.
                triangles[t++] = 0;
                triangles[t++] = i + 1;
                triangles[t++] = i + 2;
                triangles[t++] = 0;
                triangles[t++] = i + 2;
                triangles[t++] = i + 1;
            }

            target.Clear();
            target.vertices = vertices;
            target.normals = normals;
            target.triangles = triangles;
            target.RecalculateBounds();
        }
    }
}
