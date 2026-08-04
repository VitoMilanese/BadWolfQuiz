using Microsoft.Extensions.Options;

namespace BadWolfQuiz.Web.Services;

public sealed class PremiumHostAccess(IOptions<PremiumHostOptions> options)
{
    private readonly HashSet<string> _hostIds = (options.Value.HostIds ?? [])
        .Select(hostId => hostId.Trim())
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public bool IsPremium(string hostId) => _hostIds.Contains(hostId);
}
