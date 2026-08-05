using BadWolfQuiz.Web.Services;
using BadWolfQuiz.Web.ViewComponents;
using System.Globalization;
using System.Resources;

namespace BadWolfQuiz.Web.Tests;

public sealed class PortalFooterTests
{
    [Fact]
    public void Contributors_are_trimmed_deduplicated_and_empty_values_are_removed()
    {
        var options = new FooterOptions
        {
            Contributors = [null, "  Alice  ", "", "Alice", "  ", "Bob"]
        };

        Assert.Equal(new[] { "Alice", "Bob" }, options.GetContributors());
    }

    [Fact]
    public void Missing_contributors_produce_an_empty_list()
    {
        Assert.Empty(new FooterOptions().GetContributors());
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(240, 240)]
    [InlineData(-1, FooterOptions.DefaultContributorFadeDurationMilliseconds)]
    [InlineData(5001, FooterOptions.DefaultContributorFadeDurationMilliseconds)]
    public void Contributor_fade_duration_uses_valid_configuration_or_default(
        int configured,
        int expected)
    {
        var options = new FooterOptions
        {
            ContributorFadeDurationMilliseconds = configured
        };

        Assert.Equal(expected, options.EffectiveContributorFadeDurationMilliseconds);
    }

    [Fact]
    public void One_contributor_uses_the_only_index_without_selecting_randomly()
    {
        var selectorCalled = false;
        var model = PortalFooterViewComponent.CreateViewModel(
            new FooterOptions { Contributors = ["Alice"] },
            _ =>
            {
                selectorCalled = true;
                return 0;
            });

        Assert.Equal(0, model.InitialContributorIndex);
        Assert.False(selectorCalled);
    }

    [Fact]
    public void Multiple_contributors_use_the_selected_random_initial_index()
    {
        var receivedCount = 0;
        var model = PortalFooterViewComponent.CreateViewModel(
            new FooterOptions { Contributors = ["Alice", "Bob", "Carol"] },
            count =>
            {
                receivedCount = count;
                return 2;
            });

        Assert.Equal(3, receivedCount);
        Assert.Equal(2, model.InitialContributorIndex);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a url")]
    [InlineData("javascript:alert(1)")]
    [InlineData("/donate")]
    public void Invalid_donation_url_is_hidden(string? value)
    {
        var model = PortalFooterViewComponent.CreateViewModel(
            new FooterOptions { DonationUrl = value },
            _ => 0);

        Assert.Null(model.DonationUrl);
        Assert.Null(model.DonationQrCodeDataUrl);
    }

    [Fact]
    public void Valid_donation_url_is_normalized_and_has_a_generated_qr_code()
    {
        const string donationUrl = "https://example.com/support?project=badwolf";

        var model = PortalFooterViewComponent.CreateViewModel(
            new FooterOptions { DonationUrl = $"  {donationUrl}  " },
            _ => 0);

        Assert.Equal(donationUrl, model.DonationUrl);
        Assert.StartsWith("data:image/png;base64,", model.DonationQrCodeDataUrl);
        Assert.Equal(
            PortalFooterViewComponent.BuildQrCodeDataUrl(donationUrl),
            model.DonationQrCodeDataUrl);
    }

    [Fact]
    public void Different_donation_urls_generate_different_qr_codes()
    {
        var first = PortalFooterViewComponent.BuildQrCodeDataUrl("https://example.com/one");
        var second = PortalFooterViewComponent.BuildQrCodeDataUrl("https://example.com/two");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Footer_texts_are_available_in_every_supported_language()
    {
        var resources = new ResourceManager(
            "BadWolfQuiz.Web.Resources.Localization.SharedResource",
            typeof(PortalFooterViewComponent).Assembly);
        string[] keys =
        [
            "Footer_MadeWith",
            "Footer_SpecialThanks",
            "Footer_FreePlatform",
            "Footer_SupportProject",
            "Footer_DonationTitle",
            "Footer_DonationScanQr",
            "Footer_DonationQrAlt",
            "Footer_OpenDonationPage"
        ];

        foreach (var cultureName in new[] { "en", "uk", "it" })
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            foreach (var key in keys)
            {
                Assert.False(string.IsNullOrWhiteSpace(resources.GetString(key, culture)));
            }
        }
    }
}
