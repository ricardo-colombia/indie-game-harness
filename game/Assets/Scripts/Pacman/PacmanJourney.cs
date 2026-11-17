using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// The trip across the country. Each level is a farm in a different coffee region of Colombia, and between
    /// levels the farmer travels from one to the next. Plain data and geometry, no scene access, so it is easy to test.
    /// Coordinates are approximate: the point is that the places are real and in the right part of the map.
    /// </summary>
    public static class PacmanJourney
    {
        /// <summary>One stop on the route: where the farm is, in latitude/longitude, and how high.</summary>
        public struct Stop
        {
            public string Region;
            public float Lat;
            public float Lon;
            public int Altitude;
            public string Tagline;

            public Stop(string region, float lat, float lon, int altitude, string tagline)
            {
                Tagline = tagline;
                Region = region;
                Lat = lat;
                Lon = lon;
                Altitude = altitude;
            }
        }

        // Same order as PacmanFarms' names: stop N is the region of level N (the list repeats after the last one).
        private static readonly Stop[] Stops =
        {
            new Stop("Quindío", 4.53f, -75.68f, 1400, "Guadua, palmas de cera y neblina en el Eje Cafetero."),
            new Stop("Caldas", 4.98f, -75.60f, 1500, "Cordillera arriba, con el aroma del grano recién tostado."),
            new Stop("Antioquia", 5.60f, -75.82f, 1800, "Montañas de nubes bajas, caminos de herradura y buena arepa."),
            new Stop("Huila", 1.85f, -76.05f, 1300, "Sol de altura, y el Magdalena naciendo allá abajo."),
            new Stop("Nariño", 1.20f, -77.30f, 2000, "Cafés de altura, bruma fría y volcanes en el horizonte."),
            new Stop("Cauca", 2.45f, -76.60f, 1700, "Popayán blanca de lejos y cafés de ladera."),
            new Stop("Tolima", 4.90f, -75.07f, 1500, "Tierra de tiple, de bambuco y de buen tinto."),
            new Stop("Cundinamarca", 4.75f, -74.35f, 1600, "A dos pasos de la sabana, entre trapiches y cañaduzales."),
            new Stop("Boyacá", 5.20f, -73.15f, 1400, "Páramo, frailejones y madrugadas de ruana."),
            new Stop("Santander", 6.50f, -73.10f, 1500, "Cañones profundos y cafés de finca pequeña."),
            new Stop("Norte de Santander", 7.40f, -72.65f, 1700, "La frontera, el calor del valle y el café de montaña."),
            new Stop("Cesar", 10.40f, -73.60f, 1500, "Vallenato en el aire y la Sierra siempre de fondo."),
            new Stop("Sierra Nevada", 11.10f, -74.05f, 1200, "Cumbres nevadas frente al mar Caribe."),
            new Stop("Valle del Cauca", 4.20f, -76.30f, 1600, "Valle de la caña, con laderas frescas para el café."),
            new Stop("Risaralda", 4.80f, -75.70f, 1500, "Un tinto humeante en cada esquina."),
            new Stop("Antioquia", 6.30f, -76.10f, 1800, "Montaña, nubes bajas y la cosecha de mitaca."),
        };

        // The country's outline as (latitude, longitude), clockwise from the tip of La Guajira.
        private static readonly float[] Outline =
        {
            12.46f, -71.70f, 12.20f, -72.15f, 11.55f, -72.90f, 11.25f, -74.20f, 11.00f, -74.85f, 10.40f, -75.55f,
            9.60f, -75.65f, 9.00f, -76.10f, 8.60f, -76.80f, 8.10f, -76.75f, 8.65f, -77.35f, 7.90f, -77.75f,
            7.20f, -77.85f, 6.30f, -77.40f, 5.50f, -77.55f, 4.40f, -77.40f, 3.90f, -77.15f, 2.90f, -77.90f,
            1.85f, -78.80f, 1.20f, -78.85f, 0.85f, -77.65f, 0.35f, -77.00f, 0.40f, -76.30f, -0.10f, -75.40f,
            -0.40f, -74.60f, -1.50f, -73.40f, -2.40f, -71.50f, -3.40f, -70.30f, -4.20f, -69.95f, -3.80f, -69.45f,
            -1.10f, -69.45f, 0.00f, -69.90f, 1.10f, -69.40f, 1.80f, -67.90f, 2.20f, -67.30f, 3.70f, -67.80f,
            4.50f, -67.80f, 6.20f, -67.45f, 6.20f, -68.20f, 6.10f, -69.40f, 6.70f, -70.20f, 7.05f, -70.75f,
            7.10f, -72.00f, 7.90f, -72.50f, 8.30f, -72.40f, 9.00f, -72.75f, 9.90f, -72.95f, 10.85f, -72.55f,
            11.85f, -71.33f,
        };

        private const float LatNorth = 13.1f;
        private const float LatSouth = -5.0f;
        private const float LonWest = -79.9f;
        private const float LonEast = -65.9f;

        /// <summary>Degrees of latitude the map spans.</summary>
        public const float LatSpan = LatNorth - LatSouth;

        /// <summary>A place's position in pixels on a map of the given size (y down).</summary>
        public static Vector2 ProjectPixels(float lat, float lon, int width, int height)
        {
            Vector2 m = Project(lat, lon);
            return new Vector2(m.x * width, m.y * height);
        }

        /// <summary>The country's outline in pixels on a map of the given size.</summary>
        public static Vector2[] OutlineInPixels(int width, int height)
        {
            Vector2[] points = new Vector2[Outline.Length / 2];
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = ProjectPixels(Outline[i * 2], Outline[i * 2 + 1], width, height);
            }
            return points;
        }

        /// <summary>Even-odd point-in-polygon test in pixel space.</summary>
        public static bool InsidePixels(Vector2[] polygon, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        /// <summary>Width over height of the map, so a screen can lay it out.</summary>
        public static float Aspect => (LonEast - LonWest) / (LatNorth - LatSouth);

        public static int Count => Stops.Length;

        /// <summary>The stop of a level (levels count from 1; the route starts over after the last stop).</summary>
        public static Stop StopFor(int level)
        {
            int index = (level - 1) % Stops.Length;
            if (index < 0)
            {
                index += Stops.Length;
            }
            return Stops[index];
        }

        /// <summary>Map position of a place, 0..1 across and 0..1 down (north at the top).</summary>
        public static Vector2 Project(float lat, float lon)
        {
            return new Vector2((lon - LonWest) / (LonEast - LonWest), (LatNorth - lat) / (LatNorth - LatSouth));
        }

        public static Vector2 Project(Stop stop)
        {
            return Project(stop.Lat, stop.Lon);
        }

        private static Vector2[] projected;

        /// <summary>True when a map position (0..1, 0..1) is inside the country's outline.</summary>
        public static bool Inside(Vector2 map)
        {
            if (projected == null)
            {
                projected = new Vector2[Outline.Length / 2];
                for (int k = 0; k < projected.Length; k++)
                {
                    projected[k] = Project(Outline[k * 2], Outline[k * 2 + 1]);
                }
            }
            bool inside = false;
            int n = projected.Length;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                Vector2 a = projected[i];
                Vector2 b = projected[j];
                if ((a.y > map.y) != (b.y > map.y) && map.x < (b.x - a.x) * (map.y - a.y) / (b.y - a.y) + a.x)
                {
                    inside = !inside;
                }
            }
            return inside;
        }
    }
}
