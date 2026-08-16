using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Http;

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
    public void Recognition_cookie_only_suppresses_the_thank_you_dialog()
    {
        var options = new FooterOptions
        {
            Contributors = ["Vitalii Hanych"]
        };
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie =
            $"{ContributorRecognition.RecognitionCookieName}=1";

        Assert.True(ContributorRecognition.IsContributor(options, "Vitalii Hanych"));
        Assert.False(ContributorRecognition.ShouldShowThankYou(
            options,
            "Vitalii Hanych",
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
            context.Request));
    }

    [Fact]
    public void Recognition_cookie_is_long_lived_and_http_only()
    {
        var context = new DefaultHttpContext();

        ContributorRecognition.MarkThankYouShown(context.Response, secure: true);

        var cookie = context.Response.Headers.SetCookie.ToString();
        Assert.Contains($"{ContributorRecognition.RecognitionCookieName}=1", cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("gold-fang", "gold-fang")]
    [InlineData("MOONLIGHT", "moonlight")]
    [InlineData(" ember ", "ember")]
    [InlineData("unknown", ContributorAvatarFrameCatalog.DefaultId)]
    [InlineData(null, ContributorAvatarFrameCatalog.DefaultId)]
    public void Frame_ids_are_normalized_to_the_catalog(string? value, string expected)
    {
        Assert.Equal(expected, ContributorAvatarFrameCatalog.Normalize(value));
    }

    [Fact]
    public void Host_frame_settings_round_trip_through_game_settings_input()
    {
        var input = new GameSettingsInput
        {
            HostAvatarFrameEnabled = true,
            HostAvatarFrameId = "moonlight"
        };

        Assert.True(input.IsValid);
        var settings = input.ToRuntimeSettings();
        var roundTrip = GameSettingsInput.From(settings);

        Assert.True(settings.HostAvatarFrameEnabled);
        Assert.Equal("moonlight", settings.HostAvatarFrameId);
        Assert.True(roundTrip.HostAvatarFrameEnabled);
        Assert.Equal("moonlight", roundTrip.HostAvatarFrameId);
    }

    [Fact]
    public void Enabled_host_frame_rejects_unknown_frame_id()
    {
        var input = new GameSettingsInput
        {
            HostAvatarFrameEnabled = true,
            HostAvatarFrameId = "not-a-frame"
        };

        Assert.False(input.IsValid);
    }
}
