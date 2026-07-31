using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace BadWolfQuiz.Web.Pages.Admin.Settings;

public sealed class IndexModel(
    GameSettingsStore settingsStore,
    AvatarCatalog avatarCatalog,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    [BindProperty]
    public GameSettingsInput Input { get; set; } = new();

    [BindProperty]
    public IFormFile? HostImage { get; set; }

    public bool HasHostImage { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var settings = await settingsStore.LoadAsync(cancellationToken);
        Input = GameSettingsInput.From(settings);
        HasHostImage = settings.HostImageData is not null;
    }

    public async Task<IActionResult> OnGetHostImageAsync(
        CancellationToken cancellationToken)
    {
        var settings = await settingsStore.LoadAsync(cancellationToken);
        Response.Headers.CacheControl = "no-store";
        return settings.HostImageData is not null &&
               !string.IsNullOrWhiteSpace(settings.HostImageContentType)
            ? File(settings.HostImageData, settings.HostImageContentType)
            : NotFound();
    }

    public async Task<IActionResult> OnPostAsync(
        CancellationToken cancellationToken)
    {
        var existing = await settingsStore.LoadAsync(cancellationToken);
        var imageData = existing.HostImageData;
        var imageContentType = existing.HostImageContentType;
        HasHostImage = imageData is not null;

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
            Input.ToRuntimeSettings(imageData, imageContentType),
            cancellationToken);
        TempData["SuccessMessage"] =
            localizer["GameSettings_GlobalSaved"].Value;
        return RedirectToPage();
    }
}
