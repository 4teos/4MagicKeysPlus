using System.Drawing;
using _4MagicKeysPlusAgent;

namespace _4MagicKeysPlusAgentTests;

public class NoticeLayoutTests {
    private static readonly Rectangle Area = new(0, 0, 1920, 1040);
    private static readonly Size Notice = new(400, 60);
    private const int Gap = 8;

    private static Rectangle Place(Size size, params Rectangle[] open) {
        return new Rectangle(NoticeLayout.Place(Area, size, open, Gap), size);
    }

    [Fact]
    public void ANoticeAlone_GoesToTheUsualPlace_CenteredAtThreeQuartersOfTheHeight() {
        Rectangle placed = Place(Notice);

        Assert.Equal((1920 - 400) / 2, placed.X);
        Assert.Equal(1040 * 3 / 4 - 60 / 2, placed.Y);
    }

    [Fact]
    public void ASecondNotice_GoesAboveTheFirst_WithAGap() {
        Rectangle first = Place(Notice);

        Rectangle second = Place(Notice, first);

        Assert.Equal(first.X, second.X);
        Assert.Equal(first.Top - Gap - 60, second.Y);
        Assert.False(first.IntersectsWith(second));
    }

    [Fact]
    public void EveryNoticeOfAStack_IsClearOfTheOthers() {
        List<Rectangle> open = [];
        for (int count = 0; count < 6; count++) {
            Rectangle placed = Place(Notice, [.. open]);
            Assert.DoesNotContain(open, other => other.IntersectsWith(placed));
            open.Add(placed);
        }
    }

    [Fact]
    public void NoticesOfOtherHeights_StayClearOfEachOther() {
        Rectangle tall = Place(new Size(500, 160));
        Rectangle small = Place(new Size(300, 50), tall);
        Rectangle third = Place(new Size(400, 90), tall, small);

        Assert.False(tall.IntersectsWith(small));
        Assert.False(tall.IntersectsWith(third));
        Assert.False(small.IntersectsWith(third));
        Assert.True(small.Bottom + Gap <= tall.Top);
        Assert.True(third.Bottom + Gap <= small.Top);
    }

    [Fact]
    public void WhenTheFirstNoticeCloses_ThePlaceItLeftIsUsedAgain() {
        Rectangle first = Place(Notice);
        Rectangle second = Place(Notice, first);

        Rectangle third = Place(Notice, second);

        Assert.Equal(first, third);
    }

    [Fact]
    public void ANoticeThatDoesNotFitInAHole_GoesAboveTheOnesAroundIt() {
        Rectangle low = new(760, 700, 400, 60);
        Rectangle high = new(760, 700 - 60 - Gap - 20, 400, 60);

        Rectangle placed = Place(new Size(400, 60), low, high);

        Assert.False(placed.IntersectsWith(low));
        Assert.False(placed.IntersectsWith(high));
        Assert.True(placed.Bottom + Gap <= high.Top);
    }

    [Fact]
    public void WithoutRoomAbove_TheNoticeStaysOnTheScreen() {
        Rectangle area = new(0, 0, 800, 200);
        List<Rectangle> open = [];
        for (int count = 0; count < 8; count++) {
            Point at = NoticeLayout.Place(area, Notice, open, Gap);
            Assert.True(at.Y >= area.Top);
            open.Add(new Rectangle(at, Notice));
        }
    }

    [Fact]
    public void ThePlace_FollowsTheWorkAreaOfTheScreen() {
        Rectangle area = new(1920, 40, 1280, 960);

        Point at = NoticeLayout.Place(area, Notice, [], Gap);

        Assert.Equal(1920 + (1280 - 400) / 2, at.X);
        Assert.Equal(40 + 960 * 3 / 4 - 30, at.Y);
    }
}
