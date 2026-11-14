using NUnit.Framework;
using UnityEngine;

namespace Studio.Pacman.Tests
{
    public class PacmanShoeTests
    {
        [Test]
        public void EveryShoeHasItsOwnShinSprite()
        {
            var seen = new System.Collections.Generic.HashSet<Sprite>();
            for (int shoe = 0; shoe < PacmanShoes.Count; shoe++)
            {
                Sprite sprite = PacmanFarmerArt.Shin(true, PacmanShoes.Colour((Shoe)shoe));
                Assert.IsTrue(seen.Add(sprite), "Shoe " + (Shoe)shoe + " reuses another shoe's sprite");
            }
        }
    }
}
