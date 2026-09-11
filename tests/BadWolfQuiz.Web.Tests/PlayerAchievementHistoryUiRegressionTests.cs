using System.Xml.Linq;

namespace BadWolfQuiz.Web.Tests;

public sealed class PlayerAchievementHistoryUiRegressionTests
{
    [Fact]
    public void Host_tools_and_history_page_are_wired_without_replacing_the_existing_player_dialog_endpoint()
    {
        var root = FindRepositoryRoot();
        var lobby = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "Lobby.cshtml"));
        var layout = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "Pages", "Shared", "_Layout.cshtml"));
        var page = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "PlayerAchievements.cshtml"));
        var model = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "PlayerAchievements.cshtml.cs"));
        var css = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "wwwroot", "css", "player-achievement-history.css"));
        var confirmationCss = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "wwwroot", "css", "player-achievement-history-confirmation.css"));
        var script = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "wwwroot", "js", "player-achievement-history.js"));

        Assert.Contains("asp-page=\"/Admin/Games/PlayerAchievements\"", lobby);
        Assert.Contains("History_Open", lobby);
        Assert.Contains("data-achievement-history-open", lobby);
        Assert.Contains("BadWolfBusy.navigate(this.href)", lobby);
        Assert.Contains("asp-page=\"/Admin/Games/PlayerAchievements\"", layout);
        Assert.Contains("History_Open", layout);
        Assert.Contains("data-achievement-history-open", layout);
        Assert.Contains("BadWolfBusy.navigate(this.href)", layout);

        Assert.Contains("~/css/answer-history.css", page);
        Assert.Contains("~/css/gameplay-review-fixes.css", page);
        Assert.Contains("~/css/player-achievement-history-confirmation.css", page);
        Assert.Contains("answer-history-page player-achievement-history-page", page);
        Assert.Contains("answer-history-hero", page);
        Assert.Contains("answer-history-session", page);
        Assert.Contains("answer-history-content", page);
        Assert.Contains("answer-history-section-heading", page);
        Assert.Contains("answer-history-question-card", page);
        Assert.Contains("History_FilterGame", page);
        Assert.Contains("History_FilterPlayers", page);
        Assert.Contains("History_FilterPending", page);
        Assert.Contains("Model.HasPendingConfirmations", page);
        Assert.Contains("data-achievement-history-pending-tab", page);
        Assert.Contains("asp-route-selectedPlayerId", page);
        Assert.DoesNotContain("asp-route-selected-player-id", page);
        Assert.Contains("History_NoGameUnlocks", page);
        Assert.Contains("History_NoPlayerUnlocks", page);
        Assert.Contains("History_NoPlayers", page);
        Assert.Contains("data-achievement-history-root", page);
        Assert.Contains("data-back-url", page);
        Assert.Contains("data-achievement-history-nav", page);
        Assert.Contains("data-achievement-history-view-nav", page);
        Assert.Contains("data-achievement-history-view", page);
        Assert.Contains("data-achievement-history-session-count", page);
        Assert.Contains("player-achievement-history-body", page);
        Assert.Contains("player-achievement-history-sidebar", page);
        Assert.Contains("(isPendingMode && Model.PendingConfirmations.Count > 0)", page);
        Assert.Contains("@foreach (var pendingPlayer in Model.PendingConfirmations)", page);
        Assert.Contains("Model.SelectedPendingConfirmation?.PlayerId == pendingPlayer.PlayerId", page);
        Assert.Contains("asp-route-selectedPlayerId=\"@pendingPlayer.PlayerId\"", page);
        Assert.Contains("data-achievement-history-time", page);
        Assert.Contains("data-achievement-history-confirm-form", page);
        Assert.Contains("asp-page-handler=\"ConfirmIdentity\"", page);
        Assert.Contains("name=\"playerId\"", page);
        Assert.DoesNotContain("name=\"accountId\"", page);
        Assert.Contains("History_ConfirmPrompt", page);
        Assert.Contains("History_ConfirmIdentity", page);
        Assert.Contains("player-achievement-history.js", page);

        Assert.Contains("sessionRegistry.FindOwned", model);
        Assert.Contains("if (playerId.HasValue)", model);
        Assert.Contains("BuildPlayerDialogJsonAsync", model);
        Assert.Contains("LoadForPlayerAsync", model);
        Assert.Contains("LoadPersistedUnlocksAsync", model);
        Assert.Contains("SourceGameSessionId", model);
        Assert.Contains("definition is null", model);
        Assert.Contains("public const string PendingMode = \"pending\"", model);
        Assert.Contains("SelectedPendingConfirmation", model);
        Assert.Contains("PendingConfirmations.FirstOrDefault", model);
        Assert.Contains("SelectedPlayerId = SelectedPendingConfirmation?.PlayerId", model);
        Assert.Contains("OnPostConfirmIdentityAsync", model);
        Assert.Contains("PlayerAchievementRuntimeState.GetPlayerAccountId", model);
        Assert.Contains("PlayerAchievementHistoryConfirmationService", model);
        Assert.Contains("confirmationService.ConfirmAsync", model);
        Assert.Contains("confirmationService.LoadPendingAsync", model);
        Assert.Contains("remaining.Count > 0 ? PendingMode : PlayersMode", model);
        Assert.Contains("remaining[0].PlayerId", model);

        Assert.Contains("justify-content: center", css);
        Assert.Contains("grid-template-columns: minmax(230px, 290px) minmax(0, 1fr)", css);
        Assert.Contains("width: 124px", css);
        Assert.Contains("linear-gradient(90deg, var(--red-bright), var(--gold), transparent 76%)", css);
        Assert.DoesNotContain("max-height: min(68vh, 720px)", css);
        Assert.DoesNotContain("overflow: auto", css);
        Assert.Contains("player-achievement-history-confirm-form", confirmationCss);
        Assert.Contains("player-achievement-history-pending-card", confirmationCss);
        Assert.Contains(".player-achievement-history-body.is-pending-mode", confirmationCss);
        Assert.Contains("grid-template-columns: minmax(230px, 290px) minmax(0, 1fr)", confirmationCss);
        Assert.DoesNotContain("overflow: auto", confirmationCss);

        Assert.Contains("Intl.DateTimeFormat", script);
        Assert.Contains("data-achievement-history-time", script);
        Assert.Contains("data-achievement-history-nav", script);
        Assert.Contains("data-achievement-history-view-nav", script);
        Assert.Contains("data-achievement-history-view", script);
        Assert.Contains("await fetch(targetUrl.href", script);
        Assert.Contains("window.history.pushState", script);
        Assert.Contains("window.addEventListener(\"popstate\"", script);
        Assert.Contains("window.scrollTo(scrollLeft, scrollTop)", script);
        Assert.Contains("BadWolfBusy?.show?.()", script);
        Assert.Contains("BadWolfBusy?.hide?.()", script);
        Assert.Contains("event.key !== \"Escape\"", script);
        Assert.Contains("BadWolfBusy?.navigate", script);
        Assert.Contains("form[data-achievement-history-confirm-form]", script);
        Assert.Contains("window.confirm(confirmationMessage)", script);
        Assert.Contains("method: \"POST\"", script);
        Assert.Contains("new FormData(form)", script);
        Assert.Contains("forceRefresh = false", script);
        Assert.Contains("updateView(absoluteNextUrl, changesAddress, true)", script);
    }

    [Theory]
    [InlineData("PlayerAchievementHistoryResource.resx")]
    [InlineData("PlayerAchievementHistoryResource.uk.resx")]
    [InlineData("PlayerAchievementHistoryResource.it.resx")]
    [InlineData("PlayerAchievementHistoryResource.ru.resx")]
    public void History_resources_have_every_required_ui_key(string fileName)
    {
        var root = FindRepositoryRoot();
        var document = XDocument.Load(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Resources",
            "Localization",
            fileName));
        var values = document.Root!
            .Elements("data")
            .ToDictionary(
                element => element.Attribute("name")!.Value,
                element => element.Element("value")!.Value,
                StringComparer.Ordinal);
        var keys = new[]
        {
            "History_Open",
            "History_Title",
            "History_Eyebrow",
            "History_Description",
            "History_BackToGame",
            "History_FilterLabel",
            "History_FilterGame",
            "History_FilterPlayers",
            "History_FilterPending",
            "History_PlayerSelector",
            "History_NoPlayers",
            "History_NoGameUnlocks",
            "History_NoPlayerUnlocks",
            "History_UnlockedAt",
            "History_ReadOnly",
            "History_UnknownDescription",
            "History_EntryCount",
            "History_CurrentGame",
            "History_PendingNotice",
            "History_PendingPlayer",
            "History_PendingPlayerDescription",
            "History_PendingPreviousGames",
            "History_PendingAchievements",
            "History_PendingNoVisibleUnlocks",
            "History_ConfirmIdentity",
            "History_ConfirmPrompt",
            "History_ConfirmError",
            "History_ConfirmConflict",
            "History_PendingPlayerCount"
        };

        Assert.All(keys, key => Assert.True(
            values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value),
            $"Missing or empty {key} in {fileName}."));

        if (fileName.EndsWith(".ru.resx", StringComparison.Ordinal))
        {
            Assert.All(keys, key => Assert.Equal("Україна", values[key]));
        }
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
