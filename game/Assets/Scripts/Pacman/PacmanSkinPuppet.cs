using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// Draws a painted character as a skinned mesh: the picture is the texture, the bones deform it (see
    /// <see cref="PacmanSkinRig"/>). The owner sets <see cref="Rig"/> poses each frame and calls <see cref="Refresh"/>.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class PacmanSkinPuppet : MonoBehaviour
    {
        private static Material template;
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int RendererColorId = Shader.PropertyToID("_RendererColor");

        public PacmanSkinRig Rig { get; private set; }
        private Mesh mesh;
        private Vector3[] vertices;
        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock block;
        private Color colour = Color.white;

        /// <summary>The theme's rigged character, or null when the theme has no picture or rig for it.</summary>
        public static PacmanSkinPuppet Create(string name, Transform parent, float height, float footY, int sortingOrder)
        {
            Texture2D texture = PacmanTheme.GetTexture(name);
            PacmanSkinRig.Data data = PacmanTheme.GetRig(name);
            if (texture == null || data == null)
            {
                return null;
            }
            GameObject go = new GameObject(name + "Puppet");
            go.transform.SetParent(parent, false);
            PacmanSkinPuppet puppet = go.AddComponent<PacmanSkinPuppet>();
            puppet.Build(texture, new PacmanSkinRig(data, height, footY), sortingOrder);
            return puppet;
        }

        private void Build(Texture2D texture, PacmanSkinRig rig, int sortingOrder)
        {
            Rig = rig;
            vertices = new Vector3[rig.VertexCount];
            mesh = new Mesh { name = "Puppet", indexFormat = UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.MarkDynamic();
            rig.Skin(vertices);
            mesh.vertices = vertices;
            mesh.uv = rig.Uv;
            mesh.triangles = rig.Triangles;
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(6f, 6f, 1f));
            GetComponent<MeshFilter>().sharedMesh = mesh;
            meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = MaterialFor(texture);
            meshRenderer.sortingOrder = sortingOrder;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            block = new MaterialPropertyBlock();
        }

        /// <summary>One material per picture, cloned from the default sprite material so it works in whatever pipeline is active.</summary>
        private static Material MaterialFor(Texture2D texture)
        {
            if (template == null)
            {
                GameObject probe = new GameObject("probe");
                template = probe.AddComponent<SpriteRenderer>().sharedMaterial;
                Destroy(probe);
            }
            Material material = new Material(template) { name = "Puppet " + texture.name };
            material.mainTexture = texture;
            return material;
        }

        /// <summary>Multiplies the picture's colours (frightened wash, flash).</summary>
        public Color Colour
        {
            get => colour;
            set
            {
                colour = value;
                block.SetColor(ColorId, value);
                block.SetColor(RendererColorId, value);
                meshRenderer.SetPropertyBlock(block);
            }
        }

        public bool Visible
        {
            get => meshRenderer.enabled;
            set => meshRenderer.enabled = value;
        }

        /// <summary>Deforms the mesh to the rig's current pose.</summary>
        public void Refresh()
        {
            Rig.Skin(vertices);
            mesh.vertices = vertices;
        }

        private void OnDestroy()
        {
            if (mesh != null)
            {
                Destroy(mesh);
            }
        }
    }
}
