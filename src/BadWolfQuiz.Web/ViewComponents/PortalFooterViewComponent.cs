using System.Security.Cryptography;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using QRCoder;

namespace BadWolfQuiz.Web.ViewComponents;

public sealed class PortalFooterViewComponent(IOptions<FooterOptions> options) : ViewComponent
{
    internal const string GitHubRepositoryUrl = "https://github.com/VitoMilanese/BadWolfQuiz";

    public IViewComponentResult Invoke()
        => View(CreateViewModel(
            options.Value,
            count => RandomNumberGenerator.GetInt32(count)));

    internal static PortalFooterViewModel CreateViewModel(
        FooterOptions options,
        Func<int, int> selectInitialIndex)
    {
        var contributors = options.GetContributors();
        var donationUri = options.GetDonationUri();
        var initialIndex = contributors.Count > 1
            ? selectInitialIndex(contributors.Count)
            : 0;

        return new PortalFooterViewModel(
            contributors,
            initialIndex,
            donationUri?.AbsoluteUri,
            donationUri is null ? null : BuildQrCodeDataUrl(donationUri.AbsoluteUri),
            GitHubRepositoryUrl,
            options.EffectiveContributorDisplayDurationMilliseconds);
    }

    internal static string BuildQrCodeDataUrl(string donationUrl)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(
            donationUrl,
            QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(data);
        return $"data:image/png;base64,{Convert.ToBase64String(qrCode.GetGraphic(12))}";
    }
}

public sealed record PortalFooterViewModel(
    IReadOnlyList<string> Contributors,
    int InitialContributorIndex,
    string? DonationUrl,
    string? DonationQrCodeDataUrl,
    string GitHubRepositoryUrl,
    int ContributorDisplayDurationMilliseconds);
