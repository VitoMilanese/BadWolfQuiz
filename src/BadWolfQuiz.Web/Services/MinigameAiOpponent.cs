namespace BadWolfQuiz.Web.Services;

internal enum MinigameAiActionKind
{
    AskQuestion,
    Guess,
    Draw,
    Pass
}

internal sealed record MinigameAiAction(
    MinigameAiActionKind Kind,
    int QuestionOptionIndex = -1,
    string? GuessFileName = null);

public sealed record MinigameAiGameKnowledge(
    MinigameCardDescriptor Card,
    IReadOnlyDictionary<string, bool?> Answers);

public sealed record MinigameAiGameData(
    IReadOnlyList<MinigameAiGameKnowledge> Games,
    IReadOnlyList<string> Questions);

internal sealed class MinigameAiOpponent
{
    public const int MinimumCoveragePercent = 80;
    public const int FinalPhasePercent = 15;
    public const int FinalPhaseGuessPercent = 70;

    private readonly Dictionary<string, MinigameAiGameKnowledge> _knowledge;
    private readonly Random _random;
    private HashSet<string> _candidates = new(StringComparer.Ordinal);
    private int _initialCandidateCount;

    public MinigameAiOpponent(
        IReadOnlyList<MinigameAiGameKnowledge> knowledge,
        Random? random = null)
    {
        ArgumentNullException.ThrowIfNull(knowledge);
        _knowledge = knowledge.ToDictionary(
            item => item.Card.FileName,
            StringComparer.Ordinal);
        _random = random ?? Random.Shared;
    }

    public int CandidateCount => _candidates.Count;

    public IReadOnlyCollection<string> Candidates => _candidates;

    public void Reset(
        IReadOnlyList<MinigameCardDescriptor> activeCards,
        string? ownSecretFileName)
    {
        ArgumentNullException.ThrowIfNull(activeCards);
        _candidates = activeCards
            .Select(card => card.FileName)
            .Where(fileName =>
                !string.Equals(fileName, ownSecretFileName, StringComparison.Ordinal) &&
                _knowledge.ContainsKey(fileName))
            .ToHashSet(StringComparer.Ordinal);
        _initialCandidateCount = _candidates.Count;
    }

    public bool? GetAnswer(string? secretFileName, string question)
    {
        if (string.IsNullOrWhiteSpace(secretFileName) ||
            !_knowledge.TryGetValue(secretFileName, out var game) ||
            !game.Answers.TryGetValue(question, out var answer))
        {
            return null;
        }

        return answer;
    }

    public void ApplyAnswer(string question, bool? answerYes)
    {
        if (answerYes is null)
        {
            return;
        }

        _candidates.RemoveWhere(fileName =>
            !_knowledge.TryGetValue(fileName, out var game) ||
            !game.Answers.TryGetValue(question, out var answer) ||
            answer != answerYes);
    }

    public MinigameAiAction Decide(IReadOnlyList<string> availableQuestions)
    {
        ArgumentNullException.ThrowIfNull(availableQuestions);

        if (_candidates.Count == 0)
        {
            return new MinigameAiAction(MinigameAiActionKind.Draw);
        }

        if (_candidates.Count == 1)
        {
            return new MinigameAiAction(
                MinigameAiActionKind.Guess,
                GuessFileName: _candidates.Single());
        }

        var scored = availableQuestions
            .Select((question, index) => new QuestionScore(
                index,
                GetWorstCaseElimination(question)))
            .ToArray();
        var bestScore = scored.Length == 0
            ? 0
            : scored.Max(item => item.EliminatedCandidates);

        var inSearchPhase = _initialCandidateCount > 0 &&
            _candidates.Count * 100 > _initialCandidateCount * FinalPhasePercent;
        if (inSearchPhase)
        {
            if (availableQuestions.Count == 0)
            {
                return new MinigameAiAction(MinigameAiActionKind.Pass);
            }

            return new MinigameAiAction(
                MinigameAiActionKind.AskQuestion,
                ChooseQuestionIndex(scored, bestScore));
        }

        if (bestScore > 0)
        {
            return new MinigameAiAction(
                MinigameAiActionKind.AskQuestion,
                ChooseQuestionIndex(scored, bestScore));
        }

        if (availableQuestions.Count == 0 ||
            _random.Next(100) < FinalPhaseGuessPercent)
        {
            return new MinigameAiAction(
                MinigameAiActionKind.Guess,
                GuessFileName: ChooseCandidate());
        }

        return new MinigameAiAction(
            MinigameAiActionKind.AskQuestion,
            _random.Next(availableQuestions.Count));
    }

    private int GetWorstCaseElimination(string question)
    {
        var yesCount = 0;
        var noCount = 0;
        foreach (var candidate in _candidates)
        {
            if (!_knowledge.TryGetValue(candidate, out var game) ||
                !game.Answers.TryGetValue(question, out var answer))
            {
                continue;
            }

            if (answer is true)
            {
                yesCount++;
            }
            else if (answer is false)
            {
                noCount++;
            }
        }

        return Math.Min(yesCount, noCount);
    }

    private int ChooseQuestionIndex(
        IReadOnlyList<QuestionScore> scored,
        int bestScore)
    {
        var best = scored
            .Where(item => item.EliminatedCandidates == bestScore)
            .Select(item => item.OptionIndex)
            .ToArray();
        return best[_random.Next(best.Length)];
    }

    private string ChooseCandidate()
    {
        var candidates = _candidates.ToArray();
        return candidates[_random.Next(candidates.Length)];
    }

    private sealed record QuestionScore(int OptionIndex, int EliminatedCandidates);
}
