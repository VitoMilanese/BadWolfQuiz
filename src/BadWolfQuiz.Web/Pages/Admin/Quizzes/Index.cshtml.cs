using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace BadWolfQuiz.Web.Pages.Admin.Quizzes;

public sealed class IndexModel(
    QuizDbContext db,
    GameSessionLauncher gameSessionLauncher,
    GameSessionRegistry sessionRegistry,
    ActiveGameStore activeGameStore,
    QuizPackageService quizPackageService,
    CurrentHost currentHost,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    public IReadOnlyList<Quiz> Quizzes { get; private set; } = [];
    public IReadOnlySet<int> ResumableQuizIds { get; private set; } =
        new HashSet<int>();

    [BindProperty]
    public IFormFile? ImportFile { get; set; }

    public async Task OnGetAsync()
    {
        await LoadQuizzesAsync();
    }

    public IActionResult OnPostContinueGame(int quizId)
    {
        var snapshot = activeGameStore.Find(currentHost.RequiredId, quizId);
        if (snapshot is null)
        {
            return NotFound();
        }

        var game = sessionRegistry.Find(snapshot.SessionState.Id);
        if (game is null)
        {
            game = sessionRegistry.Restore(
                snapshot.PublicCode,
                BadWolfQuiz.Game.Runtime.GameSession.Restore(
                    snapshot.Quiz,
                    snapshot.Settings,
                    snapshot.SessionState),
                snapshot.HostId,
                snapshot.AllowsNewPlayers);
        }

        return RedirectToPage(
            "/Admin/Games/Lobby",
            new { id = game.Session.Id.Value });
    }

    public async Task<IActionResult> OnPostCreateGameAsync(
        int quizId,
        CancellationToken cancellationToken)
    {
        try
        {
            var session = await gameSessionLauncher.CreateAsync(quizId, cancellationToken);

            if (session is null)
            {
                return NotFound();
            }

            return RedirectToPage(
                "/Admin/Games/Lobby",
                new { id = session.Session.Id.Value });
        }
        catch (ArgumentException)
        {
            TempData["ErrorMessage"] = localizer["Error_QuizCannotStart"].Value;
            return RedirectToPage();
        }
        catch (InvalidOperationException)
        {
            TempData["ErrorMessage"] = localizer["Error_QuizCannotStart"].Value;
            return RedirectToPage();
        }
    }

    public async Task<IActionResult> OnPostRenameAsync(
        int quizId,
        string title,
        string? description)
    {
        title = title.Trim();
        description = string.IsNullOrWhiteSpace(description)
            ? null
            : description.Trim();

        if (string.IsNullOrWhiteSpace(title))
        {
            TempData["ErrorMessage"] = localizer["Error_QuizNameRequired"].Value;
            return RedirectToPage();
        }

        if (title.Length > 160)
        {
            TempData["ErrorMessage"] = localizer["Error_QuizNameMaxLength"].Value;
            return RedirectToPage();
        }

        if (description?.Length > 1000)
        {
            TempData["ErrorMessage"] = localizer["Error_QuizDescriptionMaxLength"].Value;
            return RedirectToPage();
        }

        var quiz = await db.Quizzes
            .FirstOrDefaultAsync(x => x.Id == quizId && !x.IsArchived);

        if (quiz is null)
        {
            return NotFound();
        }

        quiz.Title = title;
        quiz.Description = description;
        quiz.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync();

        TempData["SuccessMessage"] = localizer["Message_QuizRenamed"].Value;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int quizId)
    {
        var quiz = await db.Quizzes
            .FirstOrDefaultAsync(x => x.Id == quizId && !x.IsArchived);

        if (quiz is null)
        {
            return NotFound();
        }

        quiz.IsArchived = true;
        quiz.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();

        TempData["SuccessMessage"] = localizer["Message_QuizDeleted"].Value;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnGetExportAsync(int quizId, CancellationToken cancellationToken)
    {
        var package = await quizPackageService.ExportAsync(quizId, cancellationToken);
        if (package is null)
        {
            return NotFound();
        }

        var quiz = await db.Quizzes.AsNoTracking()
            .SingleAsync(item => item.Id == quizId, cancellationToken);
        var safeName = string.Concat(quiz.Title.Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        return File(package, "application/vnd.badwolfquiz+zip", safeName + QuizPackageService.FileExtension);
    }

    [RequestSizeLimit(1100L * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 1050L * 1024 * 1024)]
    public async Task<IActionResult> OnPostImportAsync(CancellationToken cancellationToken)
    {
        if (ImportFile is null ||
            !string.Equals(Path.GetExtension(ImportFile.FileName), QuizPackageService.FileExtension, StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = localizer["QuizImport_InvalidFile"].Value;
            return RedirectToPage();
        }

        try
        {
            await using var stream = ImportFile.OpenReadStream();
            await quizPackageService.ImportAsync(
                stream, ImportFile.Length, currentHost.RequiredId, cancellationToken);
            TempData["SuccessMessage"] = localizer["QuizImport_Success"].Value;
        }
        catch (InvalidDataException)
        {
            TempData["ErrorMessage"] = localizer["QuizImport_InvalidFile"].Value;
        }
        catch (System.Text.Json.JsonException)
        {
            TempData["ErrorMessage"] = localizer["QuizImport_InvalidFile"].Value;
        }

        return RedirectToPage();
    }

    private async Task LoadQuizzesAsync()
    {
        Quizzes = await db.Quizzes
            .AsNoTracking()
            .Include(x => x.Rounds)
            .Where(x => !x.IsArchived)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ToListAsync();
        ResumableQuizIds = activeGameStore.GetAll()
            .Where(snapshot => snapshot.HostId == currentHost.RequiredId)
            .Select(snapshot => snapshot.Quiz.SourceQuizId)
            .ToHashSet();
    }
}
