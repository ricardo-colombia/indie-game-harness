using System.Collections.Generic;
using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// All on-screen UI: title and pause menus, the HUD, floating score popups and the READY / game-over
    /// banners. It reads the game's public probes and draws; it never changes game state. Sizes scale with
    /// the screen height.
    /// </summary>
    public class PacmanUi : MonoBehaviour
    {
        private struct Popup
        {
            public Vector3 World;
            public string Text;
            public Color Colour;
            public float Age;
        }

        private static readonly Color Gold = new Color(1f, 0.84f, 0.35f);
        private static readonly Color Leaf = new Color(0.6f, 0.95f, 0.5f);
        private static readonly Color CherryRed = new Color(1f, 0.35f, 0.3f);
        private static readonly Color Dim = new Color(1f, 1f, 1f, 0.55f);
        private static readonly Color Panel = new Color(0.05f, 0.04f, 0.02f, 0.9f);

        [Tooltip("Seconds a score popup floats before it fades out.")]
        [SerializeField] private float popupSeconds = 1.1f;
        [Tooltip("How fast the displayed score counts up to the real one, in points per second.")]
        [SerializeField] private float countRate = 2500f;

        private static PacmanUi instance;

        private PacmanGame game;
        private Font font;
        private bool customFont;
        private Texture2D hatIcon;
        private GUIStyle label;
        private readonly List<Popup> popups = new List<Popup>();
        private float shownScore;
        private float fade;
        private PacmanState lastState = PacmanState.Title;
        private PacmanTitleParade parade;
        private PacmanStoryView storyView;
        private float castTime;
        private int castShown = -1;
        private Rect skipButton = new Rect(-1000f, -1000f, 0f, 0f);
        private float stateAge;
        private readonly List<Rect> menuRows = new List<Rect>();
        private Rect pauseButton;
        private Rect zoomButton;

        // HUD strings are rebuilt only when their value changes (no per-frame garbage).
        private readonly int[] numberShown = { -1, -1 };
        private readonly string[] numberText = new string[2];
        private int placeLevel = -1;
        private string placeFarm;
        private string placeText;

        private string NumberText(int slot, int value)
        {
            if (numberShown[slot] != value || numberText[slot] == null)
            {
                numberShown[slot] = value;
                numberText[slot] = value.ToString("N0");
            }
            return numberText[slot];
        }

        public static void Show(Vector3 world, string text, Color colour)
        {
            if (instance != null)
            {
                instance.popups.Add(new Popup { World = world, Text = text, Colour = colour });
            }
        }

        public void Init(PacmanGame owner)
        {
            game = owner;
            instance = this;
            hatIcon = MakeHatIcon(64);
            // The rounded game face that ships with the game (Lilita One, SIL Open Font License); it works in browsers too.
            font = Resources.Load<Font>("Fonts/LilitaOne-Regular");
            customFont = font != null;
            if (customFont)
            {
                return;
            }
#if UNITY_WEBGL
            // Browsers have no OS fonts to load, so use the font that ships with Unity.
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            font = Font.CreateDynamicFontFromOSFont(new[] { "Arial Rounded MT Bold", "Avenir Next Condensed", "Arial Black", "Helvetica Neue", "Arial" }, 48);
#endif
        }

        /// <summary>Index of the menu row under a screen position (y down, like IMGUI), or -1.</summary>
        public int MenuRowAt(Vector2 gui)
        {
            for (int i = 0; i < menuRows.Count; i++)
            {
                if (menuRows[i].Contains(gui))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>True when a position (y down) is on the "skip" button of the story comic.</summary>
        public bool OnSkipButton(Vector2 gui)
        {
            return game != null && (game.State == PacmanState.Story || game.State == PacmanState.Cast) && skipButton.Contains(gui);
        }

        /// <summary>True when a position (y down) is on the on-screen pause button, which only shows for touch play.</summary>
        public bool OnPauseButton(Vector2 gui)
        {
            return game != null && game.TouchMode && game.State == PacmanState.Playing && pauseButton.Contains(gui);
        }

        /// <summary>True when a position (y down) is on the on-screen zoom button, which only shows for touch play.</summary>
        public bool OnZoomButton(Vector2 gui)
        {
            return game != null && game.TouchMode && game.State == PacmanState.Playing && zoomButton.Contains(gui);
        }

        /// <summary>
        /// One scale for every size on screen. It follows the screen height on wide screens and the width on
        /// tall ones, and is larger on touch devices, where fingers need bigger targets and text.
        /// </summary>
        private float UiScale()
        {
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            float referenceWidth = Mathf.Lerp(900f, 1500f, Mathf.InverseLerp(0.7f, 1.3f, aspect));
            float referenceHeight = game.TouchMode ? 760f : 1080f;
            return Mathf.Clamp(Mathf.Min(Screen.height / referenceHeight, Screen.width / referenceWidth), 0.3f, 3f);
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private void Update()
        {
            if (game == null)
            {
                return;
            }

            if (game.State != lastState)
            {
                // Entering a new screen: fade in from black, but only when a game or level begins.
                if (game.State == PacmanState.Ready)
                {
                    fade = 1f;
                }
                lastState = game.State;
                stateAge = 0f;
            }
            stateAge += Time.unscaledDeltaTime;
            fade = Mathf.MoveTowards(fade, 0f, Time.unscaledDeltaTime * 2.2f);

            if (game.State == PacmanState.Title || game.Score < shownScore)
            {
                shownScore = game.Score;
            }
            shownScore = Mathf.MoveTowards(shownScore, game.Score, countRate * Time.unscaledDeltaTime);

            for (int i = popups.Count - 1; i >= 0; i--)
            {
                Popup p = popups[i];
                p.Age += Time.unscaledDeltaTime;
                if (p.Age >= popupSeconds)
                {
                    popups.RemoveAt(i);
                }
                else
                {
                    popups[i] = p;
                }
            }
        }

        private void OnGUI()
        {
            if (game == null || Event.current.type != EventType.Repaint)
            {
                return;
            }

            float s = UiScale();
            EnsureStyle();
            if (Time.frameCount > 20 && (journeyArt == null || !journeyArt.Done))
            {
                WarmJourney(false);
            }
            menuRows.Clear();
            pauseButton = new Rect(-1000f, -1000f, 0f, 0f);
            zoomButton = new Rect(-1000f, -1000f, 0f, 0f);
            skipButton = new Rect(-1000f, -1000f, 0f, 0f);

            if (game.State != PacmanState.Story && game.State != PacmanState.Cast && storyView != null)
            {
                Destroy(storyView.gameObject);
                storyView = null;
            }

            if (game.State == PacmanState.Title)
            {
                DrawTitle(s);
            }
            else if (game.State == PacmanState.Story || game.State == PacmanState.Cast)
            {
                if (parade != null)
                {
                    Destroy(parade.gameObject);
                    parade = null;
                }
                if (game.State == PacmanState.Story)
                {
                    DrawStory(s);
                }
                else
                {
                    DrawCast(s);
                }
            }
            else
            {
                if (parade != null)
                {
                    // The title's stage is only built while the title shows, so no stray farmer exists during play.
                    Destroy(parade.gameObject);
                    parade = null;
                }
                DrawHud(s);
                if (game.TouchMode && game.State == PacmanState.Playing)
                {
                    DrawPauseButton(s);
                    DrawZoomButton(s);
                }
                DrawPopups(s);
                DrawNameTag(s);
                DrawSaying(s);
                switch (game.State)
                {
                    case PacmanState.Ready:
                        DrawBanner("¡LISTO!", Gold, s, 1.5f);
                        break;
                    case PacmanState.LevelClear:
                        if (game.JourneyProgress < 0f)
                        {
                            DrawBanner("¡COSECHA LISTA!", Leaf, s, 1.2f);
                        }
                        else
                        {
                            DrawJourney(s, game.JourneyProgress);
                        }
                        break;
                    case PacmanState.GameOver:
                        DrawGameOver(s);
                        break;
                    case PacmanState.Paused:
                        DrawMenuPanel("PAUSA", s, false);
                        break;
                }
            }

            if (fade > 0.001f)
            {
                Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, fade));
            }
        }

        // ---------------------------------------------------------------- screens

        private void DrawTitle(float s)
        {
            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.03f, 0.02f, 0f, 0.55f));

            float cx = Screen.width * 0.5f;
            float t = Time.unscaledTime;

            // Logo: shifting gold-to-cherry colour with a soft echo behind it.
            string logo = "COFFEE-MAN";
            float size = Mathf.Min(150f * s, Screen.width * 0.92f / (logo.Length * 0.62f));   // 10 letters: keep it inside narrow phone screens
            Color glow = Color.Lerp(Gold, CherryRed, 0.5f + 0.5f * Mathf.Sin(t * 1.3f));
            float bob = Mathf.Sin(t * 2f) * 6f * s;
            Rect logoRect = new Rect(0, Screen.height * 0.12f + bob, Screen.width, size * 1.2f);
            // A soft glow, then a dark engraved edge made of the word drawn in a ring around it (clean outline, no
            // smear), a drop shadow, and the gold letters on top.
            Text(logoRect, logo, size, new Color(glow.r, glow.g, glow.b, 0.10f), TextAnchor.MiddleCenter, null, 9f * s);
            Color edge = new Color(0.2f, 0.08f, 0.02f, 1f);
            Rect drop = new Rect(logoRect.x, logoRect.y + 9f * s, logoRect.width, logoRect.height);
            Text(drop, logo, size, new Color(0.1f, 0.04f, 0.01f, 0.8f), TextAnchor.MiddleCenter);
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI * 0.25f;
                Rect ring = new Rect(logoRect.x + Mathf.Cos(a) * 3.5f * s, logoRect.y + Mathf.Sin(a) * 3.5f * s, logoRect.width, logoRect.height);
                Text(ring, logo, size, edge, TextAnchor.MiddleCenter);
            }
            Text(logoRect, logo, size, Gold, TextAnchor.MiddleCenter);

            // Colombian flag stripe under the logo: yellow (half), blue and red (a quarter each).
            float flagW = 520f * s;
            float flagY = Screen.height * 0.12f + size * 1.12f;
            Fill(new Rect(cx - flagW * 0.5f, flagY, flagW, 5f * s), new Color(0.99f, 0.82f, 0.09f));
            Fill(new Rect(cx - flagW * 0.5f, flagY + 5f * s, flagW, 2.5f * s), new Color(0.0f, 0.22f, 0.65f));
            Fill(new Rect(cx - flagW * 0.5f, flagY + 7.5f * s, flagW, 2.5f * s), new Color(0.81f, 0.07f, 0.17f));

            Rect subRect = new Rect(0, Screen.height * 0.12f + size * 1.15f + 12f * s, Screen.width, 50f * s);
            Text(subRect, "LA  COSECHA  EN  LA  MONTAÑA", 34f * s, Leaf, TextAnchor.MiddleCenter);

            if (Screen.width < Screen.height)
            {
                // Tall screens put the menu over the maze, so give it a backing panel to stay readable.
                float panelWidth = Mathf.Min(580f * s, Screen.width * 0.94f);
                float panelHeight = game.ActiveMenu.Length * RowHeight(game.ActiveMenu.Length) * s + 24f * s;
                Rect backing = new Rect((Screen.width - panelWidth) * 0.5f, Screen.height * MenuTop(game.ActiveMenu.Length) - 16f * s, panelWidth, panelHeight);
                Fill(new Rect(backing.x - 3f * s, backing.y - 3f * s, backing.width + 6f * s, backing.height + 6f * s), new Color(Leaf.r, Leaf.g, Leaf.b, 0.4f), 20f * s);
                Fill(backing, Panel, 16f * s);
            }
            DrawMenuItems(s, Screen.height * MenuTop(game.ActiveMenu.Length));

            if (game.HighScore > 0)
            {
                Text(new Rect(0, Screen.height * 0.84f, Screen.width, 40f * s), "RÉCORD  " + game.HighScore.ToString("N0"), 30f * s, Gold, TextAnchor.MiddleCenter);
            }
            string hint = game.TouchMode
                ? "DESLIZA  MOVER      TOCA  ELEGIR"
                : "FLECHAS / WASD  MOVER      ENTER  ELEGIR      V  ZOOM      ESC  PAUSA";
            Text(new Rect(0, Screen.height * 0.92f, Screen.width, 36f * s), hint, 22f * s, Dim, TextAnchor.MiddleCenter);
            Text(new Rect(0, Screen.height * 0.965f, Screen.width - 16f * s, 28f * s), "v" + Application.version, 18f * s, Dim, TextAnchor.MiddleRight);

            // Decorative chase: the picker followed by the four pests (DIAN agent, guerrilla, narco, snake) runs across the bottom,
            // drawn from the game's own rigs.
            if (parade == null)
            {
                parade = PacmanTitleParade.Create();
            }
            float stripHeight = 110f * s;
            Rect strip = new Rect(0f, Screen.height * 0.78f - 22f * s, Screen.width, stripHeight);
            parade.Frame(Mathf.RoundToInt(strip.width), Mathf.RoundToInt(strip.height), 62f * s, Mathf.Repeat(t * 0.12f, 1f));
            if (parade.Texture != null)
            {
                GUI.DrawTexture(strip, parade.Texture, ScaleMode.StretchToFill, true);
            }
        }

        // ---------------------------------------------------------------- story comic

        /// <summary>Draws text word-wrapped inside its rectangle.</summary>
        private void TextWrapped(Rect rect, string text, float size, Color colour, TextAnchor anchor, Vector2? shadow = null)
        {
            label.wordWrap = true;
            Text(rect, text, size, colour, anchor, shadow);
            label.wordWrap = false;
        }

        /// <summary>The largest font size, up to <paramref name="size"/>, at which a one-line text fits a width.</summary>
        private float FitFont(string text, float size, float width)
        {
            label.wordWrap = false;
            label.fontSize = Mathf.Max(8, Mathf.RoundToInt(size));
            float w = label.CalcSize(new GUIContent(text)).x;
            return w <= width ? size : size * width / w * 0.97f;
        }

        private void DrawStory(float s)
        {
            PacmanStoryPlayer story = game.Story;
            if (story == null)
            {
                return;
            }
            if (storyView == null)
            {
                storyView = PacmanStoryView.Create();
            }

            float W = Screen.width;
            float H = Screen.height;
            float aspect = W / Mathf.Max(1f, H);
            // Text is larger on tall screens, where the UI scale follows the narrow width.
            float cs = s * Mathf.Lerp(2.1f, 1f, Mathf.InverseLerp(0.5f, 1.2f, aspect));
            StoryPanel panel = story.Current;

            Fill(new Rect(0f, 0f, W, H), new Color(0.07f, 0.045f, 0.025f, 1f));

            float margin = 20f * s;
            // Tall screens get a squarer picture (the sides are cropped) so the characters stay big.
            float pictureAspect = Mathf.Lerp(1.15f, 1.6f, Mathf.InverseLerp(0.55f, 1.2f, aspect));
            float pictureWidth = Mathf.Min(W - 2f * margin, (H * 0.56f) * pictureAspect);
            float pictureHeight = pictureWidth / pictureAspect;
            Rect picture = new Rect((W - pictureWidth) * 0.5f, 56f * s + margin * 0.3f, pictureWidth, pictureHeight);
            float border = 6f * s;
            Fill(new Rect(picture.x - border - 3f * s, picture.y - border - 3f * s, picture.width + 2f * (border + 3f * s), picture.height + 2f * (border + 3f * s)), new Color(0.97f, 0.9f, 0.7f, 1f), 6f * s);
            Fill(new Rect(picture.x - border, picture.y - border, picture.width + 2f * border, picture.height + 2f * border), new Color(0.12f, 0.07f, 0.03f, 1f), 4f * s);

            storyView.Show(panel.Scene, story.Time, story.Progress);
            if (storyView.Texture != null)
            {
                GUI.DrawTexture(picture, storyView.Texture, ScaleMode.ScaleAndCrop, false);
            }
            // Each panel "pops" in from darkness, like turning a page.
            float pop = 1f - Mathf.Clamp01(story.Time / 0.35f);
            if (pop > 0f)
            {
                Fill(picture, new Color(0.07f, 0.045f, 0.025f, pop));
            }

            // Page badge on the picture corner.
            Rect badge = new Rect(picture.x + 10f * s, picture.y + 10f * s, 92f * s, 40f * s);
            Fill(badge, new Color(0.97f, 0.9f, 0.7f, 0.95f), 20f * s);
            Text(badge, (story.Index + 1) + " / " + story.Count, 24f * s, new Color(0.2f, 0.1f, 0.04f), TextAnchor.MiddleCenter);

            // Caption box: title, story, and what the character wants.
            float captionTop = picture.yMax + border + 18f * s;
            Rect caption = new Rect(picture.x, captionTop, picture.width, H - captionTop - 78f * s);
            Fill(caption, new Color(0.97f, 0.9f, 0.7f, 0.96f), 10f * s);
            float pad = 16f * cs;
            Color ink = new Color(0.18f, 0.09f, 0.04f);
            float y = caption.y + pad * 0.6f;
            Text(new Rect(caption.x + pad, y, caption.width - 2f * pad, 52f * cs), panel.Title, FitFont(panel.Title, 40f * cs, caption.width - 2f * pad), new Color(0.62f, 0.14f, 0.1f), TextAnchor.MiddleLeft);
            y += 52f * cs;
            float motiveHeight = string.IsNullOrEmpty(panel.Motive) ? 0f : 46f * cs;
            TextWrapped(new Rect(caption.x + pad, y, caption.width - 2f * pad, caption.yMax - y - motiveHeight - pad * 0.5f), panel.Text, 29f * cs, ink, TextAnchor.UpperLeft);
            if (motiveHeight > 0f)
            {
                Text(new Rect(caption.x + pad, caption.yMax - motiveHeight - pad * 0.4f, caption.width - 2f * pad, motiveHeight), panel.Motive, FitFont(panel.Motive, 27f * cs, caption.width - 2f * pad), new Color(0.18f, 0.4f, 0.12f), TextAnchor.MiddleLeft);
            }

            // Progress dots and the control hint.
            float dot = 12f * s;
            float dotsWidth = story.Count * dot * 2f;
            float dotsY = H - 62f * s;
            for (int i = 0; i < story.Count; i++)
            {
                Rect d = new Rect(W * 0.5f - dotsWidth * 0.5f + i * dot * 2f + dot * 0.5f, dotsY, dot, dot);
                Fill(d, i == story.Index ? Gold : (i < story.Index ? new Color(Gold.r, Gold.g, Gold.b, 0.5f) : new Color(1f, 1f, 1f, 0.2f)), dot * 0.5f);
            }
            string hint = game.TouchMode ? "TOCA  PARA  SEGUIR" : "ENTER  SEGUIR      ESC  SALTAR";
            Text(new Rect(0f, H - 46f * s, W, 36f * s), hint, 22f * s, Dim, TextAnchor.MiddleCenter);

            // Skip button, always available.
            skipButton = new Rect(W - 190f * s - margin, 10f * s, 190f * s, 44f * s);
            Fill(skipButton, new Color(0f, 0f, 0f, 0.45f), 22f * s);
            Text(skipButton, "SALTAR  »", 26f * s, Gold, TextAnchor.MiddleCenter);
        }

        /// <summary>
        /// The PERSONAJES screen: one sheet per character with their picture (the same rigs as the comic), who they
        /// are, what they want and what playing against them teaches.
        /// </summary>
        private void DrawCast(float s)
        {
            if (storyView == null)
            {
                storyView = PacmanStoryView.Create();
            }
            CastMember member = game.CastCurrent;

            float W = Screen.width;
            float H = Screen.height;
            float aspect = W / Mathf.Max(1f, H);
            float cs = s * Mathf.Lerp(2.1f, 1f, Mathf.InverseLerp(0.5f, 1.2f, aspect));
            Fill(new Rect(0f, 0f, W, H), new Color(0.07f, 0.045f, 0.025f, 1f));

            float margin = 20f * s;
            float pictureAspect = Mathf.Lerp(1.15f, 1.6f, Mathf.InverseLerp(0.55f, 1.2f, aspect));
            float pictureWidth = Mathf.Min(W - 2f * margin, (H * 0.44f) * pictureAspect);
            float pictureHeight = pictureWidth / pictureAspect;
            Rect picture = new Rect((W - pictureWidth) * 0.5f, 56f * s + margin * 0.3f, pictureWidth, pictureHeight);
            float border = 6f * s;
            Fill(new Rect(picture.x - border - 3f * s, picture.y - border - 3f * s, picture.width + 2f * (border + 3f * s), picture.height + 2f * (border + 3f * s)), new Color(0.97f, 0.9f, 0.7f, 1f), 6f * s);
            Fill(new Rect(picture.x - border, picture.y - border, picture.width + 2f * border, picture.height + 2f * border), new Color(0.12f, 0.07f, 0.03f, 1f), 4f * s);

            castTime += Time.unscaledDeltaTime;
            if (game.CastIndex != castShown)
            {
                castShown = game.CastIndex;
                castTime = 0f;
            }
            storyView.Show(member.Scene, castTime, 0.5f);
            if (storyView.Texture != null)
            {
                GUI.DrawTexture(picture, storyView.Texture, ScaleMode.ScaleAndCrop, false);
            }
            float pop = 1f - Mathf.Clamp01(castTime / 0.3f);
            if (pop > 0f)
            {
                Fill(picture, new Color(0.07f, 0.045f, 0.025f, pop));
            }

            Rect badge = new Rect(picture.x + 10f * s, picture.y + 10f * s, 92f * s, 40f * s);
            Fill(badge, new Color(0.97f, 0.9f, 0.7f, 0.95f), 20f * s);
            Text(badge, (game.CastIndex + 1) + " / " + PacmanCast.Count, 24f * s, new Color(0.2f, 0.1f, 0.04f), TextAnchor.MiddleCenter);

            // The sheet: name and role, who they are, what they want, what they teach.
            float sheetTop = picture.yMax + border + 18f * s;
            Rect sheet = new Rect(picture.x, sheetTop, picture.width, H - sheetTop - 78f * s);
            Fill(sheet, new Color(0.97f, 0.9f, 0.7f, 0.96f), 10f * s);
            float pad = 16f * cs;
            float inner = sheet.width - 2f * pad;
            Color ink = new Color(0.18f, 0.09f, 0.04f);
            float y = sheet.y + pad * 0.5f;
            Text(new Rect(sheet.x + pad, y, inner, 50f * cs), member.Name, FitFont(member.Name, 40f * cs, inner), new Color(0.62f, 0.14f, 0.1f), TextAnchor.MiddleLeft);
            y += 46f * cs;
            Text(new Rect(sheet.x + pad, y, inner, 32f * cs), member.Role.ToUpperInvariant(), FitFont(member.Role, 24f * cs, inner), new Color(0.18f, 0.4f, 0.12f), TextAnchor.MiddleLeft);
            y += 36f * cs;
            float block = (sheet.yMax - y - pad * 0.4f) / 3.2f;
            TextWrapped(new Rect(sheet.x + pad, y, inner, block * 1.2f), member.Context, 24f * cs, ink, TextAnchor.UpperLeft);
            y += block * 1.2f;
            TextWrapped(new Rect(sheet.x + pad, y, inner, block), "QUIERE: " + member.Wants, 24f * cs, new Color(0.45f, 0.2f, 0.06f), TextAnchor.UpperLeft);
            y += block;
            TextWrapped(new Rect(sheet.x + pad, y, inner, block), "ENSEÑA: " + member.Lesson, 24f * cs, new Color(0.18f, 0.4f, 0.12f), TextAnchor.UpperLeft);

            float dot = 12f * s;
            float dotsWidth = PacmanCast.Count * dot * 2f;
            float dotsY = H - 62f * s;
            for (int i = 0; i < PacmanCast.Count; i++)
            {
                Rect d = new Rect(W * 0.5f - dotsWidth * 0.5f + i * dot * 2f + dot * 0.5f, dotsY, dot, dot);
                Fill(d, i == game.CastIndex ? Gold : new Color(1f, 1f, 1f, 0.2f), dot * 0.5f);
            }
            string hint = game.TouchMode ? "TOCA  PARA  SEGUIR" : "↑ ↓  CAMBIAR      ESC  VOLVER";
            Text(new Rect(0f, H - 46f * s, W, 36f * s), hint, 22f * s, Dim, TextAnchor.MiddleCenter);

            skipButton = new Rect(W - 190f * s - margin, 10f * s, 190f * s, 44f * s);
            Fill(skipButton, new Color(0f, 0f, 0f, 0.45f), 22f * s);
            Text(skipButton, "VOLVER  «", 26f * s, Gold, TextAnchor.MiddleCenter);
        }

        /// <summary>The farmer's speech bubble: a cream comic bubble over his head, fading in and out.</summary>
        private void DrawSaying(float s)
        {
            string text = game.SayingText;
            float a = game.SayingAlpha;
            Camera cam = Camera.main;
            if (text == null || a <= 0f || cam == null)
            {
                return;
            }
            Vector3 screen = cam.WorldToScreenPoint(game.PlayerWorldPosition + new Vector3(0f, 0.9f, 0f));
            if (screen.z < 0f)
            {
                return;
            }
            float size = 30f * s;
            label.wordWrap = false;
            label.fontSize = Mathf.Max(8, Mathf.RoundToInt(size));
            float W = Screen.width;
            float width = Mathf.Min(W - 24f * s, label.CalcSize(new GUIContent(text)).x + 40f * s);
            float height = 52f * s;
            float x = Mathf.Clamp(screen.x - width * 0.5f, 12f * s, W - width - 12f * s);
            float y = Mathf.Clamp(Screen.height - screen.y - height - 12f * s, 80f * s, Screen.height - height);
            Rect bubble = new Rect(x, y - (1f - a) * 8f * s, width, height);
            Fill(new Rect(bubble.x - 3f * s, bubble.y - 3f * s, bubble.width + 6f * s, bubble.height + 6f * s), new Color(0.12f, 0.07f, 0.03f, 0.9f * a), height * 0.5f);
            Fill(bubble, new Color(0.97f, 0.9f, 0.7f, 0.97f * a), height * 0.5f);
            Text(bubble, text, FitFont(text, size, width - 24f * s), new Color(0.18f, 0.09f, 0.04f, a), TextAnchor.MiddleCenter);
        }

        /// <summary>The first-appearance name tag: a small pill under the score that fades in and out, never blocking play.</summary>
        private void DrawNameTag(float s)
        {
            CastMember member = game.TagMember;
            float a = game.TagAlpha;
            if (member == null || a <= 0f)
            {
                return;
            }
            float W = Screen.width;
            float width = Mathf.Min(W - 40f * s, 560f * s);
            Rect pill = new Rect((W - width) * 0.5f, 118f * s - (1f - a) * 14f * s, width, 84f * s);
            Fill(pill, new Color(0.04f, 0.025f, 0.01f, 0.82f * a), 18f * s);
            Color name = new Color(Gold.r, Gold.g, Gold.b, a);
            Color line = new Color(1f, 1f, 1f, 0.85f * a);
            Text(new Rect(pill.x, pill.y + 6f * s, pill.width, 40f * s), member.Name + "  ·  " + member.Role, FitFont(member.Name + "  ·  " + member.Role, 28f * s, pill.width - 24f * s), name, TextAnchor.MiddleCenter);
            Text(new Rect(pill.x, pill.y + 44f * s, pill.width, 32f * s), member.Tag, 24f * s, line, TextAnchor.MiddleCenter);
        }

        private void DrawHud(float s)
        {
            float margin = 28f * s;
            float top = 16f * s;

            // Score, top left.
            Text(new Rect(margin, top, 400f * s, 28f * s), "PUNTOS", 22f * s, Dim, TextAnchor.UpperLeft);
            Text(new Rect(margin, top + 26f * s, 500f * s, 60f * s), NumberText(0, Mathf.RoundToInt(shownScore)), 52f * s, Color.white, TextAnchor.UpperLeft, new Vector2(0f, 3f * s));

            // High score, top centre.
            float high = Mathf.Max(game.HighScore, game.Score);
            // A dark pill behind it: on wide screens the number sits over the fence and bunting.
            Fill(new Rect(Screen.width * 0.5f - 130f * s, top - 6f * s, 260f * s, 92f * s), new Color(0.04f, 0.025f, 0.01f, 0.72f), 18f * s);
            Text(new Rect(0, top, Screen.width, 28f * s), "RÉCORD", 22f * s, Dim, TextAnchor.UpperCenter);
            Text(new Rect(0, top + 26f * s, Screen.width, 60f * s), NumberText(1, Mathf.RoundToInt(high)), 40f * s, Gold, TextAnchor.UpperCenter, new Vector2(0f, 3f * s));

            // Lives, top right, as little sombreros.
            Text(new Rect(Screen.width - margin - 300f * s, top, 300f * s, 28f * s), "VIDAS", 22f * s, Dim, TextAnchor.UpperRight);
            float icon = 40f * s;
            for (int i = 0; i < game.Lives; i++)
            {
                float x = Screen.width - margin - (i + 1) * (icon + 8f * s);
                GUI.DrawTexture(new Rect(x, top + 32f * s, icon, icon), hatIcon, ScaleMode.StretchToFill, true);
            }

            // Level, bottom left; a hint while frightened, bottom centre.
            // A dark pill behind it: in the close view the label sits on the reddish ground strip below the farm.
            // The farm has a proper name; the level number is a quieter second line so the progress is still readable.
            if (placeLevel != game.Level || placeText == null)
            {
                placeLevel = game.Level;
                placeFarm = PacmanFarms.NameFor(game.Level);
                placeText = "COSECHA  " + game.Level + "  ·  " + PacmanJourney.StopFor(game.Level).Region;
            }
            string farm = placeFarm;
            string place = placeText;
            float pillWidth = Mathf.Min(Screen.width - 2f * (margin - 14f * s), (Mathf.Max(farm.Length * 15.5f, place.Length * 13.5f) + 44f) * s);
            Fill(new Rect(margin - 14f * s, Screen.height - 92f * s, pillWidth, 82f * s), new Color(0.04f, 0.025f, 0.01f, 0.72f), 14f * s);
            Text(new Rect(margin, Screen.height - 86f * s, pillWidth, 38f * s), farm, 28f * s, Leaf, TextAnchor.LowerLeft, new Vector2(0f, 2f * s));
            Text(new Rect(margin, Screen.height - 54f * s, 400f * s, 34f * s), place, 24f * s, Gold, TextAnchor.LowerLeft, new Vector2(0f, 2f * s));
            if (game.FrightenedActive)
            {
                float remaining = game.FrightenedRemaining;
                float bar = Screen.width * 0.22f;
                Rect track = new Rect((Screen.width - bar) * 0.5f, Screen.height - 46f * s, bar, 10f * s);
                // A dark pill behind the hint and its bar, so they read over the maze on narrow screens.
                Fill(new Rect(track.x - 24f * s, Screen.height - 84f * s, bar + 48f * s, 48f * s), new Color(0.04f, 0.025f, 0.01f, 0.72f), 14f * s);
                Fill(track, new Color(1f, 1f, 1f, 0.12f), 5f * s);
                float full = Mathf.Max(0.01f, game.FrightenedDuration);
                Color c = remaining < 2f ? new Color(1f, 0.35f, 0.4f) : new Color(0.4f, 0.55f, 1f);
                Fill(new Rect(track.x, track.y, bar * Mathf.Clamp01(remaining / full), track.height), c, 5f * s);
                Text(new Rect(0, Screen.height - 78f * s, Screen.width, 28f * s), "PLAGAS FUMIGADAS", 22f * s, Dim, TextAnchor.MiddleCenter);
            }
        }

        /// <summary>Touch play has no Escape key, so a pause button sits in the bottom-right corner.</summary>
        private void DrawPauseButton(float s)
        {
            float size = 76f * s;
            float margin = 24f * s;
            Rect r = new Rect(Screen.width - margin - size, Screen.height - margin - size, size, size);
            pauseButton = new Rect(r.x - 16f * s, r.y - 16f * s, r.width + 32f * s, r.height + 32f * s);
            Fill(r, new Color(1f, 1f, 1f, 0.12f), 16f * s);
            float bar = 10f * s;
            Fill(new Rect(r.x + size * 0.5f - bar * 1.6f, r.y + size * 0.26f, bar, size * 0.48f), new Color(1f, 1f, 1f, 0.7f), 3f * s);
            Fill(new Rect(r.x + size * 0.5f + bar * 0.6f, r.y + size * 0.26f, bar, size * 0.48f), new Color(1f, 1f, 1f, 0.7f), 3f * s);
        }

        /// <summary>A magnifier button left of the pause button: switches between the whole board and the close view.</summary>
        private void DrawZoomButton(float s)
        {
            float size = 76f * s;
            float margin = 24f * s;
            Rect r = new Rect(Screen.width - margin - size * 2f - 18f * s, Screen.height - margin - size, size, size);
            zoomButton = new Rect(r.x - 16f * s, r.y - 16f * s, r.width + 32f * s, r.height + 32f * s);
            Fill(r, new Color(1f, 1f, 1f, game.CloseView ? 0.3f : 0.12f), 16f * s);
            Color white = new Color(1f, 1f, 1f, 0.75f);
            float lens = size * 0.36f;
            Rect ring = new Rect(r.x + size * 0.2f, r.y + size * 0.18f, lens, lens);
            Fill(ring, white, lens * 0.5f);
            Fill(new Rect(ring.x + 5f * s, ring.y + 5f * s, lens - 10f * s, lens - 10f * s), new Color(0.12f, 0.1f, 0.08f, 0.95f), (lens - 10f * s) * 0.5f);
            Fill(new Rect(r.x + size * 0.56f, r.y + size * 0.56f, size * 0.26f, 8f * s), white, 3f * s);
        }

        private void DrawPopups(float s)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }
            foreach (Popup p in popups)
            {
                float t = p.Age / popupSeconds;
                Vector3 screen = cam.WorldToScreenPoint(p.World);
                float rise = Mathf.Sqrt(t) * 60f * s;
                float pop = t < 0.15f ? Mathf.Lerp(0.6f, 1.25f, t / 0.15f) : Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((t - 0.15f) * 4f));
                float alpha = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
                Color c = p.Colour;
                c.a = alpha;
                float size = 34f * s * pop;
                Rect r = new Rect(screen.x - 120f * s, Screen.height - screen.y - size * 0.5f - rise, 240f * s, size * 1.2f);
                // An ink outline first (four offsets), so the points read over leaves and cherries alike, like a label's lettering.
                Color ink = new Color(0.07f, 0.035f, 0.015f, alpha * 0.95f);
                float o = Mathf.Max(1.5f, 2.2f * s);
                Vector2[] offsets = { new Vector2(o, 0f), new Vector2(-o, 0f), new Vector2(0f, o), new Vector2(0f, -o) };
                foreach (Vector2 off in offsets)
                {
                    Text(new Rect(r.x + off.x, r.y + off.y, r.width, r.height), p.Text, size, ink, TextAnchor.MiddleCenter);
                }
                Text(r, p.Text, size, c, TextAnchor.MiddleCenter, new Vector2(0f, 2f * s));
            }
        }

        private void DrawBanner(string message, Color colour, float s, float pulseRate)
        {
            float pulse = 1f + 0.06f * Mathf.Sin(Time.unscaledTime * pulseRate * 2f * Mathf.PI);
            float size = 76f * s * pulse;
            Rect r = new Rect(0, Screen.height * 0.5f - size * 0.1f, Screen.width, size * 1.3f);
            // A dark ribbon with thin gold rules, like the game-over screen, so the word reads over the busy maze.
            float ribbonTop = r.y + size * 0.08f;
            float ribbonHeight = size * 1.14f;
            Fill(new Rect(0, ribbonTop, Screen.width, ribbonHeight), new Color(0.04f, 0.025f, 0.01f, 0.7f));
            Fill(new Rect(0, ribbonTop, Screen.width, 3f * s), new Color(Gold.r, Gold.g, Gold.b, 0.7f));
            Fill(new Rect(0, ribbonTop + ribbonHeight - 3f * s, Screen.width, 3f * s), new Color(Gold.r, Gold.g, Gold.b, 0.7f));
            Text(r, message, size, colour, TextAnchor.MiddleCenter, new Vector2(0f, 5f * s), 0f, new Color(0f, 0f, 0f, 0.85f));
        }

        private void DrawGameOver(float s)
        {
            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, Mathf.Clamp01(stateAge * 1.2f) * 0.6f));
            float a = Mathf.Clamp01(stateAge * 1.5f);
            // A dark ribbon with thin gold rules behind the words, so they read over the busy maze.
            float bandTop = Screen.height * 0.25f;
            float bandHeight = Screen.height * 0.52f;
            Fill(new Rect(0, bandTop, Screen.width, bandHeight), new Color(0.04f, 0.025f, 0.01f, 0.72f * a));
            Fill(new Rect(0, bandTop, Screen.width, 3f * s), new Color(Gold.r, Gold.g, Gold.b, 0.7f * a));
            Fill(new Rect(0, bandTop + bandHeight - 3f * s, Screen.width, 3f * s), new Color(Gold.r, Gold.g, Gold.b, 0.7f * a));
            Color red = new Color(1f, 0.25f, 0.3f, a);
            Text(new Rect(0, Screen.height * 0.28f, Screen.width, 140f * s), "FIN DE LA COSECHA", Mathf.Min(84f * s, Screen.width * 0.09f), red, TextAnchor.MiddleCenter, new Vector2(0f, 6f * s));
            Text(new Rect(0, Screen.height * 0.46f, Screen.width, 50f * s), "PUNTOS  " + game.Score.ToString("N0"), 44f * s, new Color(1f, 1f, 1f, a), TextAnchor.MiddleCenter);
            if (game.NewHighScore)
            {
                float blink = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 8f);
                Text(new Rect(0, Screen.height * 0.55f, Screen.width, 50f * s), "¡NUEVO RÉCORD!", 40f * s, new Color(Gold.r, Gold.g, Gold.b, a * blink), TextAnchor.MiddleCenter);
            }
            else
            {
                Text(new Rect(0, Screen.height * 0.55f, Screen.width, 44f * s), "RÉCORD  " + game.HighScore.ToString("N0"), 32f * s, new Color(Gold.r, Gold.g, Gold.b, a), TextAnchor.MiddleCenter);
            }
            float blinkPrompt = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 4f);
            Text(new Rect(0, Screen.height * 0.7f, Screen.width, 40f * s), game.TouchMode ? "TOCA PARA SEGUIR" : "PRESIONA ENTER", 30f * s, new Color(1f, 1f, 1f, a * blinkPrompt), TextAnchor.MiddleCenter);
        }

        private void DrawMenuPanel(string header, float s, bool large)
        {
            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.03f, 0.02f, 0f, 0.5f));
            float w = Mathf.Min(Mathf.Max(Screen.width * 0.34f, 640f * s), Screen.width * 0.94f);
            // Tall enough for the header and the rows, no more, so there is no empty space at the bottom.
            float h = Mathf.Min(Screen.height * 0.7f, 150f * s + game.ActiveMenu.Length * 64f * s + 24f * s);
            Rect panel = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.24f, w, h);
            Fill(new Rect(panel.x - 4f * s, panel.y - 4f * s, panel.width + 8f * s, panel.height + 8f * s), new Color(Gold.r, Gold.g, Gold.b, 0.9f), 22f * s);
            Fill(new Rect(panel.x - 1f * s, panel.y - 1f * s, panel.width + 2f * s, panel.height + 2f * s), new Color(0.07f, 0.045f, 0.02f, 1f), 19f * s);
            Fill(panel, new Color(0.1f, 0.065f, 0.03f, 1f), 18f * s);
            Text(new Rect(panel.x, panel.y + 24f * s, panel.width, 80f * s), header, 64f * s, Gold, TextAnchor.MiddleCenter, new Vector2(0f, 4f * s));
            DrawMenuItems(s, panel.y + 140f * s);
        }

        /// <summary>Spanish label for a menu item id (the ids are what PacmanGame switches on).</summary>
        private static string MenuLabel(string item)
        {
            switch (item)
            {
                case "START": return "A COSECHAR";
                case "STORY": return "HISTORIA";
                case "CAST": return "PERSONAJES";
                case "RESUME": return "SEGUIR";
                case "OPTIONS": return "OPCIONES";
                case "BACK": return "ATRÁS";
                case "QUIT": return "SALIR";
                case "QUIT TO TITLE": return "VOLVER AL INICIO";
                default: return item;
            }
        }

        /// <summary>Menus with five or more rows are packed a little tighter so they still fit above the title's chase strip.</summary>
        private static float MenuTop(int count)
        {
            return count >= 8 ? 0.38f : count >= 6 ? 0.45f : (count >= 5 ? 0.49f : 0.52f);
        }

        private static float RowHeight(int count)
        {
            return count >= 8 ? 40f : count >= 6 ? 46f : (count >= 5 ? 52f : 64f);
        }

        private void DrawMenuItems(float s, float top)
        {
            string[] items = game.ActiveMenu;
            float rowHeight = RowHeight(items.Length) * s;
            for (int i = 0; i < items.Length; i++)
            {
                bool selected = i == game.MenuIndex;
                string text = items[i] == "SOUND" ? "SONIDO:  " + (game.SoundOn ? "SÍ" : "NO")
                    : items[i] == "THEME" ? "ESTILO:  " + (game.ThemeId == PacmanTheme.Vintage ? "VINTAGE" : "CLÁSICO")
                    : items[i] == "VIEW" ? "VISTA:  " + (game.CloseView ? "CERCA" : "COMPLETA")
                    : items[i] == "SHOES" ? "ZAPATOS:  " + PacmanShoes.Label(game.CurrentShoe)
                    : MenuLabel(items[i]);
                float width = Mathf.Min(520f * s, Screen.width * 0.9f);
                Rect row = new Rect((Screen.width - width) * 0.5f, top + i * rowHeight, width, rowHeight - 8f * s);
                menuRows.Add(new Rect(row.x, row.y - 4f * s, row.width, rowHeight));
                if (selected)
                {
                    float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 6f);
                    Fill(row, new Color(Gold.r, Gold.g, Gold.b, 0.16f * pulse), 14f * s);
                    Fill(new Rect(row.x, row.y, 6f * s, row.height), Gold, 3f * s);
                    GUI.DrawTexture(new Rect(row.x + 18f * s, row.y + row.height * 0.5f - 16f * s, 36f * s, 36f * s), hatIcon, ScaleMode.StretchToFill, true);
                }
                if (PacmanGame.IsVolumeRow(items[i]))
                {
                    DrawVolumeRow(row, items[i], selected, s, items.Length);
                    continue;
                }
                Text(row, text, (items.Length >= 8 ? 26f : (items.Length >= 6 ? 30f : (items.Length >= 5 ? 34f : 38f))) * s, selected ? Gold : Color.white, TextAnchor.MiddleCenter);
            }
        }

        private static string VolumeLabel(string item)
        {
            return item == "MUSIC" ? "MÚSICA" : item == "STEPS" ? "PASOS" : "EFECTOS";
        }

        /// <summary>A volume row: the name on the left, a ten-notch bar with - and + on the right.</summary>
        private void DrawVolumeRow(Rect row, string item, bool selected, float s, int rows)
        {
            Color ink = selected ? Gold : Color.white;
            float size = (rows >= 8 ? 26f : 30f) * s;
            Text(new Rect(row.x + 64f * s, row.y, row.width * 0.4f, row.height), VolumeLabel(item), size, ink, TextAnchor.MiddleLeft);
            float barLeft = row.x + row.width * 0.5f;
            float barWidth = row.width * 0.36f;
            float notchW = barWidth / PacmanGame.VolumeNotches;
            float h = row.height * 0.46f;
            float y = row.y + (row.height - h) * 0.5f;
            Text(new Rect(barLeft - 30f * s, row.y, 26f * s, row.height), "-", size, ink, TextAnchor.MiddleCenter);
            int level = game.NotchOf(item);
            for (int n = 0; n < PacmanGame.VolumeNotches; n++)
            {
                Color c = n < level ? ink : new Color(1f, 1f, 1f, 0.16f);
                Fill(new Rect(barLeft + n * notchW + 1.5f * s, y, notchW - 3f * s, h), c, 2f * s);
            }
            Text(new Rect(barLeft + barWidth + 6f * s, row.y, 26f * s, row.height), "+", size, ink, TextAnchor.MiddleCenter);
        }

        // ---------------------------------------------------------------- the trip between farms

        private PacmanJourneyArt journeyArt;
        private Texture2D journeyPaper;
        private Texture2D journeyBean;
        private Texture2D journeyChiva;

        private static string Metres(int altitude)
        {
            return (altitude / 1000) + "." + (altitude % 1000).ToString("000") + " m s. n. m.";
        }

        /// <summary>Paints the map a slice per frame while the title is up, so the first trip is ready without a hitch.</summary>
        private int journeyPieces;

        private void WarmJourney(bool finish)
        {
            if (journeyArt == null)
            {
                journeyArt = new PacmanJourneyArt();
            }
            // The small pieces are made one per call, the big map a couple of milliseconds at a time.
            while (journeyPieces < 3)
            {
                switch (journeyPieces)
                {
                    case 0: journeyPaper = PacmanJourneyArt.BuildPaper(256, 192); break;
                    case 1: journeyBean = PacmanJourneyArt.BuildBean(48); break;
                    default: journeyChiva = PacmanJourneyArt.BuildChiva(); break;
                }
                journeyPieces++;
                if (!finish)
                {
                    return;
                }
            }
            if (!journeyArt.Done)
            {
                if (finish)
                {
                    journeyArt.Finish();
                }
                else
                {
                    journeyArt.StepFor(game.State == PacmanState.Title ? 3f : 1.5f);
                }
            }
        }

        private static Color A(Color c, float alpha)
        {
            return new Color(c.r, c.g, c.b, c.a * alpha);
        }

        /// <summary>
        /// Between harvests the farmer travels to the next farm: an old atlas page of Colombia, with a dotted
        /// road and a chiva (the painted country bus) driving from this region to the next. t runs 0..1.
        /// </summary>
        private void DrawJourney(float s, float t)
        {
            WarmJourney(true);
            Color ink = PacmanJourneyArt.Ink;
            Color red = PacmanJourneyArt.InkRed;
            float alpha = Mathf.Min(Mathf.SmoothStep(0f, 1f, t / 0.1f), 1f - Mathf.SmoothStep(0f, 1f, (t - 0.93f) / 0.07f));
            float sw = Screen.width;
            float sh = Screen.height;

            // The page, with a double rule and the flag's three bands along top and bottom.
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(new Rect(0f, 0f, sw, sh), journeyPaper, ScaleMode.StretchToFill, false);
            GUI.color = Color.white;
            float band = 9f * s;
            for (int edge = 0; edge < 2; edge++)
            {
                float y0 = edge == 0 ? 0f : sh - band;
                Fill(new Rect(0f, y0, sw, band * 0.5f), new Color(1f, 0.8f, 0.14f, alpha));
                Fill(new Rect(0f, y0 + band * 0.5f, sw, band * 0.25f), new Color(0.1f, 0.25f, 0.62f, alpha));
                Fill(new Rect(0f, y0 + band * 0.75f, sw, band * 0.25f), new Color(0.82f, 0.13f, 0.13f, alpha));
            }
            float inset = 22f * s;
            Color rule = A(ink, 0.55f * alpha);
            Fill(new Rect(inset, inset, sw - inset * 2f, 2f * s), rule);
            Fill(new Rect(inset, sh - inset - 2f * s, sw - inset * 2f, 2f * s), rule);
            Fill(new Rect(inset, inset, 2f * s, sh - inset * 2f), rule);
            Fill(new Rect(sw - inset - 2f * s, inset, 2f * s, sh - inset * 2f), rule);

            PacmanJourney.Stop from = PacmanJourney.StopFor(game.Level);
            PacmanJourney.Stop to = PacmanJourney.StopFor(game.Level + 1);
            bool wide = sw > sh * 1.25f;
            float mapH = sh * (wide ? 0.95f : 0.62f);
            float mapW = mapH * PacmanJourney.Aspect;
            if (mapW > sw * 0.86f)
            {
                mapW = sw * 0.86f;
                mapH = mapW / PacmanJourney.Aspect;
            }
            Rect map = wide
                ? new Rect(sw * 0.31f - mapW * 0.5f, (sh - mapH) * 0.5f, mapW, mapH)
                : new Rect((sw - mapW) * 0.5f, sh * 0.06f, mapW, mapH);

            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(map, journeyArt.Map, ScaleMode.StretchToFill, true);
            GUI.color = Color.white;

            // A few big towns, as on any road map, so the country reads at a glance.
            DrawTown(map, s, alpha, "Bogotá", 4.71f, -74.07f, TextAnchor.MiddleRight);
            DrawTown(map, s, alpha, "Medellín", 6.25f, -75.57f, TextAnchor.MiddleLeft);
            DrawTown(map, s, alpha, "Cali", 3.45f, -76.53f, TextAnchor.MiddleLeft);
            DrawTown(map, s, alpha, "Cartagena", 10.39f, -75.5f, TextAnchor.MiddleLeft);

            Vector2 pa = ToScreen(map, PacmanJourney.Project(from));
            Vector2 pb = ToScreen(map, PacmanJourney.Project(to));
            float bend = Mathf.Min(mapW, mapH) * 0.05f;

            // The farms of the route: a bean where the farmer has already harvested this lap, a tiny ring otherwise.
            int lapStart = ((game.Level - 1) / PacmanJourney.Count) * PacmanJourney.Count + 1;
            for (int i = 0; i < PacmanJourney.Count; i++)
            {
                Vector2 p = ToScreen(map, PacmanJourney.Project(PacmanJourney.StopFor(i + 1)));
                if (lapStart + i <= game.Level)
                {
                    float b = 18f * s;
                    GUI.color = new Color(1f, 1f, 1f, alpha);
                    GUI.DrawTexture(new Rect(p.x - b * 0.5f, p.y - b * 0.5f, b, b), journeyBean, ScaleMode.StretchToFill, true);
                    GUI.color = Color.white;
                }
                else
                {
                    float r = 3.2f * s;
                    Fill(new Rect(p.x - r, p.y - r, r * 2f, r * 2f), A(ink, 0.5f * alpha), r);
                }
            }

            // The road: dashes in ink, the stretch already driven in red.
            float travel = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.12f) / 0.72f));
            float length = Vector2.Distance(pa, pb);
            int dashes = Mathf.Max(6, Mathf.RoundToInt(length / (15f * s)));
            for (int i = 0; i <= dashes; i++)
            {
                float u = i / (float)dashes;
                Vector2 p = Curve(pa, pb, u, bend);
                bool driven = u <= travel;
                float r = (driven ? 3.4f : 2.4f) * s;
                Fill(new Rect(p.x - r, p.y - r, r * 2f, r * 2f), driven ? A(red, alpha) : A(ink, 0.4f * alpha), r);
            }

            // The destination: a pulsing ring around the bean, with its name.
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4.5f);
            float ring = (13f + 7f * pulse) * s;
            Fill(new Rect(pb.x - ring, pb.y - ring, ring * 2f, ring * 2f), A(red, (0.12f + 0.16f * pulse) * alpha), ring);
            float bean = 26f * s;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(new Rect(pb.x - bean * 0.5f, pb.y - bean * 0.5f, bean, bean), journeyBean, ScaleMode.StretchToFill, true);
            GUI.color = Color.white;

            // The chiva, with its dust.
            Vector2 now = Curve(pa, pb, travel, bend);
            Vector2 ahead = Curve(pa, pb, Mathf.Min(1f, travel + 0.03f), bend);
            Vector2 behind = Curve(pa, pb, Mathf.Max(0f, travel - 0.03f), bend);
            float dir = ahead.x >= behind.x ? 1f : -1f;
            if (travel > 0.01f && travel < 0.985f)
            {
                for (int d = 0; d < 6; d++)
                {
                    float age = (Time.unscaledTime * 2.2f + d / 6f) % 1f;
                    Vector2 puff = now - new Vector2(dir * (24f + age * 46f) * s, (6f + age * 18f) * s);
                    float pr = (4f + age * 9f) * s;
                    Fill(new Rect(puff.x - pr, puff.y - pr, pr * 2f, pr * 2f), new Color(0.62f, 0.5f, 0.36f, 0.45f * (1f - age) * alpha), pr);
                }
            }
            float cw = mapW * 0.2f;
            float ch = cw * 120f / 192f;
            float bounce = Mathf.Sin(Time.unscaledTime * 20f) * 1.4f * s;
            Rect bus = new Rect(now.x - cw * 0.5f, now.y - ch * 0.86f + bounce, cw, ch);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTextureWithTexCoords(bus, journeyChiva, dir > 0f ? new Rect(0f, 0f, 1f, 1f) : new Rect(1f, 0f, -1f, 1f), true);
            GUI.color = Color.white;

            // Words: where we are going, set like a game title card: gold tab, a heavy title with a drop shadow and
            // outline, a flag-red ribbon for the region, an altitude chip and the tagline on a dark card.
            string farm = PacmanFarms.NameFor(game.Level + 1);
            float textX = wide ? sw * 0.57f : inset + 12f * s;
            float textW = wide ? sw * 0.38f : sw - (inset + 12f * s) * 2f;
            float textY = wide ? sh * 0.25f : map.yMax + 10f * s;
            bool left = wide;
            Color gold = new Color(1f, 0.8f, 0.14f, alpha);
            Color deep = new Color(0.07f, 0.04f, 0.02f, alpha);
            Color cream = A(ink, alpha);

            Chip(textX, textY, textW, "RUMBO A", 22f * s, gold, deep, left, s);
            float nameSize = FitFont(farm, (wide ? 58f : 40f) * s, textW);
            Rect nameRect = new Rect(textX, textY + 38f * s, textW, 76f * s);
            TextAnchor anchor = left ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
            Text(nameRect, farm, nameSize, A(new Color(0.07f, 0.04f, 0.02f), alpha), anchor, new Vector2(0f, 6f * s), 3.5f * s, A(new Color(0.02f, 0.01f, 0f), 0.6f * alpha));
            Text(nameRect, farm, nameSize, cream, anchor);

            float ribbonY = textY + 124f * s;
            Chip(textX, ribbonY, textW, to.Region.ToUpperInvariant(), 28f * s, new Color(0.82f, 0.13f, 0.13f, alpha), cream, left, s);
            Chip(textX, ribbonY + 54f * s, textW, Metres(to.Altitude), 20f * s, new Color(0.1f, 0.25f, 0.62f, alpha), cream, left, s);

            float cardY = ribbonY + 112f * s;
            float cardH = 92f * s;
            Fill(new Rect(textX, cardY, textW, cardH), new Color(0.04f, 0.025f, 0.015f, 0.55f * alpha), 14f * s);
            Fill(new Rect(textX, cardY + 14f * s, 5f * s, cardH - 28f * s), gold, 2.5f * s);
            TextWrapped(new Rect(textX + 22f * s, cardY + 6f * s, textW - 40f * s, cardH - 12f * s), to.Tagline, 23f * s, A(ink, 0.92f * alpha), TextAnchor.MiddleLeft);
            if (t > 0.3f)
            {
                string hint = game.TouchMode ? "TOCA PARA SEGUIR" : "ENTER PARA SEGUIR";
                float blink = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 4f);
                Chip(textX, wide ? sh * 0.83f : sh - inset - 54f * s, textW, hint, 20f * s, new Color(1f, 1f, 1f, 0.12f * alpha), A(gold, blink), left, s);
            }
        }

        /// <summary>A rounded tab with text in it, sized to the text; returns nothing, anchors left or centred in the column.</summary>
        private void Chip(float x, float y, float columnWidth, string text, float size, Color fill, Color textColour, bool left, float s)
        {
            label.fontSize = Mathf.Max(8, Mathf.RoundToInt(size));
            float w = label.CalcSize(new GUIContent(text)).x + size * 1.3f;
            float h = size * 1.55f;
            float cx = left ? x : x + (columnWidth - w) * 0.5f;
            Fill(new Rect(cx, y + 3f * s, w, h), new Color(0f, 0f, 0f, 0.35f * fill.a), h * 0.5f);
            Fill(new Rect(cx, y, w, h), fill, h * 0.5f);
            Fill(new Rect(cx + h * 0.35f, y + h * 0.1f, w - h * 0.7f, h * 0.22f), new Color(1f, 1f, 1f, 0.16f * fill.a), h * 0.11f);
            Text(new Rect(cx, y, w, h), text, size, textColour, TextAnchor.MiddleCenter);
        }

        private void DrawTown(Rect map, float s, float alpha, string name, float lat, float lon, TextAnchor side)
        {
            Vector2 p = ToScreen(map, PacmanJourney.Project(lat, lon));
            float r = 3f * s;
            Fill(new Rect(p.x - r, p.y - r, r * 2f, r * 2f), A(PacmanJourneyArt.Ink, 0.8f * alpha), r);
            float w = 160f * s;
            float x = side == TextAnchor.MiddleRight ? p.x - r - 6f * s - w : p.x + r + 6f * s;
            Color halo = A(PacmanJourneyArt.PaperLight, 0.9f * alpha);
            for (int h = 0; h < 4; h++)
            {
                float hx = (h % 2 == 0 ? 1f : -1f) * 1.2f * s, hy = (h < 2 ? 1f : -1f) * 1.2f * s;
                Text(new Rect(x + hx, p.y - 12f * s + hy, w, 24f * s), name, 17f * s, halo, side);
            }
            Text(new Rect(x, p.y - 12f * s, w, 24f * s), name, 17f * s, A(PacmanJourneyArt.Ink, 0.95f * alpha), side);
        }

        private static Vector2 ToScreen(Rect map, Vector2 uv)
        {
            return new Vector2(map.x + uv.x * map.width, map.y + uv.y * map.height);
        }

        /// <summary>A point u (0..1) along a gently bowed road between two places.</summary>
        private static Vector2 Curve(Vector2 a, Vector2 b, float u, float bend)
        {
            Vector2 d = b - a;
            Vector2 n = d.sqrMagnitude > 0.001f ? new Vector2(-d.y, d.x).normalized : Vector2.zero;
            return Vector2.Lerp(a, b, u) + n * Mathf.Sin(u * Mathf.PI) * bend;
        }

        // ---------------------------------------------------------------- drawing helpers

        private void EnsureStyle()
        {
            if (label != null)
            {
                return;
            }
            label = new GUIStyle(GUI.skin.label)
            {
                fontStyle = customFont ? FontStyle.Normal : FontStyle.Bold,
                wordWrap = false,
                clipping = TextClipping.Overflow,
            };
            if (font != null)
            {
                label.font = font;
            }
        }

        /// <summary>
        /// Sets the text colour for every GUI state. The mouse over a label switches IMGUI to the hover state,
        /// whose colour is opaque white by default, which would turn the whole title white.
        /// </summary>
        private void SetLabelColour(Color colour)
        {
            label.normal.textColor = colour;
            label.hover.textColor = colour;
            label.active.textColor = colour;
            label.focused.textColor = colour;
            label.onNormal.textColor = colour;
            label.onHover.textColor = colour;
            label.onActive.textColor = colour;
            label.onFocused.textColor = colour;
        }

        /// <summary>Draws text with a drop shadow (and an optional outline-ish blur offset).</summary>
        private void Text(Rect rect, string text, float size, Color colour, TextAnchor anchor,
            Vector2? shadow = null, float spread = 0f, Color? shadowColour = null)
        {
            label.fontSize = Mathf.Max(8, Mathf.RoundToInt(size));
            label.alignment = anchor;
            if (shadow.HasValue)
            {
                Color sc = shadowColour ?? new Color(0f, 0f, 0f, 0.7f * colour.a);
                SetLabelColour(sc);
                GUI.Label(new Rect(rect.x + shadow.Value.x, rect.y + shadow.Value.y, rect.width, rect.height), text, label);
            }
            if (spread > 0f)
            {
                // A faint halo: the text repeated a little to the sides.
                SetLabelColour(colour);
                GUI.Label(new Rect(rect.x - spread, rect.y, rect.width, rect.height), text, label);
                GUI.Label(new Rect(rect.x + spread, rect.y, rect.width, rect.height), text, label);
                GUI.Label(new Rect(rect.x, rect.y - spread, rect.width, rect.height), text, label);
                GUI.Label(new Rect(rect.x, rect.y + spread, rect.width, rect.height), text, label);
                return;
            }
            SetLabelColour(colour);
            GUI.Label(rect, text, label);
        }

        private static void Fill(Rect rect, Color colour, float radius = 0f)
        {
            Color previous = GUI.color;
            GUI.color = Color.white;
            if (radius > 0f)
            {
                GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, colour,
                    Vector4.zero, new Vector4(radius, radius, radius, radius));
            }
            else
            {
                GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, colour, 0f, 0f);
            }
            GUI.color = previous;
        }

        private static Texture2D MakeHatIcon(int n)
        {
            // A straw hat in front view: wide brim, domed crown, dark band.
            Texture2D texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, name = "HatIcon" };
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f;
                    float dy = (y + 0.5f) / n * 2f - 1f;
                    float brim = 1f - Mathf.Sqrt(dx * dx / (0.98f * 0.98f) + (dy + 0.3f) * (dy + 0.3f) / (0.22f * 0.22f));
                    float crown = dy > -0.32f ? 1f - Mathf.Sqrt(dx * dx / (0.5f * 0.5f) + (dy + 0.32f) * (dy + 0.32f) / (0.78f * 0.78f)) : -1f;
                    Color c = new Color(0.96f, 0.86f, 0.58f);
                    if (crown > 0f)
                    {
                        c = dy < -0.02f ? new Color(0.12f, 0.08f, 0.06f) : new Color(0.99f, 0.95f, 0.82f);
                    }
                    float a = Mathf.Clamp01(Mathf.Max(brim, crown) * n * 0.25f);
                    texture.SetPixel(x, y, new Color(c.r, c.g, c.b, a));
                }
            }
            texture.Apply();
            return texture;
        }
    }
}
