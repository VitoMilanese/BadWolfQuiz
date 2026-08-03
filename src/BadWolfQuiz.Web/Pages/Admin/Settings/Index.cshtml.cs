using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace BadWolfQuiz.Web.Pages.Admin.Settings;

public sealed class IndexModel(
    GameSettingsStore settingsStore,
    AvatarCatalog avatarCatalog,
    CurrentHost currentHost,
    IStringLocalizer<SharedResource> localizer) : PageModel
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

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var settings = await settingsStore.LoadAsync(currentHost.RequiredId, cancellationToken);
        Input = GameSettingsInput.From(settings);
        HasHostImage = settings.HostImageData is not null;
        HasBrandLogo = settings.BrandLogoData is not null;
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

        if (HostImage is not null)
        {
            if (!HostImage.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
                HostImage.Length is <= 0 or > 5 * 1024 * 1024)
            {
                ModelState.AddModelError(string.Empty, localizer["HostCard_InvalidImage"].Value);
                return Page();
            }

            await using var stream = new MemoryStream();
            await HostImage.CopyToAsync(stream, cancellationToken);
            imageData = stream.ToArray();
            imageContentType = HostImage.ContentType;
            HasHostImage = true;
            Input.HostVisualSource = BadWolfQuiz.Game.Runtime.HostVisualSource.Image;
        }

        if (BrandLogo is not null)
        {
            if (!BrandLogo.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
                BrandLogo.Length is <= 0 or > 5 * 1024 * 1024)
            {
                ModelState.AddModelError(string.Empty, localizer["BrandLogo_InvalidImage"].Value);
                return Page();
            }

            await using var stream = new MemoryStream();
            await BrandLogo.CopyToAsync(stream, cancellationToken);
            logoData = stream.ToArray();
            logoContentType = BrandLogo.ContentType;
            HasBrandLogo = true;
        }

        if (!SiteThemeCatalog.IsValid(Input.SiteThemeId) ||
            !SiteThemeCatalog.AreValid(Input.CustomThemeColors))
        {
            ModelState.AddModelError(string.Empty, localizer["SiteTheme_Invalid"].Value);
            return Page();
        }

        if (!Input.IsValid)
        {
            ModelState.AddModelError(
                string.Empty,
                localizer["GameSettings_InvalidDuration"].Value);
            return Page();
        }

        if (Input.HostVisualSource == BadWolfQuiz.Game.Runtime.HostVisualSource.Avatar &&
            !avatarCatalog.IsValid(Input.HostAvatarId))
        {
            ModelState.AddModelError(string.Empty, localizer["HostCard_InvalidSettings"].Value);
            return Page();
        }

        await settingsStore.SaveAsync(
            currentHost.RequiredId,
            Input.ToRuntimeSettings(
                imageData,
                imageContentType,
                logoData,
                logoContentType),
            cancellationToken);
        TempData["SuccessMessage"] =
            localizer["GameSettings_GlobalSaved"].Value;
        return RedirectToPage();
    }
}
