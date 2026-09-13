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
        SetMutationMessage(result, deleting: false);
        return RedirectToPage(new { ring = ToKey(color) });
    }

    public async Task<IActionResult> OnPostDeleteRuleAsync(
        string? ring,
        Guid ruleId,
        CancellationToken cancellationToken)
    {
        var color = ParseRing(ring);
        var result = await Store.DeleteAsync(ruleId, cancellationToken);
        SetMutationMessage(result, deleting: true);
        return RedirectToPage(new { ring = ToKey(color) });
    }

    private void Load(string? ring)
    {
        Ring = ParseRing(ring);
        Rules = Store.GetRules(Ring);
    }

    private void SetMutationMessage(
        WordRingRuleMutationResult result,
        bool deleting)
    {
        if (result == WordRingRuleMutationResult.Success)
        {
            TempData["StatusMessage"] = localizer[
                deleting ? "EditorRuleDeleted" : "EditorRuleCreated"].Value;
            return;
        }

        TempData["ErrorMessage"] = result switch
        {
            WordRingRuleMutationResult.Duplicate => localizer["EditorRuleDuplicate"].Value,
            WordRingRuleMutationResult.InvalidText => localizer["EditorRuleInvalidText"].Value,
            WordRingRuleMutationResult.InvalidWords => localizer["EditorRuleInvalidWords"].Value,
            WordRingRuleMutationResult.NotFound => localizer["EditorRuleNotFound"].Value,
            WordRingRuleMutationResult.LastRule => localizer["EditorLastRuleCannotDelete"].Value,
            _ => localizer["EditorRuleInvalidWords"].Value
        };
    }

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
