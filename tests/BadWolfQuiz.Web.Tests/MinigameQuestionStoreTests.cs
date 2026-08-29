using BadWolfQuiz.Web.Services;

namespace BadWolfQuiz.Web.Tests;

public sealed class MinigameQuestionStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"BadWolfQuiz-question-store-{Guid.NewGuid():N}");

    public MinigameQuestionStoreTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void Reads_trimmed_non_empty_unique_questions()
    {
        var path = Path.Combine(_directory, "questions.txt");
        File.WriteAllText(path, " First? \n\nSecond?\nFirst?\nThird?\n");
        var store = new MinigameQuestionStore(path);

        var questions = store.GetQuestions();

        Assert.Equal(["First?", "Second?", "Third?"], questions);
        Assert.Equal(3, store.AvailableQuestionCount);
    }

    [Fact]
    public void Missing_file_returns_empty_question_set()
    {
        var store = new MinigameQuestionStore(Path.Combine(_directory, "missing.txt"));
        Assert.Empty(store.GetQuestions());
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
