namespace BadWolfQuiz.Web.Tests;

public sealed class MultipleChoiceAnswerOptionRemovalRegressionTests
{
    [Fact]
    public void Leaving_choice_type_re_enables_remove_buttons_for_unwrapped_options()
    {
        var script = File.ReadAllText(FindRepoFile(
            "src", "BadWolfQuiz.Web", "wwwroot", "js",
            "multiple-choice-answer-options.js"));

        var unwrapStart = script.IndexOf(
            "const unwrapStructure = () => {",
            StringComparison.Ordinal);
        var unwrapEnd = script.IndexOf(
            "const setAllowedTopLevelTypes",
            unwrapStart,
            StringComparison.Ordinal);

        Assert.True(unwrapStart >= 0);
        Assert.True(unwrapEnd > unwrapStart);

        var unwrap = script[unwrapStart..unwrapEnd];
        Assert.Contains(".content-block-remove-button", unwrap);
        Assert.Contains("remove.disabled = false;", unwrap);

        Assert.Contains(
            "remove.disabled = options.length <= min && isAllowed;",
            script);
    }

    [Theory]
    [InlineData("3")]
    [InlineData("4")]
    public void Both_choice_types_use_the_shared_unwrap_path_when_type_changes(
        string choiceType)
    {
        var script = File.ReadAllText(FindRepoFile(
            "src", "BadWolfQuiz.Web", "wwwroot", "js",
            "multiple-choice-answer-options.js"));

        var choiceTypeStart = script.IndexOf(
            "const isChoiceType = value =>",
            StringComparison.Ordinal);
        var choiceTypeEnd = script.IndexOf(
            ';',
            choiceTypeStart);
        Assert.True(choiceTypeStart >= 0);
        Assert.True(choiceTypeEnd > choiceTypeStart);

        var choiceTypeDefinition = script[choiceTypeStart..choiceTypeEnd];
        Assert.Contains($"value === \"{choiceType}\"", choiceTypeDefinition);

        var changeStart = script.IndexOf(
            "select.addEventListener(\"change\"",
            StringComparison.Ordinal);
        var submitStart = script.IndexOf(
            "form.addEventListener(\"submit\"",
            changeStart,
            StringComparison.Ordinal);
        Assert.True(changeStart >= 0);
        Assert.True(submitStart > changeStart);

        var changeHandler = script[changeStart..submitStart];
        Assert.Contains("if (isChoiceType(select.value))", changeHandler);
        Assert.Contains("ensureStructure()", changeHandler);
        Assert.Contains("unwrapStructure();", changeHandler);
    }

    private static string FindRepoFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(
                new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(path))
            {
                return path;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not find repository file: {Path.Combine(segments)}");
    }
}
