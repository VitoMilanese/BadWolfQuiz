using System.Text;
using System.Text.Json;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace BadWolfQuiz.Web.Pages.Admin;

[Authorize(Policy = "MasterHost")]
public sealed class WordRingsEditorModel(
    IWebHostEnvironment environment,
    IStringLocalizer<WordRingsResource> localizer) : PageModel
{
    private const int WordPageSize = 25;
    private const int MembershipRulePageSize = 10;
    private const long MaximumCsvImportBytes = 5L * 1024 * 1024;
    private const int MaximumMembershipChanges = 2_000;

    private static readonly JsonSerializerOptions WebJsonOptions =
        new(JsonSerializerDefaults.Web);

    private WordRingsRuleStore Store => WordRingsRuleStore.Get(environment);

    public WordRingColor Ring { get; private set; } = WordRingColor.Blue;
    public string RingKey => ToKey(Ring);
    public bool IsAllWords { get; private set; }
    public string TabKey => IsAllWords ? "words" : RingKey;
    public IReadOnlyList<WordRingRule> Rules { get; private set; } = [];
    public IReadOnlyList<WordRingsWordItem> Words { get; private set; } = [];
    public MinigameEditorPagination WordPagination { get; private set; } =
        MinigameEditorPagination.Create(1, 0, WordPageSize);
    public IReadOnlyList<int?> WordPageNumbers => BuildPageNumbers(
        WordPagination.CurrentPage,
        WordPagination.TotalPages);

    public void OnGet(string? ring, int pageNumber = 1)
    {
        if (string.IsNullOrWhiteSpace(ring) ||
            string.Equals(ring.Trim(), "words", StringComparison.OrdinalIgnoreCase))
        {
            IsAllWords = true;
            var totalWords = Store.GetWordCount();
            WordPagination = MinigameEditorPagination.Create(
                pageNumber,
                totalWords,
                WordPageSize);
            Words = Store.GetWordsPage(WordPagination.Skip, WordPageSize);
            return;
        }

        LoadRing(ring);
    }

    public IActionResult OnGetWordRules(
        string? word,
        string? ring,
        int pageNumber = 1,
        bool includedOnly = false)
    {
        var color = ParseRing(ring);
        var totalRules = Store.GetRuleMembershipCount(color, word, includedOnly);
        var pagination = MinigameEditorPagination.Create(
            pageNumber,
            totalRules,
            MembershipRulePageSize);
        var items = Store.GetRuleMembershipPage(
            color,
            word,
            includedOnly,
            pagination.Skip,
            MembershipRulePageSize);

        return new JsonResult(new
        {
            success = true,
            ring = ToKey(color),
            pageNumber = pagination.CurrentPage,
            totalPages = pagination.TotalPages,
            totalCount = pagination.TotalCount,
            items = items.Select(item => new
            {
                id = item.Id,
                text = item.Text,
                enabled = item.IsEnabled,
                included = item.ContainsWord
            })
        });
    }

    public IActionResult OnGetExportCsv()
    {
        var csv = Store.ExportCsv();
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        var preamble = encoding.GetPreamble();
        var payload = encoding.GetBytes(csv);
        var bytes = new byte[preamble.Length + payload.Length];
        Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
        Buffer.BlockCopy(payload, 0, bytes, preamble.Length, payload.Length);

        return File(
            bytes,
            "text/csv; charset=utf-8",
            "word-rings.csv");
    }

    public async Task<IActionResult> OnPostCreateRuleAsync(
        string? ring,
        string? text,
        string? words,
        CancellationToken cancellationToken)
    {
        var color = ParseRing(ring);
        var result = await Store.AddAsync(color, text, words, cancellationToken);
        return MutationResponse(result, ToKey(color), "EditorRuleCreated");
    }

    public async Task<IActionResult> OnPostUpdateRuleAsync(
        string? ring,
        Guid ruleId,
        string? text,
        string? words,
        CancellationToken cancellationToken)
    {
        var color = ParseRing(ring);
        var result = await Store.UpdateAsync(ruleId, text, words, cancellationToken);
        return MutationResponse(result, ToKey(color), "EditorRuleUpdated");
    }

    public async Task<IActionResult> OnPostSetRuleEnabledAsync(
        string? ring,
        Guid ruleId,
        bool enabled,
        CancellationToken cancellationToken)
    {
        var color = ParseRing(ring);
        var result = await Store.SetEnabledAsync(ruleId, enabled, cancellationToken);
        return MutationResponse(
            result,
            ToKey(color),
            enabled ? "EditorRuleEnabled" : "EditorRuleDisabled");
    }

    public async Task<IActionResult> OnPostDeleteRuleAsync(
        string? ring,
        Guid ruleId,
        CancellationToken cancellationToken)
    {
        var color = ParseRing(ring);
        var result = await Store.DeleteAsync(ruleId, cancellationToken);
        return MutationResponse(result, ToKey(color), "EditorRuleDeleted");
    }

    public async Task<IActionResult> OnPostSaveWordMembershipsAsync(
        string? word,
        string? changesJson,
        CancellationToken cancellationToken)
    {
        List<WordMembershipChangeInput>? changes;
        try
        {
            changes = string.IsNullOrWhiteSpace(changesJson)
                ? null
                : JsonSerializer.Deserialize<List<WordMembershipChangeInput>>(
                    changesJson,
                    WebJsonOptions);
        }
        catch (JsonException)
        {
            changes = null;
        }

        if (changes is null ||
            changes.Count == 0 ||
            changes.Count > MaximumMembershipChanges ||
            changes.Any(change => change.RuleId == Guid.Empty))
        {
            return WordMutationResponse(
                WordRingRuleMutationResult.InvalidWords,
                "EditorWordMembershipSaved");
        }

        var map = new Dictionary<Guid, bool>();
        foreach (var change in changes)
        {
            map[change.RuleId] = change.Included;
        }

        var result = await Store.ApplyWordMembershipChangesAsync(
            word,
            map,
            cancellationToken);
        return WordMutationResponse(result, "EditorWordMembershipSaved");
    }

    public async Task<IActionResult> OnPostDeleteWordAsync(
        string? word,
        CancellationToken cancellationToken)
    {
        var result = await Store.DeleteWordAsync(word, cancellationToken);
        return WordMutationResponse(result, "EditorWordDeleted");
    }

    public async Task<IActionResult> OnPostDeleteAllWordsAsync(
        CancellationToken cancellationToken)
    {
        var result = await Store.DeleteAllWordsAsync(cancellationToken);
        return WordMutationResponse(result, "EditorAllWordsDeleted");
    }

    public async Task<IActionResult> OnPostDeleteAllRulesAsync(
        string? ring,
        CancellationToken cancellationToken)
    {
        var color = ParseRing(ring);
        var result = await Store.DeleteAllRulesAsync(color, cancellationToken);
        return MutationResponse(result, ToKey(color), "EditorAllRulesDeleted");
    }

    public async Task<IActionResult> OnPostImportCsvAsync(
        IFormFile? csvFile,
        CancellationToken cancellationToken)
    {
        if (csvFile is null || csvFile.Length <= 0)
        {
            return ImportResponse(
                success: false,
                WordRingsImportSummary.Empty,
                localizer["EditorCsvFileRequired"].Value);
        }

        if (csvFile.Length > MaximumCsvImportBytes)
        {
            return ImportResponse(
                success: false,
                WordRingsImportSummary.Empty,
                localizer["EditorCsvFileTooLarge"].Value);
        }

        string content;
        await using (var stream = csvFile.OpenReadStream())
        using (var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 16 * 1024,
            leaveOpen: false))
        {
            content = await reader.ReadToEndAsync(cancellationToken);
        }

        var result = await Store.ImportCsvAsync(content, cancellationToken);
        var success = result.Result == WordRingRuleMutationResult.Success;
        return ImportResponse(
            success,
            success ? result.Summary : WordRingsImportSummary.Empty,
            success
                ? localizer["EditorImportCompleted"].Value
                : localizer["EditorCsvInvalid"].Value);
    }

    private void LoadRing(string? ring)
    {
        IsAllWords = false;
        Ring = ParseRing(ring);
        Rules = Store.GetRules(Ring);
    }

    private IActionResult MutationResponse(
        WordRingRuleMutationResult result,
        string tabKey,
        string successMessageKey)
    {
        var success = result == WordRingRuleMutationResult.Success;
        var message = success
            ? localizer[successMessageKey].Value
            : ErrorMessage(result);

        if (IsAjaxRequest())
        {
            return new JsonResult(new
            {
                success,
                message,
                ring = tabKey
            });
        }

        TempData[success ? "StatusMessage" : "ErrorMessage"] = message;
        return RedirectToPage(new { ring = tabKey });
    }

    private IActionResult WordMutationResponse(
        WordRingRuleMutationResult result,
        string successMessageKey)
    {
        var success = result == WordRingRuleMutationResult.Success;
        var message = success
            ? localizer[successMessageKey].Value
            : result switch
            {
                WordRingRuleMutationResult.InvalidWords => localizer["EditorWordInvalid"].Value,
                WordRingRuleMutationResult.LastEnabledRule => localizer["EditorLastEnabledRule"].Value,
                _ => localizer["EditorRequestFailed"].Value
            };

        if (IsAjaxRequest())
        {
            return new JsonResult(new
            {
                success,
                message,
                ring = "words"
            });
        }

        TempData[success ? "StatusMessage" : "ErrorMessage"] = message;
        return RedirectToPage(new { ring = "words" });
    }

    private IActionResult ImportResponse(
        bool success,
        WordRingsImportSummary summary,
        string message)
    {
        if (IsAjaxRequest())
        {
            return new JsonResult(new
            {
                success,
                message,
                summary = new
                {
                    hasChanges = summary.HasChanges,
                    blue = new
                    {
                        wordsAdded = summary.Blue.WordsAdded,
                        rulesCreated = summary.Blue.RulesCreated
                    },
                    yellow = new
                    {
                        wordsAdded = summary.Yellow.WordsAdded,
                        rulesCreated = summary.Yellow.RulesCreated
                    },
                    red = new
                    {
                        wordsAdded = summary.Red.WordsAdded,
                        rulesCreated = summary.Red.RulesCreated
                    }
                }
            });
        }

        TempData[success ? "StatusMessage" : "ErrorMessage"] = message;
        return RedirectToPage(new { ring = "words" });
    }

    private string ErrorMessage(WordRingRuleMutationResult result) => result switch
    {
        WordRingRuleMutationResult.Duplicate => localizer["EditorRuleDuplicate"].Value,
        WordRingRuleMutationResult.InvalidText => localizer["EditorRuleInvalidText"].Value,
        WordRingRuleMutationResult.InvalidWords => localizer["EditorRuleInvalidWords"].Value,
        WordRingRuleMutationResult.InvalidCsv => localizer["EditorCsvInvalid"].Value,
        WordRingRuleMutationResult.NotFound => localizer["EditorRuleNotFound"].Value,
        WordRingRuleMutationResult.LastRule => localizer["EditorLastRuleCannotDelete"].Value,
        WordRingRuleMutationResult.LastEnabledRule => localizer["EditorLastEnabledRule"].Value,
        _ => localizer["EditorRequestFailed"].Value
    };

    private bool IsAjaxRequest() =>
        string.Equals(
            Request.Headers["X-Requested-With"].ToString(),
            "XMLHttpRequest",
            StringComparison.OrdinalIgnoreCase);

    private static WordRingColor ParseRing(string? ring) =>
        ring?.Trim().ToLowerInvariant() switch
        {
            "yellow" => WordRingColor.Yellow,
            "red" => WordRingColor.Red,
            _ => WordRingColor.Blue
        };

    public static string ToKey(WordRingColor ring) => ring switch
    {
        WordRingColor.Yellow => "yellow",
        WordRingColor.Red => "red",
        _ => "blue"
    };

    private static IReadOnlyList<int?> BuildPageNumbers(int currentPage, int totalPages)
    {
        if (totalPages <= 7)
        {
            return Enumerable.Range(1, totalPages).Select(value => (int?)value).ToArray();
        }

        if (currentPage <= 4)
        {
            return [1, 2, 3, 4, 5, null, totalPages];
        }

        if (currentPage >= totalPages - 3)
        {
            return [1, null, totalPages - 4, totalPages - 3, totalPages - 2, totalPages - 1, totalPages];
        }

        return [1, null, currentPage - 1, currentPage, currentPage + 1, null, totalPages];
    }
}

public sealed class WordMembershipChangeInput
{
    public Guid RuleId { get; set; }
    public bool Included { get; set; }
}
