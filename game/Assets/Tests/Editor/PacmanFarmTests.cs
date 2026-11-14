using System.Collections.Generic;
using NUnit.Framework;

namespace Studio.Pacman.Tests
{
    public class PacmanFarmTests
    {
        [Test]
        public void FirstFarmIsLaEsmeralda()
        {
            Assert.AreEqual("Finca La Esmeralda", PacmanFarms.NameFor(1));
        }

        [Test]
        public void EveryNameIsRealisticAndDifferent()
        {
            var seen = new HashSet<string>();
            for (int level = 1; level <= PacmanFarms.Count; level++)
            {
                string name = PacmanFarms.NameFor(level);
                Assert.IsFalse(string.IsNullOrWhiteSpace(name));
                Assert.IsTrue(name.StartsWith("Finca ") || name.StartsWith("Hacienda "), name);
                Assert.IsFalse(char.IsDigit(name[name.Length - 1]), "a name, not 'FINCA 1': " + name);
                Assert.IsTrue(seen.Add(name), "duplicate " + name);
            }
        }

        [Test]
        public void NamesStartOverAfterTheList()
        {
            Assert.AreEqual(PacmanFarms.NameFor(1), PacmanFarms.NameFor(1 + PacmanFarms.Count));
            Assert.AreEqual(PacmanFarms.NameFor(PacmanFarms.Count), PacmanFarms.NameFor(2 * PacmanFarms.Count));
        }

        [Test]
        public void NonsenseLevelsStillGiveAName()
        {
            Assert.IsNotEmpty(PacmanFarms.NameFor(0));
            Assert.IsNotEmpty(PacmanFarms.NameFor(-5));
        }
    }
}
