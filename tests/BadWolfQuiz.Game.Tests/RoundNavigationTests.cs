using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Game.Tests;

public sealed class RoundNavigationTests
{
    public static IEnumerable<object[]> ToolsMenuMatrix()
    {
        string[] backward = ["B", "D", "FH", "FI"];
        string[] forward = ["C", "E", "GJ", "GK"];
        foreach (var b in backward)
        foreach (var f in forward)
        foreach (var final in new[] { false, true })
        {
  var expectedPrevious = b is "D" or "FH";
  var expectedNext = f is "E" or "GJ";
  yield return [b + f + (final ? "L" : "M"), expectedPrevious, expectedNext, final];
        }
    }

    [Theory]
    [MemberData(nameof(ToolsMenuMatrix))]
    public void Tools_menu_matrix_matches_unfinished_round_availability(
        string scenario,
        bool expectedPrevious,
        bool expectedNext,
        bool hasFinalQuestion)
    {
        var session = CreateMatrixSession(scenario, hasFinalQuestion);

        Assert.Equal(expectedPrevious, session.HasPreviousUnfinishedRound);
        Assert.Equal(expectedNext, session.HasNextUnfinishedRound);
        Assert.Equal(hasFinalQuestion, session.Quiz.FinalQuestion is not null);
    }

    [Fact]
    public void Previous_round_skips_fully_closed_rounds()
    {
        var session = RestoreAtRound([false, true, false, false], 2);

        var round = session.ReturnToPreviousUnfinishedRound();

        Assert.Equal(1, round.SourceRoundId);
        Assert.Equal(0, session.CurrentRoundIndex);
    }

    [Fact]
    public void Next_round_skips_fully_closed_rounds()
    {
        var session = RestoreAtRound([false, false, true, false], 1);
        CloseCurrentRound(session);

        var round = session.AdvanceToNextRound();

        Assert.Equal(4, round.SourceRoundId);
        Assert.Equal(3, session.CurrentRoundIndex);
    }

    [Fact]
    public void Forced_round_summary_then_start_next_round_keeps_previous_round_unfinished()
    {
        var session = RestoreAtRound([false, false], 0);
        session.AddPlayer("Rose");
        var firstQuestion = session.Board.Questions.Single(q => q.SourceQuestionId == 101);

        session.ForceCompleteCurrentRound();

        Assert.True(session.IsCurrentRoundComplete);
        Assert.True(session.IsForcedRoundAdvancePending);
        Assert.Equal(RuntimeQuestionStatus.Available, firstQuestion.Status);
        Assert.Equal(0, session.CurrentRoundIndex);

        session.AdvanceToNextRound();

        Assert.Equal(1, session.CurrentRoundIndex);
        Assert.False(session.IsForcedRoundAdvancePending);
        Assert.True(session.HasPreviousUnfinishedRound);
        Assert.Equal(RuntimeQuestionStatus.Available, firstQuestion.Status);
    }

    [Fact]
    public void Forced_round_without_players_can_advance_immediately_and_keeps_previous_round_unfinished()
    {
        var session = RestoreAtRound([false, false], 0);
        var firstQuestion = session.Board.Questions.Single(q => q.SourceQuestionId == 101);

        session.ForceCompleteCurrentRound();
        session.AdvanceToNextRound();

        Assert.Equal(1, session.CurrentRoundIndex);
        Assert.True(session.HasPreviousUnfinishedRound);
        Assert.Equal(RuntimeQuestionStatus.Available, firstQuestion.Status);
    }

    [Fact]
    public void Forced_next_round_preserves_unopened_questions()
    {
        var session = RestoreAtRound([false, false], 0);
        var firstQuestion = session.Board.Questions.Single(q => q.SourceQuestionId == 101);

        session.ForceAdvanceToNextRound();

        Assert.Equal(RuntimeQuestionStatus.Available, firstQuestion.Status);
        Assert.Equal(1, session.CurrentRoundIndex);
        Assert.True(session.HasPreviousUnfinishedRound);
    }

    [Fact]
    public void Forced_next_round_resolves_only_an_in_progress_question()
    {
        var session = RestoreAtRound([false, false], 0);
        session.AddPlayer("Rose");
        var selected = session.SelectQuestion(101);

        session.ForceAdvanceToNextRound();

        Assert.Equal(RuntimeQuestionStatus.Resolved, selected.Status);
        Assert.Equal(RuntimeQuestionStatus.Available,
  session.Board.Questions.Single(q => q.SourceQuestionId == 102).Status);
    }

    [Fact]
    public void Returning_backward_preserves_scores_and_closed_questions()
    {
        var session = RestoreAtRound([false, true, false], 2);
        var player = session.AddPlayer("Rose");
        session.AdjustPlayerScore(player.Id, 500);

        session.ReturnToPreviousUnfinishedRound();

        Assert.Equal(500, player.Score);
        Assert.Equal(RuntimeQuestionStatus.Resolved,
  session.Board.Questions.Single(q => q.SourceQuestionId == 102).Status);
    }

    [Fact]
    public void Forced_final_does_not_close_unopened_regular_questions()
    {
        var session = RestoreAtRound([false, false], 0, hasFinalQuestion: true);
        session.AddPlayer("Rose");

        session.ForceAdvanceToFinalQuestion();

        Assert.Equal(GameSessionStatus.FinalWagering, session.Status);
        Assert.All(session.Board.Questions,
  q => Assert.Equal(RuntimeQuestionStatus.Available, q.Status));
    }

    [Fact]
    public void Manual_final_guard_can_exclude_current_round()
    {
        var session = RestoreAtRound([true, false, true], 1, hasFinalQuestion: true);

        Assert.False(session.HasUnfinishedRegularRoundExcludingCurrent);
        Assert.True(session.HasAnyUnfinishedRegularRound);
    }

    [Fact]
    public void Manual_final_guard_detects_other_unfinished_round()
    {
        var session = RestoreAtRound([false, false, true], 1, hasFinalQuestion: true);

        Assert.True(session.HasUnfinishedRegularRoundExcludingCurrent);
    }

    [Fact]
    public void Forced_final_guard_ignores_unvisited_future_rounds()
    {
        var session = RestoreAtRound(
            [false, false, false],
            0,
            hasFinalQuestion: true,
            furthestVisitedRoundIndex: 0);

        Assert.False(session.HasUnfinishedRegularRoundExcludingCurrent);
    }

    [Fact]
    public void Forced_final_guard_sees_unfinished_previous_round_but_not_unvisited_future_round()
    {
        var session = RestoreAtRound(
            [false, false, false],
            1,
            hasFinalQuestion: true,
            furthestVisitedRoundIndex: 1);

        Assert.True(session.HasUnfinishedRegularRoundExcludingCurrent);
        Assert.Equal(1, session.ReturnToNearestUnfinishedRoundExcludingCurrent().SourceRoundId);
    }

    [Fact]
    public void Forced_final_guard_returns_to_a_visited_future_round_after_going_back()
    {
        var session = RestoreAtRound(
            [false, false, false],
            0,
            hasFinalQuestion: true,
            furthestVisitedRoundIndex: 1);

        Assert.True(session.HasUnfinishedRegularRoundExcludingCurrent);
        Assert.Equal(2, session.ReturnToNearestUnfinishedRoundExcludingCurrent().SourceRoundId);
    }

    [Fact]
    public void Forced_final_guard_prefers_unfinished_previous_round_on_equal_distance()
    {
        var session = RestoreAtRound(
            [false, false, false],
            1,
            hasFinalQuestion: true,
            furthestVisitedRoundIndex: 2);

        Assert.True(session.HasUnfinishedRegularRoundExcludingCurrent);
        Assert.Equal(1, session.ReturnToNearestUnfinishedRoundExcludingCurrent().SourceRoundId);
    }

    [Fact]
    public void Forced_final_guard_returns_to_nearest_unfinished_visited_round()
    {
        var session = RestoreAtRound(
            [false, false, false],
            0,
            hasFinalQuestion: true,
            furthestVisitedRoundIndex: 2);

        Assert.Equal(2, session.ReturnToNearestUnfinishedRoundExcludingCurrent().SourceRoundId);
    }

    [Fact]
    public void Forced_final_guard_skips_closed_visited_round_when_returning_forward()
    {
        var session = RestoreAtRound(
            [false, true, false],
            0,
            hasFinalQuestion: true,
            furthestVisitedRoundIndex: 2);

        Assert.Equal(3, session.ReturnToNearestUnfinishedRoundExcludingCurrent().SourceRoundId);
    }

    [Fact]
    public void Forced_final_guard_returns_forward_when_previous_rounds_are_closed()
    {
        var session = RestoreAtRound(
            [true, false, false],
            1,
            hasFinalQuestion: true,
            furthestVisitedRoundIndex: 2);

        Assert.Equal(3, session.ReturnToNearestUnfinishedRoundExcludingCurrent().SourceRoundId);
    }

    [Fact]
    public void Furthest_visited_round_survives_state_capture_and_restore()
    {
        var session = RestoreAtRound(
            [false, false, false],
            0,
            furthestVisitedRoundIndex: 2);

        var restored = GameSession.Restore(
            session.Quiz,
            session.Settings,
            session.CaptureState());

        Assert.Equal(2, restored.FurthestVisitedRoundIndex);
    }

    [Fact]
    public void Previous_round_transition_can_show_leaderboard_before_returning()
    {
        var session = RestoreAtRound([false, false], 1);

        session.PrepareReturnToPreviousUnfinishedRound();

        Assert.True(session.IsPreviousRoundReturnPending);
        Assert.True(session.IsCurrentRoundComplete);
        Assert.Empty(session.GetCurrentRoundStandings());
        Assert.Equal(1, session.CurrentRoundIndex);

        session.ReturnToPreviousUnfinishedRound();

        Assert.False(session.IsPreviousRoundReturnPending);
        Assert.Equal(0, session.CurrentRoundIndex);
    }

    [Fact]
    public void Final_guard_return_can_show_leaderboard_before_returning_to_unfinished_round()
    {
        var session = RestoreAtRound(
            [false, false, false],
            1,
            furthestVisitedRoundIndex: 2,
            hasFinalQuestion: true);

        session.PrepareReturnToNearestUnfinishedRoundExcludingCurrent();

        Assert.True(session.IsUnfinishedRoundReturnPending);
        Assert.True(session.IsCurrentRoundComplete);
        Assert.Empty(session.GetCurrentRoundStandings());
        Assert.Equal(1, session.CurrentRoundIndex);

        var restored = GameSession.Restore(
            session.Quiz,
            session.Settings,
            session.CaptureState());

        Assert.True(restored.IsUnfinishedRoundReturnPending);
        restored.ReturnToNearestUnfinishedRoundExcludingCurrent();
        Assert.False(restored.IsUnfinishedRoundReturnPending);
        Assert.NotEqual(1, restored.CurrentRoundIndex);
    }

    [Fact]
    public void Forced_final_transition_can_show_leaderboard_before_starting_final()
    {
        var session = RestoreAtRound([false], 0, hasFinalQuestion: true);

        session.PrepareFinalQuestionAdvance();

        Assert.True(session.IsFinalQuestionAdvancePending);
        Assert.True(session.IsCurrentRoundComplete);
        Assert.Empty(session.GetCurrentRoundStandings());

        var restored = GameSession.Restore(
            session.Quiz,
            session.Settings,
            session.CaptureState());

        Assert.True(restored.IsFinalQuestionAdvancePending);
        Assert.Equal(GameSessionStatus.Running, restored.Status);
    }

    [Fact]
    public void Forced_next_after_return_skips_a_fully_closed_round()
    {
        var session = RestoreAtRound([false, false, true, false], 1);
        session.ReturnToPreviousUnfinishedRound();
        var round = session.ForceAdvanceToNextRound();
        Assert.Equal(2, round.SourceRoundId);
        session.ForceAdvanceToNextRound();
        Assert.Equal(4, session.CurrentRound.SourceRoundId);
    }

    [Fact]
    public void Natural_final_guard_detects_an_earlier_unfinished_round()
    {
        var session = RestoreAtRound([false, true, true], 2, hasFinalQuestion: true);
        Assert.True(session.HasAnyUnfinishedRegularRound);
        Assert.False(session.HasNextUnfinishedRound);
        Assert.Equal(1, session.ReturnToNearestUnfinishedRoundExcludingCurrent().SourceRoundId);
    }

    [Fact]
    public void Natural_final_guard_is_clear_when_all_regular_rounds_are_closed()
    {
        var session = RestoreAtRound([true, true, true], 1, hasFinalQuestion: true);
        Assert.False(session.HasAnyUnfinishedRegularRound);
        Assert.False(session.HasNextUnfinishedRound);
    }

    [Fact]
    public void Finish_game_guard_detects_unfinished_rounds_without_a_final_question()
    {
        var session = RestoreAtRound([false, true, true], 2);
        Assert.Null(session.Quiz.FinalQuestion);
        Assert.True(session.HasAnyUnfinishedRegularRound);
        Assert.False(session.HasNextUnfinishedRound);
    }

    [Fact]
    public void Return_to_nearest_unfinished_round_prefers_previous_on_equal_distance()
    {
        var session = RestoreAtRound([false, true, false], 1);
        Assert.Equal(1, session.ReturnToNearestUnfinishedRoundExcludingCurrent().SourceRoundId);
    }

    [Fact]
    public void Natural_final_can_start_after_finishing_a_returned_unfinished_round()
    {
        var session = RestoreAtRound(
            [true, false, true],
            2,
            hasFinalQuestion: true,
            furthestVisitedRoundIndex: 2);
        session.AddPlayer("Rose");

        session.ReturnToNearestUnfinishedRoundExcludingCurrent();
        CloseCurrentRound(session);

        session.StartFinalQuestion();

        Assert.Equal(GameSessionStatus.FinalWagering, session.Status);
        Assert.Equal(2, session.CurrentRoundIndex);
        Assert.False(session.HasAnyUnfinishedRegularRound);
    }

    [Fact]
    public void Final_question_state_has_no_regular_round_navigation()
    {
        var session = RestoreAtRound([true], 0, hasFinalQuestion: true);
        session.AddPlayer("Rose");
        session.StartFinalQuestion();

        Assert.Equal(GameSessionStatus.FinalWagering, session.Status);
        Assert.False(session.HasPreviousUnfinishedRound);
        Assert.False(session.HasNextUnfinishedRound);
    }

    private static GameSession CreateMatrixSession(string scenario, bool hasFinalQuestion)
    {
        var backward = scenario[..scenario.IndexOfAny(['C','E','G'])];
        var forwardStart = backward.Length;
        var forward = scenario[forwardStart..^1];

        var complete = new List<bool>();
        int current;
        switch (backward)
        {
  case "B": complete.Add(false); current = 0; break;
  case "D": complete.Add(false); complete.Add(false); current = 1; break;
  case "FH": complete.Add(false); complete.Add(true); complete.Add(false); current = 2; break;
  case "FI": complete.Add(true); complete.Add(true); complete.Add(false); current = 2; break;
  default: throw new InvalidOperationException(backward);
        }

        switch (forward)
        {
  case "C": break;
  case "E": complete.Add(false); break;
  case "GJ": complete.Add(true); complete.Add(false); break;
  case "GK": complete.Add(true); complete.Add(true); break;
  default: throw new InvalidOperationException(forward);
        }

        return RestoreAtRound([.. complete], current, hasFinalQuestion);
    }

    private static GameSession RestoreAtRound(
        bool[] complete,
        int currentRoundIndex,
        bool hasFinalQuestion = false,
        int? furthestVisitedRoundIndex = null)
    {
        var rounds = complete.Select((_, index) => new QuizRoundSnapshot(
  index + 1,
  $"Round {index + 1}",
  index,
  [new QuizQuestionSnapshot(101 + index, 10 + index, 0, 100, false, $"Category {index + 1}")]))
  .ToArray();

        FinalQuestionSnapshot? final = hasFinalQuestion
  ? new FinalQuestionSnapshot(
      [new ContentBlockSnapshot(900, ContentBlockKind.Text, "Final?", null, null, null, null, null, null, null, 0, false)],
      [new ContentBlockSnapshot(901, ContentBlockKind.Text, "Final!", null, null, null, null, null, null, null, 0, false)])
  : null;
        var quiz = new QuizSnapshot(1, "Navigation", rounds, final);
        var seed = GameSession.Create(quiz);
        seed.Start();
        var state = seed.CaptureState();
        var questionStates = state.Questions.Select((question, index) =>
  question with { Status = complete[index] ? RuntimeQuestionStatus.Resolved : RuntimeQuestionStatus.Available })
  .ToArray();

        return GameSession.Restore(
  quiz,
  seed.Settings,
  state with
  {
      CurrentRoundIndex = currentRoundIndex,
      FurthestVisitedRoundIndex = furthestVisitedRoundIndex ?? currentRoundIndex,
      Questions = questionStates
  });
    }

    private static void CloseCurrentRound(GameSession session)
    {
        foreach (var question in session.Board.Questions.Where(q =>
           q.SourceRoundId == session.CurrentRound.SourceRoundId &&
           q.Status == RuntimeQuestionStatus.Available))
        {
  session.CloseAvailableQuestion(question.SourceQuestionId);
        }
    }
}
