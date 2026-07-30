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

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Input = GameSettingsInput.From(
            await settingsStore.LoadAsync(cancellationToken));
    }

    public async Task<IActionResult> OnPostAsync(
        CancellationToken cancellationToken)
    {
        if (!Input.IsValid)
        {
            ModelState.AddModelError(
                string.Empty,
                localizer["GameSettings_InvalidDuration"].Value);
            return Page();
        }

        await settingsStore.SaveAsync(
            Input.ToRuntimeSettings(),
            cancellationToken);
        TempData["SuccessMessage"] =
            localizer["GameSettings_GlobalSaved"].Value;
        return RedirectToPage();
    }
}
