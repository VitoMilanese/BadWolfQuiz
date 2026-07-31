using System.Collections.ObjectModel;

namespace BadWolfQuiz.Game.Definitions;

public sealed class QuizSnapshot
{
    private readonly ReadOnlyCollection<QuizRoundSnapshot> _rounds;

    public QuizSnapshot(
        int sourceQuizId,
        string title,
        IEnumerable<QuizRoundSnapshot> rounds,
        FinalQuestionSnapshot? finalQuestion = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceQuizId);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(rounds);

        var roundList = rounds.ToList();

        if (roundList.Count == 0)
        {
            throw new ArgumentException("A quiz must contain at least one round.", nameof(rounds));
        }

        if (roundList.Select(x => x.SourceRoundId).Distinct().Count() != roundList.Count)
        {
            throw new ArgumentException("Round identifiers must be unique within a quiz.", nameof(rounds));
        }

        SourceQuizId = sourceQuizId;
        Title = title.Trim();
        _rounds = roundList.AsReadOnly();
        FinalQuestion = finalQuestion;
    }

    public int SourceQuizId { get; }

    public string Title { get; }

    public IReadOnlyList<QuizRoundSnapshot> Rounds => _rounds;

    public FinalQuestionSnapshot? FinalQuestion { get; }
}

public sealed class FinalQuestionSnapshot
{
    public FinalQuestionSnapshot(
        IEnumerable<ContentBlockSnapshot>? questionBlocks = null,
        IEnumerable<ContentBlockSnapshot>? answerBlocks = null)
    {
        QuestionBlocks = (questionBlocks ?? [])
            .OrderBy(block => block.SortOrder)
            .ToArray();
        AnswerBlocks = (answerBlocks ?? [])
            .OrderBy(block => block.SortOrder)
            .ToArray();
    }

    public IReadOnlyList<ContentBlockSnapshot> QuestionBlocks { get; }

    public IReadOnlyList<ContentBlockSnapshot> AnswerBlocks { get; }
}

public sealed class QuizRoundSnapshot
{
    private readonly ReadOnlyCollection<QuizQuestionSnapshot> _questions;

    public QuizRoundSnapshot(
        int sourceRoundId,
        string title,
        int sortOrder,
        IEnumerable<QuizQuestionSnapshot> questions,
        bool useRandomWagerQuestions = false,
        int randomWagerQuestionCount = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceRoundId);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentOutOfRangeException.ThrowIfNegative(sortOrder);
        ArgumentNullException.ThrowIfNull(questions);

        var questionList = questions.ToList();

        if (questionList.Count == 0)
        {
            throw new ArgumentException("A round must contain at least one question.", nameof(questions));
        }

        if (questionList.Select(x => x.SourceQuestionId).Distinct().Count() != questionList.Count)
        {
            throw new ArgumentException("Question identifiers must be unique within a round.", nameof(questions));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(randomWagerQuestionCount);

        var eligibleQuestionCount = questionList.Count(question =>
            !question.ExcludeFromRandomWagerSelection);

        if (useRandomWagerQuestions &&
            randomWagerQuestionCount > eligibleQuestionCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(randomWagerQuestionCount),
                "Random wager question count must be between zero and the number of eligible questions.");
        }

        SourceRoundId = sourceRoundId;
        Title = title.Trim();
        SortOrder = sortOrder;
        UseRandomWagerQuestions = useRandomWagerQuestions;
        RandomWagerQuestionCount = randomWagerQuestionCount;
        _questions = questionList.AsReadOnly();
    }

    public int SourceRoundId { get; }

    public string Title { get; }

    public int SortOrder { get; }

    public bool UseRandomWagerQuestions { get; }

    public int RandomWagerQuestionCount { get; }

    public IReadOnlyList<QuizQuestionSnapshot> Questions => _questions;
}

public sealed class QuizQuestionSnapshot
{
    public QuizQuestionSnapshot(
        int sourceQuestionId,
        int sourceCategoryId,
        int rowIndex,
        int points,
        bool isSpecial,
        string? categoryTitle = null,
        bool excludeFromRandomWagerSelection = false,
        IEnumerable<ContentBlockSnapshot>? questionBlocks = null,
        IEnumerable<ContentBlockSnapshot>? answerBlocks = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceQuestionId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceCategoryId);
        ArgumentOutOfRangeException.ThrowIfNegative(rowIndex);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(points);

        SourceQuestionId = sourceQuestionId;
        SourceCategoryId = sourceCategoryId;
        RowIndex = rowIndex;
        Points = points;
        IsSpecial = isSpecial;
        ExcludeFromRandomWagerSelection = excludeFromRandomWagerSelection;
        CategoryTitle = string.IsNullOrWhiteSpace(categoryTitle)
            ? sourceCategoryId.ToString()
            : categoryTitle.Trim();
        QuestionBlocks = (questionBlocks ?? []).OrderBy(block => block.SortOrder).ToArray();
        AnswerBlocks = (answerBlocks ?? []).OrderBy(block => block.SortOrder).ToArray();
    }

    public int SourceQuestionId { get; }

    public int SourceCategoryId { get; }

    public int RowIndex { get; }

    public int Points { get; }

    public bool IsSpecial { get; }

    public bool ExcludeFromRandomWagerSelection { get; }

    public string CategoryTitle { get; }

    public IReadOnlyList<ContentBlockSnapshot> QuestionBlocks { get; }

    public IReadOnlyList<ContentBlockSnapshot> AnswerBlocks { get; }
}

public sealed record ContentBlockSnapshot(
    int SourceContentBlockId,
    ContentBlockKind Kind,
    string? TextContent,
    string? TopCaption,
    string? BottomCaption,
    string? MediaPath,
    string? ExternalUrl,
    byte[]? FileData,
    string? FileContentType,
    string? FileName,
    int SortOrder,
    bool AudioOnly);

public enum ContentBlockKind
{
    Text = 1,
    Image = 2,
    Audio = 3,
    Video = 4,
    YouTube = 5
}
