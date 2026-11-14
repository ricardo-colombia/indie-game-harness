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
        private Texture2D[] pacIcons;
        private Texture2D hatIcon;
        private GUIStyle label;
        private readonly List<Popup> popups = new List<Popup>();
        private float shownScore;
        private float fade;
        private PacmanState lastState = PacmanState.Title;
        private float stateAge;
        private readonly List<Rect> menuRows = new List<Rect>();
        private Rect pauseButton;
        private Rect zoomButton;

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
            pacIcons = new[] { MakePacIcon(64, 0.7f), MakePacIcon(64, 0.35f), MakePacIcon(64, 0.05f) };
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
            menuRows.Clear();
            pauseButton = new Rect(-1000f, -1000f, 0f, 0f);
            zoomButton = new Rect(-1000f, -1000f, 0f, 0f);

            if (game.State == PacmanState.Title)
            {
                DrawTitle(s);
            }
            else
            {
                DrawHud(s);
                if (game.TouchMode && game.State == PacmanState.Playing)
                {
                    DrawPauseButton(s);
                    DrawZoomButton(s);
                }
                DrawPopups(s);
                switch (game.State)
                {
                    case PacmanState.Ready:
                        DrawBanner("¡LISTO!", Gold, s, 1.5f);
                        break;
                    case PacmanState.LevelClear:
                        DrawBanner("¡COSECHA LISTA!", Leaf, s, 1.2f);
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
            string logo = "PAC-CAFÉ";
            float size = 150f * s;
            Color glow = Color.Lerp(Gold, CherryRed, 0.5f + 0.5f * Mathf.Sin(t * 1.3f));
            float bob = Mathf.Sin(t * 2f) * 6f * s;
            Rect logoRect = new Rect(0, Screen.height * 0.12f + bob, Screen.width, size * 1.2f);
            for (int i = 4; i >= 1; i--)
            {
                Text(logoRect, logo, size, new Color(glow.r, glow.g, glow.b, 0.09f), TextAnchor.MiddleCenter, new Vector2(0f, 0f), i * 5f * s);
            }
            Text(logoRect, logo, size, Gold, TextAnchor.MiddleCenter, new Vector2(0f, 6f * s), 0f, new Color(0.3f, 0.12f, 0.03f, 0.9f));

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
                float panelHeight = game.ActiveMenu.Length * 64f * s + 24f * s;
                Rect backing = new Rect((Screen.width - panelWidth) * 0.5f, Screen.height * 0.52f - 16f * s, panelWidth, panelHeight);
                Fill(new Rect(backing.x - 3f * s, backing.y - 3f * s, backing.width + 6f * s, backing.height + 6f * s), new Color(Leaf.r, Leaf.g, Leaf.b, 0.4f), 20f * s);
                Fill(backing, Panel, 16f * s);
            }
            DrawMenuItems(s, Screen.height * 0.52f);

            if (game.HighScore > 0)
            {
                Text(new Rect(0, Screen.height * 0.84f, Screen.width, 40f * s), "RÉCORD  " + game.HighScore.ToString("N0"), 30f * s, Gold, TextAnchor.MiddleCenter);
            }
            string hint = game.TouchMode
                ? "DESLIZA  MOVER      TOCA  ELEGIR"
                : "FLECHAS / WASD  MOVER      ENTER  ELEGIR      V  ZOOM      ESC  PAUSA";
            Text(new Rect(0, Screen.height * 0.92f, Screen.width, 36f * s), hint, 22f * s, Dim, TextAnchor.MiddleCenter);

            // Decorative chase: the picker followed by the four pests (DIAN agent, guerrilla, narco, snake) runs across the bottom.
            float run = Mathf.Repeat(t * 0.12f, 1f);
            float x = Mathf.Lerp(-0.1f, 1.1f, run) * Screen.width;
            float y = Screen.height * 0.78f;
            DrawFarmer(new Rect(x, y - 6f * s, 64f * s, 72f * s), t);
            for (int i = 0; i < PacmanGame.GhostColours.Length; i++)
            {
                float gx = x - (i + 1) * 80f * s - 30f * s;
                DrawGhostIcon(new Rect(gx, y + 2f * s, 56f * s, 60f * s), PacmanGame.GhostColours[i], i, t + i);
            }
        }

        private void DrawHud(float s)
        {
            float margin = 28f * s;
            float top = 16f * s;

            // Score, top left.
            Text(new Rect(margin, top, 400f * s, 28f * s), "PUNTOS", 22f * s, Dim, TextAnchor.UpperLeft);
            Text(new Rect(margin, top + 26f * s, 500f * s, 60f * s), Mathf.RoundToInt(shownScore).ToString("N0"), 52f * s, Color.white, TextAnchor.UpperLeft, new Vector2(0f, 3f * s));

            // High score, top centre.
            float high = Mathf.Max(game.HighScore, game.Score);
            Text(new Rect(0, top, Screen.width, 28f * s), "RÉCORD", 22f * s, Dim, TextAnchor.UpperCenter);
            Text(new Rect(0, top + 26f * s, Screen.width, 60f * s), high.ToString("N0"), 40f * s, Gold, TextAnchor.UpperCenter, new Vector2(0f, 3f * s));

            // Lives, top right, as little sombreros.
            Text(new Rect(Screen.width - margin - 300f * s, top, 300f * s, 28f * s), "VIDAS", 22f * s, Dim, TextAnchor.UpperRight);
            float icon = 40f * s;
            for (int i = 0; i < game.Lives; i++)
            {
                float x = Screen.width - margin - (i + 1) * (icon + 8f * s);
                GUI.DrawTexture(new Rect(x, top + 32f * s, icon, icon), hatIcon, ScaleMode.StretchToFill, true);
            }

            // Level, bottom left; a hint while frightened, bottom centre.
            Text(new Rect(margin, Screen.height - 54f * s, 400f * s, 40f * s), "FINCA  " + game.Level, 30f * s, Leaf, TextAnchor.LowerLeft, new Vector2(0f, 2f * s));
            if (game.FrightenedActive)
            {
                float remaining = game.FrightenedRemaining;
                float bar = Screen.width * 0.22f;
                Rect track = new Rect((Screen.width - bar) * 0.5f, Screen.height - 46f * s, bar, 10f * s);
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
                Text(r, p.Text, size, c, TextAnchor.MiddleCenter, new Vector2(0f, 2f * s));
            }
        }

        private void DrawBanner(string message, Color colour, float s, float pulseRate)
        {
            float pulse = 1f + 0.06f * Mathf.Sin(Time.unscaledTime * pulseRate * 2f * Mathf.PI);
            float size = 76f * s * pulse;
            Rect r = new Rect(0, Screen.height * 0.5f - size * 0.1f, Screen.width, size * 1.3f);
            Text(r, message, size, colour, TextAnchor.MiddleCenter, new Vector2(0f, 5f * s), 0f, new Color(0f, 0f, 0f, 0.85f));
        }

        private void DrawGameOver(float s)
        {
            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, Mathf.Clamp01(stateAge * 1.2f) * 0.6f));
            float a = Mathf.Clamp01(stateAge * 1.5f);
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
            float h = Mathf.Min(Screen.height * 0.5f, 560f * s);
            Rect panel = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.24f, w, h);
            Fill(new Rect(panel.x - 4f * s, panel.y - 4f * s, panel.width + 8f * s, panel.height + 8f * s), new Color(Leaf.r, Leaf.g, Leaf.b, 0.5f), 22f * s);
            Fill(panel, Panel, 18f * s);
            Text(new Rect(panel.x, panel.y + 24f * s, panel.width, 80f * s), header, 64f * s, Gold, TextAnchor.MiddleCenter, new Vector2(0f, 4f * s));
            DrawMenuItems(s, panel.y + 140f * s);
        }

        /// <summary>Spanish label for a menu item id (the ids are what PacmanGame switches on).</summary>
        private static string MenuLabel(string item)
        {
            switch (item)
            {
                case "START": return "A COSECHAR";
                case "RESUME": return "SEGUIR";
                case "QUIT": return "SALIR";
                case "QUIT TO TITLE": return "VOLVER AL INICIO";
                default: return item;
            }
        }

        private void DrawMenuItems(float s, float top)
        {
            string[] items = game.ActiveMenu;
            float rowHeight = 64f * s;
            for (int i = 0; i < items.Length; i++)
            {
                bool selected = i == game.MenuIndex;
                string text = items[i] == "SOUND" ? "SONIDO:  " + (game.SoundOn ? "SÍ" : "NO")
                    : items[i] == "THEME" ? "ESTILO:  " + (game.ThemeId == PacmanTheme.Vintage ? "VINTAGE" : "CLÁSICO")
                    : items[i] == "VIEW" ? "VISTA:  " + (game.CloseView ? "CERCA" : "COMPLETA")
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
                Text(row, text, 38f * s, selected ? Gold : Color.white, TextAnchor.MiddleCenter);
            }
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
                fontStyle = FontStyle.Bold,
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

        /// <summary>The campesino in profile, walking right with his basket: straw hat, white shirt, jeans, swinging legs.</summary>
        private void DrawFarmer(Rect rect, float time)
        {
            float w = rect.width;
            float h = rect.height;
            float swing = Mathf.Sin(time * 12f);
            float bob = Mathf.Abs(swing) * h * 0.03f;
            Color skin = new Color(0.8f, 0.55f, 0.38f);
            Color jeans = new Color(0.2f, 0.32f, 0.6f);
            Color shirt = new Color(0.96f, 0.95f, 0.9f);
            Color straw = new Color(0.95f, 0.82f, 0.5f);
            Color boot = new Color(0.25f, 0.15f, 0.08f);
            float top = rect.y - bob;

            // Far leg and arm first, then body, near leg and arm.
            Fill(new Rect(rect.x + w * (0.44f - swing * 0.1f), top + h * 0.62f, w * 0.14f, h * 0.3f), jeans * 0.75f, 2f);
            Fill(new Rect(rect.x + w * (0.44f - swing * 0.16f), top + h * 0.9f, w * 0.2f, h * 0.09f), boot, 2f);
            // Canasto on the back, with its strap.
            Fill(new Rect(rect.x + w * 0.08f, top + h * 0.38f, w * 0.26f, h * 0.3f), new Color(0.65f, 0.4f, 0.18f), 4f);
            Fill(new Rect(rect.x + w * 0.1f, top + h * 0.4f, w * 0.22f, h * 0.06f), new Color(0.85f, 0.15f, 0.12f), 2f);
            Fill(new Rect(rect.x + w * 0.3f, top + h * 0.4f, w * 0.06f, h * 0.06f), new Color(0.85f, 0.15f, 0.12f), 3f);
            // Torso.
            Fill(new Rect(rect.x + w * 0.3f, top + h * 0.34f, w * 0.34f, h * 0.34f), shirt, w * 0.08f);
            // Near leg.
            Fill(new Rect(rect.x + w * (0.4f + swing * 0.1f), top + h * 0.62f, w * 0.16f, h * 0.3f), jeans, 2f);
            Fill(new Rect(rect.x + w * (0.4f + swing * 0.16f), top + h * 0.9f, w * 0.22f, h * 0.09f), boot, 2f);
            // Arm swinging.
            Fill(new Rect(rect.x + w * (0.42f - swing * 0.08f), top + h * 0.38f, w * 0.12f, h * 0.24f), new Color(0.88f, 0.87f, 0.82f), 3f);
            // Head, in profile, with a nose.
            Fill(new Rect(rect.x + w * 0.36f, top + h * 0.17f, w * 0.26f, h * 0.2f), skin, w * 0.1f);
            Fill(new Rect(rect.x + w * 0.6f, top + h * 0.24f, w * 0.06f, h * 0.06f), skin, 2f);
            Fill(new Rect(rect.x + w * 0.54f, top + h * 0.22f, w * 0.04f, h * 0.04f), new Color(0.1f, 0.06f, 0.04f), 2f);
            // Straw hat: wide brim, domed crown, dark band.
            Fill(new Rect(rect.x + w * 0.22f, top + h * 0.15f, w * 0.56f, h * 0.06f), straw, 3f);
            Fill(new Rect(rect.x + w * 0.34f, top + h * 0.03f, w * 0.3f, h * 0.15f), straw, w * 0.1f);
            Fill(new Rect(rect.x + w * 0.34f, top + h * 0.12f, w * 0.3f, h * 0.035f), new Color(0.12f, 0.08f, 0.06f), 1f);
        }

        /// <summary>Pac-Man icon, mouth to the right. openness 0..1 picks the frame; negative chomps with time.</summary>
        private void DrawPac(Rect rect, float time, float openness = -1f)
        {
            float open = openness >= 0f ? openness : 0.5f + 0.5f * Mathf.Sin(time * 10f);
            int frame = open > 0.66f ? 0 : open > 0.33f ? 1 : 2;
            Color previous = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(rect, pacIcons[frame], ScaleMode.StretchToFill, true);
            GUI.color = previous;
        }

        /// <summary>A little pest for the title chase: 0 DIAN agent, 1 guerrilla fighter, 2 narco, 3 coral snake.</summary>
        private void DrawGhostIcon(Rect rect, Color colour, int kind, float time)
        {
            float wiggle = Mathf.Sin(time * 14f);
            float w = rect.width;
            float h = rect.height;
            if (kind == 3)
            {
                // Snake: a column of shrinking segments swaying side to side, with a tongue.
                for (int i = 0; i < 7; i++)
                {
                    float t = i / 6f;
                    float size = Mathf.Lerp(w * 0.42f, w * 0.14f, t);
                    float cx = rect.center.x + Mathf.Sin(t * 5f + time * 9f) * w * 0.28f * Mathf.Min(1f, t * 3f);
                    Fill(new Rect(cx - size * 0.5f, rect.y + h * (0.2f + t * 0.72f) - size * 0.5f, size, size), i % 3 == 1 ? new Color(1f, 0.85f, 0.15f) : colour, size * 0.5f);
                }
                Fill(new Rect(rect.center.x - 2f, rect.y - h * 0.04f, 3f, h * 0.14f), new Color(0.95f, 0.1f, 0.25f), 1f);
                Fill(new Rect(rect.center.x - w * 0.22f, rect.y + h * 0.08f, w * 0.44f, h * 0.2f), new Color(0.12f, 0.1f, 0.1f), w * 0.2f);
                foreach (float ex in new[] { -0.12f, 0.06f })
                {
                    Fill(new Rect(rect.center.x + w * ex, rect.y + h * 0.14f, w * 0.08f, w * 0.08f), Color.white, w * 0.04f);
                }
                return;
            }

            // Person: legs, torso, head, then the prop that says who it is.
            Fill(new Rect(rect.x + w * 0.22f, rect.y + h * 0.78f, w * 0.18f, h * 0.2f + wiggle * 3f), colour * 0.7f, 2f);
            Fill(new Rect(rect.x + w * 0.58f, rect.y + h * 0.78f, w * 0.18f, h * 0.2f - wiggle * 3f), colour * 0.7f, 2f);
            Fill(new Rect(rect.x + w * 0.12f, rect.y + h * 0.4f, w * 0.76f, h * 0.44f), colour, w * 0.2f);
            Fill(new Rect(rect.x + w * 0.26f, rect.y + h * 0.05f, w * 0.48f, h * 0.44f), colour, w * 0.24f);
            switch (kind)
            {
                case 0:
                    Fill(new Rect(rect.center.x - w * 0.04f, rect.y + h * 0.46f, w * 0.08f, h * 0.3f), new Color(0.85f, 0.1f, 0.12f), 1f);
                    Fill(new Rect(rect.xMax - w * 0.2f, rect.y + h * 0.7f, w * 0.3f, h * 0.22f), new Color(0.5f, 0.28f, 0.12f), 2f);
                    break;
                case 1:
                    Fill(new Rect(rect.x + w * 0.22f, rect.y - h * 0.02f, w * 0.58f, h * 0.18f), new Color(0.5f, 0.1f, 0.08f), w * 0.1f);
                    Fill(new Rect(rect.x + w * 0.3f, rect.y + h * 0.02f, w * 0.1f, w * 0.1f), new Color(1f, 0.85f, 0.15f), w * 0.05f);
                    break;
                default:
                    foreach (float gx in new[] { 0.3f, 0.46f, 0.62f })
                    {
                        Fill(new Rect(rect.x + w * gx, rect.y + h * (gx == 0.46f ? 0.5f : 0.46f), w * 0.1f, w * 0.1f), new Color(1f, 0.8f, 0.1f), w * 0.05f);
                    }
                    break;
            }
            float eye = w * 0.14f;
            foreach (float ex in new[] { 0.33f, 0.53f })
            {
                if (kind == 2)
                {
                    Fill(new Rect(rect.x + w * ex - 1f, rect.y + h * 0.22f, eye * 1.2f, eye * 0.8f), new Color(0.05f, 0.05f, 0.08f), 2f);
                    continue;
                }
                Fill(new Rect(rect.x + w * ex, rect.y + h * 0.2f, eye, eye * 1.2f), Color.white, eye * 0.5f);
                Fill(new Rect(rect.x + w * ex + eye * 0.35f, rect.y + h * 0.25f, eye * 0.5f, eye * 0.6f), new Color(0.12f, 0.06f, 0.03f), eye * 0.25f);
            }
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

        private static Texture2D MakePacIcon(int n, float mouthSlope)
        {
            Texture2D texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, name = "PacIcon" };
            Color clear = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f;
                    float dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float edge = Mathf.Clamp01((0.96f - d) * n * 0.5f);
                    // A sombrero seen from above: straw brim with a bite out of it, dark band, pale crown.
                    bool mouth = dx > 0f && Mathf.Abs(dy) < dx * mouthSlope && d > 0.42f;
                    Color hat = d < 0.3f ? new Color(0.99f, 0.95f, 0.82f) : d < 0.42f ? new Color(0.12f, 0.08f, 0.06f) : new Color(0.96f, 0.86f, 0.58f);
                    Color c = mouth ? clear : new Color(hat.r, hat.g, hat.b, edge);
                    texture.SetPixel(x, y, c);
                }
            }
            texture.Apply();
            return texture;
        }
    }
}
