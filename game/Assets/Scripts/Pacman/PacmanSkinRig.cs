using System;
using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// Bones and a weighted triangle mesh for one painted character (made by art/comfy/make_rig.py from the picture).
    /// Plain C#, no scene access: the pose goes in as per-bone rotation / offset / scale about each joint, the deformed
    /// vertices come out. Linear-blend skinning with four influences per vertex, no allocations per frame.
    /// </summary>
    public sealed class PacmanSkinRig
    {
        /// <summary>Serialized form (JsonUtility), pixel coordinates with y down.</summary>
        [Serializable]
        public sealed class Data
        {
            [Serializable]
            public sealed class Bone
            {
                public string n;
                public int p;
                public float x;
                public float y;
            }

            public int w;
            public int h;
            public Bone[] bones;
            public float[] v;
            public int[] bi;
            public float[] bw;
            public int[] t;
        }

        private struct Affine
        {
            public float A, B, C, D, Tx, Ty; // x' = A*x + B*y + Tx ; y' = C*x + D*y + Ty

            public static Affine Identity => new Affine { A = 1f, D = 1f };

            public static Affine operator *(Affine p, Affine c)
            {
                return new Affine
                {
                    A = p.A * c.A + p.B * c.C,
                    B = p.A * c.B + p.B * c.D,
                    C = p.C * c.A + p.D * c.C,
                    D = p.C * c.B + p.D * c.D,
                    Tx = p.A * c.Tx + p.B * c.Ty + p.Tx,
                    Ty = p.C * c.Tx + p.D * c.Ty + p.Ty,
                };
            }
        }

        public readonly int BoneCount;
        public readonly int VertexCount;
        public readonly int[] Triangles;
        public readonly Vector2[] Uv;
        public readonly string[] BoneNames;

        /// <summary>Per-bone pose, all about the bone's joint: rotation in radians (counter-clockwise), offset and scale in units.</summary>
        public readonly float[] Rotation;
        public readonly Vector2[] Offset;
        public readonly Vector2[] Scale;

        private readonly int[] parent;
        private readonly Vector2[] joint; // bind positions in units
        private readonly Vector2[] bind;  // vertex bind positions in units
        private readonly int[] bi;
        private readonly float[] bw;
        private readonly Affine[] world;

        /// <param name="height">Height of the picture in units (1 = one tile).</param>
        /// <param name="footY">Local y of the picture's bottom edge.</param>
        public PacmanSkinRig(Data data, float height, float footY)
        {
            float s = height / data.h;
            BoneCount = data.bones.Length;
            VertexCount = data.v.Length / 2;
            Triangles = data.t;
            bi = data.bi;
            bw = data.bw;
            parent = new int[BoneCount];
            joint = new Vector2[BoneCount];
            BoneNames = new string[BoneCount];
            Rotation = new float[BoneCount];
            Offset = new Vector2[BoneCount];
            Scale = new Vector2[BoneCount];
            world = new Affine[BoneCount];
            for (int i = 0; i < BoneCount; i++)
            {
                parent[i] = data.bones[i].p;
                joint[i] = ToUnits(data.bones[i].x, data.bones[i].y, data.w, data.h, s, footY);
                BoneNames[i] = data.bones[i].n;
                Scale[i] = Vector2.one;
            }
            bind = new Vector2[VertexCount];
            Uv = new Vector2[VertexCount];
            for (int i = 0; i < VertexCount; i++)
            {
                float px = data.v[2 * i];
                float py = data.v[2 * i + 1];
                bind[i] = ToUnits(px, py, data.w, data.h, s, footY);
                Uv[i] = new Vector2(px / data.w, 1f - py / data.h);
            }
        }

        private static Vector2 ToUnits(float px, float py, int w, int h, float s, float footY)
        {
            return new Vector2((px - w * 0.5f) * s, (h - py) * s + footY);
        }

        public int IndexOf(string boneName)
        {
            return Array.IndexOf(BoneNames, boneName);
        }

        public void ResetPose()
        {
            for (int i = 0; i < BoneCount; i++)
            {
                Rotation[i] = 0f;
                Offset[i] = Vector2.zero;
                Scale[i] = Vector2.one;
            }
        }

        /// <summary>Applies the current pose and writes the deformed vertex positions (z = 0) into <paramref name="output"/>.</summary>
        public void Skin(Vector3[] output)
        {
            for (int i = 0; i < BoneCount; i++)
            {
                float cos = Mathf.Cos(Rotation[i]);
                float sin = Mathf.Sin(Rotation[i]);
                Vector2 sc = Scale[i];
                Vector2 j = joint[i];
                // local = T(joint + offset) * R * S * T(-joint)
                Affine local;
                local.A = cos * sc.x;
                local.B = -sin * sc.y;
                local.C = sin * sc.x;
                local.D = cos * sc.y;
                local.Tx = j.x + Offset[i].x - (local.A * j.x + local.B * j.y);
                local.Ty = j.y + Offset[i].y - (local.C * j.x + local.D * j.y);
                world[i] = parent[i] >= 0 ? world[parent[i]] * local : local;
            }
            for (int v = 0; v < VertexCount; v++)
            {
                Vector2 b = bind[v];
                float x = 0f;
                float y = 0f;
                for (int k = 0; k < 4; k++)
                {
                    float w = bw[4 * v + k];
                    if (w <= 0f)
                    {
                        continue;
                    }
                    Affine m = world[bi[4 * v + k]];
                    x += w * (m.A * b.x + m.B * b.y + m.Tx);
                    y += w * (m.C * b.x + m.D * b.y + m.Ty);
                }
                output[v] = new Vector3(x, y, 0f);
            }
        }
    }
}
