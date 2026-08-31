using System.Text;
using System.Text.Json;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace BadWolfQuiz.Web.Pages.Admin;

[Authorize(Policy = "MasterHost")]
public sealed class MinigameEditorModel(
    IDbContextFactory<QuizDbContext> dbFactory,
    IOptions<MinigameOptions> options,
    IStringLocalizer<MinigameEditorResource> localizer) : PageModel
{
    private const long MaximumImageBytes = 5L * 1024 * 1024;
    private const long MaximumAnswerImportBytes = 2L * 1024 * 1024;

    public string Section { get; private set; } = "games";
    public MinigameCatalogCounts Counts { get; private set; } = new(0, 0);
    public IReadOnlyList<MinigameCatalogGameItem> Games { get; private set; } = [];
    public IReadOnlyList<MinigameCatalogQuestionItem> Questions { get; private set; } = [];
    public IReadOnlySet<int> DisabledQuestionIds { get; private set; } = new HashSet<int>();
    public IReadOnlyList<MinigameCatalogAnswerItem> AnswerItems { get; private set; } = [];
    public MinigameCatalogGameItem? SelectedGame { get; private set; }

    private MinigameCatalogStore Store =>
        new(dbFactory, options.Value.CardCount);

    private MinigameQuestionAvailabilityStore QuestionAvailability =>
        new(dbFactory);

    public async Task<IActionResult> OnGetAsync(
        string? section,
        int? gameId,
        CancellationToken cancellationToken)
    {
        Section = NormalizeSection(section);
        Counts = await Store.GetCountsAsync(cancellationToken);

        if (Section == "games")
        {
            Games = await Store.GetGamesAsync(cancellationToken);
        }
        else if (Section == "questions")
        {
            Questions = await Store.GetQuestionItemsAsync(cancellationToken);
            DisabledQuestionIds = await QuestionAvailability.GetDisabledQuestionIdsAsync(
                cancellationToken);
        }
        else
        {
            Games = await Store.GetGamesAsync(cancellationToken);
            var selectedId = gameId ?? Games.FirstOrDefault()?.Id;
            if (selectedId is int id)
            {
                SelectedGame = Games.FirstOrDefault(game => game.Id == id);
                if (SelectedGame is not null)
                {
                    AnswerItems = await Store.GetAnswerItemsAsync(id, cancellationToken);
                }
            }
        }

        return Page();
    }

    public async Task<IActionResult> OnGetGameImageAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var image = await Store.GetGameImageAsync(id, cancellationToken);
        if (image is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        return File(image.Data, image.ContentType);
    }

    public async Task<IActionResult> OnGetExportAnswersAsync(
        int gameId,
        CancellationToken cancellationToken)
    {
        var game = await Store.GetGameAsync(gameId, cancellationToken);
        if (game is null)
        {
            Error(localizer["GameNotFound"]);
            return RedirectToAnswers(gameId);
        }

        var answers = await Store.GetAnswerItemsAsync(gameId, cancellationToken);
        if (answers.Count == 0 || answers.Any(answer => !answer.AnswerYes.HasValue))
        {
            Error(localizer["AnswersInvalid"]);
            return RedirectToAnswers(gameId);
        }

        var content = string.Join(
            Environment.NewLine,
            answers.Select(answer => answer.AnswerYes == true ? "1" : "0")) +
            Environment.NewLine;
        return File(
            Encoding.UTF8.GetBytes(content),
            "text/plain; charset=utf-8",
            BuildAnswerExportFileName(game.Name));
    }

    public async Task<IActionResult> OnPostCreateGameAsync(
        string? name,
        IFormFile? image,
        CancellationToken cancellationToken)
    {
        var upload = await ReadImageAsync(image, required: true, cancellationToken);
        if (!upload.Success)
        {
            Error(upload.ErrorMessage!);
            return RedirectToSection("games");
        }

        var result = await Store.CreateGameAsync(
            name,
            upload.Data,
            upload.ContentType,
            cancellationToken);
        SetMutationMessage(
            result,
            localizer["GameCreated"],
            localizer["GameDuplicate"],
            localizer["GameInvalid"]);
        return RedirectToSection("games");
    }

    public async Task<IActionResult> OnPostUpdateGameAsync(
        int gameId,
        string? name,
        IFormFile? image,
        CancellationToken cancellationToken)
    {
        var upload = await ReadImageAsync(image, required: false, cancellationToken);
        if (!upload.Success)
        {
            Error(upload.ErrorMessage!);
            return RedirectToSection("games");
        }

        var result = await Store.UpdateGameAsync(
            gameId,
            name,
            upload.Data,
            upload.ContentType,
            cancellationToken);
        SetMutationMessage(
            result,
            localizer["GameUpdated"],
            localizer["GameDuplicate"],
            localizer["GameInvalid"],
            localizer["GameNotFound"]);
        return RedirectToSection("games");
    }

    public async Task<IActionResult> OnPostDeleteGameAsync(
        int gameId,
        CancellationToken cancellationToken)
    {
        if (await Store.DeleteGameAsync(gameId, cancellationToken))
        {
            Success(localizer["GameDeleted"]);
        }
        else
        {
            Error(localizer["GameNotFound"]);
        }
        return RedirectToSection("games");
    }

    public async Task<IActionResult> OnPostCreateQuestionAsync(
        string? text,
        CancellationToken cancellationToken)
    {
        var result = await Store.CreateQuestionAsync(text, cancellationToken);
        SetMutationMessage(
            result,
            localizer["QuestionCreated"],
            localizer["QuestionDuplicate"],
            localizer["QuestionInvalid"]);
        return RedirectToSection("questions");
    }

    public async Task<IActionResult> OnPostUpdateQuestionAsync(
        int questionId,
        string? text,
        bool enabled,
        CancellationToken cancellationToken)
    {
        var result = await Store.UpdateQuestionAsync(questionId, text, cancellationToken);
        if (result == MinigameCatalogMutationResult.Success &&
            !await QuestionAvailability.SetEnabledAsync(
                questionId,
                enabled,
                cancellationToken))
        {
            result = MinigameCatalogMutationResult.NotFound;
        }

        SetMutationMessage(
            result,
            localizer["QuestionUpdated"],
            localizer["QuestionDuplicate"],
            localizer["QuestionInvalid"],
            localizer["QuestionNotFound"]);
        return RedirectToSection("questions");
    }

    public async Task<IActionResult> OnPostDeleteQuestionAsync(
        int questionId,
        CancellationToken cancellationToken)
    {
        if (await Store.DeleteQuestionAsync(questionId, cancellationToken))
        {
            Success(localizer["QuestionDeleted"]);
        }
        else
        {
            Error(localizer["QuestionNotFound"]);
        }
        return RedirectToSection("questions");
    }

    public async Task<IActionResult> OnPostSaveAnswersAsync(
        int gameId,
        string? answersJson,
        CancellationToken cancellationToken)
    {
        List<MinigameAnswerInput>? answers;
        try
        {
            answers = string.IsNullOrWhiteSpace(answersJson)
                ? null
                : JsonSerializer.Deserialize<List<MinigameAnswerInput>>(answersJson);
        }
        catch (JsonException)
        {
            return AnswerSaveError(localizer["AnswersInvalid"]);
        }

        if (answers is null)
        {
            return AnswerSaveError(localizer["AnswersInvalid"]);
        }

        var values = new Dictionary<int, bool?>();
        foreach (var row in answers)
        {
            if (row.QuestionId <= 0 || values.ContainsKey(row.QuestionId))
            {
                return AnswerSaveError(localizer["AnswersInvalid"]);
            }

            bool? value = row.Value switch
            {
                "1" => true,
                "0" => false,
                "" or null => null,
                _ => null
            };
            if (row.Value is not ("1" or "0" or "" or null))
            {
                return AnswerSaveError(localizer["AnswersInvalid"]);
            }

            values[row.QuestionId] = value;
        }

        var questions = await Store.GetQuestionItemsAsync(cancellationToken);
        if (values.Count != questions.Count ||
            questions.Any(question => !values.ContainsKey(question.Id)))
        {
            return AnswerSaveError(localizer["AnswersInvalid"]);
        }

        var result = await Store.SaveAnswersAsync(gameId, values, cancellationToken);
        return result switch
        {
            MinigameCatalogMutationResult.Success => new JsonResult(new
            {
                success = true,
                assignedAnswerCount = values.Values.Count(value => value.HasValue)
            }),
            MinigameCatalogMutationResult.NotFound => NotFound(new
            {
                success = false,
                message = localizer["GameNotFound"].Value
            }),
            _ => AnswerSaveError(localizer["AnswersInvalid"])
        };
    }

    public async Task<IActionResult> OnPostImportAnswersAsync(
        int gameId,
        IFormFile? answerFile,
        CancellationToken cancellationToken)
    {
        if (answerFile is null || answerFile.Length <= 0)
        {
            Error(localizer["AnswerFileRequired"]);
            return RedirectToAnswers(gameId);
        }
        if (answerFile.Length > MaximumAnswerImportBytes)
        {
            Error(localizer["AnswerFileTooLarge"]);
            return RedirectToAnswers(gameId);
        }

        string content;
        await using (var stream = answerFile.OpenReadStream())
        using (var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true))
        {
            content = await reader.ReadToEndAsync(cancellationToken);
        }

        var questions = await Store.GetQuestionItemsAsync(cancellationToken);
        var parsed = MinigameAnswerImportParser.Parse(content, questions.Count);
        if (!parsed.Success)
        {
            if (parsed.InvalidLineNumber > 0)
            {
                Error(localizer["AnswerFileInvalidLine", parsed.InvalidLineNumber]);
            }
            else
            {
                Error(localizer[
                    "AnswerFileWrongCount",
                    parsed.ExpectedCount,
                    parsed.ActualCount]);
            }
            return RedirectToAnswers(gameId);
        }

        var result = await Store.ReplaceAnswersAsync(
            gameId,
            parsed.Answers,
            cancellationToken);
        SetMutationMessage(
            result,
            localizer["AnswersImported"],
            localizer["AnswersInvalid"],
            localizer["AnswersInvalid"],
            localizer["GameNotFound"]);
        return RedirectToAnswers(gameId);
    }

    private async Task<ImageUploadResult> ReadImageAsync(
        IFormFile? image,
        bool required,
        CancellationToken cancellationToken)
    {
        if (image is null || image.Length <= 0)
        {
            return required
                ? ImageUploadResult.Failure(localizer["ImageRequired"])
                : ImageUploadResult.Empty;
        }
        if (image.Length > MaximumImageBytes)
        {
            return ImageUploadResult.Failure(localizer["ImageTooLarge"]);
        }
        if (image.ContentType is not
            ("image/png" or "image/jpeg" or "image/webp" or "image/gif"))
        {
            return ImageUploadResult.Failure(localizer["ImageInvalidType"]);
        }

        await using var input = image.OpenReadStream();
        await using var output = new MemoryStream((int)image.Length);
        await input.CopyToAsync(output, cancellationToken);
        return new ImageUploadResult(true, output.ToArray(), image.ContentType, null);
    }

    private IActionResult AnswerSaveError(string message) =>
        BadRequest(new { success = false, message });

    private void SetMutationMessage(
        MinigameCatalogMutationResult result,
        string success,
        string duplicate,
        string invalid,
        string? notFound = null)
    {
        switch (result)
        {
            case MinigameCatalogMutationResult.Success:
                Success(success);
                break;
            case MinigameCatalogMutationResult.Duplicate:
                Error(duplicate);
                break;
            case MinigameCatalogMutationResult.NotFound:
                Error(notFound ?? invalid);
                break;
            default:
                Error(invalid);
                break;
        }
    }

    private IActionResult RedirectToSection(string section) =>
        RedirectToPage(new { section });

    private IActionResult RedirectToAnswers(int gameId) =>
        RedirectToPage(new { section = "answers", gameId });

    private void Success(string message) => TempData["StatusMessage"] = message;

    private void Error(string message) => TempData["ErrorMessage"] = message;

    private static string BuildAnswerExportFileName(string gameName)
    {
        var invalid = Path.GetInvalidFileNameChars()
            .Concat(new[] { '/', '\\' })
            .ToHashSet();
        var safeName = new string(
            gameName.Select(character => invalid.Contains(character) ? '_' : character)
                .ToArray())
            .Trim();

        return $"{(string.IsNullOrWhiteSpace(safeName) ? "answers" : safeName)}.txt";
    }

    private static string NormalizeSection(string? section) =>
        section?.Trim().ToLowerInvariant() switch
        {
            "questions" => "questions",
            "answers" => "answers",
            _ => "games"
        };

    private sealed record ImageUploadResult(
        bool Success,
        byte[]? Data,
        string? ContentType,
        string? ErrorMessage)
    {
        public static ImageUploadResult Empty { get; } = new(true, null, null, null);
        public static ImageUploadResult Failure(string message) =>
            new(false, null, null, message);
    }
}

public sealed class MinigameAnswerInput
{
    public int QuestionId { get; set; }
    public string? Value { get; set; }
}
