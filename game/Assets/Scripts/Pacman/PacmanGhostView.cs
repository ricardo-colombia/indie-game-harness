using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// The ghost's look: hem ripple, frightened blue with a wavy mouth and a white flash before it ends,
    /// and eyes-only while eaten. It reads the ghost and game state but never changes them.
    /// </summary>
    public class PacmanGhostView : MonoBehaviour
    {
        private static readonly Color FrightenedBlue = new Color(0.1f, 0.18f, 0.9f);
        private static readonly Color FlashWhite = new Color(0.95f, 0.95f, 1f);

        [Tooltip("Hem ripple frames per second.")]
        [SerializeField] private float rippleRate = 6f;
        [Tooltip("Seconds of frightened time left when the ghost starts to flash.")]
        [SerializeField] private float flashSeconds = 2f;
        [Tooltip("Flashes per second near the end of fright.")]
        [SerializeField] private float flashRate = 4f;

        private Color baseColour;
        private MeshFilter filter;
        private MeshRenderer body;
        private Material bodyMaterial;
        private Transform[] eyes;
        private Renderer[] pupils;
        private Renderer mouth;
        private PacmanGhost ghost;
        private PacmanGame game;
        private int shownFrame = -1;
        private int shownLook = -1;

        public void Setup(Color colour, MeshRenderer bodyRenderer, Material material, Transform[] eyeTransforms,
            Renderer[] pupilRenderers, Renderer mouthRenderer)
        {
            baseColour = colour;
            body = bodyRenderer;
            filter = bodyRenderer.GetComponent<MeshFilter>();
            bodyMaterial = material;
            eyes = eyeTransforms;
            pupils = pupilRenderers;
            mouth = mouthRenderer;
        }

        public void Bind(PacmanGhost owner, PacmanGame owningGame)
        {
            ghost = owner;
            game = owningGame;
        }

        private void Update()
        {
            if (filter == null)
            {
                return;
            }

            int frame = Mathf.FloorToInt(Time.time * rippleRate) & 1;
            if (frame != shownFrame)
            {
                shownFrame = frame;
                filter.sharedMesh = PacmanGhostMesh.Frame(frame);
            }

            // Look: 0 normal, 1 frightened blue, 2 frightened flash, 3 eaten (eyes only).
            int look = 0;
            if (ghost != null)
            {
                if (ghost.Mode == GhostMode.Eaten)
                {
                    look = 3;
                }
                else if (ghost.IsScared)
                {
                    bool flashing = game != null && game.FrightenedRemaining < flashSeconds
                        && Mathf.FloorToInt(Time.time * flashRate * 2f) % 2 == 0;
                    look = flashing ? 2 : 1;
                }
            }

            PlaceEye();

            if (look != shownLook)
            {
                shownLook = look;
                Apply(look);
            }
        }

        /// <summary>Keeps the single profile eye on the upper side of the screen (right side when moving vertically).</summary>
        private void PlaceEye()
        {
            if (eyes == null || eyes.Length == 0 || ghost == null)
            {
                return;
            }
            // The model's front is local +Z. Moving sideways the eye goes to the top of the screen, otherwise to the right.
            Vector3 front = transform.forward;
            Vector3 side = Mathf.Abs(front.z) < 0.5f ? Vector3.forward : Vector3.right;
            Vector3 lateral = transform.InverseTransformDirection(side);
            lateral.y = 0f;
            eyes[0].localPosition = new Vector3(0f, 0.27f, 0.13f) + lateral.normalized * 0.17f;
        }

        private void Apply(int look)
        {
            bool frightened = look == 1 || look == 2;
            body.enabled = look != 3;
            mouth.enabled = frightened;

            Color colour = look == 1 ? FrightenedBlue : look == 2 ? FlashWhite : baseColour;
            PacmanMaterials.SetColour(bodyMaterial, colour);
            PacmanMaterials.SetEmission(bodyMaterial, colour * (look == 2 ? 0.7f : 0.25f));

            // Frightened eyes shrink to pale dots; eaten eyes stay full size and white.
            foreach (Transform eye in eyes)
            {
                eye.localScale = frightened ? new Vector3(0.13f, 0.13f, 0.13f) : new Vector3(0.27f, 0.31f, 0.27f);
            }
            Color pupil = frightened ? Color.clear : new Color(0.1f, 0.2f, 0.85f);
            foreach (Renderer r in pupils)
            {
                r.enabled = !frightened;
                PacmanMaterials.SetColour(r.sharedMaterial, pupil == Color.clear ? Color.white : pupil);
            }
            Color mouthColour = look == 2 ? new Color(0.9f, 0.1f, 0.15f) : new Color(1f, 0.85f, 0.75f);
            PacmanMaterials.SetColour(mouth.sharedMaterial, mouthColour);
            PacmanMaterials.SetEmission(mouth.sharedMaterial, mouthColour * 0.8f);
        }
    }
}
