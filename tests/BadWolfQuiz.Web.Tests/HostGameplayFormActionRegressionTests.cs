namespace BadWolfQuiz.Web.Tests;

public sealed class HostGameplayFormActionRegressionTests
{
    [Fact]
    public void Gameplay_submitter_without_formaction_uses_the_form_handler_url()
    {
        var script = File.ReadAllText(FindSiteScript());

        Assert.Contains(
            "submitter?.hasAttribute(\"formaction\") === true",
            script);
        Assert.Contains(
            "submitterHasFormAction ? submitter.formAction : form.action",
            script);
        Assert.DoesNotContain(
            "submitter?.formAction || form.action",
            script);
    }

    private static string FindSiteScript()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "BadWolfQuiz.Web",
                "wwwroot",
                "js",
                "site.js");
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "Could not locate BadWolfQuiz.Web/wwwroot/js/site.js from the test output directory.");
    }
}
