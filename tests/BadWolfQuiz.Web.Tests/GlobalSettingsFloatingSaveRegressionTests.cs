namespace BadWolfQuiz.Web.Tests;

public sealed class GlobalSettingsFloatingSaveRegressionTests
{
    [Fact]
    public void Global_settings_use_dirty_only_ajax_floating_save_bar_and_navigation_guard()
    {
        var root = FindRepositoryRoot();
        var page = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Settings",
            "Index.cshtml"));
        var viewImports = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "_ViewImports.cshtml"));
        var tagHelper = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "TagHelpers",
            "HostSettingsFloatingSaveAssetsTagHelper.cs"));
        var script = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "host-settings-floating-save.js"));
        var shortcut = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "host-settings-save-shortcut.js"));
        var stylesheet = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "css",
            "host-settings-floating-save.css"));

        Assert.Contains("class=\"host-settings-form\"", page, StringComparison.Ordinal);
        Assert.Contains("class=\"host-settings-actions\"", page, StringComparison.Ordinal);
        Assert.Contains("Button_Save", page, StringComparison.Ordinal);

        Assert.Contains(
            "HostSettingsFloatingSaveAssetsTagHelper, BadWolfQuiz.Web",
            viewImports,
            StringComparison.Ordinal);
        Assert.Contains("[HtmlTargetElement(\"head\")]", tagHelper, StringComparison.Ordinal);
        Assert.Contains("[HtmlTargetElement(\"body\")]", tagHelper, StringComparison.Ordinal);
        Assert.Contains("ViewData.Model is not IndexModel", tagHelper, StringComparison.Ordinal);
        Assert.Contains("/css/host-settings-floating-save.css?v=2", tagHelper, StringComparison.Ordinal);
        Assert.Contains("/js/host-settings-floating-save.js?v=2", tagHelper, StringComparison.Ordinal);
        Assert.Contains("/js/host-settings-save-shortcut.js?v=1", tagHelper, StringComparison.Ordinal);

        Assert.Contains("host-settings-floating-save-bar", script, StringComparison.Ordinal);
        Assert.Contains("actions.hidden = true", script, StringComparison.Ordinal);
        Assert.Contains("serializeFormState", script, StringComparison.Ordinal);
        Assert.Contains("dirtyCandidate && serializeFormState() !== baselineState", script, StringComparison.Ordinal);
        Assert.Contains("form.addEventListener(\"input\"", script, StringComparison.Ordinal);
        Assert.Contains("form.addEventListener(\"change\"", script, StringComparison.Ordinal);
        Assert.DoesNotContain("form.addEventListener(\"click\"", script, StringComparison.Ordinal);
        Assert.Contains("form.addEventListener(\"submit\", event =>", script, StringComparison.Ordinal);
        Assert.Contains("event.preventDefault();", script, StringComparison.Ordinal);
        Assert.Contains("await fetch(form.action || window.location.href", script, StringComparison.Ordinal);
        Assert.Contains("\"X-Requested-With\": \"XMLHttpRequest\"", script, StringComparison.Ordinal);
        Assert.Contains("new DOMParser().parseFromString", script, StringComparison.Ordinal);
        Assert.Contains("response.redirected", script, StringComparison.Ordinal);
        Assert.Contains("baselineState = serializeFormState();", script, StringComparison.Ordinal);
        Assert.Contains("window.BadWolfBusy?.show()", script, StringComparison.Ordinal);
        Assert.Contains("window.BadWolfBusy?.hide()", script, StringComparison.Ordinal);
        Assert.Contains("form.requestSubmit()", script, StringComparison.Ordinal);
        Assert.Contains("event.key.toLowerCase() !== \"s\"", script, StringComparison.Ordinal);
        Assert.Contains("beforeunload", script, StringComparison.Ordinal);
        Assert.Contains("event.returnValue = \"\"", script, StringComparison.Ordinal);
        Assert.Contains("host-settings-unsaved-dialog", script, StringComparison.Ordinal);
        Assert.Contains("dialog.showModal()", script, StringComparison.Ordinal);
        Assert.DoesNotContain("window.confirm", script, StringComparison.Ordinal);
        Assert.Contains("document.querySelector(\".portal-footer\")", script, StringComparison.Ordinal);
        Assert.Contains("footer.getBoundingClientRect()", script, StringComparison.Ordinal);
        Assert.Contains("--host-settings-footer-offset", script, StringComparison.Ordinal);
        Assert.Contains("ResizeObserver", script, StringComparison.Ordinal);
        Assert.Contains("ru: {", script, StringComparison.Ordinal);
        Assert.Contains("unsaved: \"Україна\"", script, StringComparison.Ordinal);
        Assert.Contains("saved: \"Україна\"", script, StringComparison.Ordinal);

        Assert.Contains("event.code === \"KeyS\"", shortcut, StringComparison.Ordinal);
        Assert.Contains("event.ctrlKey || event.metaKey", shortcut, StringComparison.Ordinal);
        Assert.Contains("event.preventDefault();", shortcut, StringComparison.Ordinal);
        Assert.Contains("event.stopImmediatePropagation();", shortcut, StringComparison.Ordinal);
        Assert.Contains("window.BadWolfHostSettingsFloatingSave?.hasUnsavedChanges?.()", shortcut, StringComparison.Ordinal);
        Assert.Contains("form.requestSubmit();", shortcut, StringComparison.Ordinal);
        Assert.Contains("{ capture: true }", shortcut, StringComparison.Ordinal);

        Assert.Contains(
            ".host-settings-actions.host-settings-floating-save-bar[hidden]",
            stylesheet,
            StringComparison.Ordinal);
        Assert.Contains("position: fixed", stylesheet, StringComparison.Ordinal);
        Assert.Contains(
            "bottom: calc(var(--host-settings-footer-offset) + var(--host-settings-save-gap))",
            stylesheet,
            StringComparison.Ordinal);
        Assert.Contains("z-index: 120", stylesheet, StringComparison.Ordinal);
        Assert.Contains(".host-settings-page.host-settings-dirty", stylesheet, StringComparison.Ordinal);
        Assert.Contains("prefers-reduced-motion", stylesheet, StringComparison.Ordinal);
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
