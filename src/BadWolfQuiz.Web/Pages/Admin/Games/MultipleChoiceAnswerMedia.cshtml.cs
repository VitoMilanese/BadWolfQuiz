using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BadWolfQuiz.Web.Pages.Admin.Games;

public sealed class MultipleChoiceAnswerMediaModel(
    GameSessionRegistry sessionRegistry,
    CurrentHost currentHost,
    DeferredGameMediaStore mediaStore) : PageModel
{
    public IActionResult OnGet() => NotFound();

    public async Task<IActionResult> OnGetContentBlockAsync(
        Guid id,
        int sourceQuestionId,
        int sourceContentBlockId,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.FindOwned(
            new GameSessionId(id),
            currentHost.RequiredId);
        if (game is null)
        {
            return NotFound();
        }

        var definition = game.Session.Quiz.Rounds
            .SelectMany(round => round.Questions)
            .SingleOrDefault(question =>
                question.SourceQuestionId == sourceQuestionId);
        var block = definition?.RevealAnswerBlocks.SingleOrDefault(item =>
            item.SourceContentBlockId == sourceContentBlockId);
        if (block is null)
        {
            return NotFound();
        }

        var media = await mediaStore.ResolveAsync(
            game.Session.Id.Value,
            game.Session.Quiz,
            DeferredGameMediaRole.Answer,
            block,
            cancellationToken);
        if (media is null)
        {
            return NotFound();
        }

        return new FileContentResult(media.Data, media.ContentType)
        {
            EnableRangeProcessing = true
        };
    }
}
