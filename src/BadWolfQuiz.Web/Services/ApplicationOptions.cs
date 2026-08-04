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
    public int MaximumImageUploadMegabytes { get; set; } = 10;
    public int MaximumAudioUploadMegabytes { get; set; } = 10;
    public int MaximumImageWidth { get; set; } = 3840;
    public int MaximumImageHeight { get; set; } = 2160;
    public bool ConvertAudioToMp3 { get; set; } = true;
    public int Mp3BitrateKbps { get; set; } = 128;
    public string FfmpegExecutablePath { get; set; } = "ffmpeg";
    public bool ConvertOpaqueImagesToJpeg { get; set; } = true;
    public int JpegQuality { get; set; } = 85;

    public bool IsValid =>
        MaximumImageUploadMegabytes is >= 1 and <= 1024 &&
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

    public bool IsValid => HostIds is not null && HostIds.All(hostId =>
        !string.IsNullOrWhiteSpace(hostId) && hostId.Length <= 36);
}
