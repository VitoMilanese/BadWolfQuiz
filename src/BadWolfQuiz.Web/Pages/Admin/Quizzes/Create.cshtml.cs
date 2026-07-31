using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using System.ComponentModel.DataAnnotations;

namespace BadWolfQuiz.Web.Pages.Admin.Quizzes;

public sealed class CreateModel(
    QuizDbContext db,
    CurrentHost currentHost,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var quiz = new Quiz
        {
            HostId = currentHost.RequiredId,
            Title = Input.Title.Trim(),
            Description = Input.Description?.Trim()
        };

        var round = new QuizRound
        {
            Title = localizer["Default_RoundTitle", 1].Value,
            SortOrder = 1
        };

        for (var row = 1; row <= 5; row++)
        {
            round.Rows.Add(new QuizRoundRow
            {
                RowIndex = row,
                Points = row * 200
            });
        }

        for (var categoryIndex = 1; categoryIndex <= 6; categoryIndex++)
        {
            var category = new QuizCategory
            {
                Title = localizer["Default_CategoryTitle", categoryIndex].Value,
                SortOrder = categoryIndex
            };

            for (var row = 1; row <= 5; row++)
            {
                var question = new QuizQuestion
                {
                    RowIndex = row
                };

                question.QuestionBlocks.Add(new QuestionContentBlock
                {
                    BlockType = ContentBlockType.Text,
                    SortOrder = 1
                });
                question.AnswerBlocks.Add(new AnswerContentBlock
                {
                    BlockType = ContentBlockType.Text,
                    SortOrder = 1
                });
                category.Questions.Add(question);
            }

            round.Categories.Add(category);
        }

        quiz.Rounds.Add(round);
        db.Quizzes.Add(quiz);
        await db.SaveChangesAsync();

        TempData["SuccessMessage"] = localizer["Message_QuizCreated"].Value;
        return RedirectToPage("Editor", new { id = quiz.Id });
    }

    public sealed class InputModel
    {
        [Required(ErrorMessage = "Error_QuizNameRequired")]
        [MaxLength(160, ErrorMessage = "Error_MaxLength")]
        [Display(Name = "Label_QuizName")]
        public string Title { get; set; } = string.Empty;

        [MaxLength(1000, ErrorMessage = "Error_MaxLength")]
        [Display(Name = "Label_Description")]
        public string? Description { get; set; }
    }
}
