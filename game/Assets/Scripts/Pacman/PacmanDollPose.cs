using UnityEngine;

namespace Studio.Pacman
{
    /// <summary>Which of the three drawn views of a doll is showing (west is the side view mirrored).</summary>
    public enum DollView { Front = 0, Back = 1, Side = 2 }

    /// <summary>
    /// The pose of a paper-doll rig at one moment, as plain numbers: degrees for rotations, figure pixels for offsets
    /// (the doll is drawn 768 px tall). No scene access, so it is testable and the look can change without touching it.
    /// "L" and "R" are the left and right of the picture; in the side view L is the far/back limb.
    /// </summary>
    public struct DollPose
    {
        public float RootRoll, RootSX, RootSY, Bob, TorsoRoll, HeadRoll, ArmL, ArmR, ThighL, ThighR, ShinL, ShinR, LiftL, LiftR, ArmSL, ArmSR;

        public static DollPose Identity => new DollPose { RootSX = 1f, RootSY = 1f, ArmSL = 1f, ArmSR = 1f };

        public static DollPose Lerp(DollPose a, DollPose b, float t)
        {
            return new DollPose
            {
                RootRoll = Mathf.Lerp(a.RootRoll, b.RootRoll, t), RootSX = Mathf.Lerp(a.RootSX, b.RootSX, t), RootSY = Mathf.Lerp(a.RootSY, b.RootSY, t),
                Bob = Mathf.Lerp(a.Bob, b.Bob, t), TorsoRoll = Mathf.Lerp(a.TorsoRoll, b.TorsoRoll, t), HeadRoll = Mathf.Lerp(a.HeadRoll, b.HeadRoll, t),
                ArmL = Mathf.Lerp(a.ArmL, b.ArmL, t), ArmR = Mathf.Lerp(a.ArmR, b.ArmR, t),
                ThighL = Mathf.Lerp(a.ThighL, b.ThighL, t), ThighR = Mathf.Lerp(a.ThighR, b.ThighR, t),
                ShinL = Mathf.Lerp(a.ShinL, b.ShinL, t), ShinR = Mathf.Lerp(a.ShinR, b.ShinR, t),
                LiftL = Mathf.Lerp(a.LiftL, b.LiftL, t), LiftR = Mathf.Lerp(a.LiftR, b.LiftR, t),
                ArmSL = Mathf.Lerp(a.ArmSL, b.ArmSL, t), ArmSR = Mathf.Lerp(a.ArmSR, b.ArmSR, t),
            };
        }
    }

    /// <summary>Tunable swing sizes of the walk (degrees / figure pixels). A round body waddles more than it strides.</summary>
    [System.Serializable]
    public sealed class DollTuning
    {
        public float SideThighSwing = 22f, SideKneeBend = 30f, SideFootLift = 12f, SideArmSwing = 16f, SideLean = -2.5f;
        public float FrontFootLift = 12f, FrontArmSwing = 4f, FrontArmForeshorten = 0.07f, FrontTorsoRoll = 4f, FrontRootRoll = 2.5f;
        public float Bob = 10f, SquashX = 0.04f, SquashY = 0.05f;
    }

    public static class PacmanDollPose
    {
        /// <summary>The walk pose at stride phase <paramref name="gait"/> (0..2π; one full turn is two steps).</summary>
        public static DollPose Walk(DollView view, float gait, DollTuning t)
        {
            float s = Mathf.Sin(gait), c = Mathf.Cos(gait);
            DollPose p = DollPose.Identity;
            float step = Mathf.Cos(2f * gait);
            p.RootSX = 1f - t.SquashX * step;
            p.RootSY = 1f + t.SquashY * step;
            p.Bob = Mathf.Abs(c) * t.Bob;
            if (view == DollView.Side)
            {
                p.ThighL = t.SideThighSwing * s;
                p.ThighR = -p.ThighL;
                float flexL = -t.SideKneeBend * Mathf.Max(0f, c), flexR = -t.SideKneeBend * Mathf.Max(0f, -c);
                p.ShinL = -0.75f * p.ThighL + flexL;       // the boot stays flat on the ground while the thigh swings, and folds when the leg comes forward
                p.ShinR = -0.75f * p.ThighR + flexR;
                p.LiftL = t.SideFootLift * Mathf.Max(0f, c);
                p.LiftR = t.SideFootLift * Mathf.Max(0f, -c);
                p.ArmL = -t.SideArmSwing * s;
                p.ArmR = t.SideArmSwing * s;
                p.TorsoRoll = t.SideLean;
                p.HeadRoll = -t.SideLean * 0.4f;
                p.RootRoll = s * 1.5f;
            }
            else
            {
                p.LiftL = t.FrontFootLift * Mathf.Max(0f, s);
                p.LiftR = t.FrontFootLift * Mathf.Max(0f, -s);
                // Seen head on, arms swing towards and away from the camera: they foreshorten (one longer as the other shortens) and barely rotate.
                p.ArmL = -t.FrontArmSwing * s;
                p.ArmR = t.FrontArmSwing * s;
                p.ArmSL = 1f + t.FrontArmForeshorten * s;
                p.ArmSR = 1f - t.FrontArmForeshorten * s;
                p.TorsoRoll = s * t.FrontTorsoRoll;
                p.HeadRoll = -s * t.FrontTorsoRoll * 0.5f;
                p.RootRoll = s * t.FrontRootRoll;
            }
            return p;
        }

        /// <summary>Standing still: slow breathing, arms and head drifting a little.</summary>
        public static DollPose Idle(float seconds)
        {
            float b = Mathf.Sin(seconds * 2.2f), w = Mathf.Sin(seconds * 1.1f);
            DollPose p = DollPose.Identity;
            p.RootSX = 1f - 0.008f * b;
            p.RootSY = 1f + 0.014f * b;
            p.ArmL = 2.5f * Mathf.Sin(seconds * 2.2f + 0.5f);
            p.ArmR = -p.ArmL;
            p.TorsoRoll = 0.8f * w;
            p.HeadRoll = 1.2f * Mathf.Sin(seconds * 1.1f + 1f);
            return p;
        }
    }
}
