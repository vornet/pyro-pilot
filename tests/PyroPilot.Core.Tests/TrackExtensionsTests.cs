using PyroPilot.Core.Model;

namespace PyroPilot.Core.Tests;

public class TrackExtensionsTests
{
    [Theory]
    [InlineData(0, 1000, 500, 1500, true)]   // candidate starts inside existing clip
    [InlineData(500, 1500, 0, 1000, true)]   // candidate ends inside existing clip
    [InlineData(0, 1000, 1000, 2000, false)] // back-to-back, touching but not overlapping
    [InlineData(0, 1000, 2000, 1000, false)] // fully separate
    public void HasOverlap_DetectsTimeRangeOverlap(int existingStart, int existingDuration, int candidateStart, int candidateDuration, bool expectOverlap)
    {
        var track = new Track
        {
            Clips = [new FireCue { StartMs = existingStart, DurationMs = existingDuration }],
        };
        var candidate = new FireCue { StartMs = candidateStart, DurationMs = candidateDuration };

        Assert.Equal(expectOverlap, track.HasOverlap(candidate));
    }

    [Fact]
    public void HasOverlap_IgnoresTheClipBeingMoved()
    {
        var clip = new FireCue { Id = Guid.NewGuid(), StartMs = 0, DurationMs = 1000 };
        var track = new Track { Clips = [clip] };

        // Same clip, just being dragged to a new position -- shouldn't collide with itself.
        var movedCopy = new FireCue { Id = clip.Id, StartMs = 100, DurationMs = 1000 };

        Assert.False(track.HasOverlap(movedCopy));
    }
}

public class ShowTests
{
    [Fact]
    public void ComputeDurationMs_ReturnsLatestClipEndAcrossAllTracks()
    {
        var show = new Show
        {
            Tracks =
            [
                new Track { Clips = [new FireCue { StartMs = 0, DurationMs = 1000 }] },
                new Track { Clips = [new AudioClip { StartMs = 500, DurationMs = 10000 }] },
            ],
        };

        Assert.Equal(10500, show.ComputeDurationMs());
    }

    [Fact]
    public void ComputeDurationMs_ReturnsZero_WhenNoClips()
    {
        Assert.Equal(0, new Show().ComputeDurationMs());
    }
}
