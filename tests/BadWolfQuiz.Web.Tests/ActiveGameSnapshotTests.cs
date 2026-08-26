using System.Text.Json;
using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Services;
using Microsoft.Extensions.Options;

namespace BadWolfQuiz.Web.Tests;

public sealed class ActiveGameSnapshotTests
{
    [Fact]
    public void Snapshot_round_trips_through_json()
    {
        var quiz = new QuizSnapshot(
            42,
            "Recovery quiz",
            [
                new QuizRoundSnapshot(
                    7,
                    "Round 1",
                    0,
                    [new QuizQuestionSnapshot(9, 3, 0, 100, false)])
            ]);
        var session = GameSession.Create(quiz);
        var player = session.AddPlayer("Rose");
        session.AdjustPlayerScore(player.Id, 250);
        session.Start();
        var snapshot = new ActiveGameSnapshot(
            "ABC123",
            "host-1",
            true,
            quiz,
            session.Settings,
            session.CaptureState());

        var options = new JsonSerializerOptions
        {
            Converters = { new QuizSnapshotJsonConverter() }
        };
        // The first implementation wrote snapshots with the default serializer.
        // The converter must remain able to read those existing files.
        var json = JsonSerializer.Serialize(snapshot);
        var restored = JsonSerializer.Deserialize<ActiveGameSnapshot>(json, options);

        Assert.NotNull(restored);
        Assert.Equal(42, restored.Quiz.SourceQuizId);
        Assert.Equal(session.Id, restored.SessionState.Id);
        Assert.Equal(250, restored.SessionState.Players.Single().Score);
    }

    [Fact]
    public void Structured_multiple_choice_answer_round_trips_with_reveal_content()
    {
        var answerBlocks = new ContentBlockSnapshot[]
        {
            new(
                100,
                ContentBlockKind.Text,
                MultipleChoiceAnswerContract.CreateRuntimeMarker(2),
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                0,
                false),
            TextBlock(101, "Correct", 1),
            TextBlock(102, "Wrong", 2),
            TextBlock(103, "Explanation", 3)
        };
        var question = new QuizQuestionSnapshot(
            9,
            3,
            0,
            100,
            false,
            questionBlocks: [TextBlock(99, "Question", 0)],
            answerBlocks: answerBlocks,
            presentationType: QuestionPresentationType.AllPlayerMultipleChoice);
        var quiz = new QuizSnapshot(
            42,
            "Recovery quiz",
            [new QuizRoundSnapshot(7, "Round 1", 0, [question])]);
        var session = GameSession.Create(quiz);
        session.AddPlayer("Rose");
        var snapshot = new ActiveGameSnapshot(
            "ABC123",
            "host-1",
            true,
            quiz,
            session.Settings,
            session.CaptureState());
        var options = new JsonSerializerOptions
        {
            Converters = { new QuizSnapshotJsonConverter() }
        };

        var json = JsonSerializer.Serialize(snapshot, options);
        var restored = JsonSerializer.Deserialize<ActiveGameSnapshot>(json, options);

        Assert.NotNull(restored);
        var restoredQuestion = restored.Quiz.Rounds.Single().Questions.Single();
        Assert.Equal([101, 102], restoredQuestion.AnswerBlocks
            .Select(block => block.SourceContentBlockId));
        Assert.Equal([101, 103], restoredQuestion.RevealAnswerBlocks
            .Select(block => block.SourceContentBlockId));
        Assert.True(MultipleChoiceAnswerContract.TryParseRuntimeMarker(
            restoredQuestion.StoredAnswerBlocks[0].TextContent,
            out var optionCount));
        Assert.Equal(2, optionCount);
    }

    [Fact]
    public void Resume_availability_expires_after_configured_days()
    {
        var now = new DateTimeOffset(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);
        var quiz = new QuizSnapshot(
            42,
            "Recovery quiz",
            [
                new QuizRoundSnapshot(
                    7,
                    "Round 1",
                    0,
                    [new QuizQuestionSnapshot(9, 3, 0, 100, false)])
            ]);
        var session = GameSession.Create(quiz);
        session.AddPlayer("Rose");
        session.Start();
        session.SelectQuestion(9);
        var snapshot = new ActiveGameSnapshot(
            "ABC123",
            "host-1",
            true,
            quiz,
            session.Settings,
            session.CaptureState(),
            now.AddDays(-30));
        var availability = new ActiveGameAvailability(
            Options.Create(new ActiveGameOptions
            {
                ResumeAvailabilityDays = 30
            }),
            new FixedTimeProvider(now));

        Assert.True(availability.CanResume(snapshot));
        Assert.False(availability.CanResume(snapshot with
        {
            SavedAtUtc = now.AddDays(-30).AddTicks(-1)
        }));
    }

    private static ContentBlockSnapshot TextBlock(
        int id,
        string text,
        int sortOrder) => new(
            id,
            ContentBlockKind.Text,
            text,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            sortOrder,
            false);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}