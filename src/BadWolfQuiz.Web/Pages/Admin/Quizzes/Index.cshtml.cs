using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Services;
using BadWolfQuiz.Web.TagHelpers;
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
    ActiveGameAvailability activeGameAvailability,
    QuizPackageService quizPackageService,
    IQuizMediaArchiveService mediaArchiveService,
    CurrentHost currentHost,
    IConfiguration configuration,
    IStringLocalizer<SharedResource> localizer,
    IStringLocalizer<UnfinishedGameResource> unfinishedGameLocalizer) : PageModel
{
    public const string ExportCompletionCookieName = "badwolfquiz-export-complete";
    public const string TransferCompletionCookieName = "badwolfquiz-transfer-complete";

    public IReadOnlyList<Quiz> Quizzes { get; private set; } = [];
    public IReadOnlySet<int> ResumableQuizIds { get; private set; } =
        new HashSet<int>();
    public IReadOnlyDictionary<int, QuizRatingSummary> Ratings { get; private set; } =
        new Dictionary<int, QuizRatingSummary>();
    public IReadOnlyDictionary<int, string> DescriptionLinks { get; private set; } =
        new Dictionary<int, string>();
    public bool CanDisableAutomaticArchiving => IsMasterHost();

    [BindProperty]
    public IFormFile? ImportFile { get; set; }

    public async Task OnGetAsync()
    {
        await LoadQuizzesAsync();
    }

    public IActionResult OnPostContinueGame(int quizId)
    {
        var snapshot = activeGameStore.Find(currentHost.RequiredId, quizId);
        if (snapshot is null || !activeGameAvailability.CanResume(snapshot))
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
        bool replaceUnfinished,
        CancellationToken cancellationToken)
    {
        try
        {
            var unfinished = activeGameStore.Find(
                currentHost.RequiredId,
                quizId);
            if (unfinished is not null &&
                activeGameAvailability.CanResume(unfinished) &&
                !replaceUnfinished)
            {
                TempData["ErrorMessage"] = unfinishedGameLocalizer[
                    "ReplacementConfirmationRequired"].Value;
                return RedirectToPage();
            }

            var session = await gameSessionLauncher.CreateAsync(
                quizId,
                cancellationToken);

            if (session is null)
            {
                var mediaState = await db.Quizzes
                    .AsNoTracking()
                    .Where(quiz => quiz.Id == quizId && !quiz.IsArchived)
                    .Select(quiz => (QuizMediaState?)quiz.MediaState)
                    .SingleOrDefaultAsync(cancellationToken);
                if (!mediaState.HasValue)
                {
                    return NotFound();
                }

                if (mediaState == QuizMediaState.Archived)
                {
                    var restore = await mediaArchiveService.RestoreAsync(
                        quizId,
                        currentHost.RequiredId,
                        cancellationToken);
                    if (!restore.Succeeded)
                    {
                        TempData["ErrorMessage"] = localizer["MediaArchive_RestoreFailed"].Value;
                        return RedirectToPage();
                    }

                    session = await gameSessionLauncher.CreateAsync(
                        quizId,
                        cancellationToken);
                    if (session is null)
                    {
                        return NotFound();
                    }
                }
                else if (mediaState != QuizMediaState.Active)
                {
                    TempData["ErrorMessage"] = localizer["MediaArchive_PublicRestoreUnavailable"].Value;
                    return RedirectToPage();
                }
                else
                {
                    return NotFound();
                }
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

    public async Task<IActionResult> OnPostDeleteUnfinishedGameAsync(
        int quizId,
        CancellationToken cancellationToken)
    {
        var quizExists = await db.Quizzes
            .AsNoTracking()
            .AnyAsync(
                quiz => quiz.Id == quizId && !quiz.IsArchived,
                cancellationToken);
        if (!quizExists)
        {
            return NotFound();
        }

        var snapshot = activeGameStore.Find(currentHost.RequiredId, quizId);
        if (snapshot is null || !activeGameAvailability.CanResume(snapshot))
        {
            return NotFound();
        }

        sessionRegistry.RemoveUnfinished(currentHost.RequiredId, quizId);
        if (!await activeGameStore.RemoveAsync(currentHost.RequiredId, quizId))
        {
            return NotFound();
        }

        TempData["SuccessMessage"] = unfinishedGameLocalizer["Deleted"].Value;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostArchiveMediaAsync(int quizId, CancellationToken cancellationToken)
    {
        var result = await mediaArchiveService.ArchiveAsync(quizId, currentHost.RequiredId, cancellationToken);
        var messageKey = result.Succeeded
            ? "MediaArchive_Archived"
            : result.Code == "no-media"
                ? "MediaArchive_NoMedia"
                : "MediaArchive_Failed";
        TempData[result.Succeeded || result.Code == "no-media"
            ? "SuccessMessage"
            : "ErrorMessage"] =
            localizer[messageKey].Value;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRestoreMediaAsync(int quizId, CancellationToken cancellationToken)
    {
        var result = await mediaArchiveService.RestoreAsync(quizId, currentHost.RequiredId, cancellationToken);
        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = localizer[
            result.Succeeded ? "MediaArchive_Restored" : "MediaArchive_RestoreFailed"].Value;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSetAutomaticArchivingAsync(int quizId, bool prevent, CancellationToken cancellationToken)
    {
        if (!IsMasterHost()) return Forbid();
        var changed = await db.Quizzes.Where(x => x.Id == quizId && !x.IsArchived)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.PreventAutomaticArchiving, prevent), cancellationToken);
        return changed == 1 ? RedirectToPage() : NotFound();
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

    public async Task<IActionResult> OnPostSetPublicationAsync(
        int quizId,
        bool isPublic,
        CancellationToken cancellationToken)
    {
        var quiz = await db.Quizzes.SingleOrDefaultAsync(
            item => item.Id == quizId && !item.IsArchived,
            cancellationToken);
        if (quiz is null)
        {
            return NotFound();
        }

        quiz.IsPublic = isPublic;
        quiz.PublishedAtUtc = isPublic ? DateTime.UtcNow : null;
        quiz.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] = localizer[
            isPublic ? "QuizPublication_Published" : "QuizPublication_Unpublished"].Value;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnGetExportAsync(
        int quizId,
        string? exportToken,
        CancellationToken cancellationToken)
    {
        var succeeded = false;
        try
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
            var result = File(
                package,
                "application/vnd.badwolfquiz+zip",
                safeName + QuizPackageService.FileExtension);
            succeeded = true;
            return result;
        }
        finally
        {
            SignalExportCompletion(exportToken);
            SignalTransferCompletion(exportToken, "export", succeeded);
        }
    }

    [RequestSizeLimit(1100L * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 1050L * 1024 * 1024)]
    public async Task<IActionResult> OnPostImportAsync(
        string? importToken,
        CancellationToken cancellationToken)
    {
        var succeeded = false;
        try
        {
            if (ImportFile is null ||
                !string.Equals(Path.GetExtension(ImportFile.FileName), QuizPackageService.FileExtension, StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = localizer["QuizImport_InvalidFile"].Value;
                return CompleteImport(importToken);
            }

            try
            {
                await using var stream = ImportFile.OpenReadStream();
                await quizPackageService.ImportAsync(
                    stream, ImportFile.Length, currentHost.RequiredId, cancellationToken);
                TempData["SuccessMessage"] = localizer["QuizImport_Success"].Value;
                succeeded = true;
            }
            catch (InvalidDataException)
            {
                TempData["ErrorMessage"] = localizer["QuizImport_InvalidFile"].Value;
            }
            catch (System.Text.Json.JsonException)
            {
                TempData["ErrorMessage"] = localizer["QuizImport_InvalidFile"].Value;
            }

            return CompleteImport(importToken);
        }
        finally
        {
            SignalTransferCompletion(importToken, "import", succeeded);
        }
    }

    private IActionResult CompleteImport(string? importToken) =>
        Guid.TryParse(importToken, out _)
            ? new EmptyResult()
            : RedirectToPage();

    private void SignalExportCompletion(string? exportToken)
    {
        if (!Guid.TryParse(exportToken, out var token))
        {
            return;
        }

        Response.Cookies.Append(
            ExportCompletionCookieName,
            token.ToString("D"),
            CreateCompletionCookieOptions());
    }

    private void SignalTransferCompletion(
        string? transferToken,
        string operation,
        bool succeeded)
    {
        if (!Guid.TryParse(transferToken, out var token))
        {
            return;
        }

        Response.Cookies.Append(
            TransferCompletionCookieName,
            $"{operation}:{token:D}:{(succeeded ? "success" : "failure")}",
            CreateCompletionCookieOptions());
    }

    private CookieOptions CreateCompletionCookieOptions() => new()
    {
        HttpOnly = false,
        IsEssential = true,
        Secure = Request.IsHttps,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        MaxAge = TimeSpan.FromMinutes(5)
    };

    private async Task LoadQuizzesAsync()
    {
        Quizzes = await db.Quizzes
            .AsNoTracking()
            .Include(x => x.Rounds)
            .Where(x => !x.IsArchived)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ToListAsync();

        DescriptionLinks = Quizzes.ToDictionary(
            quiz => quiz.Id,
            quiz =>
            {
                var token = QuizDescriptionLink.CreateToken(
                    quiz.Id,
                    quiz.HostId,
                    quiz.CreatedAtUtc);
                return SeoHeadTagHelper.BuildAbsolutePathUrl(
                    Request,
                    QuizDescriptionLink.BuildPath(quiz.Id, token));
            });

        var quizIds = Quizzes.Select(quiz => quiz.Id).ToArray();
        Ratings = await db.QuizRatings
            .AsNoTracking()
            .Where(rating => quizIds.Contains(rating.QuizId))
            .GroupBy(rating => rating.QuizId)
            .Select(group => new
            {
                QuizId = group.Key,
                Average = group.Average(rating => rating.Score),
                Count = group.Count()
            })
            .ToDictionaryAsync(
                item => item.QuizId,
                item => new QuizRatingSummary(item.Average, item.Count));
        ResumableQuizIds = activeGameStore.GetAll()
            .Where(snapshot =>
                snapshot.HostId == currentHost.RequiredId &&
                activeGameAvailability.CanResume(snapshot))
            .Select(snapshot => snapshot.Quiz.SourceQuizId)
            .ToHashSet();
    }

    private bool IsMasterHost() =>
        !string.IsNullOrWhiteSpace(configuration["MasterHostId"]) &&
        string.Equals(
            currentHost.RequiredId,
            configuration["MasterHostId"]?.Trim(),
            StringComparison.Ordinal);
}

public sealed record QuizRatingSummary(double Average, int Count);
