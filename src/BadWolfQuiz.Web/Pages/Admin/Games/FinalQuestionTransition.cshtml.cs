using System.Globalization;
using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BadWolfQuiz.Web.Pages.Admin.Games;

public sealed class FinalQuestionTransitionModel(
    GameSessionRegistry sessionRegistry,
    CurrentHost currentHost) : PageModel
{
    public Guid GameId { get; private set; }
    public bool Force { get; private set; }
    public IReadOnlyList<ContentBlockSnapshot> Blocks { get; private set; } = [];

    public string Heading => CurrentLanguage switch
    {
        "uk" => "ФІНАЛЬНЕ ПИТАННЯ",
        "it" => "DOMANDA FINALE",
        _ => "FINAL QUESTION"
    };

    public string ProceedToWagersLabel => CurrentLanguage switch
    {
        "uk" => "Перейти до ставок",
        "ru" => "Перейти к ставкам",
        "it" => "Vai alle puntate",
        _ => "Proceed to wagers"
    };

    private static string CurrentLanguage =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    public IActionResult OnGet(Guid id, bool force = false)
    {
        var game = FindOwned(id);
        if (game is null)
        {
            return NotFound();
        }

        if (game.Session.Status != GameSessionStatus.Running ||
            game.Session.Quiz.FinalQuestion is null)
        {
            return RedirectToPage("Lobby", new { id });
        }

        GameId = id;
        Force = force;
        Blocks = game.Session.Quiz.FinalQuestion.DescriptionBlocks
            .Where(block => block.Kind switch
            {
                ContentBlockKind.Text => !string.IsNullOrWhiteSpace(block.TextContent),
                ContentBlockKind.Image => block.FileData is { Length: > 0 } ||
                                          !string.IsNullOrWhiteSpace(block.MediaPath),
                _ => false
            })
            .OrderBy(block => block.SortOrder)
            .ToArray();
        return Page();
    }

    public IActionResult OnGetContentBlock(Guid id, int sourceContentBlockId)
    {
        var game = FindOwned(id);
        if (game?.Session.Quiz.FinalQuestion is null)
        {
            return NotFound();
        }

        var block = game.Session.Quiz.FinalQuestion.DescriptionBlocks
            .SingleOrDefault(item => item.SourceContentBlockId == sourceContentBlockId);

        if (block?.FileData is null ||
            block.FileData.Length == 0 ||
            string.IsNullOrWhiteSpace(block.FileContentType))
        {
            return NotFound();
        }

        return new FileContentResult(block.FileData, block.FileContentType)
        {
            EnableRangeProcessing = true
        };
    }

    private GameSessionRegistration? FindOwned(Guid id) =>
        sessionRegistry.FindOwned(new GameSessionId(id), currentHost.RequiredId);
}
