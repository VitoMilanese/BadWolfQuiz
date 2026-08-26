using System.Text.Json;
using System.Text.Json.Serialization;
using BadWolfQuiz.Game.Definitions;

namespace BadWolfQuiz.Web.Services;

public sealed class QuizSnapshotJsonConverter : JsonConverter<QuizSnapshot>
{
    public override QuizSnapshot Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var data = JsonSerializer.Deserialize<QuizSnapshotData>(
            ref reader,
            options) ?? throw new JsonException("The quiz snapshot is missing.");
        return data.ToSnapshot();
    }

    public override void Write(
        Utf8JsonWriter writer,
        QuizSnapshot value,
        JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(
            writer,
            QuizSnapshotData.From(value),
            options);
    }

    private sealed record QuizSnapshotData(
        int SourceQuizId,
        string Title,
        QuizRoundSnapshotData[] Rounds,
        FinalQuestionSnapshotData? FinalQuestion)
    {
        public QuizSnapshot ToSnapshot() => new(
            SourceQuizId,
            Title,
            Rounds.Select(round => round.ToSnapshot()),
            FinalQuestion?.ToSnapshot());

        public static QuizSnapshotData From(QuizSnapshot snapshot) => new(
            snapshot.SourceQuizId,
            snapshot.Title,
            snapshot.Rounds.Select(QuizRoundSnapshotData.From).ToArray(),
            snapshot.FinalQuestion is null
                ? null
                : FinalQuestionSnapshotData.From(snapshot.FinalQuestion));
    }

    private sealed record FinalQuestionSnapshotData(
        ContentBlockSnapshot[] QuestionBlocks,
        ContentBlockSnapshot[] AnswerBlocks,
        ContentBlockSnapshot[]? DescriptionBlocks = null)
    {
        public FinalQuestionSnapshot ToSnapshot() => new(
            QuestionBlocks,
            AnswerBlocks,
            DescriptionBlocks ?? []);

        public static FinalQuestionSnapshotData From(
            FinalQuestionSnapshot snapshot) => new(
                snapshot.QuestionBlocks.ToArray(),
                snapshot.AnswerBlocks.ToArray(),
                snapshot.DescriptionBlocks.ToArray());
    }

    private sealed record QuizRoundSnapshotData(
        int SourceRoundId,
        string Title,
        int SortOrder,
        QuizQuestionSnapshotData[] Questions,
        bool UseRandomWagerQuestions,
        int RandomWagerQuestionCount)
    {
        public QuizRoundSnapshot ToSnapshot() => new(
            SourceRoundId,
            Title,
            SortOrder,
            Questions.Select(question => question.ToSnapshot()),
            UseRandomWagerQuestions,
            RandomWagerQuestionCount);

        public static QuizRoundSnapshotData From(QuizRoundSnapshot snapshot) => new(
            snapshot.SourceRoundId,
            snapshot.Title,
            snapshot.SortOrder,
            snapshot.Questions.Select(QuizQuestionSnapshotData.From).ToArray(),
            snapshot.UseRandomWagerQuestions,
            snapshot.RandomWagerQuestionCount);
    }

    private sealed record QuizQuestionSnapshotData(
        int SourceQuestionId,
        int SourceCategoryId,
        int RowIndex,
        int Points,
        bool IsSpecial,
        string CategoryTitle,
        bool ExcludeFromRandomWagerSelection,
        ContentBlockSnapshot[] QuestionBlocks,
        ContentBlockSnapshot[] AnswerBlocks,
        QuestionPresentationType PresentationType = QuestionPresentationType.Standard)
    {
        public QuizQuestionSnapshot ToSnapshot() => new(
            SourceQuestionId,
            SourceCategoryId,
            RowIndex,
            Points,
            IsSpecial,
            CategoryTitle,
            ExcludeFromRandomWagerSelection,
            QuestionBlocks,
            AnswerBlocks,
            PresentationType);

        public static QuizQuestionSnapshotData From(
            QuizQuestionSnapshot snapshot) => new(
                snapshot.SourceQuestionId,
                snapshot.SourceCategoryId,
                snapshot.RowIndex,
                snapshot.Points,
                snapshot.IsSpecial,
                snapshot.CategoryTitle,
                snapshot.ExcludeFromRandomWagerSelection,
                snapshot.QuestionBlocks.ToArray(),
                snapshot.StoredAnswerBlocks.ToArray(),
                snapshot.PresentationType);
    }
}
