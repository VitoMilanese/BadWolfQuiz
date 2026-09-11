namespace BadWolfQuiz.Web.Tests;

public sealed class AnswerRewardButtonsRegressionTests
{
    [Fact]
    public void Question_editor_exposes_opt_in_setting_and_persists_it()
    {
        var root = FindRepositoryRoot();
        var view = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Quizzes", "QuestionEditor.cshtml"));
        var model = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Quizzes", "QuestionEditor.cshtml.cs"));
        var entity = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "Models", "QuizModels.cs"));

        Assert.Contains("asp-for=\"Input.AllowAnswerRewardModifiers\"", view);
        Assert.Contains("Label_AllowAnswerRewardModifiers", view);
        Assert.Contains("Hint_AllowAnswerRewardModifiers", view);
        Assert.Contains("public bool AllowAnswerRewardModifiers { get; set; }", entity);
        Assert.Contains("AllowAnswerRewardModifiers = question.AllowAnswerRewardModifiers", model);
        Assert.Contains("question.AllowAnswerRewardModifiers = Input.AllowAnswerRewardModifiers", model);
    }

    [Fact]
    public void Host_generic_judging_orders_correct_double_half_incorrect_and_maps_modifiers()
    {
        var root = FindRepositoryRoot();
        var view = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "Lobby.cshtml"));
        var model = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "Lobby.cshtml.cs"));
        var registry = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "Services", "GameSessionRegistry.cs"));

        var formStart = view.IndexOf("<form class=\"question-judge-actions\"", StringComparison.Ordinal);
        Assert.True(formStart >= 0);
        var formEnd = view.IndexOf("</form>", formStart, StringComparison.Ordinal);
        Assert.True(formEnd > formStart);
        var form = view[formStart..formEnd];

        Assert.Contains("Model.CurrentQuestion.AllowAnswerRewardModifiers", form);
        var correct = form.IndexOf("value=\"correct\"", StringComparison.Ordinal);
        var doubleReward = form.IndexOf("value=\"double\"", StringComparison.Ordinal);
        var halfReward = form.IndexOf("value=\"half\"", StringComparison.Ordinal);
        var incorrect = form.IndexOf("value=\"incorrect\"", StringComparison.Ordinal);
        Assert.True(correct >= 0 && doubleReward > correct && halfReward > doubleReward && incorrect > halfReward);
        Assert.Contains(">x2</button>", form);
        Assert.Contains(">1/2</button>", form);

        Assert.Contains("\"double\" => (IsCorrect: true, RewardModifier: AnswerRewardModifier.Double)", model);
        Assert.Contains("\"half\" => (IsCorrect: true, RewardModifier: AnswerRewardModifier.Half)", model);
        Assert.Contains("resolvedJudgment.RewardModifier", model);
        Assert.Contains("AnswerRewardModifier rewardModifier = AnswerRewardModifier.Normal", registry);
        Assert.Contains("rewardModifier);", registry);
        Assert.Contains("judgingButton?.name === \"judgment\"", view);
        Assert.Contains("judgingButton.value !== \"incorrect\"", view);
    }

    [Fact]
    public void Authored_reward_setting_flows_through_snapshot_copy_clone_and_package_paths()
    {
        var root = FindRepositoryRoot();
        string Read(params string[] parts) => File.ReadAllText(Path.Combine([root, .. parts]));

        Assert.Contains("question.AllowAnswerRewardModifiers",
            Read("src", "BadWolfQuiz.Web", "Services", "QuizSnapshotFactory.cs"));
        Assert.Contains("snapshot.AllowAnswerRewardModifiers",
            Read("src", "BadWolfQuiz.Web", "Services", "QuizSnapshotJsonConverter.cs"));
        Assert.Contains("question.AllowAnswerRewardModifiers",
            Read("src", "BadWolfQuiz.Web", "Services", "DeferredGameMedia.cs"));
        Assert.Contains("sourceQuestion.AllowAnswerRewardModifiers",
            Read("src", "BadWolfQuiz.Web", "Services", "QuizPackageService.cs"));
        Assert.Contains("sourceQuestion.AllowAnswerRewardModifiers",
            Read("src", "BadWolfQuiz.Web", "Services", "QuizCloneOperations.cs"));
        Assert.Contains("source.AllowAnswerRewardModifiers",
            Read("src", "BadWolfQuiz.Web", "Services", "QuestionCopyOperations.cs"));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tests")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
