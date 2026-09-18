using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Services;
using BadWolfQuiz.Web.TagHelpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Pages.Admin.Games;

public sealed class QuestionHintsModel(
    QuizDbContext db,
    GameSessionRegistry sessionRegistry,
    CurrentHost currentHost) : PageModel
{
    public Task<IActionResult> OnPostAsync(
        Guid id,
        int sourceQuestionId,
        CancellationToken cancellationToken) =>
        RevealAsync(
            id,
            sourceQuestionId,
            revealAll: false,
            cancellationToken);

    public Task<IActionResult> OnPostAllAsync(
        Guid id,
        int sourceQuestionId,
        CancellationToken cancellationToken) =>
        RevealAsync(
            id,
            sourceQuestionId,
            revealAll: true,
            cancellationToken);

    private async Task<IActionResult> RevealAsync(
        Guid id,
        int sourceQuestionId,
        bool revealAll,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.FindOwned(
            new GameSessionId(id),
            currentHost.RequiredId);
        if (game is null)
        {
            return NotFound();
        }

        var hints = await QuestionHintGameplayData.LoadAsync(
            db,
            game.Session.Quiz.SourceQuizId,
            sourceQuestionId,
            cancellationToken);
        if (hints.Count == 0)
        {
            return new JsonResult(new
            {
                error = "No hints are available for this question."
            })
            {
                StatusCode = StatusCodes.Status409Conflict
            };
        }

        int revealedHintCount;
        int rewardValue;
        lock (game)
        {
            var question = game.Session.Board.Questions.SingleOrDefault(item =>
                item.SourceQuestionId == sourceQuestionId);
            if (question is null)
            {
                return NotFound();
            }

            try
            {
                if (revealAll)
                {
                    question.RevealAllHints(hints.Count);
                }
                else
                {
                    question.RevealNextHint(hints.Count);
                }
                game.MarkPersistenceChanged();
            }
            catch (GameRuleViolationException)
            {
                // A stale/repeated request should return the current hint state.
            }

            revealedHintCount = Math.Min(
                question.RevealedHintCount,
                hints.Count);
            rewardValue = question.CorrectAnswerValue;
        }

        var strings = QuestionHintStrings.Current;
        var revealedHints = hints
            .Take(revealedHintCount)
            .Select(hint => new
            {
                id = hint.Id,
                blockType = hint.BlockType == ContentBlockType.Image
                    ? "image"
                    : "text",
                textContent = hint.TextContent?.Trim(),
                topCaption = hint.TopCaption?.Trim(),
                bottomCaption = hint.BottomCaption?.Trim(),
                imageUrl = hint.BlockType == ContentBlockType.Image
                    ? Url.Page(
                        "/Admin/Games/QuestionHints",
                        "ContentBlock",
                        new
                        {
                            id,
                            sourceQuestionId,
                            sourceContentBlockId = hint.Id
                        })
                    : null
            })
            .ToArray();

        return new JsonResult(new
        {
            sourceQuestionId,
            totalHintCount = hints.Count,
            revealedHintCount,
            rewardValue,
            hintsLabel = strings.Hints,
            showAnotherHintLabel = strings.ShowAnotherHint,
            revealedHints
        });
    }

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

        var question = game.Session.Board.Questions.SingleOrDefault(item =>
            item.SourceQuestionId == sourceQuestionId);
        if (question is null || question.RevealedHintCount <= 0)
        {
            return NotFound();
        }

        var hints = await QuestionHintGameplayData.LoadAsync(
            db,
            game.Session.Quiz.SourceQuizId,
            sourceQuestionId,
            cancellationToken);
        var revealedHintIds = hints
            .Take(Math.Min(question.RevealedHintCount, hints.Count))
            .Select(item => item.Id)
            .ToHashSet();
        if (!revealedHintIds.Contains(sourceContentBlockId))
        {
            return NotFound();
        }

        var block = await db.QuestionHintContentBlocks
            .AsNoTracking()
            .Where(item =>
                item.Id == sourceContentBlockId &&
                item.QuizQuestionId == sourceQuestionId &&
                item.Question.Category.Round.QuizId ==
                    game.Session.Quiz.SourceQuizId)
            .Select(item => new
            {
                item.FileData,
                item.FileContentType
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (block?.FileData is null ||
            block.FileData.Length == 0 ||
            string.IsNullOrWhiteSpace(block.FileContentType))
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "no-store";
        return File(block.FileData, block.FileContentType);
    }
}
