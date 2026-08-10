using System.Reflection;

namespace BadWolfQuiz.Web.Services;

public sealed record ProductVersionSnapshot(string Version, string? Commit);

public static class ProductVersionInfo
{
    public static ProductVersionSnapshot Current { get; } = Create();

    private static ProductVersionSnapshot Create()
    {
        var assembly = typeof(ProductVersionInfo).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        var version = informationalVersion?.Split('+', 2)[0];
        if (string.IsNullOrWhiteSpace(version))
        {
            version = assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        }

        var commit = NormalizeCommit(
            Environment.GetEnvironmentVariable("GIT_COMMIT") ??
            Environment.GetEnvironmentVariable("SOURCE_VERSION") ??
            Environment.GetEnvironmentVariable("GITHUB_SHA"));

        if (commit is null && informationalVersion?.Split('+', 2) is { Length: 2 } parts)
        {
            commit = NormalizeCommit(parts[1]);
        }

        return new ProductVersionSnapshot(version, commit);
    }

    private static string? NormalizeCommit(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        candidate = candidate.Trim();
        if (candidate.Length < 7 || !candidate.All(Uri.IsHexDigit))
        {
            return null;
        }

        return candidate[..Math.Min(7, candidate.Length)];
    }
}
