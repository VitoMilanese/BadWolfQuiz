using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace BadWolfQuiz.Web.Pages.Admin.Games;

public sealed class CreateModel(
    QuizDbContext db,
    GameSettingsStore settingsStore,
    GameSessionLauncher launcher,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int QuizId { get; set; }

    [BindProperty]
    public GameSettingsInput Input { get; set; } = new();

    public string QuizTitle { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(
        CancellationToken cancellationToken)
    {
        if (!await LoadQuizTitleAsync(cancellationToken))
        {
            return NotFound();
        }

        Input = GameSettingsInput.From(
            await settingsStore.LoadAsync(cancellationToken));
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(
        CancellationToken cancellationToken)
    {
        if (!await LoadQuizTitleAsync(cancellationToken))
        {
            return NotFound();
        }

        if (!Input.IsValid)
        {
            ModelState.AddModelError(
                string.Empty,
                localizer["GameSettings_InvalidDuration"].Value);
            return Page();
        }

        try
        {
            var game = await launcher.CreateAsync(
                QuizId,
                Input.ToRuntimeSettings(),
                cancellationToken);

            return game is null
                ? NotFound()
                : RedirectToPage(
                    "/Admin/Games/Lobby",
                    new { id = game.Session.Id.Value });
        }
        catch (ArgumentException)
        {
            ModelState.AddModelError(
                string.Empty,
                localizer["Error_QuizCannotStart"].Value);
            return Page();
        }
        catch (InvalidOperationException)
        {
            ModelState.AddModelError(
                string.Empty,
                localizer["Error_QuizCannotStart"].Value);
            return Page();
        }
    }

    private async Task<bool> LoadQuizTitleAsync(
        CancellationToken cancellationToken)
    {
        QuizTitle = await db.Quizzes
            .AsNoTracking()
            .Where(item => item.Id == QuizId && !item.IsArchived)
            .Select(item => item.Title)
            .SingleOrDefaultAsync(cancellationToken)
            ?? string.Empty;

        return QuizTitle.Length > 0;
    }
}
