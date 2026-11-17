using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>
    /// The shared walk / idle / startle animation for skinned painted characters. Every rig names its bones the same
    /// way (root, spine, head, hat, armL1, armL2, armR1, armR2, legL, footL, legR, footR); a rig may lack the hat.
    /// Overlapping action: head, hat and hands lag behind the body, the body squashes on each landing.
    /// </summary>
    public sealed class PacmanPuppetPose
    {
        private const float D = Mathf.Deg2Rad;
        private readonly PacmanSkinRig rig;
        private readonly int root, spine, head, hat, armL1, armL2, armR1, armR2, legL, footL, legR, footR;

        public PacmanPuppetPose(PacmanSkinRig rig)
        {
            this.rig = rig;
            root = rig.IndexOf("root"); spine = rig.IndexOf("spine"); head = rig.IndexOf("head"); hat = rig.IndexOf("hat");
            armL1 = rig.IndexOf("armL1"); armL2 = rig.IndexOf("armL2"); armR1 = rig.IndexOf("armR1"); armR2 = rig.IndexOf("armR2");
            legL = rig.IndexOf("legL"); footL = rig.IndexOf("footL"); legR = rig.IndexOf("legR"); footR = rig.IndexOf("footR");
        }

        /// <param name="gait">Stride phase in radians (one full cycle = two steps).</param>
        /// <param name="amp">0 standing, 1 walking at full stride (blend).</param>
        /// <param name="breathe">Idle breathing, -1..1 (already faded out while walking).</param>
        /// <param name="raise">0..1 startle / cheer: stretches the torso and throws the right arm up.</param>
        /// <param name="shake">Extra trembling of the body in degrees (frightened).</param>
        public void Apply(float gait, float amp, float breathe, float raise, float shake)
        {
            float p = gait;
            float s = Mathf.Sin(p);
            float hop = Mathf.Abs(s);
            float land = Mathf.Cos(2f * p);
            rig.ResetPose();
            rig.Offset[root] = new Vector2(0f, amp * 0.06f * hop + breathe * 0.004f);
            rig.Rotation[root] = (amp * 3f * s + shake) * D;
            rig.Scale[root] = new Vector2(1f + amp * 0.03f * land, 1f - amp * 0.03f * land);
            rig.Rotation[spine] = (amp * -4f * s + breathe * 0.8f) * D;
            rig.Scale[spine] = new Vector2(1f - raise * 0.05f, 1f + breathe * 0.006f + raise * 0.08f);
            rig.Rotation[head] = (amp * 4f * Mathf.Sin(p - 0.8f) + breathe * 0.6f) * D;
            if (hat >= 0)
            {
                rig.Rotation[hat] = (amp * 7f * Mathf.Sin(p - 1.7f) + breathe * 1.2f) * D;
                rig.Offset[hat] = new Vector2(0f, amp * 0.012f * Mathf.Cos(2f * p - 1.2f));
            }
            rig.Rotation[armL1] = (amp * 9f * s + breathe * 1.5f) * D;
            rig.Rotation[armL2] = amp * -12f * Mathf.Sin(p - 0.9f) * D;
            rig.Rotation[armR1] = (amp * -9f * s - breathe * 1.5f - raise * 70f) * D;
            rig.Rotation[armR2] = (amp * 12f * Mathf.Sin(p - 0.9f) - raise * 40f) * D;
            float l = amp * 22f * s;
            rig.Rotation[legL] = l * D;
            rig.Rotation[legR] = -l * D;
            rig.Rotation[footL] = (-l * 0.7f + amp * 10f * Mathf.Max(0f, Mathf.Cos(p))) * D;
            rig.Rotation[footR] = (l * 0.7f + amp * 10f * Mathf.Max(0f, -Mathf.Cos(p))) * D;
        }
    }
}
