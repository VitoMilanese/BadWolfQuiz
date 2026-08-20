namespace BadWolfQuiz.Web.Tests;

public sealed class HostMultipleChoiceWebRegressionTests
{
    [Fact]
    public void Editor_and_host_gameplay_wire_host_multiple_choice_rules()
    {
        var root = FindRepositoryRoot();
        var editor = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Quizzes",
            "QuestionEditor.cshtml.cs"));
        var endpoint = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Games",
            "HostMultipleChoice.cshtml.cs"));
        var gameContentPreview = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Games",
            "_GameContentPreview.cshtml"));
        var script = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "host-multiple-choice.js"));
        var bootstrap = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "host-multiple-choice-bootstrap.js"));
        var viewImports = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "_ViewImports.cshtml"));
        var assetsTagHelper = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "TagHelpers",
            "HostMultipleChoiceAssetsTagHelper.cs"));
        var answerBlockTagHelper = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "TagHelpers",
            "HostMultipleChoiceAnswerBlockTagHelper.cs"));
        var genericControlsTagHelper = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "TagHelpers",
            "HostMultipleChoiceGenericControlsTagHelper.cs"));

        Assert.Contains(
            "QuestionPresentationType.HostMultipleChoice",
            editor);
        Assert.Contains("options.Count is < 4 or > 10", editor);
        Assert.Contains("option.TextContent.Trim().Length > 20", editor);
        Assert.Contains("Distinct(StringComparer.OrdinalIgnoreCase)", editor);
        Assert.Contains("isHostMultipleChoice || Input.ExcludeFromRandomWagerSelection", editor);

        Assert.Contains("SelectHostMultipleChoiceOption", endpoint);
        Assert.Contains("HostMultipleChoiceRewardPercentage", endpoint);
        Assert.Contains("RemainingHostMultipleChoiceOptions", endpoint);
        Assert.Contains("BuzzerStateChanged", endpoint);
        Assert.Contains("TimerStateChanged", endpoint);
        Assert.Contains("CreateOptionDisplayOrder", endpoint);
        Assert.Contains("new Random(CreateOptionDisplaySeed", endpoint);
        Assert.Contains("shuffledOrder.SequenceEqual(originalOrder)", endpoint);
        Assert.Contains(".Where(remainingOptionsById.ContainsKey)", endpoint);
        Assert.Contains(
            "var hasEligiblePlayer = game.Session.Players.Any",
            endpoint);
        Assert.Contains(
            "game.Session.ResolveQuestionWithoutCorrectAnswer",
            endpoint);
        Assert.Contains(
            "result = result with { QuestionClosed = true };",
            endpoint);

        var stateHandlerStart = endpoint.IndexOf(
            "public IActionResult OnGetState(Guid id)",
            StringComparison.Ordinal);
        var selectHandlerStart = endpoint.IndexOf(
            "public async Task<IActionResult> OnPostSelectAsync",
            StringComparison.Ordinal);
        Assert.True(stateHandlerStart >= 0 && selectHandlerStart > stateHandlerStart);
        var stateHandler = endpoint[stateHandlerStart..selectHandlerStart];
        Assert.DoesNotContain("BuzzerStateChanged", stateHandler);
        Assert.DoesNotContain("SendAsync", stateHandler);

        Assert.Contains("value = \"4\"", script);
        Assert.Contains("textarea.maxLength = 20", script);
        Assert.Contains("cards.length < 4 || cards.length > 10", script);
        Assert.Contains("host-multiple-choice-correct-badge", script);
        Assert.Contains("host-multiple-choice-panel", script);
        Assert.Contains("data-question-heading", script);
        Assert.Contains("handler=ResolveQuestion", script);
        Assert.Contains("handler=Select", script);
        Assert.Contains("window.setInterval(poll, 750)", script);
        Assert.Contains(
            "\"badwolf:host-gameplay-updated\",\n                initializeHostGameplay",
            script);
        Assert.Contains(
            "document.addEventListener(\"badwolf:host-gameplay-updated\", poll);",
            script);
        Assert.Contains("await refreshHostGameplay();", script);
        Assert.DoesNotContain("window.location.reload()", script);

        var optionLoopIndex = script.IndexOf(
            "for (const option of state.options ?? [])",
            StringComparison.Ordinal);
        var noAnswerIndex = script.IndexOf(
            "panel.appendChild(createNoAnswerForm(state));",
            StringComparison.Ordinal);
        Assert.True(optionLoopIndex >= 0 && noAnswerIndex > optionLoopIndex);

        Assert.Contains(
            "'[data-open-question-preview=\"answer\"]'",
            bootstrap);
        Assert.Contains("presentationType.value !== \"4\"", bootstrap);
        Assert.Contains("const firstCard = answerSection.querySelector", bootstrap);
        Assert.Contains("event.stopImmediatePropagation();", bootstrap);
        Assert.Contains("content.replaceChildren(answerPreview);", bootstrap);
        Assert.Contains("top: 13rem !important", bootstrap);
        Assert.DoesNotContain("initializeHostGameplayLifecycle", bootstrap);
        Assert.DoesNotContain("host-multiple-choice.js?v=", bootstrap);

        Assert.Contains(
            "QuestionPresentationType.HostMultipleChoice",
            gameContentPreview);
        Assert.Contains(
            ".OrderBy(block => block.SortOrder)",
            gameContentPreview);
        Assert.Contains(".Take(1)", gameContentPreview);

        Assert.Contains(
            "RuntimeQuestionStatus.ShowingAnswer",
            answerBlockTagHelper);
        Assert.Contains(
            "question.HostMultipleChoiceCorrectOptionId",
            answerBlockTagHelper);
        Assert.Contains("output.SuppressOutput();", answerBlockTagHelper);

        Assert.Contains("asp-page-handler", genericControlsTagHelper);
        Assert.Contains("IsHostMultipleChoice: true", genericControlsTagHelper);
        Assert.Contains("\"JudgeQuestionAnswer\"", genericControlsTagHelper);
        Assert.Contains("\"ResolveQuestion\"", genericControlsTagHelper);
        Assert.Contains("output.SuppressOutput();", genericControlsTagHelper);

        Assert.Contains(
            "HostMultipleChoiceAssetsTagHelper",
            viewImports);
        Assert.Contains(
            "HostMultipleChoiceAnswerBlockTagHelper",
            viewImports);
        Assert.Contains(
            "HostMultipleChoiceGenericControlsTagHelper",
            viewImports);
        Assert.Contains("host-multiple-choice-bootstrap.js", assetsTagHelper);
        Assert.DoesNotContain("data-host-lobby", assetsTagHelper);
        Assert.Contains("v=1.20.0-259.6", assetsTagHelper);
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
