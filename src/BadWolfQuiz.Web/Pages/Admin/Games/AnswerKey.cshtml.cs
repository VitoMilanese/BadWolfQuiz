using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BadWolfQuiz.Web.Pages.Admin.Games;

public sealed class AnswerKeyModel(
    GameSessionRegistry sessionRegistry,
    CurrentHost currentHost,
    DeferredGameMediaStore mediaStore) : PageModel
{
    public GameSessionRegistration Game { get; private set; } = null!;

    public IReadOnlyList<ContentBlockSnapshot> AnswerBlocks { get; private set; } = [];

    public bool IsFinalQuestion { get; private set; }

    public int? CurrentSourceQuestionId { get; private set; }

    public IActionResult OnGet(Guid id)
    {
        var game = sessionRegistry.FindOwned(new GameSessionId(id), currentHost.RequiredId);

        if (game is null)
        {
            return NotFound();
        }

        Game = game;

        var isFinalStatus = game.Session.Status is
            GameSessionStatus.FinalWagering or
            GameSessionStatus.FinalAnswering or
            GameSessionStatus.FinalJudging or
            GameSessionStatus.Completed;

        if (isFinalStatus &&
            game.Session.FinalQuestion is { } finalQuestion)
        {
            IsFinalQuestion = true;
            AnswerBlocks = finalQuestion.Definition.AnswerBlocks;
            return Page();
        }

        if (game.Session.Status != GameSessionStatus.Running)
        {
            return Page();
        }

        var question = game.Session.Board.Questions.FirstOrDefault(item =>
            item.Status is not RuntimeQuestionStatus.Available and
                not RuntimeQuestionStatus.Resolved);

        if (question is null)
        {
            return Page();
        }

        CurrentSourceQuestionId = question.SourceQuestionId;
        AnswerBlocks = GetVisibleAnswerBlocks(game, question);
        return Page();
    }

    public async Task<IActionResult> OnGetContentBlockAsync(
        Guid id,
        int sourceContentBlockId,
        bool final,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.FindOwned(new GameSessionId(id), currentHost.RequiredId);

        if (game is null)
        {
            return NotFound();
        }

        IReadOnlyList<ContentBlockSnapshot>? blocks;
        if (final)
        {
            blocks = game.Session.FinalQuestion?.Definition.AnswerBlocks;
        }
        else
        {
            var question = game.Session.Board.Questions.FirstOrDefault(item =>
                item.Status is not RuntimeQuestionStatus.Available and
                    not RuntimeQuestionStatus.Resolved);
            blocks = question is null
                ? null
                : GetVisibleAnswerBlocks(game, question);
        }

        var block = blocks?.SingleOrDefault(item =>
            item.SourceContentBlockId == sourceContentBlockId);
        if (block is null)
        {
            return NotFound();
        }

        var media = await mediaStore.ResolveAsync(
            game.Session.Id.Value,
            game.Session.Quiz,
            final
                ? DeferredGameMediaRole.FinalAnswer
                : DeferredGameMediaRole.Answer,
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

    private static IReadOnlyList<ContentBlockSnapshot> GetVisibleAnswerBlocks(
        GameSessionRegistration game,
        RuntimeQuestion question)
    {
        if (!MultipleChoiceAnswerContract.IsMultipleChoice(question.PresentationType))
        {
            return question.AnswerBlocks;
        }

        var definition = game.Session.Quiz.Rounds
            .SelectMany(round => round.Questions)
            .SingleOrDefault(item =>
                item.SourceQuestionId == question.SourceQuestionId);
        return definition?.RevealAnswerBlocks ??
            question.AnswerBlocks.Take(1).ToArray();
    }
}
