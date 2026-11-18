using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Studio.Pacman
{
    /// <summary>
    /// A character built like a paper doll: a hierarchy of separately drawn parts (head, torso, arms, thighs, boots) joined at
    /// pivots and posed by <see cref="PacmanDollPose"/>. Three drawn views (front, back, side; west is the side mirrored). Parts and
    /// pivots come from art/doll (doll_cut.py / doll_pack.py) as doll_NAME.json plus one PNG atlas (.bytes) per view.
    /// </summary>
    public sealed class PacmanDoll : MonoBehaviour
    {
        [System.Serializable] private sealed class PartMeta { public string name, parent; public int ax, ay, aw, ah, bx, by, order; public float px, py; }
        [System.Serializable] private sealed class ViewMeta { public string name; public int w, h; public PartMeta[] parts; }
        [System.Serializable] private sealed class Meta { public int figureHeight; public ViewMeta[] views; }

        private sealed class Data
        {
            public Meta Meta;
            public Texture2D[] Atlas; // [view]
        }

        private sealed class Part
        {
            public Transform T;
            public SpriteRenderer Sr;
            public Vector3 Base;
            public string Name;
        }

        private static readonly Dictionary<string, Data> Cache = new Dictionary<string, Data>();
        private static readonly string[] ViewNames = { "front", "back", "side" };

        public DollTuning Tuning = new DollTuning();

        private Transform[] roots = new Transform[3];
        private Dictionary<string, Part>[] parts = new Dictionary<string, Part>[3];
        private float ppu;
        private DollView view = DollView.Front;
        private bool flip;
        private float idleT, walkBlend;
        private Color colour = Color.white;
        private bool visible = true;

        /// <summary>True when the theme has a doll called this.</summary>
        public static bool Exists(string name) => Load(name) != null;

        private static Data Load(string name)
        {
            if (Cache.TryGetValue(name, out Data cached) && (cached == null || cached.Atlas[0] != null))
            {
                return cached;
            }
            Data result = null;
            TextAsset json = PacmanTheme.GetText("doll_" + name);
            if (json != null)
            {
                Meta m = JsonUtility.FromJson<Meta>(json.text);
                result = new Data { Meta = m, Atlas = new Texture2D[3] };
                for (int v = 0; v < 3; v++)
                {
                    TextAsset png = PacmanTheme.GetText("doll_" + name + "_" + ViewNames[v]);
                    if (png == null)
                    {
                        result = null;
                        break;
                    }
                    Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                    tex.LoadImage(png.bytes);
                    result.Atlas[v] = tex;
                }
            }
            Cache[name] = result;
            return result;
        }

        /// <summary>Builds the doll under <paramref name="host"/>: feet at the host's origin, <paramref name="height"/> world units tall.</summary>
        public static PacmanDoll Build(string name, GameObject host, float height, int sortingOrder)
        {
            Data d = Load(name);
            if (d == null)
            {
                return null;
            }
            PacmanDoll doll = host.AddComponent<PacmanDoll>();
            doll.ppu = d.Meta.figureHeight / height;
            SortingGroup group = host.AddComponent<SortingGroup>();
            group.sortingOrder = sortingOrder;
            for (int v = 0; v < 3; v++)
            {
                doll.BuildView(v, d);
            }
            doll.SetView(DollView.Front, false);
            return doll;
        }

        private void BuildView(int v, Data d)
        {
            ViewMeta vm = d.Meta.views[v];
            GameObject rootGo = new GameObject("doll_" + vm.name);
            rootGo.transform.SetParent(transform, false);
            roots[v] = rootGo.transform;
            parts[v] = new Dictionary<string, Part>();
            // Centre the figure on its torso pivot so the doll stands where the feet are, whatever the drawn width.
            float cx = 0f;
            foreach (PartMeta p in vm.parts)
            {
                if (p.name == "torso") { cx = p.px; }
            }
            float scale = 1f / ppu;
            Texture2D tex = d.Atlas[v];
            List<PartMeta> pending = new List<PartMeta>(vm.parts);
            int guard = 0;
            while (pending.Count > 0 && guard++ < 64)
            {
                for (int i = pending.Count - 1; i >= 0; i--)
                {
                    PartMeta p = pending[i];
                    bool root = string.IsNullOrEmpty(p.parent);
                    if (!root && !parts[v].ContainsKey(p.parent))
                    {
                        continue;
                    }
                    GameObject go = new GameObject(p.name);
                    Transform par = root ? rootGo.transform : parts[v][p.parent].T;
                    go.transform.SetParent(par, false);
                    Vector3 pos;
                    if (root)
                    {
                        pos = new Vector3((p.px - cx) * scale, (vm.h - p.py) * scale, 0f);
                    }
                    else
                    {
                        PartMeta pp = System.Array.Find(vm.parts, q => q.name == p.parent);
                        pos = new Vector3((p.px - pp.px) * scale, -(p.py - pp.py) * scale, 0f);
                    }
                    go.transform.localPosition = pos;
                    SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                    Vector2 pivot = new Vector2((p.px - p.bx) / p.aw, 1f - (p.py - p.by) / p.ah);
                    sr.sprite = Sprite.Create(tex, new Rect(p.ax, tex.height - p.ay - p.ah, p.aw, p.ah), pivot, ppu, 0, SpriteMeshType.FullRect);
                    sr.sortingOrder = p.order;
                    parts[v][p.name] = new Part { T = go.transform, Sr = sr, Base = pos, Name = p.name };
                    pending.RemoveAt(i);
                }
            }
        }

        private void SetView(DollView v, bool mirrored)
        {
            view = v;
            flip = mirrored;
            for (int i = 0; i < 3; i++)
            {
                roots[i].gameObject.SetActive(i == (int)v);
            }
            roots[(int)v].localScale = new Vector3(mirrored ? -1f : 1f, 1f, 1f);
        }

        /// <summary>Faces a tile heading (x east, y south) and poses the doll for this moment.</summary>
        public void Show(Vector2Int heading, float gait, bool moving)
        {
            if (heading.x != 0)
            {
                SetView(DollView.Side, heading.x < 0);
            }
            else if (heading.y > 0)
            {
                SetView(DollView.Front, false);
            }
            else if (heading.y < 0)
            {
                SetView(DollView.Back, false);
            }
            idleT += Time.deltaTime;
            walkBlend = Mathf.MoveTowards(walkBlend, moving ? 1f : 0f, Time.deltaTime * 9f);
            DollPose pose = DollPose.Lerp(PacmanDollPose.Idle(idleT), PacmanDollPose.Walk(view, gait, Tuning), walkBlend);
            Apply(pose);
        }

        private void Apply(DollPose p)
        {
            Dictionary<string, Part> d = parts[(int)view];
            float inv = 1f / ppu;
            transform.localRotation = Quaternion.Euler(0f, 0f, p.RootRoll);
            transform.localScale = new Vector3(p.RootSX, p.RootSY, 1f);
            Set(d, "torso", p.TorsoRoll, p.Bob * inv);
            Set(d, "head", p.HeadRoll, 0f);
            Set(d, "armL", p.ArmL, 0f);
            Set(d, "armR", p.ArmR, 0f);
            Set(d, "thighL", p.ThighL, 0f);
            Set(d, "thighR", p.ThighR, 0f);
            Set(d, "shinL", p.ShinL, p.LiftL * inv);
            Set(d, "shinR", p.ShinR, p.LiftR * inv);
        }

        private static void Set(Dictionary<string, Part> d, string name, float roll, float lift)
        {
            if (d.TryGetValue(name, out Part part))
            {
                part.T.localRotation = Quaternion.Euler(0f, 0f, roll);
                part.T.localPosition = part.Base + new Vector3(0f, lift, 0f);
            }
        }

        public Color Colour
        {
            get => colour;
            set
            {
                colour = value;
                foreach (Dictionary<string, Part> d in parts)
                {
                    foreach (Part p in d.Values)
                    {
                        p.Sr.color = value;
                    }
                }
            }
        }

        public bool Visible
        {
            get => visible;
            set
            {
                visible = value;
                foreach (Transform r in roots)
                {
                    foreach (SpriteRenderer sr in r.GetComponentsInChildren<SpriteRenderer>(true))
                    {
                        sr.enabled = value;
                    }
                }
            }
        }
    }
}
