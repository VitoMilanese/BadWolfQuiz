namespace BadWolfQuiz.Web.Tests;

public sealed class QuizTransferCompletionRegressionTests
{
    [Fact]
    public void QuizListLoadsCompletionSoundAssetsOnlyForItsPageModel()
    {
        var root = FindRepositoryRoot();
        var imports = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "Pages", "_ViewImports.cshtml"));
        var tagHelper = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "TagHelpers",
            "QuizTransferCompletionAssetsTagHelper.cs"));

        Assert.Contains(
            "QuizTransferCompletionAssetsTagHelper, BadWolfQuiz.Web",
            imports,
            StringComparison.Ordinal);
        Assert.Contains("[HtmlTargetElement(\"body\")]", tagHelper, StringComparison.Ordinal);
        Assert.Contains("ViewContext.ViewData.Model is not IndexModel", tagHelper, StringComparison.Ordinal);
        Assert.Contains("/js/quiz-transfer-completion.js", tagHelper, StringComparison.Ordinal);
    }

    [Fact]
    public void TransferCompletionSoundIsArmedByUserGestureAndPlayedOnlyForSuccess()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "wwwroot", "js",
            "quiz-transfer-completion.js"));

        Assert.Contains("window.AudioContext || window.webkitAudioContext", script, StringComparison.Ordinal);
        Assert.Contains("const armCompletionSound = () =>", script, StringComparison.Ordinal);
        Assert.Contains("const playCompletionSound = () =>", script, StringComparison.Ordinal);
        Assert.Contains("oscillator.type = \"sine\"", script, StringComparison.Ordinal);
        Assert.Contains("armCompletionSound();\n        startTracking(\"export\")", script, StringComparison.Ordinal);
        Assert.Contains("armCompletionSound();\n        startTracking(\"import\", token)", script, StringComparison.Ordinal);
        Assert.Contains("const delay = succeeded ? playCompletionSound() : 0", script, StringComparison.Ordinal);
        Assert.Contains("if (succeeded) {\n            playCompletionSound();\n        }", script, StringComparison.Ordinal);
        Assert.DoesNotContain("if (!succeeded) {\n            playCompletionSound();", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ImportKeepsNativeMultipartSubmissionAliveInHiddenSameOriginFrame()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "wwwroot", "js",
            "quiz-transfer-completion.js"));
        var page = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Quizzes",
            "Index.cshtml.cs"));

        Assert.Contains("document.createElement(\"iframe\")", script, StringComparison.Ordinal);
        Assert.Contains("frame.hidden = true", script, StringComparison.Ordinal);
        Assert.Contains("tokenInput.name = \"importToken\"", script, StringComparison.Ordinal);
        Assert.Contains("form.target = frame.name", script, StringComparison.Ordinal);
        Assert.DoesNotContain("fetch(", script, StringComparison.Ordinal);

        Assert.Contains("string? importToken", page, StringComparison.Ordinal);
        Assert.Contains("Guid.TryParse(importToken, out _)", page, StringComparison.Ordinal);
        Assert.Contains("? new EmptyResult()", page, StringComparison.Ordinal);
        Assert.Contains(": RedirectToPage();", page, StringComparison.Ordinal);
    }

    [Fact]
    public void ServerSignalsSuccessOrFailureWithoutReplacingExistingExportBusyCookie()
    {
        var root = FindRepositoryRoot();
        var page = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Quizzes",
            "Index.cshtml.cs"));

        Assert.Contains(
            "ExportCompletionCookieName = \"badwolfquiz-export-complete\"",
            page,
            StringComparison.Ordinal);
        Assert.Contains(
            "TransferCompletionCookieName = \"badwolfquiz-transfer-complete\"",
            page,
            StringComparison.Ordinal);
        Assert.Contains("SignalExportCompletion(exportToken);", page, StringComparison.Ordinal);
        Assert.Contains(
            "SignalTransferCompletion(exportToken, \"export\", succeeded);",
            page,
            StringComparison.Ordinal);
        Assert.Contains(
            "SignalTransferCompletion(importToken, \"import\", succeeded);",
            page,
            StringComparison.Ordinal);
        Assert.Contains(
            "$\"{operation}:{token:D}:{(succeeded ? \"success\" : \"failure\")}\"",
            page,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ImportMarksSuccessOnlyAfterPackageImportReturnsSuccessfully()
    {
        var root = FindRepositoryRoot();
        var page = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Quizzes",
            "Index.cshtml.cs"));

        var importCall = page.IndexOf("await quizPackageService.ImportAsync(", StringComparison.Ordinal);
        var successAssignment = page.IndexOf("succeeded = true;", importCall, StringComparison.Ordinal);
        var invalidCatch = page.IndexOf("catch (InvalidDataException)", importCall, StringComparison.Ordinal);
        var jsonCatch = page.IndexOf("catch (System.Text.Json.JsonException)", importCall, StringComparison.Ordinal);
        var completionSignal = page.IndexOf(
            "SignalTransferCompletion(importToken, \"import\", succeeded);",
            importCall,
            StringComparison.Ordinal);

        Assert.True(importCall >= 0);
        Assert.True(successAssignment > importCall);
        Assert.True(invalidCatch > successAssignment);
        Assert.True(jsonCatch > invalidCatch);
        Assert.True(completionSignal > jsonCatch);
    }

    [Fact]
    public void QuizListErrorMessageAutoDismissesAfterTransferFailureReload()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "wwwroot", "js",
            "quiz-transfer-completion.js"));

        Assert.Contains("const messageAutoDismissMilliseconds = 4000", script, StringComparison.Ordinal);
        Assert.Contains("const scheduleErrorAutoDismiss = () =>", script, StringComparison.Ordinal);
        Assert.Contains(".message.message-error[role=\"alert\"]", script, StringComparison.Ordinal);
        Assert.Contains("message.classList.add(\"message-hidden\")", script, StringComparison.Ordinal);
        Assert.Contains("scheduleErrorAutoDismiss();", script, StringComparison.Ordinal);
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
