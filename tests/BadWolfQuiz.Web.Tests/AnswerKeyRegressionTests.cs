namespace BadWolfQuiz.Web.Tests;

public sealed class AnswerKeyRegressionTests
{
    [Fact]
    public void All_player_multiple_choice_answer_key_uses_only_the_correct_option()
    {
        var model = File.ReadAllText(FindWebFile(
            "Pages",
            "Admin",
            "Games",
            "AnswerKey.cshtml.cs"));

        Assert.Contains(
            "QuestionPresentationType.AllPlayerMultipleChoice",
            model,
            StringComparison.Ordinal);
        Assert.Contains(
            "return [question.AnswerBlocks[0]];",
            model,
            StringComparison.Ordinal);
        Assert.Contains(
            "AnswerBlocks = GetVisibleAnswerBlocks(question);",
            model,
            StringComparison.Ordinal);
        Assert.Contains(
            ": GetVisibleAnswerBlocks(question);",
            model,
            StringComparison.Ordinal);
        Assert.Contains(
            "return question.AnswerBlocks;",
            model,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Answer_key_layout_is_compact_and_uses_the_available_width()
    {
        var page = File.ReadAllText(FindWebFile(
            "Pages",
            "Admin",
            "Games",
            "AnswerKey.cshtml"));
        var css = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "answer-key.css"));

        Assert.Contains("~/css/answer-key.css", page, StringComparison.Ordinal);
        Assert.Contains(
            "<h1>@Localizer[\"Label_CorrectAnswer\"]</h1>",
            page,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Model.QuestionLabel", page, StringComparison.Ordinal);
        Assert.DoesNotContain("GameBoard_Answer", page, StringComparison.Ordinal);

        Assert.Contains(
            "body:has(.answer-key-page) .page-shell",
            css,
            StringComparison.Ordinal);
        Assert.Contains("max-width: none;", css, StringComparison.Ordinal);
        Assert.Contains("padding-inline: 0;", css, StringComparison.Ordinal);
        Assert.Contains(
            ".answer-key-content.game-content-presentation",
            css,
            StringComparison.Ordinal);
        Assert.Contains("width: 100%;", css, StringComparison.Ordinal);
    }

    private static string FindWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidateParts = new[]
            {
                directory.FullName,
                "src",
                "BadWolfQuiz.Web"
            }.Concat(parts).ToArray();
            var candidate = Path.Combine(candidateParts);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate BadWolfQuiz.Web file: {Path.Combine(parts)}");
    }
}
