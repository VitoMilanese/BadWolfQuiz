using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;

namespace BadWolfQuiz.Web.Tests;

public sealed class ContributorRecognitionTests
{
    [Theory]
    [InlineData("Vitalii Hanych")]
    [InlineData(" vitalii hanych ")]
    [InlineData("VITALII HANYCH")]
    public void Contributor_matching_is_trimmed_and_case_insensitive(string name)
    {
        var options = new FooterOptions
        {
            Contributors = ["Vitalii Hanych", "Other Contributor"]
        };

        Assert.True(ContributorRecognition.IsContributor(options, name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Not a contributor")]
    public void Contributor_matching_rejects_unknown_names(string? name)
    {
        var options = new FooterOptions
        {
            Contributors = ["Vitalii Hanych"]
        };

        Assert.False(ContributorRecognition.IsContributor(options, name));
    }

    [Fact]
    public void Recognition_cookie_only_suppresses_the_same_host()
    {
        var options = new FooterOptions
        {
            Contributors = ["Vitalii Hanych"]
        };
        var context = new DefaultHttpContext();
        const string hostId = "host-1";
        context.Request.Headers.Cookie =
            $"{ContributorRecognition.GetRecognitionCookieName(hostId)}=1";

        Assert.True(ContributorRecognition.IsContributor(options, "Vitalii Hanych"));
        Assert.False(ContributorRecognition.ShouldShowThankYou(
            options,
            "Vitalii Hanych",
            hostId,
            context.Request));
    }

    [Fact]
    public void Recognition_cookie_for_another_host_does_not_suppress_thank_you()
    {
        var options = new FooterOptions
        {
            Contributors = ["Vitalii Hanych"]
        };
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie =
            $"{ContributorRecognition.GetRecognitionCookieName("host-1")}=1";

        Assert.True(ContributorRecognition.ShouldShowThankYou(
            options,
            "Vitalii Hanych",
            "host-2",
            context.Request));
    }

    [Fact]
    public void Legacy_global_recognition_cookie_does_not_suppress_a_host()
    {
        var options = new FooterOptions
        {
            Contributors = ["Vitalii Hanych"]
        };
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie =
            $"{ContributorRecognition.RecognitionCookieName}=1";

        Assert.True(ContributorRecognition.ShouldShowThankYou(
            options,
            "Vitalii Hanych",
            "host-1",
            context.Request));
    }

    [Fact]
    public void Thank_you_is_shown_without_the_recognition_cookie()
    {
        var options = new FooterOptions
        {
            Contributors = ["Vitalii Hanych"]
        };
        var context = new DefaultHttpContext();

        Assert.True(ContributorRecognition.ShouldShowThankYou(
            options,
            "Vitalii Hanych",
            "host-1",
            context.Request));
    }

    [Fact]
    public void Recognition_cookie_is_long_lived_and_http_only()
    {
        var context = new DefaultHttpContext();
        const string hostId = "host-1";

        ContributorRecognition.MarkThankYouShown(
            context.Response,
            hostId,
            secure: true);

        var cookie = context.Response.Headers.SetCookie.ToString();
        Assert.Contains(
            $"{ContributorRecognition.GetRecognitionCookieName(hostId)}=1",
            cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Frame_catalog_reads_avatar_inset_from_file_name()
    {
        var contentRoot = Path.Combine(
            Path.GetTempPath(),
            $"badwolfquiz-frame-catalog-{Guid.NewGuid():N}");
        var frameRoot = Path.Combine(contentRoot, "Resources", "Frames");
        Directory.CreateDirectory(frameRoot);

        try
        {
            File.WriteAllBytes(Path.Combine(frameRoot, "1.png"), [0]);
            File.WriteAllBytes(Path.Combine(frameRoot, "1-10.png"), [0]);
            File.WriteAllBytes(Path.Combine(frameRoot, "25-15.png"), [0]);
            File.WriteAllBytes(Path.Combine(frameRoot, "bonus-5.png"), [0]);
            File.WriteAllBytes(Path.Combine(frameRoot, "legacy.png"), [0]);
            File.WriteAllBytes(Path.Combine(frameRoot, "ignored.jpg"), [0]);

            var environment = new TestWebHostEnvironment
            {
                ContentRootPath = contentRoot
            };
            var frames = ContributorAvatarFrameCatalog.GetFrames(environment);

            Assert.Equal(
                new[] { "1", "25", "bonus", "legacy" },
                frames.Select(frame => frame.Id).ToArray());
            Assert.Equal(
                new[] { 10, 15, 5, ContributorAvatarFrameCatalog.DefaultAvatarInsetPixels },
                frames.Select(frame => frame.AvatarInsetPixels).ToArray());
            Assert.True(ContributorAvatarFrameCatalog.IsValid(environment, "25"));
            Assert.True(ContributorAvatarFrameCatalog.IsValid(environment, "bonus"));
            Assert.False(ContributorAvatarFrameCatalog.IsValid(environment, "missing"));
            Assert.Equal(
                "25-15.png",
                Path.GetFileName(ResolveFramePath(environment, "25")));
            Assert.Equal(
                "1",
                ContributorAvatarFrameCatalog.Normalize(environment, "missing"));
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    [Fact]
    public void Renaming_only_the_inset_suffix_keeps_the_same_frame_id()
    {
        var contentRoot = Path.Combine(
            Path.GetTempPath(),
            $"badwolfquiz-frame-catalog-{Guid.NewGuid():N}");
        var frameRoot = Path.Combine(contentRoot, "Resources", "Frames");
        Directory.CreateDirectory(frameRoot);

        try
        {
            var framePath = Path.Combine(frameRoot, "7-12.png");
            File.WriteAllBytes(framePath, [0]);
            var environment = new TestWebHostEnvironment
            {
                ContentRootPath = contentRoot
            };

            Assert.Equal("7", ContributorAvatarFrameCatalog.Normalize(environment, "7"));
            Assert.Equal(12, ContributorAvatarFrameCatalog.GetAvatarInsetPixels(environment, "7"));

            File.Move(framePath, Path.Combine(frameRoot, "7-6.png"));

            Assert.Equal("7", ContributorAvatarFrameCatalog.Normalize(environment, "7"));
            Assert.Equal(6, ContributorAvatarFrameCatalog.GetAvatarInsetPixels(environment, "7"));
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    [Fact]
    public void Host_frame_settings_round_trip_through_game_settings_input()
    {
        var input = new GameSettingsInput
        {
            HostAvatarFrameEnabled = true,
            HostAvatarFrameId = "12"
        };

        Assert.True(input.IsValid);
        var settings = input.ToRuntimeSettings();
        var roundTrip = GameSettingsInput.From(settings);

        Assert.True(settings.HostAvatarFrameEnabled);
        Assert.Equal("12", settings.HostAvatarFrameId);
        Assert.True(roundTrip.HostAvatarFrameEnabled);
        Assert.Equal("12", roundTrip.HostAvatarFrameId);
    }

    [Fact]
    public void Enabled_host_frame_rejects_unsafe_frame_id()
    {
        var input = new GameSettingsInput
        {
            HostAvatarFrameEnabled = true,
            HostAvatarFrameId = "../12"
        };

        Assert.False(input.IsValid);
    }

    private static string ResolveFramePath(
        IWebHostEnvironment environment,
        string id)
    {
        Assert.True(ContributorAvatarFrameCatalog.TryResolvePath(
            environment,
            id,
            out var path));
        return path;
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "BadWolfQuiz.Web.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
