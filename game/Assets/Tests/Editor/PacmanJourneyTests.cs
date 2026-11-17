using NUnit.Framework;
using Studio.Pacman;
using UnityEngine;

public class PacmanJourneyTests
{
    [Test]
    public void EveryStopIsInsideTheCountry()
    {
        for (int level = 1; level <= PacmanJourney.Count; level++)
        {
            PacmanJourney.Stop stop = PacmanJourney.StopFor(level);
            Assert.IsTrue(PacmanJourney.Inside(PacmanJourney.Project(stop)), stop.Region + " (level " + level + ") should be on land");
        }
    }

    [Test]
    public void TheRouteStartsOverAfterTheLastStop()
    {
        Assert.AreEqual(PacmanJourney.StopFor(1).Region, PacmanJourney.StopFor(PacmanJourney.Count + 1).Region);
        Assert.AreEqual(PacmanFarms.Count, PacmanJourney.Count);
    }

    [Test]
    public void SeaIsNotLand()
    {
        // Out in the Pacific and over the Caribbean.
        Assert.IsFalse(PacmanJourney.Inside(PacmanJourney.Project(4f, -82f)));
        Assert.IsFalse(PacmanJourney.Inside(PacmanJourney.Project(13.5f, -75f)));
    }
}
