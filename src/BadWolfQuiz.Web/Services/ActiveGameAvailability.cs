using Microsoft.Extensions.Options;

namespace BadWolfQuiz.Web.Services;

public sealed class ActiveGameAvailability(
    IOptions<ActiveGameOptions> options,
    TimeProvider timeProvider)
{
    private readonly TimeSpan _availability = TimeSpan.FromDays(
        options.Value.ResumeAvailabilityDays);

    public bool CanResume(ActiveGameSnapshot snapshot)
    {
        var savedAtUtc = snapshot.SavedAtUtc ?? snapshot.SessionState.CreatedAtUtc;
        return savedAtUtc >= timeProvider.GetUtcNow() - _availability;
    }
}
