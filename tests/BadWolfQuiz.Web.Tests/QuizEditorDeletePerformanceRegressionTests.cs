namespace BadWolfQuiz.Web.Tests;

public sealed class QuizEditorDeletePerformanceRegressionTests
{
    [Fact]
    public void Question_delete_does_not_materialize_content_block_collections()
    {
        var source = File.ReadAllText(FindRepoFile(
            "src", "BadWolfQuiz.Web", "Pages", "Admin", "Quizzes",
            "Editor.cshtml.cs"));
        var handler = ExtractHandler(
            source,
            "OnPostDeleteQuestionAsync()",
            "OnPostDeleteFinalQuestionAsync()");

        Assert.DoesNotContain(".Include(x => x.QuestionBlocks)", handler);
        Assert.DoesNotContain(".Include(x => x.AnswerBlocks)", handler);
        Assert.DoesNotContain("RemoveRange(question.QuestionBlocks)", handler);
        Assert.DoesNotContain("RemoveRange(question.AnswerBlocks)", handler);
        Assert.Contains("db.QuestionContentBlocks", handler);
        Assert.Contains("db.AnswerContentBlocks", handler);
        Assert.Equal(2, CountOccurrences(handler, ".ExecuteDeleteAsync()"));
        Assert.Contains("BeginTransactionAsync()", handler);
        Assert.Contains("CommitAsync()", handler);
        Assert.Contains("db.QuestionContentBlocks.Add(new QuestionContentBlock", handler);
        Assert.Contains("db.AnswerContentBlocks.Add(new AnswerContentBlock", handler);
    }

    [Fact]
    public void Final_question_delete_does_not_materialize_content_block_collections()
    {
        var source = File.ReadAllText(FindRepoFile(
            "src", "BadWolfQuiz.Web", "Pages", "Admin", "Quizzes",
            "Editor.cshtml.cs"));
        var handler = ExtractHandler(
            source,
            "OnPostDeleteFinalQuestionAsync()",
            "OnPostSaveRoundRowsAsync(");

        Assert.DoesNotContain(".Include(x => x.FinalQuestionBlocks)", handler);
        Assert.DoesNotContain(".Include(x => x.FinalAnswerBlocks)", handler);
        Assert.DoesNotContain("RemoveRange(quiz.FinalQuestionBlocks)", handler);
        Assert.DoesNotContain("RemoveRange(quiz.FinalAnswerBlocks)", handler);
        Assert.Contains("db.FinalQuestionContentBlocks", handler);
        Assert.Contains("db.FinalAnswerContentBlocks", handler);
        Assert.Equal(2, CountOccurrences(handler, ".ExecuteDeleteAsync()"));
        Assert.Contains("BeginTransactionAsync()", handler);
        Assert.Contains("CommitAsync()", handler);
    }

    private static string ExtractHandler(
        string source,
        string startMarker,
        string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);

        Assert.True(start >= 0);
        Assert.True(end > start);
        return source[start..end];
    }

    private static int CountOccurrences(string value, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    private static string FindRepoFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(
                new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(path))
            {
                return path;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not find repository file: {Path.Combine(segments)}");
    }
}
