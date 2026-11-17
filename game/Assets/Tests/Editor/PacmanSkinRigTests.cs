using NUnit.Framework;
using UnityEngine;

namespace Studio.Pacman.Tests
{
    public class PacmanSkinRigTests
    {
        // 100x100 picture: a root bone at the bottom centre, an arm bone hanging from (50,50) to the right. Two vertices.
        private static PacmanSkinRig.Data TwoBoneData()
        {
            return new PacmanSkinRig.Data
            {
                w = 100, h = 100,
                bones = new[]
                {
                    new PacmanSkinRig.Data.Bone { n = "root", p = -1, x = 50, y = 100 },
                    new PacmanSkinRig.Data.Bone { n = "arm", p = 0, x = 50, y = 50 },
                },
                v = new float[] { 50, 90, 80, 50 }, // body vertex, hand vertex
                bi = new[] { 0, 0, 0, 0, 1, 0, 0, 0 },
                bw = new float[] { 1, 0, 0, 0, 1, 0, 0, 0 },
                t = new[] { 0, 1, 0 },
            };
        }

        [Test]
        public void RestPoseKeepsBindPositionsAndFeetOnTheFloor()
        {
            PacmanSkinRig rig = new PacmanSkinRig(TwoBoneData(), 1f, -0.5f);
            Vector3[] output = new Vector3[rig.VertexCount];
            rig.Skin(output);
            Assert.AreEqual(0f, output[0].x, 1e-4f);
            Assert.AreEqual(-0.5f + 0.1f, output[0].y, 1e-4f);
            Assert.AreEqual(0.3f, output[1].x, 1e-4f);
            Assert.AreEqual(0f, output[1].y, 1e-4f);
        }

        [Test]
        public void RotatingTheArmMovesOnlyItsVertexAboutTheJoint()
        {
            PacmanSkinRig rig = new PacmanSkinRig(TwoBoneData(), 1f, -0.5f);
            Vector3[] output = new Vector3[rig.VertexCount];
            rig.Rotation[rig.IndexOf("arm")] = Mathf.PI * 0.5f; // counter-clockwise quarter turn: the hand points up
            rig.Skin(output);
            Assert.AreEqual(0f, output[0].x, 1e-4f);
            Assert.AreEqual(0f, output[1].x, 1e-4f);
            Assert.AreEqual(0.3f, output[1].y, 1e-4f);
        }

        [Test]
        public void ChildFollowsParentMotion()
        {
            PacmanSkinRig rig = new PacmanSkinRig(TwoBoneData(), 1f, -0.5f);
            Vector3[] output = new Vector3[rig.VertexCount];
            rig.Offset[rig.IndexOf("root")] = new Vector2(0f, 0.2f);
            rig.Skin(output);
            Assert.AreEqual(0.2f - 0.4f, output[0].y, 1e-4f);
            Assert.AreEqual(0.2f, output[1].y, 1e-4f);
        }
    }
}
