using NUnit.Framework;
using UnityEngine;

namespace Studio.Pacman.Tests
{
    public class PacmanDollPoseTests
    {
        private static readonly DollTuning T = new DollTuning();

        [Test]
        public void SideWalkSwingsLegsOppositely()
        {
            DollPose p = PacmanDollPose.Walk(DollView.Side, Mathf.PI / 2f, T);
            Assert.AreEqual(T.SideThighSwing, p.ThighL, 0.01f);
            Assert.AreEqual(-p.ThighL, p.ThighR, 0.01f);
        }

        [Test]
        public void OnlyTheLegComingForwardLiftsItsFoot()
        {
            DollPose p = PacmanDollPose.Walk(DollView.Side, 0f, T); // cos = 1: left leg forward
            Assert.Greater(p.LiftL, 0f);
            Assert.AreEqual(0f, p.LiftR, 0.001f);
            Assert.Less(p.ShinL, 0f);
        }

        [Test]
        public void StrideRepeatsEveryTurn()
        {
            DollPose a = PacmanDollPose.Walk(DollView.Front, 1.3f, T), b = PacmanDollPose.Walk(DollView.Front, 1.3f + 2f * Mathf.PI, T);
            Assert.AreEqual(a.TorsoRoll, b.TorsoRoll, 0.001f);
            Assert.AreEqual(a.LiftL, b.LiftL, 0.001f);
        }

        [Test]
        public void FrontWalkNeverSwingsThighs()
        {
            for (float g = 0f; g < 6.3f; g += 0.4f)
            {
                DollPose p = PacmanDollPose.Walk(DollView.Front, g, T);
                Assert.AreEqual(0f, p.ThighL, 0.001f);
            }
        }

        [Test]
        public void IdleStaysNearNeutral()
        {
            for (float t = 0f; t < 10f; t += 0.37f)
            {
                DollPose p = PacmanDollPose.Idle(t);
                Assert.That(Mathf.Abs(p.ArmL), Is.LessThan(3f));
                Assert.That(p.RootSY, Is.InRange(0.98f, 1.02f));
            }
        }
    }
}
