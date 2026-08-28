namespace BadWolfQuiz.Web.Services;

public static class PeerRatedAllPlayerScoring
{
    public static int CalculateRewardPercentage(double averageStars)
    {
        if (double.IsNaN(averageStars) || double.IsInfinity(averageStars))
        {
            throw new ArgumentOutOfRangeException(nameof(averageStars));
        }

        var stars = Math.Clamp(averageStars, 0, 5);
        if (stars <= 0)
        {
            return 0;
        }

        if (stars >= 5)
        {
            return 100;
        }

        var fullStars = (int)Math.Floor(stars);
        var remainder = stars - fullStars;
        var percentage = fullStars * 20;

        if (remainder >= 0.5)
        {
            percentage += 10;
            remainder -= 0.5;
        }

        if (remainder > 0.000001)
        {
            percentage += 5;
        }

        return Math.Min(100, percentage);
    }

    public static int CalculateAwardedPoints(int questionPoints, double averageStars)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(questionPoints);
        var percentage = CalculateRewardPercentage(averageStars);
        return (int)Math.Round(
            questionPoints * percentage / 100.0,
            MidpointRounding.AwayFromZero);
    }
}
