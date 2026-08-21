using BadWolfQuiz.Web.Services;

namespace BadWolfQuiz.Web.Tests;

public sealed class YouTubeEmbedUrlTests
{
    [Theory]
    [InlineData(
        "https://youtu.be/E387SU9WhWc?t=898",
        "https://www.youtube-nocookie.com/embed/E387SU9WhWc?enablejsapi=1&start=898")]
    [InlineData(
        "https://www.youtube.com/watch?v=E387SU9WhWc&t=898",
        "https://www.youtube-nocookie.com/embed/E387SU9WhWc?enablejsapi=1&start=898")]
    [InlineData(
        "https://www.youtube.com/watch?v=E387SU9WhWc&start=898",
        "https://www.youtube-nocookie.com/embed/E387SU9WhWc?enablejsapi=1&start=898")]
    [InlineData(
        "https://www.youtube.com/embed/E387SU9WhWc?start=898",
        "https://www.youtube-nocookie.com/embed/E387SU9WhWc?enablejsapi=1&start=898")]
    [InlineData(
        "https://youtu.be/E387SU9WhWc?t=14m58s",
        "https://www.youtube-nocookie.com/embed/E387SU9WhWc?enablejsapi=1&start=898")]
    [InlineData(
        "https://www.youtube.com/watch?v=E387SU9WhWc#t=898s",
        "https://www.youtube-nocookie.com/embed/E387SU9WhWc?enablejsapi=1&start=898")]
    public void Timestamp_is_converted_to_embed_start_parameter(
        string source,
        string expected)
    {
        Assert.Equal(expected, YouTubeEmbedUrlBuilder.GetYouTubeEmbedUrl(source));
    }

    [Theory]
    [InlineData(
        "https://youtu.be/E387SU9WhWc",
        "https://www.youtube-nocookie.com/embed/E387SU9WhWc?enablejsapi=1")]
    [InlineData(
        "https://www.youtube.com/watch?v=E387SU9WhWc",
        "https://www.youtube-nocookie.com/embed/E387SU9WhWc?enablejsapi=1")]
    [InlineData(
        "https://www.youtube.com/shorts/E387SU9WhWc",
        "https://www.youtube-nocookie.com/embed/E387SU9WhWc?enablejsapi=1")]
    [InlineData(
        "https://www.youtube.com/embed/E387SU9WhWc",
        "https://www.youtube-nocookie.com/embed/E387SU9WhWc?enablejsapi=1")]
    public void Existing_formats_without_timestamp_still_start_from_beginning(
        string source,
        string expected)
    {
        Assert.Equal(expected, YouTubeEmbedUrlBuilder.GetYouTubeEmbedUrl(source));
    }

    [Fact]
    public void Invalid_timestamp_is_ignored()
    {
        const string source =
            "https://youtu.be/E387SU9WhWc?t=not-a-time";

        Assert.Equal(
            "https://www.youtube-nocookie.com/embed/E387SU9WhWc?enablejsapi=1",
            YouTubeEmbedUrlBuilder.GetYouTubeEmbedUrl(source));
    }

    [Fact]
    public void Razor_pages_use_the_shared_builder()
    {
        var root = FindRepositoryRoot();
        var imports = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "_ViewImports.cshtml"));

        Assert.Contains(
            "@using BadWolfQuiz.Web.Services",
            imports,
            StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
