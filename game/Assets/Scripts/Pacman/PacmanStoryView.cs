using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Studio.Pacman
{
    /// <summary>
    /// The pictures of the prologue comic. Like the title parade, the real farmer and pest rigs are built on a stage
    /// far from the board and filmed into a texture, so the story's characters are exactly the game's. Each scene is
    /// a few painted shapes (sky, hills, ground, coffee bushes) and the cast in a pose. It exists only while the
    /// story shows.
    /// </summary>
    public class PacmanStoryView : MonoBehaviour
    {
        public const int PictureWidth = 960;
        public const int PictureHeight = 600;

        private static readonly Vector3 StageCentre = new Vector3(3000f, 60f, 0f);

        private struct Look
        {
            public Color Sky, HillFar, HillNear, Ground;
        }

        private static Look LookOf(StoryScene scene)
        {
            switch (scene)
            {
                case StoryScene.Dawn: return Make(0.95f, 0.64f, 0.42f, 0.5f, 0.36f, 0.36f, 0.3f, 0.46f, 0.26f, 0.25f, 0.38f, 0.2f);
                case StoryScene.Farmer: return Make(0.88f, 0.72f, 0.46f, 0.5f, 0.55f, 0.35f, 0.3f, 0.5f, 0.24f, 0.36f, 0.25f, 0.14f);
                case StoryScene.Snake: return Make(0.1f, 0.2f, 0.12f, 0.1f, 0.28f, 0.14f, 0.08f, 0.2f, 0.1f, 0.15f, 0.1f, 0.06f);
                case StoryScene.Taxes: return Make(0.68f, 0.7f, 0.74f, 0.52f, 0.54f, 0.58f, 0.42f, 0.44f, 0.48f, 0.4f, 0.4f, 0.43f);
                case StoryScene.Guerrilla: return Make(0.62f, 0.7f, 0.46f, 0.4f, 0.5f, 0.28f, 0.3f, 0.4f, 0.2f, 0.35f, 0.28f, 0.15f);
                case StoryScene.Boss: return Make(0.9f, 0.8f, 0.52f, 0.62f, 0.5f, 0.3f, 0.5f, 0.4f, 0.24f, 0.5f, 0.38f, 0.22f);
                default: return Make(0.5f, 0.22f, 0.16f, 0.28f, 0.14f, 0.12f, 0.2f, 0.1f, 0.08f, 0.22f, 0.12f, 0.08f);
            }
        }

        private static Look Make(float sr, float sg, float sb, float fr, float fg, float fb, float nr, float ng, float nb, float gr, float gg, float gb)
        {
            return new Look
            {
                Sky = new Color(sr, sg, sb), HillFar = new Color(fr, fg, fb),
                HillNear = new Color(nr, ng, nb), Ground = new Color(gr, gg, gb),
            };
        }

        private Camera cam;
        private RenderTexture texture;
        private PacmanPlayer farmer;
        private readonly Transform[] pests = new Transform[4];   // indexed by GhostKind
        private readonly PacmanGhostView[] pestViews = new PacmanGhostView[4];
        private SpriteRenderer sun, hillFarA, hillFarB, hillNear, ground;
        private Transform bushA, bushB, bushFrontL, bushFrontR;
        private SpriteRenderer[] bits;       // falling coins or notes
        private StoryScene shown = (StoryScene)(-1);

        public RenderTexture Texture => texture;

        public static PacmanStoryView Create()
        {
            GameObject root = new GameObject("StoryView");
            root.transform.position = StageCentre;
            PacmanStoryView view = root.AddComponent<PacmanStoryView>();
            view.Build();
            return view;
        }

        private void Build()
        {
            texture = new RenderTexture(PictureWidth, PictureHeight, 16, RenderTextureFormat.ARGB32) { name = "StoryPicture", filterMode = FilterMode.Bilinear };
            GameObject camObject = new GameObject("StoryCamera");
            camObject.transform.SetParent(transform, false);
            camObject.transform.localPosition = new Vector3(0f, 0f, -10f);
            cam = camObject.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 2.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            cam.targetTexture = texture;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = false;

            sun = Shape("Sun", PacmanSprites.Disc, new Vector2(1.6f, 0.4f), new Vector2(1.6f, 1.6f), new Color(1f, 0.9f, 0.55f), -80);
            hillFarA = Shape("HillFarA", PacmanSprites.Disc, new Vector2(-2f, -1.1f), new Vector2(9f, 3.4f), Color.white, -70);
            hillFarB = Shape("HillFarB", PacmanSprites.Disc, new Vector2(3f, -1.2f), new Vector2(8f, 3.2f), Color.white, -70);
            hillNear = Shape("HillNear", PacmanSprites.Disc, new Vector2(0f, -1.6f), new Vector2(18f, 2.6f), Color.white, -60);
            ground = Shape("Ground", PacmanSprites.Square, new Vector2(0f, -2.6f), new Vector2(40f, 3.4f), Color.white, -50);
            bushA = Bush("BushA", new Vector2(-2.7f, -0.9f), 1.5f, -40);
            bushB = Bush("BushB", new Vector2(2.9f, -0.8f), 1.8f, -40);
            bushFrontL = Bush("BushFrontL", new Vector2(-2.6f, -1.6f), 2.3f, 100);
            bushFrontR = Bush("BushFrontR", new Vector2(2.2f, -1.9f), 2.6f, 100);

            bits = new SpriteRenderer[12];
            for (int i = 0; i < bits.Length; i++)
            {
                bits[i] = Shape("Bit" + i, PacmanSprites.Disc, Vector2.zero, Vector2.one * 0.2f, Color.white, 50);
            }

            GameObject farmerObject = new GameObject("StoryFarmer");
            farmerObject.transform.SetParent(transform, false);
            farmer = farmerObject.AddComponent<PacmanPlayer>();
            farmer.Init(null);
            farmer.ShowParade(1f, false);

            GhostKind[] kinds = { GhostKind.Blinky, GhostKind.Pinky, GhostKind.Inky, GhostKind.Clyde };
            for (int i = 0; i < kinds.Length; i++)
            {
                GameObject pest = new GameObject("StoryPest" + kinds[i]);
                pest.transform.SetParent(transform, false);
                Transform body = PacmanGhostView.Create(pest.transform, PacmanGame.GhostColours[(int)kinds[i]], kinds[i]);
                pestViews[(int)kinds[i]] = body.GetComponent<PacmanGhostView>();
                pests[(int)kinds[i]] = pest.transform;
            }
        }

        private SpriteRenderer Shape(string name, Sprite sprite, Vector2 position, Vector2 scale, Color colour, int order)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = position;
            go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = colour;
            sr.sortingOrder = order;
            return sr;
        }

        private int bushCount;

        /// <summary>A coffee bush: the same painted cafeto sprites the board's walls use.</summary>
        private Transform Bush(string name, Vector2 position, float size, int order)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(transform, false);
            root.transform.localPosition = position;
            root.transform.localScale = Vector3.one * size * 1.15f;
            Child(root.transform, PacmanBushes.Variant(7 + bushCount * 5), Vector2.zero, Vector2.one, Color.white, order);
            bushCount++;
            return root.transform;
        }

        private static void Child(Transform parent, Sprite sprite, Vector2 position, Vector2 scale, Color colour, int order)
        {
            GameObject go = new GameObject("Part");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = colour;
            sr.sortingOrder = order;
        }

        private static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t);
        }

        private void Place(Transform pest, GhostKind kind, float x, float y, float scale, float lookSign, float t)
        {
            pest.gameObject.SetActive(true);
            // A little sway keeps the walking animation going while the character holds its place.
            float sway = Mathf.Sin(t * 5f) * 0.05f;
            pest.localPosition = new Vector3(x + sway, y, 0f);
            pest.localScale = Vector3.one * scale;
            pestViews[(int)kind].SetLook(new Vector2(lookSign, 0f));
        }

        /// <summary>Poses the cast for a scene. <paramref name="t"/> is seconds into the panel, <paramref name="p"/> is 0..1 through it.</summary>
        public void Show(StoryScene scene, float t, float p)
        {
            if (scene != shown)
            {
                shown = scene;
            }
            Look look = LookOf(scene);
            cam.backgroundColor = look.Sky;
            hillFarA.color = look.HillFar;
            hillFarB.color = look.HillFar;
            hillNear.color = look.HillNear;
            ground.color = look.Ground;
            cam.orthographicSize = Mathf.Lerp(2.55f, 2.25f, p);
            cam.transform.localPosition = new Vector3(scene == StoryScene.Dawn ? Mathf.Lerp(-0.9f, 0.9f, p) : 0f, 0f, -10f);

            sun.gameObject.SetActive(scene == StoryScene.Dawn || scene == StoryScene.Finale);
            if (scene == StoryScene.Dawn)
            {
                sun.transform.localPosition = new Vector3(1.5f, Mathf.Lerp(-1.1f, 0.9f, Ease(p)), 0f);
                sun.transform.localScale = new Vector3(1.7f, 1.7f, 1f);
                sun.color = new Color(1f, 0.9f, 0.55f);
            }
            else if (scene == StoryScene.Finale)
            {
                sun.transform.localPosition = new Vector3(0f, 0.2f, 0f);
                sun.transform.localScale = Vector3.one * Mathf.Lerp(4.6f, 5.4f, p);
                sun.color = new Color(0.95f, 0.62f, 0.2f);
            }

            bool bushes = scene == StoryScene.Dawn || scene == StoryScene.Farmer || scene == StoryScene.Snake || scene == StoryScene.Finale;
            bushA.gameObject.SetActive(scene != StoryScene.Farmer && bushes);
            bushB.gameObject.SetActive(scene == StoryScene.Farmer || scene == StoryScene.Dawn);
            bushB.localPosition = scene == StoryScene.Farmer ? new Vector3(1.7f, -0.45f, 0f) : new Vector3(2.9f, -0.8f, 0f);
            bushB.localScale = Vector3.one * (scene == StoryScene.Farmer ? 3.1f : 1.8f);
            bushFrontL.gameObject.SetActive(scene == StoryScene.Snake);
            bushFrontR.gameObject.SetActive(scene == StoryScene.Snake);

            farmer.gameObject.SetActive(false);
            for (int i = 0; i < pests.Length; i++)
            {
                pests[i].gameObject.SetActive(false);
            }
            for (int i = 0; i < bits.Length; i++)
            {
                bits[i].gameObject.SetActive(false);
            }

            switch (scene)
            {
                case StoryScene.Dawn:
                    farmer.gameObject.SetActive(true);
                    farmer.ShowParade(1f, true);
                    farmer.transform.localScale = Vector3.one * 0.85f;
                    farmer.transform.localPosition = new Vector3(Mathf.Lerp(-3.4f, 3.4f, p), -1.15f, 0f);
                    break;

                case StoryScene.Farmer:
                    farmer.gameObject.SetActive(true);
                    farmer.ShowParade(1f, false);
                    farmer.transform.localScale = Vector3.one * 2.5f;
                    farmer.transform.localPosition = new Vector3(-1.4f, -0.05f + Mathf.Sin(t * 2f) * 0.03f, 0f);
                    break;

                case StoryScene.Snake:
                    Place(pests[(int)GhostKind.Clyde], GhostKind.Clyde, Mathf.Lerp(3.2f, 0.2f, Ease(p * 1.6f)), -0.75f, 2.7f, -1f, t);
                    break;

                case StoryScene.Taxes:
                    Place(pests[(int)GhostKind.Blinky], GhostKind.Blinky, 0.2f, 0f, 2.7f, -1f, t);
                    Rain(t, new Color(1f, 0.82f, 0.2f), 0.22f, 0.22f, true);
                    break;

                case StoryScene.Guerrilla:
                    Place(pests[(int)GhostKind.Pinky], GhostKind.Pinky, 0.2f, 0f, 2.7f, -1f, t);
                    break;

                case StoryScene.Boss:
                    Place(pests[(int)GhostKind.Inky], GhostKind.Inky, 0.6f, 0f, 2.7f, -1f, t);
                    Rain(t, new Color(0.5f, 0.78f, 0.42f), 0.36f, 0.2f, false);
                    break;

                default:
                    farmer.gameObject.SetActive(true);
                    farmer.ShowParade(1f, false);
                    farmer.transform.localScale = Vector3.one * 2.6f;
                    farmer.transform.localPosition = new Vector3(0f, -0.1f + Mathf.Sin(t * 2f) * 0.03f, 0f);
                    float come = Ease(p * 1.3f);
                    Place(pests[(int)GhostKind.Blinky], GhostKind.Blinky, -Mathf.Lerp(5.6f, 2.9f, come), -0.6f, 1.7f, 1f, t);
                    Place(pests[(int)GhostKind.Clyde], GhostKind.Clyde, -Mathf.Lerp(7.4f, 4.2f, come), -0.85f, 1.7f, 1f, t);
                    Place(pests[(int)GhostKind.Pinky], GhostKind.Pinky, Mathf.Lerp(5.8f, 2.9f, come), -0.6f, 1.7f, -1f, t);
                    Place(pests[(int)GhostKind.Inky], GhostKind.Inky, Mathf.Lerp(7.6f, 4.3f, come), -0.6f, 1.7f, -1f, t);
                    break;
            }
        }

        /// <summary>Coins or notes drifting down the picture.</summary>
        private void Rain(float t, Color colour, float width, float height, bool round)
        {
            for (int i = 0; i < bits.Length; i++)
            {
                float phase = Mathf.Repeat(t * 0.5f + i * 0.083f, 1f);
                float x = Mathf.Lerp(-3.2f, 3.2f, Mathf.Repeat(i * 0.37f + 0.11f, 1f));
                SpriteRenderer sr = bits[i];
                sr.gameObject.SetActive(true);
                sr.sprite = round ? PacmanSprites.Disc : PacmanSprites.Square;
                sr.color = colour;
                sr.transform.localPosition = new Vector3(x, Mathf.Lerp(2.6f, -1.2f, phase), 0f);
                sr.transform.localScale = new Vector3(width, height, 1f);
                sr.transform.localRotation = Quaternion.Euler(0f, 0f, round ? 0f : Mathf.Sin(t * 3f + i) * 25f);
            }
        }

        private void OnDestroy()
        {
            if (texture != null)
            {
                cam.targetTexture = null;
                texture.Release();
                Destroy(texture);
            }
        }
    }
}
