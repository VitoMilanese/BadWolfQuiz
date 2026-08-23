using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BadWolfQuiz.Web.Pages.Admin.Games;

public sealed class QuickScoreQuestionsModel(
    GameSessionRegistry sessionRegistry,
    CurrentHost currentHost) : PageModel
{
    public IActionResult OnGet(Guid id)
    {
        var game = sessionRegistry.FindOwned(
            new GameSessionId(id),
            currentHost.RequiredId);
        if (game is null)
        {
            return NotFound();
        }

        lock (game)
        {
            var rounds = game.Session.Quiz.Rounds
                .OrderBy(round => round.SortOrder)
                .ToArray();
            var addableRoundIds = rounds
                .Take(game.Session.CurrentRoundIndex + 1)
                .Select(round => round.SourceRoundId)
                .ToHashSet();
            var roundOrder = rounds
                .Select((round, index) => new { round.SourceRoundId, Index = index })
                .ToDictionary(item => item.SourceRoundId, item => item.Index);
            var roundTitles = rounds.ToDictionary(
                round => round.SourceRoundId,
                round => round.Title);

            var questions = game.Session.Board.Questions
                .Select(question => new
                {
                    Question = question,
                    OpenSequence = game.GetQuestionOpenSequence(
                        question.SourceQuestionId)
                })
                .Where(item =>
                    item.OpenSequence.HasValue ||
                    addableRoundIds.Contains(item.Question.SourceRoundId))
                .OrderBy(item => item.OpenSequence.HasValue ? 0 : 1)
                .ThenByDescending(item => item.OpenSequence ?? long.MinValue)
                .ThenBy(item => roundOrder[item.Question.SourceRoundId])
                .ThenBy(item => item.Question.RowIndex)
                .ThenBy(item => item.Question.SourceCategoryId)
                .Select(item => new
                {
                    sourceQuestionId = item.Question.SourceQuestionId,
                    label = $"{roundTitles[item.Question.SourceRoundId]} — " +
                        $"{item.Question.CategoryTitle} — {item.Question.Points}",
                    points = item.Question.Points,
                    status = item.Question.Status.ToString().ToLowerInvariant(),
                    attemptedPlayerIds = item.Question.AnswerAttempts
                        .Select(attempt => attempt.PlayerId.Value)
                        .ToArray(),
                    played = item.OpenSequence.HasValue,
                    openSequence = item.OpenSequence
                })
                .ToArray();

            return new JsonResult(new
            {
                success = true,
                questions
            });
        }
    }
}
