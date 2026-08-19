using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace BadWolfQuiz.Web.Pages.Admin.Quizzes;

public sealed class QuestionCopyModel(
    QuizDbContext db,
    CurrentHost currentHost,
    IOptions<QuizEditorOptions> editorOptions) : PageModel
{
    public IActionResult OnGet() => NotFound();

    public async Task<IActionResult> OnGetTargetsAsync(
        int questionId,
        CancellationToken cancellationToken)
    {
        if (questionId <= 0)
        {
            return BadRequest();
        }

        var destinations = await QuestionCopyOperations.GetDestinationsAsync(
            db,
            currentHost.RequiredId,
            questionId,
            editorOptions.Value.MaximumQuestionCount,
            cancellationToken);
        if (destinations is null)
        {
            return new JsonResult(new
            {
                success = false,
                error = "source-not-found"
            })
            {
                StatusCode = StatusCodes.Status404NotFound
            };
        }

        var quizzes = destinations
            .GroupBy(destination => new
            {
                destination.QuizId,
                destination.QuizTitle
            })
            .Select(group => new
            {
                id = group.Key.QuizId,
                title = group.Key.QuizTitle,
                categories = group.Select(destination => new
                {
                    id = destination.CategoryId,
                    title = destination.CategoryTitle,
                    roundId = destination.RoundId,
                    roundTitle = destination.RoundTitle,
                    hasCapacity = destination.HasCapacity
                }).ToArray()
            })
            .ToArray();

        return new JsonResult(new
        {
            success = true,
            quizzes
        });
    }

    public async Task<IActionResult> OnPostAsync(
        int questionId,
        int targetCategoryId,
        CancellationToken cancellationToken)
    {
        if (questionId <= 0 || targetCategoryId <= 0)
        {
            return BadRequest(new
            {
                success = false,
                error = "invalid-request"
            });
        }

        var result = await QuestionCopyOperations.CopyAsync(
            db,
            currentHost.RequiredId,
            questionId,
            targetCategoryId,
            editorOptions.Value.MaximumQuestionCount,
            cancellationToken);

        if (result.Succeeded)
        {
            return new JsonResult(new
            {
                success = true,
                questionId = result.QuestionId,
                quizId = result.QuizId,
                roundId = result.RoundId,
                categoryId = result.CategoryId
            });
        }

        var error = result.Status switch
        {
            QuestionCopyStatus.SourceNotFound => "source-not-found",
            QuestionCopyStatus.TargetNotFound => "target-not-found",
            QuestionCopyStatus.NoCapacity => "no-capacity",
            _ => "invalid-request"
        };
        var statusCode = result.Status is
            QuestionCopyStatus.SourceNotFound or
            QuestionCopyStatus.TargetNotFound
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;

        return new JsonResult(new
        {
            success = false,
            error
        })
        {
            StatusCode = statusCode
        };
    }
}
