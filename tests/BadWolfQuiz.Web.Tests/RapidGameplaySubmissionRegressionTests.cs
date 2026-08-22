namespace BadWolfQuiz.Web.Tests;

public sealed class RapidGameplaySubmissionRegressionTests
{
    [Fact]
    public void Host_gameplay_loads_the_rapid_submit_guard()
    {
        var layout = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "board-header-layout.js"));

        Assert.Contains("/js/host-gameplay-submit-guard.js", layout);
        Assert.Contains("data-host-gameplay-submit-guard", layout);
    }

    [Fact]
    public void Busy_gameplay_submits_are_intercepted_and_replayed_once()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "host-gameplay-submit-guard.js"));

        Assert.Contains(
            "document.addEventListener(\"DOMContentLoaded\", initialize, { once: true });",
            script);
        Assert.Contains(
            "document.addEventListener(\"submit\", event =>",
            script);
        Assert.Contains("event.preventDefault();", script);
        Assert.Contains("event.stopImmediatePropagation();", script);
        Assert.Contains("submitter?.hasAttribute(\"disabled\")", script);
        Assert.Contains("pendingSubmission?.key === key", script);
        Assert.Contains("if (pendingSubmission === null)", script);
        Assert.Contains("pendingSubmission = { form, submitter, key };", script);
        Assert.Contains("submitter?.setAttribute(\"disabled\", \"disabled\");", script);
        Assert.Contains("submitter?.setAttribute(\"aria-busy\", \"true\");", script);
        Assert.Contains("pendingSubmission = null;", script);
        Assert.Contains("submitter?.removeAttribute(\"disabled\");", script);
        Assert.Contains("form.requestSubmit(submitter);", script);
        Assert.Contains("}, true);", script);
    }

    [Fact]
    public void Rapid_submit_key_ignores_antiforgery_token_and_includes_submitter_value()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "host-gameplay-submit-guard.js"));

        Assert.Contains(
            ".filter(([name]) => name !== \"__RequestVerificationToken\")",
            script);
        Assert.Contains("formData.append(submitter.name, submitter.value);", script);
        Assert.Contains("form.method.toUpperCase()", script);
        Assert.Contains("action.href", script);
    }

    private static string FindWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var pathParts = new string[parts.Length + 3];
            pathParts[0] = directory.FullName;
            pathParts[1] = "src";
            pathParts[2] = "BadWolfQuiz.Web";
            Array.Copy(parts, 0, pathParts, 3, parts.Length);

            var candidate = Path.Combine(pathParts);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not find web file: {string.Join('/', parts)}.");
    }
}
