using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// Paints the travel map between farms entirely from code: an old hand-painted atlas of Colombia (hill-shaded
    /// cordilleras, rivers, a coast with ripples), a sheet of aged paper, a compass rose, a coffee bean and a chiva,
    /// the painted country bus. No image files: everything is drawn into textures at startup.
    /// The country is painted in slices (Step) so the title screen never stalls while it is made.
    /// </summary>
    public class PacmanJourneyArt
    {
        // Ink and paper colours shared with the screen that shows them.
        public static readonly Color Ink = new Color(0.97f, 0.9f, 0.74f);
        public static readonly Color InkRed = new Color(1f, 0.78f, 0.2f);
        public static readonly Color PaperLight = new Color(0.1f, 0.065f, 0.045f);

        private const int MapW = 560;
        private const int MapH = 770;

        // Mountain ranges as (latitude, longitude) polylines, with how high each one stands.
        private static readonly float[][] Ranges =
        {
            new[] { 1.0f, -77.6f, 2.5f, -76.9f, 4.0f, -76.3f, 5.5f, -76.2f, 7.0f, -76.0f, 8.2f, -76.3f },  // Occidental
            new[] { 1.8f, -76.5f, 3.0f, -76.2f, 4.5f, -75.5f, 5.8f, -75.4f, 7.5f, -75.0f, 8.5f, -74.8f },  // Central
            new[] { 2.0f, -75.3f, 3.5f, -74.8f, 5.0f, -73.6f, 6.5f, -73.0f, 7.5f, -72.5f, 8.5f, -72.7f },  // Oriental
        };
        private static readonly float[] RangeHeight = { 0.8f, 1.0f, 0.85f };

        private static readonly float[][] Rivers =
        {
            new[] { 1.9f, -76.5f, 2.5f, -75.8f, 3.5f, -75.2f, 4.5f, -74.8f, 5.5f, -74.6f, 6.7f, -74.4f, 8.0f, -74.1f, 9.0f, -74.6f, 10.2f, -74.9f, 10.95f, -74.8f }, // Magdalena
            new[] { 2.2f, -76.7f, 3.5f, -76.4f, 4.8f, -75.8f, 6.0f, -75.6f, 7.2f, -75.3f, 8.3f, -74.9f, 8.9f, -74.6f },                                       // Cauca
            new[] { 4.0f, -73.0f, 4.6f, -71.5f, 5.8f, -70.3f, 6.2f, -67.5f },                                                                                 // Meta
            new[] { 2.4f, -73.8f, 3.5f, -71.0f, 4.0f, -67.8f },                                                                                               // Guaviare
            new[] { 1.0f, -75.8f, 0.3f, -73.0f, -1.0f, -70.8f },                                                                                              // Caquetá
            new[] { 5.5f, -76.6f, 6.8f, -76.8f, 8.0f, -76.8f },                                                                                               // Atrato
        };

        private Vector2[] coast;
        private float[] elevation;
        private float[] signed;
        private Color32[] pixels;
        private int row;
        private int phase; // 0 = distances and heights, 1 = shading, 2 = rivers and finish

        public bool Done { get; private set; }
        public Texture2D Map { get; private set; }

        public PacmanJourneyArt()
        {
            coast = PacmanJourney.OutlineInPixels(MapW, MapH);
            elevation = new float[MapW * MapH];
            signed = new float[MapW * MapH];
            pixels = new Color32[MapW * MapH];
        }

        /// <summary>Paints a few more rows of the country. Call once per frame until Done.</summary>
        public void Step(int rows)
        {
            for (int n = 0; n < rows && !Done; n++)
            {
                if (phase == 0)
                {
                    HeightRow(row);
                }
                else if (phase == 1)
                {
                    ShadeRow(row);
                }
                if (++row >= MapH)
                {
                    row = 0;
                    phase++;
                    if (phase == 2)
                    {
                        PaintMountains();
                        PaintRivers();
                        Map = ToTexture(pixels, MapW, MapH);
                        pixels = null;
                        elevation = null;
                        signed = null;
                        Done = true;
                    }
                }
            }
        }

        /// <summary>Finishes whatever is left at once (a trip that starts before the map was ready).</summary>
        public void Finish()
        {
            Step(int.MaxValue / 2);
        }

        // ------------------------------------------------------------------ the country

        private float PixelsPerDegree => MapH / PacmanJourney.LatSpan;

        private void HeightRow(int y)
        {
            for (int x = 0; x < MapW; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float d = DistanceToCoast(p);
                signed[y * MapW + x] = PacmanJourney.InsidePixels(coast, p) ? d : -d;
            }
        }

        private float Gauss(Vector2 p, float lat, float lon, float sigmaDeg)
        {
            Vector2 c = PacmanJourney.ProjectPixels(lat, lon, MapW, MapH);
            float g = Vector2.Distance(p, c) / (sigmaDeg * PixelsPerDegree);
            return Mathf.Exp(-g * g);
        }

        private float DistanceToCoast(Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0, j = coast.Length - 1; i < coast.Length; j = i++)
            {
                best = Mathf.Min(best, SegmentDistance(p, coast[j], coast[i]));
            }
            return best;
        }

        private float DistanceToPolyline(float[] latLon, Vector2 p)
        {
            float best = float.MaxValue;
            Vector2 previous = PacmanJourney.ProjectPixels(latLon[0], latLon[1], MapW, MapH);
            for (int i = 2; i < latLon.Length; i += 2)
            {
                Vector2 next = PacmanJourney.ProjectPixels(latLon[i], latLon[i + 1], MapW, MapH);
                best = Mathf.Min(best, SegmentDistance(p, previous, next));
                previous = next;
            }
            return best;
        }

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        private static Color Ground(float e)
        {
            // Llanos green, warm foothills, brown peaks, pale snow caps on the Sierra Nevada.
            Color low = new Color(0.62f, 0.68f, 0.4f);
            Color foot = new Color(0.78f, 0.72f, 0.45f);
            Color hill = new Color(0.66f, 0.54f, 0.35f);
            Color peak = new Color(0.52f, 0.4f, 0.29f);
            Color snow = new Color(0.84f, 0.8f, 0.7f);
            if (e < 0.3f) return Color.Lerp(low, foot, e / 0.3f);
            if (e < 0.65f) return Color.Lerp(foot, hill, (e - 0.3f) / 0.35f);
            if (e < 1.12f) return Color.Lerp(hill, peak, (e - 0.65f) / 0.47f);
            return Color.Lerp(peak, snow, Mathf.Clamp01((e - 1.12f) / 0.3f));
        }

        // A flat, poster-like country: leaf green that glows lighter toward the middle, a crisp cream coast and two
        // thin contour lines in the sea. No hill-shading: clean shapes read as finished at any size.
        private void ShadeRow(int y)
        {
            Color rim = new Color(0.1f, 0.26f, 0.18f);
            Color mid = new Color(0.2f, 0.45f, 0.3f);
            Color coastLine = new Color(0.97f, 0.9f, 0.74f);
            for (int x = 0; x < MapW; x++)
            {
                int i = y * MapW + x;
                float sd = signed[i];
                Color c = default;
                float a = 0f;
                if (sd > 0f)
                {
                    float glow = Mathf.SmoothStep(0f, 1f, sd / 70f);
                    float grain = Mathf.PerlinNoise(x * 0.05f + 5f, y * 0.05f + 1f) * 0.05f;
                    c = Color.Lerp(rim, mid, glow) * (0.97f + grain);
                    a = 1f;
                }
                else
                {
                    float dd = -sd;
                    float[] rings = { 9f, 19f, 31f };
                    for (int r = 0; r < rings.Length; r++)
                    {
                        float k = Mathf.Clamp01(1.2f - Mathf.Abs(dd - rings[r]) / 0.9f) * (0.34f - 0.09f * r);
                        if (k > a)
                        {
                            a = k;
                            c = coastLine;
                        }
                    }
                }
                float line = Mathf.Clamp01(1.5f - Mathf.Abs(sd) / 0.9f);
                if (line > 0f)
                {
                    c = Color.Lerp(a > 0f ? c : coastLine, coastLine, line);
                    a = Mathf.Max(a, line);
                }
                c.a = a;
                pixels[i] = c;
            }
        }

        // Little triangle peaks marching along the three cordilleras, each a dark side and a lit side.
        private void PaintMountains()
        {
            Color dark = new Color(0.07f, 0.19f, 0.13f);
            Color lit = new Color(0.31f, 0.58f, 0.38f);
            float unit = MapH / 578f;
            for (int r = 0; r < Ranges.Length; r++)
            {
                float[] line = Ranges[r];
                float carried = 0f;
                for (int i = 0; i + 3 < line.Length; i += 2)
                {
                    Vector2 a = PacmanJourney.ProjectPixels(line[i], line[i + 1], MapW, MapH);
                    Vector2 b = PacmanJourney.ProjectPixels(line[i + 2], line[i + 3], MapW, MapH);
                    float len = Vector2.Distance(a, b);
                    float gap = 13f * unit;
                    for (float d = carried; d < len; d += gap)
                    {
                        Vector2 c = Vector2.Lerp(a, b, d / Mathf.Max(0.01f, len));
                        float h = (8f + 6f * Mathf.PerlinNoise(c.x * 0.09f, c.y * 0.09f + r * 7f)) * unit * RangeHeight[r];
                        float half = h * 0.62f;
                        Vector2 apex = new Vector2(c.x, c.y - h * 0.9f), baseL = new Vector2(c.x - half, c.y + h * 0.1f), baseR = new Vector2(c.x + half, c.y + h * 0.1f), foot = new Vector2(c.x + half * 0.05f, c.y + h * 0.1f);
                        Triangle(pixels, MapW, MapH, apex, baseL, foot, dark);
                        Triangle(pixels, MapW, MapH, apex, foot, baseR, lit);
                    }
                    carried = (carried + Mathf.Ceil((len - carried) / (13f * unit)) * 13f * unit) - len;
                }
            }
        }

        private void PaintRivers()
        {
            Color river = new Color(0.5f, 0.82f, 0.88f);
            float scale = MapH / 578f;
            for (int r = 0; r < Rivers.Length; r++)
            {
                float[] line = Rivers[r];
                Vector2 previous = PacmanJourney.ProjectPixels(line[0], line[1], MapW, MapH);
                for (int i = 2; i < line.Length; i += 2)
                {
                    Vector2 next = PacmanJourney.ProjectPixels(line[i], line[i + 1], MapW, MapH);
                    int steps = Mathf.CeilToInt(Vector2.Distance(previous, next) * 1.5f);
                    for (int s = 0; s <= steps; s++)
                    {
                        Vector2 p = Vector2.Lerp(previous, next, s / (float)Mathf.Max(1, steps));
                        // Rivers wander a little instead of running dead straight.
                        p += new Vector2(Mathf.Sin(p.y * 0.21f + r), Mathf.Cos(p.x * 0.19f + r)) * 1.2f;
                        Disc(pixels, MapW, MapH, p.x, p.y, 1.0f * scale, river, 0.9f);
                    }
                    previous = next;
                }
            }
        }

        // ------------------------------------------------------------------ drawing primitives

        private static Texture2D ToTexture(Color32[] top, int w, int h)
        {
            Color32[] flipped = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                System.Array.Copy(top, y * w, flipped, (h - 1 - y) * w, w);
            }
            Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            t.SetPixels32(flipped);
            t.Apply(false, true);
            return t;
        }

        private static void Blend(Color32[] px, int w, int h, int x, int y, Color c, float a)
        {
            if (x < 0 || y < 0 || x >= w || y >= h || a <= 0f)
            {
                return;
            }
            Color32 dst = px[y * w + x];
            float da = dst.a / 255f;
            float oa = a + da * (1f - a);
            if (oa <= 0f)
            {
                return;
            }
            float r = (c.r * a + dst.r / 255f * da * (1f - a)) / oa;
            float g = (c.g * a + dst.g / 255f * da * (1f - a)) / oa;
            float b = (c.b * a + dst.b / 255f * da * (1f - a)) / oa;
            px[y * w + x] = new Color32((byte)(Mathf.Clamp01(r) * 255f), (byte)(Mathf.Clamp01(g) * 255f), (byte)(Mathf.Clamp01(b) * 255f), (byte)(Mathf.Clamp01(oa) * 255f));
        }

        private static void Disc(Color32[] px, int w, int h, float cx, float cy, float radius, Color c, float alpha)
        {
            int x0 = Mathf.FloorToInt(cx - radius - 1f), x1 = Mathf.CeilToInt(cx + radius + 1f);
            int y0 = Mathf.FloorToInt(cy - radius - 1f), y1 = Mathf.CeilToInt(cy + radius + 1f);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    Blend(px, w, h, x, y, c, Mathf.Clamp01(radius - d + 0.5f) * alpha);
                }
            }
        }

        private static void RRect(Color32[] px, int w, int h, float x0, float y0, float x1, float y1, float radius, Color c, float alpha = 1f)
        {
            for (int y = Mathf.FloorToInt(y0) - 1; y <= Mathf.CeilToInt(y1); y++)
            {
                for (int x = Mathf.FloorToInt(x0) - 1; x <= Mathf.CeilToInt(x1); x++)
                {
                    float px_ = x + 0.5f, py = y + 0.5f;
                    float cx = Mathf.Clamp(px_, x0 + radius, x1 - radius);
                    float cy = Mathf.Clamp(py, y0 + radius, y1 - radius);
                    float d = Vector2.Distance(new Vector2(px_, py), new Vector2(cx, cy));
                    Blend(px, w, h, x, y, c, Mathf.Clamp01(radius - d + 0.5f) * alpha);
                }
            }
        }

        private static void Ellipse(Color32[] px, int w, int h, float cx, float cy, float rx, float ry, Color c, float alpha = 1f)
        {
            for (int y = Mathf.FloorToInt(cy - ry) - 1; y <= Mathf.CeilToInt(cy + ry) + 1; y++)
            {
                for (int x = Mathf.FloorToInt(cx - rx) - 1; x <= Mathf.CeilToInt(cx + rx) + 1; x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    float d = (Mathf.Sqrt(dx * dx + dy * dy) - 1f) * Mathf.Min(rx, ry);
                    Blend(px, w, h, x, y, c, Mathf.Clamp01(0.5f - d) * alpha);
                }
            }
        }

        private static void Triangle(Color32[] px, int w, int h, Vector2 a, Vector2 b, Vector2 c, Color col)
        {
            int x0 = Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))), x1 = Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x)));
            int y0 = Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))), y1 = Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y)));
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float d1 = Edge(p, a, b), d2 = Edge(p, b, c), d3 = Edge(p, c, a);
                    bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
                    if (!(neg && pos))
                    {
                        Blend(px, w, h, x, y, col, 1f);
                    }
                }
            }
        }

        private static float Edge(Vector2 p, Vector2 a, Vector2 b)
        {
            return (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
        }

        private static void Line(Color32[] px, int w, int h, Vector2 a, Vector2 b, float radius, Color c, float alpha = 1f)
        {
            int steps = Mathf.CeilToInt(Vector2.Distance(a, b) * 1.5f);
            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, i / (float)Mathf.Max(1, steps));
                Disc(px, w, h, p.x, p.y, radius, c, alpha);
            }
        }

        // ------------------------------------------------------------------ the small pieces

        /// <summary>A sheet of aged paper: warm fibre noise, soft stains and darker, foxed edges.</summary>
        public static Texture2D BuildPaper(int w, int h)
        {
            Color32[] px = new Color32[w * h];
            Color centre = new Color(0.2f, 0.125f, 0.085f);
            Color edge = new Color(0.07f, 0.043f, 0.03f);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = x / (float)w - 0.5f, dy = y / (float)h - 0.5f;
                    float v = Mathf.SmoothStep(0f, 1f, Mathf.Sqrt(dx * dx + dy * dy) * 1.45f);
                    Color c = Color.Lerp(centre, edge, v) * (0.98f + 0.03f * Mathf.PerlinNoise(x * 0.3f, y * 0.3f));
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            }
            Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            t.SetPixels32(px);
            t.Apply(false, true);
            return t;
        }

        /// <summary>A compass rose: a ring, four long points and four short ones, drawn in ink.</summary>
        public static Texture2D BuildCompass(int n)
        {
            Color32[] px = new Color32[n * n];
            float c = n * 0.5f;
            float r = n * 0.46f;
            Disc(px, n, n, c, c, r, Ink, 0.9f);
            Disc(px, n, n, c, c, r - n * 0.018f, new Color(0.94f, 0.87f, 0.68f), 1f);
            Disc(px, n, n, c, c, r * 0.72f, Ink, 0.55f);
            Disc(px, n, n, c, c, r * 0.72f - n * 0.012f, new Color(0.94f, 0.87f, 0.68f), 1f);
            for (int k = 0; k < 4; k++)
            {
                float ang = k * Mathf.PI * 0.5f;
                Vector2 dir = new Vector2(Mathf.Sin(ang), -Mathf.Cos(ang));
                Vector2 side = new Vector2(-dir.y, dir.x);
                Vector2 tip = new Vector2(c, c) + dir * r * 1.02f;
                Triangle(px, n, n, tip, new Vector2(c, c) + side * r * 0.14f, new Vector2(c, c), k == 0 ? InkRed : Ink);
                Triangle(px, n, n, tip, new Vector2(c, c) - side * r * 0.14f, new Vector2(c, c), k == 0 ? new Color(0.78f, 0.3f, 0.2f) : new Color(0.45f, 0.3f, 0.18f));
                float angd = ang + Mathf.PI * 0.25f;
                Vector2 d2 = new Vector2(Mathf.Sin(angd), -Mathf.Cos(angd));
                Vector2 s2 = new Vector2(-d2.y, d2.x);
                Triangle(px, n, n, new Vector2(c, c) + d2 * r * 0.64f, new Vector2(c, c) + s2 * r * 0.07f, new Vector2(c, c), Ink);
                Triangle(px, n, n, new Vector2(c, c) + d2 * r * 0.64f, new Vector2(c, c) - s2 * r * 0.07f, new Vector2(c, c), new Color(0.45f, 0.3f, 0.18f));
            }
            Disc(px, n, n, c, c, r * 0.07f, Ink, 1f);
            return ToTexture(px, n, n);
        }

        /// <summary>A coffee bean with its crease, to mark farms on the map.</summary>
        public static Texture2D BuildBean(int n)
        {
            Color32[] px = new Color32[n * n];
            float c = n * 0.5f;
            Ellipse(px, n, n, c, c, n * 0.34f, n * 0.44f, new Color(0.2f, 0.1f, 0.05f), 1f);
            Ellipse(px, n, n, c - n * 0.02f, c - n * 0.02f, n * 0.3f, n * 0.4f, new Color(0.45f, 0.24f, 0.11f), 1f);
            Ellipse(px, n, n, c - n * 0.09f, c - n * 0.14f, n * 0.1f, n * 0.16f, new Color(0.7f, 0.45f, 0.25f), 0.5f);
            for (int i = 0; i <= 24; i++)
            {
                float v = i / 24f;
                Disc(px, n, n, c + Mathf.Sin(v * Mathf.PI * 2f) * n * 0.07f, n * 0.1f + v * n * 0.8f, n * 0.035f, new Color(0.13f, 0.06f, 0.03f), 1f);
            }
            return ToTexture(px, n, n);
        }

        /// <summary>
        /// A chiva, the painted country bus: yellow, blue and red stripes like the flag, a roof rack stacked with
        /// coffee sacks, a round-eyed hood. Faces right.
        /// </summary>
        public static Texture2D BuildChiva()
        {
            const int w = 192, h = 120;
            Color32[] px = new Color32[w * h];
            Color yellow = new Color(1f, 0.78f, 0.12f);
            Color blue = new Color(0.1f, 0.25f, 0.62f);
            Color red = new Color(0.82f, 0.13f, 0.13f);
            Color wood = new Color(0.34f, 0.2f, 0.1f);
            float g = 108f; // ground line

            Ellipse(px, w, h, 96f, g + 3f, 84f, 6f, Color.black, 0.28f);

            // Roof rack with its load: coffee sacks, a rolled tarp, a bundle of plantains.
            Line(px, w, h, new Vector2(14f, 26f), new Vector2(150f, 26f), 1.4f, wood);
            for (int post = 0; post < 4; post++)
            {
                Line(px, w, h, new Vector2(16f + post * 44f, 26f), new Vector2(16f + post * 44f, 34f), 1.4f, wood);
            }
            for (int s = 0; s < 3; s++)
            {
                float sx = 18f + s * 26f;
                RRect(px, w, h, sx, 8f, sx + 24f, 27f, 6f, new Color(0.8f, 0.64f, 0.38f));
                RRect(px, w, h, sx + 2f, 9f, sx + 12f, 15f, 3f, new Color(0.9f, 0.76f, 0.5f), 0.7f);
                Line(px, w, h, new Vector2(sx + 4f, 11f), new Vector2(sx + 20f, 11f), 1.1f, new Color(0.5f, 0.32f, 0.16f));
                Disc(px, w, h, sx + 12f, 20f, 3.2f, red, 0.9f);
            }
            RRect(px, w, h, 100f, 14f, 140f, 27f, 6f, new Color(0.18f, 0.4f, 0.24f));
            Line(px, w, h, new Vector2(108f, 16f), new Vector2(108f, 26f), 1f, new Color(0.1f, 0.25f, 0.14f));
            Line(px, w, h, new Vector2(122f, 15f), new Vector2(122f, 26f), 1f, new Color(0.1f, 0.25f, 0.14f));
            for (int b = 0; b < 4; b++)
            {
                Ellipse(px, w, h, 144f + b * 3f, 22f - b * 1.5f, 9f, 3f, new Color(0.62f, 0.8f, 0.25f), 1f);
            }

            // Body: roof lip, yellow cabin, hood, then the three flag stripes along the sides.
            RRect(px, w, h, 8f, 33f, 150f, 39f, 3f, new Color(0.8f, 0.5f, 0.05f));
            RRect(px, w, h, 10f, 36f, 150f, 100f, 7f, yellow);
            RRect(px, w, h, 148f, 62f, 184f, 100f, 9f, yellow);
            RRect(px, w, h, 10f, 70f, 184f, 80f, 0f, blue);
            RRect(px, w, h, 10f, 80f, 184f, 90f, 0f, red);
            RRect(px, w, h, 10f, 90f, 184f, 100f, 5f, new Color(0.96f, 0.72f, 0.1f));
            RRect(px, w, h, 10f, 36f, 150f, 40f, 2f, new Color(1f, 0.92f, 0.5f), 0.55f);

            // Windows with a little glint, and a driver's silhouette in the cab.
            for (int win = 0; win < 6; win++)
            {
                float wx = 20f + win * 21f;
                RRect(px, w, h, wx, 46f, wx + 17f, 64f, 3f, new Color(0.14f, 0.24f, 0.33f));
                Triangle(px, w, h, new Vector2(wx + 2f, 63f), new Vector2(wx + 10f, 47f), new Vector2(wx + 13f, 47f), new Color(0.5f, 0.68f, 0.78f));
            }
            RRect(px, w, h, 148f, 46f, 166f, 64f, 3f, new Color(0.14f, 0.24f, 0.33f));
            Disc(px, w, h, 157f, 56f, 4f, new Color(0.35f, 0.22f, 0.15f), 1f);

            // Hood details: grille, round headlight, bumper.
            RRect(px, w, h, 178f, 70f, 185f, 96f, 2f, new Color(0.15f, 0.12f, 0.1f));
            Disc(px, w, h, 181f, 66f, 5f, new Color(1f, 0.97f, 0.8f), 1f);
            Disc(px, w, h, 181f, 66f, 2.4f, new Color(1f, 1f, 1f), 1f);
            RRect(px, w, h, 176f, 96f, 190f, 102f, 2f, new Color(0.6f, 0.58f, 0.55f));

            // Wheels over dark arches.
            float[] wheels = { 42f, 140f };
            for (int i = 0; i < wheels.Length; i++)
            {
                Disc(px, w, h, wheels[i], 98f, 17f, new Color(0.1f, 0.08f, 0.07f), 1f);
                Disc(px, w, h, wheels[i], 100f, 13f, new Color(0.06f, 0.05f, 0.05f), 1f);
                Disc(px, w, h, wheels[i], 100f, 7f, new Color(0.85f, 0.82f, 0.72f), 1f);
                Disc(px, w, h, wheels[i], 100f, 2.5f, new Color(0.3f, 0.28f, 0.26f), 1f);
            }
            return ToTexture(px, w, h);
        }
    }
}
