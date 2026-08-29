namespace BadWolfQuiz.Web.Tests;

public sealed class MultipleChoiceEditorStabilityRegressionTests
{
    [Fact]
    public void Multiple_choice_editor_scopes_structural_guard_to_controller_initialization()
    {
        var root = FindRepositoryRoot();
        var guard = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "multiple-choice-answer-options-guard.js"));
        var tagHelper = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "TagHelpers",
            "MultipleChoiceAnswerOptionsAssetsTagHelper.cs"));

        Assert.Contains(
            ".multiple-choice-answer-options-help[hidden]",
            guard,
            StringComparison.Ordinal);
        Assert.Contains("display: none !important;", guard, StringComparison.Ordinal);
        Assert.Contains("isStructuralAnswerMutation", guard, StringComparison.Ordinal);
        Assert.Contains("containsContentBlockCard", guard, StringComparison.Ordinal);
        Assert.Contains(
            "#answer-blocks [data-content-block-list]",
            guard,
            StringComparison.Ordinal);
        Assert.Contains(
            "multiple-choice-answer-option-correct-badge",
            guard,
            StringComparison.Ordinal);
        Assert.Contains("isRedundantAnswerInputSync", guard, StringComparison.Ordinal);
        Assert.Contains("listener.name === \"scheduleSync\"", guard, StringComparison.Ordinal);
        Assert.Contains("const installOverrides = () =>", guard, StringComparison.Ordinal);
        Assert.Contains("const restoreOverrides = () =>", guard, StringComparison.Ordinal);
        Assert.Contains("capture: true, once: true", guard, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "window.MutationObserver = GuardedMutationObserver;\n    }\n\n    EventTarget.prototype.addEventListener",
            guard,
            StringComparison.Ordinal);

        var guardAsset = "multiple-choice-answer-options-guard.js?v=382.7";
        var controllerAsset = "multiple-choice-answer-options.js?v=382.7";
        Assert.Contains(guardAsset, tagHelper, StringComparison.Ordinal);
        Assert.Contains(controllerAsset, tagHelper, StringComparison.Ordinal);
        Assert.True(
            tagHelper.IndexOf(guardAsset, StringComparison.Ordinal) <
            tagHelper.IndexOf(controllerAsset, StringComparison.Ordinal));
        Assert.Contains(
            "badWolfMultipleChoiceAnswerOptionsRestoreMutationObserver",
            tagHelper,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Multiple_choice_reveal_uses_direct_vertical_full_width_blocks_and_hides_them_during_close()
    {
        var root = FindRepositoryRoot();
        var reveal = File.ReadAllText(Path.Combine(
                root,
                "src",
                "BadWolfQuiz.Web",
                "Pages",
                "Admin",
                "Games",
                "_MultipleChoiceRevealBlocks.cshtml"))
            .ReplaceLineEndings("\n");

        Assert.Contains(
            ".game-content-blocks.multiple-choice-answer-reveal-grid",
            reveal,
            StringComparison.Ordinal);
        Assert.Contains("display: flex !important;", reveal, StringComparison.Ordinal);
        Assert.Contains("flex-direction: column !important;", reveal, StringComparison.Ordinal);
        Assert.Contains(
            "<article class=\"game-content-block @correctOptionClass @additionalClass\">",
            reveal,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "multiple-choice-answer-reveal-stack",
            reveal,
            StringComparison.Ordinal);
        Assert.Contains("border: 0 !important;", reveal, StringComparison.Ordinal);
        Assert.Contains(
            "> .all-player-answer-option-correct",
            reveal,
            StringComparison.Ordinal);
        Assert.Contains(
            "form[action*=\"handler=CloseAnswer\"] button:disabled",
            reveal,
            StringComparison.Ordinal);
        Assert.Contains("visibility: hidden !important;", reveal, StringComparison.Ordinal);
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
