using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Studio.Pacman
{
    /// <summary>
    /// The chase that runs across the title screen, drawn with the game's own characters: the very same farmer and
    /// pest rigs that play in the maze are built on a small stage far from the board, filmed by a camera into a
    /// transparent texture, and the menu draws that texture. It exists only while the title is showing.
    /// </summary>
    public class PacmanTitleParade : MonoBehaviour
    {
        private static readonly Vector3 StageCentre = new Vector3(3000f, 0f, 0f);

        private Camera cam;
        private RenderTexture texture;
        private PacmanPlayer farmer;
        private readonly Transform[] pests = new Transform[4];
        private float unitsWide = 20f;

        /// <summary>The strip the characters are drawn into, or null before the first frame.</summary>
        public RenderTexture Texture => texture;

        public static PacmanTitleParade Create()
        {
            GameObject root = new GameObject("TitleParade");
            root.transform.position = StageCentre;
            PacmanTitleParade parade = root.AddComponent<PacmanTitleParade>();
            parade.Build();
            return parade;
        }

        private void Build()
        {
            GameObject camObject = new GameObject("ParadeCamera");
            camObject.transform.SetParent(transform, false);
            camObject.transform.localPosition = new Vector3(0f, 0.05f, -10f);
            cam = camObject.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.allowHDR = false;
            cam.allowMSAA = false;
            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;

            GameObject farmerObject = new GameObject("ParadeFarmer");
            farmerObject.transform.SetParent(transform, false);
            farmer = farmerObject.AddComponent<PacmanPlayer>();
            farmer.Init(null);
            farmer.ShowParade(1f);

            GhostKind[] kinds = { GhostKind.Blinky, GhostKind.Pinky, GhostKind.Inky, GhostKind.Clyde };
            for (int i = 0; i < kinds.Length; i++)
            {
                GameObject pest = new GameObject("ParadePest" + kinds[i]);
                pest.transform.SetParent(transform, false);
                Transform body = PacmanGhostView.Create(pest.transform, PacmanGame.GhostColours[i], kinds[i]);
                body.GetComponent<PacmanGhostView>().SetLook(Vector2.right);
                pests[i] = pest.transform;
            }
        }

        /// <summary>
        /// Sizes the strip (in screen pixels) and the camera so that one world unit is <paramref name="pixelsPerUnit"/> pixels,
        /// then places the farmer at <paramref name="run"/> (0..1 across the strip) with the pests behind him.
        /// </summary>
        public void Frame(int width, int height, float pixelsPerUnit, float run)
        {
            width = Mathf.Max(64, width);
            height = Mathf.Max(32, height);
            if (texture == null || texture.width != width || texture.height != height)
            {
                if (texture != null)
                {
                    cam.targetTexture = null;
                    texture.Release();
                    Destroy(texture);
                }
                texture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32) { name = "TitleParade", filterMode = FilterMode.Bilinear };
                cam.targetTexture = texture;
            }
            cam.orthographicSize = height * 0.5f / pixelsPerUnit;
            unitsWide = width / pixelsPerUnit;

            float farmerX = (Mathf.Lerp(-0.1f, 1.1f, run) - 0.5f) * unitsWide;
            farmer.transform.localPosition = new Vector3(farmerX, 0f, 0f);
            for (int i = 0; i < pests.Length; i++)
            {
                pests[i].localPosition = new Vector3(farmerX - (i + 1) * 1.3f - 0.5f, -0.05f, 0f);
            }
        }

        private void OnDestroy()
        {
            if (texture != null)
            {
                texture.Release();
                Destroy(texture);
            }
        }
    }
}
