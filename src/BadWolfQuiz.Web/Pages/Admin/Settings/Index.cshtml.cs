using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Hubs;
using Microsoft.AspNetCore.SignalR;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BadWolfQuiz.Web.Pages.Admin.Settings;

public sealed class IndexModel(
    GameSettingsStore settingsStore,
    AvatarCatalog avatarCatalog,
    CurrentHost currentHost,
    QuizDbContext db,
    MediaUploadProcessor mediaUploadProcessor,
    PremiumHostAccess premiumHostAccess,
    GameSessionRegistry sessionRegistry,
    IHubContext<GameHub> gameHub,
    IOptions<FooterOptions> footerOptions,
    IStringLocalizer<SharedResource> localizer,
    IWebHostEnvironment environment) : PageModel
{
    [BindProperty]
    public GameSettingsInput Input { get; set; } = new();

    [BindProperty]
    public IFormFile? HostImage { get; set; }

    [BindProperty]
    public IFormFile? BrandLogo { get; set; }

    [BindProperty]
    public bool RemoveBrandLogo { get; set; }

    public bool HasHostImage { get; private set; }
    public bool HasBrandLogo { get; private set; }
    public bool IsContributor { get; private set; }
    public string HostId => currentHost.RequiredId;
    public int MaximumImageUploadMegabytes =>
        mediaUploadProcessor.MaximumImageUploadMegabytes(
            premiumHostAccess.IsPremium(currentHost.RequiredId));

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var settings = await settingsStore.LoadAsync(currentHost.RequiredId, cancellationToken);
        Input = GameSettingsInput.From(settings);
        Input.HostAvatarFrameId = ContributorAvatarFrameCatalog.Normalize(
            environment,
            Input.HostAvatarFrameId);
        if (Input.HostAvatarFrameEnabled &&
            !ContributorAvatarFrameCatalog.IsValid(environment, Input.HostAvatarFrameId))
        {
            Input.HostAvatarFrameEnabled = false;
        }

        var host = await db.Hosts.SingleAsync(
            item => item.Id == currentHost.RequiredId,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(host.DisplayName) &&
            !string.IsNullOrWhiteSpace(settings.HostName))
        {
            host.DisplayName = settings.HostName.Trim();
            await db.SaveChangesAsync(cancellationToken);
        }

        Input.HostName = host.DisplayName;
        HasHostImage = settings.HostImageData is not null;
        HasBrandLogo = settings.BrandLogoData is not null;
        SetContributorViewData(ContributorRecognition.IsContributor(footerOptions.Value, host.DisplayName));
    }

    public async Task<IActionResult> OnGetBrandLogoAsync(
        CancellationToken cancellationToken)
    {
        var settings = await settingsStore.LoadAsync(currentHost.RequiredId, cancellationToken);
        Response.Headers.CacheControl = "no-store";
        return settings.BrandLogoData is not null &&
               !string.IsNullOrWhiteSpace(settings.BrandLogoContentType)
            ? File(settings.BrandLogoData, settings.BrandLogoContentType)
            : NotFound();
    }

    public async Task<IActionResult> OnGetHostImageAsync(
        CancellationToken cancellationToken)
    {
        var settings = await settingsStore.LoadAsync(currentHost.RequiredId, cancellationToken);
        Response.Headers.CacheControl = "no-store";
        return settings.HostImageData is not null &&
               !string.IsNullOrWhiteSpace(settings.HostImageContentType)
            ? File(settings.HostImageData, settings.HostImageContentType)
            : NotFound();
    }

    public async Task<IActionResult> OnPostAsync(
        CancellationToken cancellationToken)
    {
        var existing = await settingsStore.LoadAsync(currentHost.RequiredId, cancellationToken);
        var imageData = existing.HostImageData;
        var imageContentType = existing.HostImageContentType;
        HasHostImage = imageData is not null;
        var logoData = RemoveBrandLogo ? null : existing.BrandLogoData;
        var logoContentType = RemoveBrandLogo ? null : existing.BrandLogoContentType;
        HasBrandLogo = logoData is not null;
        var host = await db.Hosts.SingleAsync(
            item => item.Id == currentHost.RequiredId,
            cancellationToken);
        SetContributorViewData(ContributorRecognition.IsContributor(footerOptions.Value, host.DisplayName));

        if (!IsContributor)
        {
            Input.HostAvatarFrameEnabled = false;
            Input.HostAvatarFrameId = null;
        }
        else
        {
            Input.HostAvatarFrameId = ContributorAvatarFrameCatalog.Normalize(
                environment,
                Input.HostAvatarFrameId);
            if (Input.HostAvatarFrameEnabled &&
                !ContributorAvatarFrameCatalog.IsValid(environment, Input.HostAvatarFrameId))
            {
                ModelState.AddModelError(
                    string.Empty,
                    localizer["HostCard_InvalidSettings"].Value);
            }
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (HostImage is not null)
        {
            try
            {
                var media = await mediaUploadProcessor.ProcessImageAsync(
                    HostImage,
                    premiumHostAccess.IsPremium(currentHost.RequiredId),
                    cancellationToken);
                imageData = media.Data;
                imageContentType = media.ContentType;
                HasHostImage = true;
                Input.HostVisualSource = BadWolfQuiz.Game.Runtime.HostVisualSource.Image;
            }
            catch (MediaUploadException exception)
            {
                ModelState.AddModelError(
                    string.Empty,
                    localizer[exception.ResourceKey, exception.ResourceArguments]);
                return Page();
            }
        }

        if (BrandLogo is not null)
        {
            try
            {
                var media = await mediaUploadProcessor.ProcessImageAsync(
                    BrandLogo,
                    premiumHostAccess.IsPremium(currentHost.RequiredId),
                    cancellationToken);
                logoData = media.Data;
                logoContentType = media.ContentType;
                HasBrandLogo = true;
            }
            catch (MediaUploadException exception)
            {
                ModelState.AddModelError(
                    string.Empty,
                    localizer[exception.ResourceKey, exception.ResourceArguments]);
                return Page();
            }
        }

        if (!SiteThemeCatalog.IsValid(Input.SiteThemeId) ||
            !SiteThemeCatalog.AreValid(Input.CustomThemeColors))
        {
            ModelState.AddModelError(string.Empty, localizer["SiteTheme_Invalid"].Value);
            return Page();
        }

        if ((Input.HostVisualSource == BadWolfQuiz.Game.Runtime.HostVisualSource.Avatar &&
             !avatarCatalog.IsValid(Input.HostAvatarId)) ||
            (Input.HostVisualSource == BadWolfQuiz.Game.Runtime.HostVisualSource.WebcamUrl &&
             !GameSettingsInput.IsValidWebcamUrl(Input.HostWebcamUrl)))
        {
            ModelState.AddModelError(string.Empty, localizer["HostCard_InvalidSettings"].Value);
            return Page();
        }

        if (!Input.IsValid)
        {
            ModelState.AddModelError(
                string.Empty,
                localizer["GameSettings_InvalidDuration"].Value);
            return Page();
        }

        host.DisplayName = string.IsNullOrWhiteSpace(Input.HostName)
            ? null
            : Input.HostName.Trim();
        Input.HostName = host.DisplayName;
        await db.SaveChangesAsync(cancellationToken);

        // Re-check after a display-name change. A non-contributor cannot gain
        // frame access in the same POST, while a contributor who removes their
        // contributor name loses the frame immediately.
        IsContributor = IsContributor &&
            ContributorRecognition.IsContributor(footerOptions.Value, host.DisplayName);
        if (!IsContributor)
        {
            Input.HostAvatarFrameEnabled = false;
            Input.HostAvatarFrameId = null;
        }

        var savedSettings = Input.ToRuntimeSettings(
            imageData,
            imageContentType,
            logoData,
            logoContentType);
        await settingsStore.SaveAsync(
            currentHost.RequiredId,
            savedSettings,
            cancellationToken);

        var activeGames = sessionRegistry.GetAll()
            .Where(game =>
                string.Equals(
                    game.HostId,
                    currentHost.RequiredId,
                    StringComparison.Ordinal) &&
                game.Session.Status != BadWolfQuiz.Game.Runtime.GameSessionStatus.Completed)
            .ToArray();
        var themeUpdate = GameHub.CreateThemeUpdate(savedSettings);
        var contributorFrameUpdate = new
        {
            enabled = IsContributor && savedSettings.HostAvatarFrameEnabled,
            frameId = IsContributor ? savedSettings.HostAvatarFrameId : null
        };
        foreach (var game in activeGames)
        {
            await gameHub.Clients
                .Group(GameHub.GroupName(game.PublicCode))
                .SendAsync("SiteThemeChanged", themeUpdate, cancellationToken);
            await gameHub.Clients
                .Group(GameHub.GroupName(game.PublicCode))
                .SendAsync(
                    "HostContributorFrameChanged",
                    contributorFrameUpdate,
                    cancellationToken);
        }
        TempData["SuccessMessage"] =
            localizer["GameSettings_GlobalSaved"].Value;
        return RedirectToPage();
    }

    private void SetContributorViewData(bool isContributor)
    {
        IsContributor = isContributor;
        ViewData["ContributorHost"] = isContributor;
    }
}
