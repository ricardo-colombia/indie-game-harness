using System.Collections.Generic;
using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// A character rendered from a 3D model (art/blender): a sheet of walk and idle frames seen from the south, east and
    /// north at the game's three-quarter angle. West is the east row mirrored. The owner says which way it is heading and
    /// where in the stride it is; frames are pre-cut sprites, so playing them allocates nothing.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PacmanSpriteActor : MonoBehaviour
    {
        [System.Serializable]
        private sealed class Meta
        {
            public int w, h, walk, idle, dirs, pad;
            public float idleFps;
        }

        private sealed class Cut
        {
            public Sprite[][] Walk; // [dir][frame]
            public Sprite[][] Idle;
            public int WalkCount, IdleCount;
            public float IdleFps;
        }

        private static readonly Dictionary<string, Cut> Cache = new Dictionary<string, Cut>();

        private Cut cut;
        private SpriteRenderer sr;
        private int dirRow;
        private bool flip;
        private float idleClock;

        /// <summary>The sheet "anim_NAME" from the theme, or null when the theme has none.</summary>
        public static PacmanSpriteActor Create(string name, Transform parent, float height, float footY, int sortingOrder)
        {
            Cut c = Load(name, height);
            if (c == null)
            {
                return null;
            }
            GameObject go = new GameObject(name + "Actor");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, footY, 0f);
            PacmanSpriteActor a = go.AddComponent<PacmanSpriteActor>();
            a.cut = c;
            a.sr = go.GetComponent<SpriteRenderer>();
            a.sr.sortingOrder = sortingOrder;
            a.sr.sprite = c.Idle[0][0];
            return a;
        }

        private static Cut Load(string name, float height)
        {
            string key = name + "@" + height.ToString("0.###");
            if (Cache.TryGetValue(key, out Cut cached))
            {
                return cached;
            }
            Cut result = null;
            Texture2D tex = PacmanTheme.GetTexture("anim_" + name);
            TextAsset json = PacmanTheme.GetText("anim_" + name);
            if (tex != null && json != null)
            {
                Meta m = JsonUtility.FromJson<Meta>(json.text);
                float ppu = (m.h - m.pad) / height;
                Vector2 pivot = new Vector2(0.5f, (float)m.pad / m.h);
                result = new Cut { WalkCount = m.walk, IdleCount = m.idle, IdleFps = m.idleFps };
                result.Walk = new Sprite[m.dirs][];
                result.Idle = new Sprite[m.dirs][];
                for (int d = 0; d < m.dirs; d++)
                {
                    result.Walk[d] = Frames(tex, m, d, m.walk, ppu, pivot);
                    result.Idle[d] = Frames(tex, m, m.dirs + d, m.idle, ppu, pivot);
                }
            }
            Cache[key] = result;
            return result;
        }

        private static Sprite[] Frames(Texture2D tex, Meta m, int row, int count, float ppu, Vector2 pivot)
        {
            Sprite[] frames = new Sprite[count];
            int y = tex.height - (row + 1) * m.h;
            for (int i = 0; i < count; i++)
            {
                frames[i] = Sprite.Create(tex, new Rect(i * m.w, y, m.w, m.h), pivot, ppu, 0, SpriteMeshType.FullRect);
            }
            return frames;
        }

        /// <summary>Faces a tile heading (x east, y south) and shows the frame for the current moment.</summary>
        /// <param name="heading">Tile direction; zero keeps the last facing.</param>
        /// <param name="gait">Stride phase 0..2π.</param>
        /// <param name="moving">Walks when true, idles otherwise.</param>
        public void Show(Vector2Int heading, float gait, bool moving)
        {
            if (heading.x != 0)
            {
                dirRow = 1;
                flip = heading.x < 0;
            }
            else if (heading.y > 0)
            {
                dirRow = 0;
                flip = false;
            }
            else if (heading.y < 0)
            {
                dirRow = 2;
                flip = false;
            }
            Sprite[] set;
            int frame;
            if (moving)
            {
                set = cut.Walk[dirRow];
                frame = Mathf.Clamp((int)(gait / (2f * Mathf.PI) * cut.WalkCount), 0, cut.WalkCount - 1);
            }
            else
            {
                set = cut.Idle[dirRow];
                idleClock += Time.deltaTime * cut.IdleFps;
                frame = (int)idleClock % cut.IdleCount;
            }
            sr.sprite = set[frame];
            sr.flipX = flip;
        }

        public Color Colour
        {
            get => sr.color;
            set => sr.color = value;
        }

        public bool Visible
        {
            get => sr.enabled;
            set => sr.enabled = value;
        }
    }
}
