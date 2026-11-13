using NUnit.Framework;
using Studio.Pacman;
using UnityEngine;

public class PacmanSwipeTests
{
    private static PacmanSwipe Started(float threshold = 40f)
    {
        PacmanSwipe swipe = new PacmanSwipe { Threshold = threshold };
        swipe.Begin(new Vector2(100f, 100f));
        return swipe;
    }

    [Test]
    public void ShortDragIsNotASwipe()
    {
        PacmanSwipe swipe = Started();
        Assert.AreEqual(Vector2Int.zero, swipe.Move(new Vector2(120f, 110f)));
    }

    [Test]
    public void DominantAxisWins()
    {
        Assert.AreEqual(Vector2Int.right, Started().Move(new Vector2(190f, 130f)));
        Assert.AreEqual(Vector2Int.left, Started().Move(new Vector2(10f, 130f)));
        Assert.AreEqual(Vector2Int.up, Started().Move(new Vector2(130f, 190f)));
        Assert.AreEqual(Vector2Int.down, Started().Move(new Vector2(130f, 10f)));
    }

    [Test]
    public void OneDragCanTurnSeveralTimes()
    {
        PacmanSwipe swipe = Started();
        Assert.AreEqual(Vector2Int.right, swipe.Move(new Vector2(160f, 100f)));
        Assert.AreEqual(Vector2Int.zero, swipe.Move(new Vector2(165f, 100f)));
        Assert.AreEqual(Vector2Int.down, swipe.Move(new Vector2(165f, 40f)));
    }

    [Test]
    public void NothingHappensWithoutAFinger()
    {
        PacmanSwipe swipe = Started();
        swipe.End();
        Assert.AreEqual(Vector2Int.zero, swipe.Move(new Vector2(500f, 100f)));
    }
}
