using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Studio.Pacman.Tests
{
    public class PacmanSayingsTests
    {
        private static readonly SayKind[] Kinds = { SayKind.Walk, SayKind.Power, SayKind.EatPest, SayKind.Hurt, SayKind.Clear };

        [Test]
        public void EveryKindHasLines()
        {
            foreach (SayKind kind in Kinds)
            {
                Assert.GreaterOrEqual(PacmanSayings.Pool(kind).Count, 4, kind.ToString());
            }
            Assert.GreaterOrEqual(PacmanSayings.Pool(SayKind.Walk).Count, 15);
        }

        [Test]
        public void LinesAreShortAndUnique()
        {
            HashSet<string> seen = new HashSet<string>();
            foreach (SayKind kind in Kinds)
            {
                foreach (string line in PacmanSayings.Pool(kind))
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(line));
                    Assert.LessOrEqual(line.Length, PacmanSayings.MaxLength, line);
                    Assert.IsTrue(seen.Add(line), "repeated: " + line);
                }
            }
        }

        [Test]
        public void BagSaysEveryLineBeforeRepeating()
        {
            IReadOnlyList<string> pool = PacmanSayings.Pool(SayKind.Walk);
            PacmanSayings.Bag bag = new PacmanSayings.Bag(pool, new Random(7));
            for (int round = 0; round < 3; round++)
            {
                HashSet<string> round1 = new HashSet<string>();
                for (int i = 0; i < pool.Count; i++)
                {
                    Assert.IsTrue(round1.Add(bag.Next()), "repeated inside a round");
                }
                Assert.AreEqual(pool.Count, round1.Count);
            }
        }

        [Test]
        public void BagNeverSaysTheSameLineTwiceInARow()
        {
            for (int seed = 0; seed < 40; seed++)
            {
                PacmanSayings.Bag bag = new PacmanSayings.Bag(PacmanSayings.Pool(SayKind.Hurt), new Random(seed));
                string last = null;
                for (int i = 0; i < 60; i++)
                {
                    string line = bag.Next();
                    Assert.AreNotEqual(last, line, "seed " + seed + " step " + i);
                    last = line;
                }
            }
        }
    }
}
