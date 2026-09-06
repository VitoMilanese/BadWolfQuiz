using BadWolfQuiz.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Services;

public sealed class MinigameAiCatalogStore
{
    private readonly MinigameCatalogStore _catalog;

    public MinigameAiCatalogStore(
        IDbContextFactory<QuizDbContext> dbFactory,
        int defaultCardCount)
    {
        _catalog = new MinigameCatalogStore(dbFactory, defaultCardCount);
    }

    public async Task<int> GetEligibleGameCountAsync(
        CancellationToken cancellationToken = default)
    {
        var questions = await _catalog.GetQuestionsAsync(cancellationToken);
        if (questions.Count < MinigameQuestionStore.MinimumQuestionCount)
        {
            return 0;
        }

        var games = await _catalog.GetGamesAsync(cancellationToken);
        return games.Count(game =>
            HasRequiredCoverage(game.AssignedAnswerCount, questions.Count));
    }

    public async Task<MinigameAiGameData> GenerateGameAsync(
        int cardCount,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cardCount);
        var questions = await _catalog.GetQuestionsAsync(cancellationToken);
        if (questions.Count < MinigameQuestionStore.MinimumQuestionCount)
        {
            throw new MinigameRoomException(MinigameRoomError.QuestionsUnavailable);
        }

        var eligible = (await _catalog.GetGamesAsync(cancellationToken))
            .Where(game => HasRequiredCoverage(game.AssignedAnswerCount, questions.Count))
            .ToList();
        if (cardCount > eligible.Count)
        {
            throw new MinigameRoomException(MinigameRoomError.InvalidCardCount);
        }

        Shuffle(eligible);
        var selected = eligible.Take(cardCount).ToArray();
        var knowledge = new List<MinigameAiGameKnowledge>(selected.Length);
        foreach (var game in selected)
        {
            var answers = await _catalog.GetAnswerItemsAsync(game.Id, cancellationToken);
            knowledge.Add(new MinigameAiGameKnowledge(
                new MinigameCardDescriptor(game.Id.ToString(), game.Name),
                answers.ToDictionary(
                    answer => answer.QuestionText,
                    answer => answer.AnswerYes,
                    StringComparer.Ordinal)));
        }

        return new MinigameAiGameData(knowledge, questions);
    }

    internal static bool HasRequiredCoverage(int answeredQuestions, int enabledQuestions) =>
        enabledQuestions > 0 &&
        answeredQuestions * 100 >= enabledQuestions * MinigameAiOpponent.MinimumCoveragePercent;

    private static void Shuffle<T>(IList<T> items)
    {
        for (var index = items.Count - 1; index > 0; index--)
        {
            var swapIndex = Random.Shared.Next(index + 1);
            (items[index], items[swapIndex]) = (items[swapIndex], items[index]);
        }
    }
}
