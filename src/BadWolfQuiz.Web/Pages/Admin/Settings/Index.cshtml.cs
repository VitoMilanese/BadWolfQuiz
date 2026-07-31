using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace BadWolfQuiz.Web.Pages.Admin.Settings;

public sealed class IndexModel(
    GameSettingsStore settingsStore,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    [BindProperty]
    public GameSettingsInput Input { get; set; } = new();

    [BindProperty]
    public IFormFile? HostImage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Input = GameSettingsInput.From(
            await settingsStore.LoadAsync(cancellationToken));
    }

    public async Task<IActionResult> OnPostAsync(
        CancellationToken cancellationToken)
    {
        var existing = await settingsStore.LoadAsync(cancellationToken);
        var imageData = existing.HostImageData;
        var imageContentType = existing.HostImageContentType;

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
            Input.HostVisualSource = BadWolfQuiz.Game.Runtime.HostVisualSource.Image;
        }

        if (!Input.IsValid)
        {
            ModelState.AddModelError(
                string.Empty,
                localizer["GameSettings_InvalidDuration"].Value);
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
