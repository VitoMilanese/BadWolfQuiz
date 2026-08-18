using System.Collections.ObjectModel;

namespace BadWolfQuiz.Game.Definitions;

public enum QuestionPresentationType
{
    Standard = 0,
    FourClues = 1,
    AllPlayerText = 2,
    AllPlayerMultipleChoice = 3
}

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

public sealed class QuizCategoryIntroSnapshot
{
    public QuizCategoryIntroSnapshot(
        int sourceCategoryId,
        string title,
        int sortOrder,
        IEnumerable<ContentBlockSnapshot>? descriptionBlocks = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceCategoryId);
        ArgumentOutOfRangeException.ThrowIfNegative(sortOrder);

        SourceCategoryId = sourceCategoryId;
        Title = title?.Trim() ?? string.Empty;
        SortOrder = sortOrder;
        DescriptionBlocks = (descriptionBlocks ?? [])
            .OrderBy(block => block.SortOrder)
            .ToArray();
    }

    public int SourceCategoryId { get; }

    public string Title { get; }

    public int SortOrder { get; }

    public IReadOnlyList<ContentBlockSnapshot> DescriptionBlocks { get; }
}

public sealed class QuizRoundSnapshot
{
    private readonly ReadOnlyCollection<QuizQuestionSnapshot> _questions;
    private readonly ReadOnlyCollection<QuizCategoryIntroSnapshot> _categoryIntros;

    public QuizRoundSnapshot(
        int sourceRoundId,
        string title,
        int sortOrder,
        IEnumerable<QuizQuestionSnapshot> questions,
        bool useRandomWagerQuestions = false,
        int randomWagerQuestionCount = 0,
        IEnumerable<ContentBlockSnapshot>? descriptionBlocks = null,
        IEnumerable<QuizCategoryIntroSnapshot>? categoryIntros = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceRoundId);
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

        var categoryIntroList = (categoryIntros ?? [])
            .OrderBy(category => category.SortOrder)
            .ToList();

        if (categoryIntroList.Select(x => x.SourceCategoryId).Distinct().Count() != categoryIntroList.Count)
        {
            throw new ArgumentException(
                "Category identifiers must be unique within a round.",
                nameof(categoryIntros));
        }

        SourceRoundId = sourceRoundId;
        Title = title?.Trim() ?? string.Empty;
        SortOrder = sortOrder;
        UseRandomWagerQuestions = useRandomWagerQuestions;
        RandomWagerQuestionCount = randomWagerQuestionCount;
        DescriptionBlocks = (descriptionBlocks ?? [])
            .OrderBy(block => block.SortOrder)
            .ToArray();
        _categoryIntros = categoryIntroList.AsReadOnly();
        _questions = questionList.AsReadOnly();
    }

    public int SourceRoundId { get; }

    public string Title { get; }

    public int SortOrder { get; }

    public bool UseRandomWagerQuestions { get; }

    public int RandomWagerQuestionCount { get; }

    public IReadOnlyList<ContentBlockSnapshot> DescriptionBlocks { get; }

    public IReadOnlyList<QuizCategoryIntroSnapshot> CategoryIntros => _categoryIntros;

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
        IEnumerable<ContentBlockSnapshot>? answerBlocks = null,
        QuestionPresentationType presentationType = QuestionPresentationType.Standard)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceQuestionId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceCategoryId);
        ArgumentOutOfRangeException.ThrowIfNegative(rowIndex);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(points);

        SourceQuestionId = sourceQuestionId;
        SourceCategoryId = sourceCategoryId;
        RowIndex = rowIndex;
        Points = points;
        var orderedQuestionBlocks = (questionBlocks ?? [])
            .OrderBy(block => block.SortOrder)
            .ToArray();
        var orderedAnswerBlocks = (answerBlocks ?? [])
            .OrderBy(block => block.SortOrder)
            .ToArray();

        if (presentationType == QuestionPresentationType.FourClues &&
            orderedQuestionBlocks.Length != 4)
        {
            throw new ArgumentException(
                "A four-clue question must contain exactly four content blocks.",
                nameof(questionBlocks));
        }

        if (presentationType == QuestionPresentationType.AllPlayerText &&
            (orderedAnswerBlocks.Length != 1 ||
             orderedAnswerBlocks[0].Kind != ContentBlockKind.Text ||
             string.IsNullOrWhiteSpace(orderedAnswerBlocks[0].TextContent)))
        {
            throw new ArgumentException(
                "An all-player text question must contain exactly one non-empty text answer block.",
                nameof(answerBlocks));
        }

        if (presentationType == QuestionPresentationType.AllPlayerMultipleChoice)
        {
            if (orderedQuestionBlocks.Any(block =>
                    block.Kind is not ContentBlockKind.Text and
                        not ContentBlockKind.Image))
            {
                throw new ArgumentException(
                    "An all-player multiple-choice question can contain only text or image question blocks.",
                    nameof(questionBlocks));
            }

            if (orderedAnswerBlocks.Length is < 2 or > 4 ||
                orderedAnswerBlocks.Any(block => !IsValidAllPlayerChoiceOption(block)))
            {
                throw new ArgumentException(
                    "An all-player multiple-choice question must contain two to four text or image answer blocks.",
                    nameof(answerBlocks));
            }

            var textChoices = orderedAnswerBlocks
                .Where(block => block.Kind == ContentBlockKind.Text)
                .Select(block => block.TextContent!.Trim())
                .ToArray();

            if (textChoices.Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
                textChoices.Length)
            {
                throw new ArgumentException(
                    "Text answer options for an all-player multiple-choice question must be distinct.",
                    nameof(answerBlocks));
            }
        }

        var isAllPlayer = presentationType is
            QuestionPresentationType.AllPlayerText or
            QuestionPresentationType.AllPlayerMultipleChoice;

        IsSpecial = presentationType == QuestionPresentationType.FourClues || isAllPlayer
            ? false
            : isSpecial;
        PresentationType = presentationType;
        ExcludeFromRandomWagerSelection = isAllPlayer || excludeFromRandomWagerSelection;
        CategoryTitle = string.IsNullOrWhiteSpace(categoryTitle)
            ? sourceCategoryId.ToString()
            : categoryTitle.Trim();
        QuestionBlocks = orderedQuestionBlocks;
        AnswerBlocks = orderedAnswerBlocks;
    }

    private static bool IsValidAllPlayerChoiceOption(ContentBlockSnapshot block) =>
        block.Kind switch
        {
            ContentBlockKind.Text => !string.IsNullOrWhiteSpace(block.TextContent),
            ContentBlockKind.Image =>
                block.FileData is { Length: > 0 } &&
                !string.IsNullOrWhiteSpace(block.FileContentType),
            _ => false
        };

    public int SourceQuestionId { get; }

    public int SourceCategoryId { get; }

    public int RowIndex { get; }

    public int Points { get; }

    public bool IsSpecial { get; }

    public QuestionPresentationType PresentationType { get; }

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
    bool AudioOnly,
    bool Autoplay = false);

public enum ContentBlockKind
{
    Text = 1,
    Image = 2,
    Audio = 3,
    Video = 4,
    YouTube = 5
}
