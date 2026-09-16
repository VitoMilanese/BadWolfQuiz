namespace BadWolfQuiz.Web.Tests;

public sealed class HostPanelClickRecoveryRegressionTests
{
    [Fact]
    public void Host_shell_loads_panel_click_recovery_before_submit_guard()
    {
        var helper = File.ReadAllText(FindWebFile(
            "TagHelpers",
            "HostGameplaySubmitGuardAssetsTagHelper.cs"));

        var recoveryIndex = helper.IndexOf(
            "/js/host-panel-click-recovery.js?v=1",
            StringComparison.Ordinal);
        var submitGuardIndex = helper.IndexOf(
            "/js/host-gameplay-submit-guard.js?v=5",
            StringComparison.Ordinal);

        Assert.True(recoveryIndex >= 0);
        Assert.True(submitGuardIndex > recoveryIndex);
        Assert.Contains(
            "data-host-panel-click-recovery",
            helper,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Recovery_tracks_every_dynamic_host_panel_with_clickable_rows()
    {
        var script = ReadRecoveryScript();

        Assert.Contains(".host-multiple-choice-panel", script, StringComparison.Ordinal);
        Assert.Contains(".all-player-host-progress", script, StringComparison.Ordinal);
        Assert.Contains(".peer-rated-host-sidebar", script, StringComparison.Ordinal);
        Assert.Contains(".final-submission-list", script, StringComparison.Ordinal);
        Assert.Contains(
            ".host-multiple-choice-panel button:not([disabled])",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            ".all-player-host-progress button:not([disabled])",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            ".peer-rated-host-sidebar button:not([disabled])",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            ".final-submission-list button:not([disabled])",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Recovery_replays_only_a_matching_replacement_after_native_click_was_lost()
    {
        var script = ReadRecoveryScript();

        Assert.Contains("window.addEventListener(\"pointerdown\"", script, StringComparison.Ordinal);
        Assert.Contains("window.addEventListener(\"pointerup\"", script, StringComparison.Ordinal);
        Assert.Contains("window.addEventListener(\"click\"", script, StringComparison.Ordinal);
        Assert.Contains("press.clickDelivered", script, StringComparison.Ordinal);
        Assert.Contains("press.button.isConnected", script, StringComparison.Ordinal);
        Assert.Contains("document.elementFromPoint", script, StringComparison.Ordinal);
        Assert.Contains("actionSignature(button) === press.signature", script, StringComparison.Ordinal);
        Assert.Contains("replacement.click();", script, StringComparison.Ordinal);
        Assert.Contains("trackedButtonSelector", script, StringComparison.Ordinal);
        Assert.Contains("window.setTimeout(() =>", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Recovery_signature_disambiguates_player_rows_and_form_actions()
    {
        var script = ReadRecoveryScript();

        Assert.Contains("handler: handlerName(form)", script, StringComparison.Ordinal);
        Assert.Contains("playerId: fieldValue(form, \"playerId\")", script, StringComparison.Ordinal);
        Assert.Contains("sourceQuestionId: fieldValue(form, \"sourceQuestionId\")", script, StringComparison.Ordinal);
        Assert.Contains("row: rowIdentity(button)", script, StringComparison.Ordinal);
        Assert.Contains("row.querySelector(\":scope > strong\")", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Recovery_does_not_turn_drag_or_cancel_into_a_synthetic_click()
    {
        var script = ReadRecoveryScript();

        Assert.Contains("Math.hypot", script, StringComparison.Ordinal);
        Assert.Contains("drift > maximumPointerDrift", script, StringComparison.Ordinal);
        Assert.Contains("window.addEventListener(\"pointercancel\"", script, StringComparison.Ordinal);
        Assert.Contains("replacementHitSlop", script, StringComparison.Ordinal);
    }

    private static string ReadRecoveryScript() => File.ReadAllText(FindWebFile(
        "wwwroot",
        "js",
        "host-panel-click-recovery.js"));

    private static string FindWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                new[]
                {
                    directory.FullName,
                    "src",
                    "BadWolfQuiz.Web"
                }.Concat(parts).ToArray());

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate BadWolfQuiz.Web file: {string.Join('/', parts)}");
    }
}
