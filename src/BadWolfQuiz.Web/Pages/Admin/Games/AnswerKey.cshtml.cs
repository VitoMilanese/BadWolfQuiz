using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BadWolfQuiz.Web.Pages.Admin.Games;

public sealed class AnswerKeyModel(
    GameSessionRegistry sessionRegistry,
    CurrentHost currentHost) : PageModel
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
        AnswerBlocks = GetVisibleAnswerBlocks(question);
        return Page();
    }

    public IActionResult OnGetContentBlock(
        Guid id,
        int sourceContentBlockId,
        bool final)
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
                : GetVisibleAnswerBlocks(question);
        }

        var block = blocks?.SingleOrDefault(item =>
            item.SourceContentBlockId == sourceContentBlockId);

        if (block?.FileData is null ||
            string.IsNullOrWhiteSpace(block.FileContentType))
        {
            return NotFound();
        }

        return string.IsNullOrWhiteSpace(block.FileName)
            ? File(block.FileData, block.FileContentType)
            : File(block.FileData, block.FileContentType, block.FileName);
    }

    private static IReadOnlyList<ContentBlockSnapshot> GetVisibleAnswerBlocks(
        RuntimeQuestion question)
    {
        if (question.PresentationType ==
                QuestionPresentationType.AllPlayerMultipleChoice &&
            question.AnswerBlocks.Count > 0)
        {
            return [question.AnswerBlocks[0]];
        }

        return question.AnswerBlocks;
    }
}
