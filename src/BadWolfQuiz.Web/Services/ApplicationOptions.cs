namespace BadWolfQuiz.Web.Services;

public sealed class QuizEditorOptions
{
    public const string SectionName = "QuizEditor";
    public const int StandardCategoryCount = 6;
    public const int StandardQuestionCount = 5;
    public int MinimumCategoryCount { get; set; } = 3;
    public int MaximumCategoryCount { get; set; } = 7;
    public int MinimumQuestionCount { get; set; } = 5;
    public int MaximumQuestionCount { get; set; } = 10;

    public int InitialCategoryCount => Math.Clamp(
        StandardCategoryCount,
        MinimumCategoryCount,
        MaximumCategoryCount);

    public int InitialQuestionCount => Math.Clamp(
        StandardQuestionCount,
        MinimumQuestionCount,
        MaximumQuestionCount);

    public bool IsValid =>
        MinimumCategoryCount > 0 &&
        MaximumCategoryCount >= MinimumCategoryCount &&
        MinimumQuestionCount > 0 &&
        MaximumQuestionCount >= MinimumQuestionCount;
}

public sealed class SiteDefaultsOptions
{
    public const string SectionName = "SiteDefaults";
    public string Culture { get; set; } = "en";
    public string ThemeId { get; set; } = SiteThemeCatalog.DefaultId;
}

public sealed class MediaProcessingOptions
{
    public const string SectionName = "MediaProcessing";
    public int MaximumImageUploadMegabytes { get; set; } = 5;
    public int MaximumGifUploadMegabytes { get; set; } = 30;
    public int MaximumAudioUploadMegabytes { get; set; } = 5;
    public int MaximumImageWidth { get; set; } = 1920;
    public int MaximumImageHeight { get; set; } = 1080;
    public bool ConvertAudioToMp3 { get; set; } = true;
    public int Mp3BitrateKbps { get; set; } = 128;
    public string FfmpegExecutablePath { get; set; } = "ffmpeg";
    public bool ConvertOpaqueImagesToJpeg { get; set; } = true;
    public int JpegQuality { get; set; } = 85;

    public bool IsValid =>
        MaximumImageUploadMegabytes is >= 1 and <= 1024 &&
        MaximumGifUploadMegabytes is >= 1 and <= 1024 &&
        MaximumAudioUploadMegabytes is >= 1 and <= 1024 &&
        MaximumImageWidth is >= 1 and <= 32768 &&
        MaximumImageHeight is >= 1 and <= 32768 &&
        Mp3BitrateKbps is >= 32 and <= 320 &&
        (!ConvertAudioToMp3 || !string.IsNullOrWhiteSpace(FfmpegExecutablePath)) &&
        JpegQuality is >= 1 and <= 100;
}

public sealed class PremiumHostOptions
{
    public const string SectionName = "PremiumHosts";
    public string[] HostIds { get; set; } = [];
    public int MaximumImageUploadMegabytes { get; set; } = 10;
    public int MaximumGifUploadMegabytes { get; set; } = 50;
    public int MaximumAudioUploadMegabytes { get; set; } = 10;

    public bool IsValid =>
        MaximumImageUploadMegabytes is >= 1 and <= 1024 &&
        MaximumGifUploadMegabytes is >= 1 and <= 1024 &&
        MaximumAudioUploadMegabytes is >= 1 and <= 1024 &&
        HostIds is not null && HostIds.All(hostId =>
            !string.IsNullOrWhiteSpace(hostId) && hostId.Length <= 36);
}

public sealed class ActiveGameOptions
{
    public const string SectionName = "ActiveGames";
    public int ResumeAvailabilityDays { get; set; } = 30;

    public bool IsValid => ResumeAvailabilityDays is >= 1 and <= 3650;
}

public sealed class MinigameOptions
{
    public const string SectionName = "Minigames";
    public int CardCount { get; set; } = 4;

    public bool IsValid => CardCount is >= 1 and <= 100;
}

public sealed class ProjectOptions
{
    public const string SectionName = "Project";
    public string? GitHubUrl { get; set; }

    public string? GetGitHubUrl() => ConfiguredExternalUrl.Normalize(GitHubUrl);
}

public sealed class DiscordInviteOptions
{
    public const string SectionName = "Discord";
    public string? InviteUrl { get; set; }

    public string? GetInviteUrl() => ConfiguredExternalUrl.Normalize(InviteUrl);
}

public sealed class FooterOptions
{
    public const string SectionName = "Footer";
    public const int DefaultContributorDisplayDurationMilliseconds = 2000;
    public string?[]? Contributors { get; set; }
    public string? DonationUrl { get; set; }
    public int ContributorDisplayDurationMilliseconds { get; set; } =
        DefaultContributorDisplayDurationMilliseconds;

    public int EffectiveContributorDisplayDurationMilliseconds =>
        ContributorDisplayDurationMilliseconds is >= 250 and <= 600000
            ? ContributorDisplayDurationMilliseconds
            : DefaultContributorDisplayDurationMilliseconds;

    public IReadOnlyList<string> GetContributors() =>
        (Contributors ?? [])
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    public Uri? GetDonationUri()
    {
        var value = DonationUrl?.Trim();
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return null;
        }

        return uri;
    }
}

public sealed class MediaArchiveOptions
{
    public const string SectionName = "MediaArchive";
    public bool Enabled { get; set; } = true;
    public int ArchiveAfterDays { get; set; } = 180;
    public int ScanIntervalHours { get; set; } = 24;
    public TimeSpan ScanStartTimeUtc { get; set; } = TimeSpan.FromHours(3);
    public int MaximumQuizzesPerRun { get; set; } = 2;
    public int DeleteArchiveCopyAfterRestoreDays { get; set; } = 14;
    public int OrphanRetentionDays { get; set; } = 30;

    public bool IsValid =>
        ArchiveAfterDays > 0 &&
        ScanIntervalHours > 0 &&
        ScanStartTimeUtc >= TimeSpan.Zero &&
        ScanStartTimeUtc < TimeSpan.FromDays(1) &&
        MaximumQuizzesPerRun > 0 &&
        DeleteArchiveCopyAfterRestoreDays >= 0 &&
        OrphanRetentionDays >= 0;
}

internal static class ConfiguredExternalUrl
{
    public static string? Normalize(string? value)
    {
        var normalized = value?.Trim();
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return null;
        }

        return normalized;
    }
}
