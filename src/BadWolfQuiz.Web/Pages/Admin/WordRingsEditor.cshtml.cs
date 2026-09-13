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
    private WordRingsRuleStore Store => WordRingsRuleStore.Get(environment);

    public WordRingColor Ring { get; private set; } = WordRingColor.Blue;
    public string RingKey => ToKey(Ring);
    public IReadOnlyList<WordRingRule> Rules { get; private set; } = [];

    public void OnGet(string? ring)
    {
        Load(ring);
    }

    public async Task<IActionResult> OnPostCreateRuleAsync(
        string? ring,
        string? text,
        string? words,
        CancellationToken cancellationToken)
    {
        var color = ParseRing(ring);
        var result = await Store.AddAsync(color, text, words, cancellationToken);
        return MutationResponse(result, color, "EditorRuleCreated");
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
        return MutationResponse(result, color, "EditorRuleUpdated");
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
            color,
            enabled ? "EditorRuleEnabled" : "EditorRuleDisabled");
    }

    public async Task<IActionResult> OnPostDeleteRuleAsync(
        string? ring,
        Guid ruleId,
        CancellationToken cancellationToken)
    {
        var color = ParseRing(ring);
        var result = await Store.DeleteAsync(ruleId, cancellationToken);
        return MutationResponse(result, color, "EditorRuleDeleted");
    }

    private void Load(string? ring)
    {
        Ring = ParseRing(ring);
        Rules = Store.GetRules(Ring);
    }

    private IActionResult MutationResponse(
        WordRingRuleMutationResult result,
        WordRingColor color,
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
                ring = ToKey(color)
            });
        }

        TempData[success ? "StatusMessage" : "ErrorMessage"] = message;
        return RedirectToPage(new { ring = ToKey(color) });
    }

    private string ErrorMessage(WordRingRuleMutationResult result) => result switch
    {
        WordRingRuleMutationResult.Duplicate => localizer["EditorRuleDuplicate"].Value,
        WordRingRuleMutationResult.InvalidText => localizer["EditorRuleInvalidText"].Value,
        WordRingRuleMutationResult.InvalidWords => localizer["EditorRuleInvalidWords"].Value,
        WordRingRuleMutationResult.NotFound => localizer["EditorRuleNotFound"].Value,
        WordRingRuleMutationResult.LastRule => localizer["EditorLastRuleCannotDelete"].Value,
        WordRingRuleMutationResult.LastEnabledRule => localizer["EditorLastEnabledRule"].Value,
        _ => localizer["EditorRuleInvalidWords"].Value
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
}
