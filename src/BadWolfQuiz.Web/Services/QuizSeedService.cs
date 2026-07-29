using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace BadWolfQuiz.Web.Services;

public sealed class QuizSeedService(
    QuizDbContext db,
    IStringLocalizer<SharedResource> localizer)
{
    public async Task SeedAsync()
    {
        if (await db.Quizzes.AnyAsync())
        {
            return;
        }

        var quiz = new Quiz
        {
            Title = localizer["Seed_DemoQuizTitle"],
            Description = localizer["Seed_DemoQuizDescription"]
        };

        var round = new QuizRound
        {
            Title = localizer["Default_RoundTitle", 1],
            SortOrder = 1,
            DefaultBuzzMode = BuzzActivationMode.Manual
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
                Title = localizer["Default_CategoryTitle", categoryIndex],
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
                    TextContent = row == 1 && categoryIndex == 1
                        ? localizer["Seed_TestQuestion"]
                        : string.Empty,
                    SortOrder = 1
                });

                question.AnswerBlocks.Add(new AnswerContentBlock
                {
                    BlockType = ContentBlockType.Text,
                    TextContent = row == 1 && categoryIndex == 1
                        ? localizer["Seed_TestAnswer"]
                        : string.Empty,
                    SortOrder = 1
                });

                category.Questions.Add(question);
            }

            round.Categories.Add(category);
        }

        quiz.Rounds.Add(round);
        db.Quizzes.Add(quiz);
        await db.SaveChangesAsync();
    }
}
