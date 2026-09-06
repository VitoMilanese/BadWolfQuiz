using BadWolfQuiz.Web.Services;

namespace BadWolfQuiz.Web.Tests;

public sealed class MinigameAiOpponentTests
{
    [Theory]
    [InlineData(8, 10, true)]
    [InlineData(79, 100, false)]
    [InlineData(80, 100, true)]
    [InlineData(0, 0, false)]
    public void Coverage_requires_at_least_eighty_percent(
        int answered,
        int enabled,
        bool expected)
    {
        Assert.Equal(
            expected,
            MinigameAiCatalogStore.HasRequiredCoverage(answered, enabled));
    }

    [Fact]
    public void Dont_know_eliminates_nothing()
    {
        var opponent = CreateOpponent(
            4,
            (index, _) => index % 2 == 0,
            new Random(1));
        opponent.Reset(CreateCards(4), "4");
        var before = opponent.CandidateCount;

        opponent.ApplyAnswer("Question?", null);

        Assert.Equal(before, opponent.CandidateCount);
    }

    [Fact]
    public void Yes_and_no_keep_only_matching_candidates()
    {
        var knowledge = Enumerable.Range(1, 5)
            .Select(index => CreateKnowledge(
                index,
                new Dictionary<string, bool?>
                {
                    ["Question?"] = index <= 2
                }))
            .ToArray();
        var yes = new MinigameAiOpponent(knowledge, new Random(1));
        yes.Reset(CreateCards(5), "5");
        var no = new MinigameAiOpponent(knowledge, new Random(1));
        no.Reset(CreateCards(5), "5");

        yes.ApplyAnswer("Question?", true);
        no.ApplyAnswer("Question?", false);

        Assert.Equal(["1", "2"], yes.Candidates.Order().ToArray());
        Assert.Equal(["3", "4"], no.Candidates.Order().ToArray());
    }

    [Fact]
    public void Search_phase_never_guesses_when_question_cannot_reduce_candidates()
    {
        var opponent = CreateOpponent(
            11,
            (_, _) => true,
            new Random(1));
        opponent.Reset(CreateCards(11), "11");

        var action = opponent.Decide(["Question?"]);

        Assert.Equal(MinigameAiActionKind.AskQuestion, action.Kind);
        Assert.Equal(0, action.QuestionOptionIndex);
    }

    [Fact]
    public void Final_phase_asks_the_best_question_when_it_can_reduce_candidates()
    {
        var knowledge = Enumerable.Range(1, 21)
            .Select(index => CreateKnowledge(
                index,
                new Dictionary<string, bool?>
                {
                    ["Reduce to three?"] = index <= 3,
                    ["Best?"] = index == 1,
                    ["Useless?"] = true
                }))
            .ToArray();
        var opponent = new MinigameAiOpponent(knowledge, new Random(1));
        opponent.Reset(CreateCards(21), "21");
        opponent.ApplyAnswer("Reduce to three?", true);

        var action = opponent.Decide(["Best?", "Useless?"]);

        Assert.Equal(3, opponent.CandidateCount);
        Assert.Equal(MinigameAiActionKind.AskQuestion, action.Kind);
        Assert.Equal(0, action.QuestionOptionIndex);
    }

    [Fact]
    public void Question_score_does_not_count_unknown_answers_as_eliminated()
    {
        var knowledge = new[]
        {
            CreateKnowledge(1, new Dictionary<string, bool?> { ["Mostly unknown?"] = true, ["Split?"] = true }),
            CreateKnowledge(2, new Dictionary<string, bool?> { ["Mostly unknown?"] = null, ["Split?"] = true }),
            CreateKnowledge(3, new Dictionary<string, bool?> { ["Mostly unknown?"] = null, ["Split?"] = false }),
            CreateKnowledge(4, new Dictionary<string, bool?> { ["Mostly unknown?"] = null, ["Split?"] = false }),
            CreateKnowledge(5, new Dictionary<string, bool?> { ["Mostly unknown?"] = null, ["Split?"] = false })
        };
        var opponent = new MinigameAiOpponent(knowledge, new Random(1));
        opponent.Reset(CreateCards(5), ownSecretFileName: null);

        var action = opponent.Decide(["Mostly unknown?", "Split?"]);

        Assert.Equal(MinigameAiActionKind.AskQuestion, action.Kind);
        Assert.Equal(1, action.QuestionOptionIndex);
    }

    [Fact]
    public void One_candidate_is_guessed_immediately()
    {
        var knowledge = Enumerable.Range(1, 4)
            .Select(index => CreateKnowledge(
                index,
                new Dictionary<string, bool?>
                {
                    ["Question?"] = index == 1
                }))
            .ToArray();
        var opponent = new MinigameAiOpponent(knowledge, new Random(1));
        opponent.Reset(CreateCards(4), "4");
        opponent.ApplyAnswer("Question?", true);

        var action = opponent.Decide(["Question?"]);

        Assert.Equal(MinigameAiActionKind.Guess, action.Kind);
        Assert.Equal("1", action.GuessFileName);
    }

    [Fact]
    public void Empty_candidate_list_is_a_draw()
    {
        var opponent = CreateOpponent(
            4,
            (_, _) => true,
            new Random(1));
        opponent.Reset(CreateCards(4), "4");
        opponent.ApplyAnswer("Question?", false);

        var action = opponent.Decide(["Question?"]);

        Assert.Equal(MinigameAiActionKind.Draw, action.Kind);
    }

    private static MinigameAiOpponent CreateOpponent(
        int count,
        Func<int, string, bool?> answerFactory,
        Random random) =>
        new(
            Enumerable.Range(1, count)
                .Select(index => CreateKnowledge(
                    index,
                    new Dictionary<string, bool?>
                    {
                        ["Question?"] = answerFactory(index, "Question?")
                    }))
                .ToArray(),
            random);

    private static MinigameAiGameKnowledge CreateKnowledge(
        int index,
        IReadOnlyDictionary<string, bool?> answers) =>
        new(
            new MinigameCardDescriptor(index.ToString(), $"Game {index}"),
            answers);

    private static IReadOnlyList<MinigameCardDescriptor> CreateCards(int count) =>
        Enumerable.Range(1, count)
            .Select(index => new MinigameCardDescriptor(index.ToString(), $"Game {index}"))
            .ToArray();
}
