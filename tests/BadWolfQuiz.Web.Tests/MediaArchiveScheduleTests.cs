using BadWolfQuiz.Web.Services;

namespace BadWolfQuiz.Web.Tests;

public sealed class MediaArchiveScheduleTests
{
    [Theory]
    [InlineData("2026-08-05T01:00:00+00:00", 24, "03:00:00", "2026-08-05T03:00:00+00:00")]
    [InlineData("2026-08-05T04:00:00+00:00", 24, "03:00:00", "2026-08-06T03:00:00+00:00")]
    [InlineData("2026-08-05T04:00:00+00:00", 12, "03:00:00", "2026-08-05T15:00:00+00:00")]
    [InlineData("2026-08-05T03:00:00+00:00", 24, "03:00:00", "2026-08-06T03:00:00+00:00")]
    public void CalculateNextScanUtc_UsesConfiguredAnchor(
        string now,
        int intervalHours,
        string startTime,
        string expected)
    {
        var actual = MediaArchiveBackgroundService.CalculateNextScanUtc(
            DateTimeOffset.Parse(now),
            TimeSpan.FromHours(intervalHours),
            TimeSpan.Parse(startTime));

        Assert.Equal(DateTimeOffset.Parse(expected), actual);
    }

    [Fact]
    public void Options_RejectStartTimeOutsideUtcDay()
    {
        Assert.False(new MediaArchiveOptions
        {
            ScanStartTimeUtc = TimeSpan.FromHours(24)
        }.IsValid);
    }
}
