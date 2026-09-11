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
        var workspaceStyles = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "wwwroot", "css", "quiz-editor-workspace.css"));
        var localizationRoot = Path.Combine(
            root, "src", "BadWolfQuiz.Web", "Resources", "Localization");
        var english = File.ReadAllText(Path.Combine(localizationRoot, "SharedResource.resx"));
        var ukrainian = File.ReadAllText(Path.Combine(localizationRoot, "SharedResource.uk.resx"));
        var italian = File.ReadAllText(Path.Combine(localizationRoot, "SharedResource.it.resx"));
        var russian = File.ReadAllText(Path.Combine(localizationRoot, "SharedResource.ru.resx"));

        Assert.Contains("asp-for=\"Input.AllowAnswerRewardModifiers\"", view);
        Assert.Contains("Label_AllowAnswerRewardModifiers", view);
        Assert.Contains("Hint_AllowAnswerRewardModifiers", view);

        var wagerMode = view.IndexOf("id=\"wager-mode-setting\"", StringComparison.Ordinal);
        var buzzMode = view.IndexOf("id=\"buzz-mode-setting\"", StringComparison.Ordinal);
        var rewardSetting = view.IndexOf(
            "class=\"checkbox-row answer-reward-modifier-setting\"",
            StringComparison.Ordinal);
        Assert.True(wagerMode >= 0 && buzzMode > wagerMode && rewardSetting > buzzMode);

        Assert.Contains(
            "body[data-quiz-editor-workspace=\"question\"] .answer-reward-modifier-setting > span > strong",
            workspaceStyles);
        Assert.Contains("Дозволити оцінювання відповіді x2 та 1/2.", ukrainian);
        Assert.Contains(
            "Ведучий зможе зарахувати правильну відповідь із її подвійною або половинною нагородою.",
            ukrainian);
        Assert.Contains("Allow x2 and 1/2 answer scoring.", english);
        Assert.Contains(
            "The host can award a correct answer double or half its normal reward.",
            english);
        Assert.Contains("Consenti la valutazione delle risposte x2 e 1/2.", italian);
        Assert.Contains(
            "Il conduttore potrà assegnare a una risposta corretta una ricompensa doppia o dimezzata.",
            italian);
        Assert.Contains("Разрешить оценивание ответа x2 и 1/2.", russian);
        Assert.Contains(
            "Ведущий сможет засчитать правильный ответ с удвоенной или половинной наградой.",
            russian);

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
