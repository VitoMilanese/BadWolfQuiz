using Microsoft.Extensions.Hosting;

namespace BadWolfQuiz.Web.Services;

public sealed class MinigameQuestionStore
{
    public const int MinimumQuestionCount = 3;

    private readonly string _questionsPath;

    public MinigameQuestionStore(string questionsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(questionsPath);
        _questionsPath = Path.GetFullPath(questionsPath);
    }

    public int AvailableQuestionCount => GetQuestions().Count;

    public static string ResolveQuestionsPath(IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return Path.Combine(
            MinigameCardSetStore.ResolveRootPath(environment),
            "questions.txt");
    }

    public IReadOnlyList<string> GetQuestions()
    {
        if (!File.Exists(_questionsPath))
        {
            return [];
        }

        try
        {
            return File
                .ReadLines(_questionsPath)
                .Select(question => question.Trim())
                .Where(question => !string.IsNullOrWhiteSpace(question))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }
}
