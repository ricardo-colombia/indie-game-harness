using System.Collections.Generic;
using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// The art theme of the game. A theme is a folder of PNG sprites under <c>Resources/Themes/&lt;Id&gt;/</c> named
    /// after what they draw (<c>bean</c>, <c>mug</c>, <c>fruit_banana</c>...). Whatever a theme does not provide is
    /// drawn by the built-in procedural art, so a theme can be added one sprite at a time. The choice is
    /// remembered between runs.
    /// </summary>
    public static class PacmanTheme
    {
        /// <summary>The built-in procedural art (no sprite files).</summary>
        public const string Procedural = "Procedural";

        /// <summary>The engraved coffee-label look.</summary>
        public const string Vintage = "Vintage";

        private const string PrefKey = "pacman.theme";
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        private static string current;

        /// <summary>The active theme id. Changing it applies to the next level or game that builds its sprites.</summary>
        public static string Current
        {
            get
            {
                if (current == null)
                {
                    current = Vintage;
                    try
                    {
                        current = PlayerPrefs.GetString(PrefKey, Vintage);
                    }
                    catch (System.Exception)
                    {
                        // Storage can be unavailable (private windows, locked-down players): keep the default.
                    }
                }
                return current;
            }
            set
            {
                current = value;
                try
                {
                    PlayerPrefs.SetString(PrefKey, value);
                }
                catch (System.Exception)
                {
                    // The choice just is not remembered.
                }
            }
        }

        /// <summary>The theme's sprite with this name, one tile across at its largest side, or null when it has none.</summary>
        public static Sprite Get(string name)
        {
            if (Current == Procedural)
            {
                return null;
            }
            string key = Current + "/" + name;
            if (Cache.TryGetValue(key, out Sprite cached))
            {
                return cached;
            }
            Texture2D texture = Resources.Load<Texture2D>("Themes/" + key);
            Sprite sprite = null;
            if (texture != null)
            {
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;
                sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f),
                    Mathf.Max(texture.width, texture.height), 0, SpriteMeshType.FullRect);
                sprite.name = key;
            }
            Cache[key] = sprite;
            return sprite;
        }
    }
}
