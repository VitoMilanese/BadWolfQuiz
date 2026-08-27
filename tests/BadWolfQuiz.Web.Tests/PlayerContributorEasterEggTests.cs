using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Services;

namespace BadWolfQuiz.Web.Tests;

public sealed class PlayerContributorEasterEggTests
{
    [Theory]
    [InlineData("1Wolf", 1, "Wolf")]
    [InlineData("01Wolf", 1, "Wolf")]
    [InlineData("7Wolf", 7, "Wolf")]
    [InlineData("07Wolf", 7, "Wolf")]
    [InlineData("15Wolf", 15, "Wolf")]
    [InlineData("31 Wolf", 31, "Wolf")]
    public void Matching_day_prefix_is_removed(
        string nickname,
        int dayOfMonth,
        string expectedDisplayName)
    {
        var result = PlayerContributorEasterEgg.ResolveAlias(
            nickname,
            dayOfMonth);

        Assert.True(result.IsActive);
        Assert.Equal(expectedDisplayName, result.DisplayName);
    }

    [Theory]
    [InlineData("23Wolf", 22)]
    [InlineData("231Wolf", 23)]
    [InlineData("007Wolf", 7)]
    [InlineData("7", 7)]
    [InlineData("07", 7)]
    [InlineData("Wolf", 23)]
    public void Non_matching_or_empty_alias_keeps_original_name(
        string nickname,
        int dayOfMonth)
    {
        var result = PlayerContributorEasterEgg.ResolveAlias(
            nickname,
            dayOfMonth);

        Assert.False(result.IsActive);
        Assert.Equal(nickname, result.DisplayName);
    }

    [Fact]
    public void Existing_player_reconnect_uses_original_identity_after_day_changes()
    {
        var session = CreateSession();
        var player = session.AddPlayer("Wolf");
        player.ApplyTemporaryContributorAlias("23Wolf", "Wolf");

        var result = PlayerContributorEasterEgg.ResolveForJoin(
            session.AllPlayers,
            "23Wolf",
            dayOfMonth: 24);

        Assert.Equal("Wolf", result.JoinName);
        Assert.Equal("23Wolf", result.OriginalName);
        Assert.Equal("Wolf", result.DisplayName);
        Assert.False(result.ActivateTemporaryPrivileges);
    }

    [Fact]
    public void Temporary_contributor_identity_survives_active_game_restore()
    {
        var session = CreateSession();
        var player = session.AddPlayer("Wolf");
        player.ApplyTemporaryContributorAlias("23Wolf", "Wolf");

        var restored = GameSession.Restore(
            session.Quiz,
            session.Settings,
            session.CaptureState());
        var restoredPlayer = Assert.Single(restored.Players);

        Assert.Equal("Wolf", restoredPlayer.Name);
        Assert.Equal("23Wolf", restoredPlayer.OriginalName);
        Assert.True(restoredPlayer.HasTemporaryContributorPrivileges);
    }

    [Fact]
    public void Temporary_player_gets_contributor_equivalent_access()
    {
        var session = CreateSession();
        var player = session.AddPlayer("Wolf");
        player.ApplyTemporaryContributorAlias("23Wolf", "Wolf");
        var options = new FooterOptions { Contributors = [] };

        Assert.True(PlayerContributorAccess.IsContributor(options, player));
    }

    [Fact]
    public void Regular_player_does_not_gain_contributor_access()
    {
        var session = CreateSession();
        var player = session.AddPlayer("22Wolf");
        var options = new FooterOptions { Contributors = [] };

        Assert.False(PlayerContributorAccess.IsContributor(options, player));
        Assert.Equal("22Wolf", player.Name);
        Assert.Equal("22Wolf", player.OriginalName);
    }

    [Fact]
    public void Player_frame_get_and_post_paths_use_temporary_contributor_access()
    {
        var source = File.ReadAllText(FindFile(
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Player",
            "Lobby.cshtml.cs"));

        Assert.Equal(
            2,
            source.Split(
                "PlayerContributorAccess.IsContributor(",
                StringSplitOptions.None).Length - 1);
        Assert.Contains("avatarCatalog.CanUseFrame(currentPlayer.AvatarId)", source);
        Assert.Contains(
            "(!isContributor && premiumHostId is null && !avatarCanUseFrame)",
            source);
    }

    [Fact]
    public void Avatar_picker_uses_server_frame_eligibility_for_dynamic_selection()
    {
        var picker = File.ReadAllText(FindFile(
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Shared",
            "_AvatarPicker.cshtml"));
        var pickerScript = File.ReadAllText(FindFile(
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "avatar-picker.js"));
        var frameScript = File.ReadAllText(FindFile(
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "contributor-frames.js"));
        var playerLobby = File.ReadAllText(FindFile(
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Player",
            "Lobby.cshtml"));

        Assert.Contains("AvatarCatalog.CanUseFrame(avatar.Id)", picker);
        Assert.Contains("data-avatar-frame-eligible", picker);
        Assert.Contains("avatarFrameEligible:", pickerScript);
        Assert.Contains("selectedHostAvatarFrameEligible", frameScript);
        Assert.Contains("playerLobby.dataset.avatarFrameEligible", frameScript);
        Assert.Contains("lobby.dataset.avatarFrameEligible", playerLobby);
    }

    [Fact]
    public void Shared_frame_feed_includes_temporary_contributors()
    {
        var source = File.ReadAllText(FindFile(
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "ContributorFrames.cshtml.cs"));

        Assert.Contains("PlayerContributorAccess.IsContributor(", source);
        Assert.Contains("footerOptions.Value", source);
        Assert.Contains("player) ||", source);
        Assert.DoesNotContain(
            "ContributorRecognition.IsContributor(\n                    footerOptions.Value,\n                    player.Name)",
            source);
    }

    [Fact]
    public void Avatar_frame_panel_hidden_state_is_not_overridden_by_component_css()
    {
        var css = File.ReadAllText(FindFile(
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "css",
            "contributor-frames.css")).Replace("\r\n", "\n");

        Assert.Contains(
            ".contributor-frame-settings[hidden] {\n    display: none;\n}",
            css);
    }

    [Fact]
    public void Shared_frame_feed_returns_explicit_disabled_state_after_eligibility_loss()
    {
        var source = File.ReadAllText(FindFile(
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "ContributorFrames.cshtml.cs"));

        Assert.Contains("enabled = canUseFrame && player.AvatarFrameEnabled", source);
        Assert.Contains("frameId = canUseFrame ? player.AvatarFrameId : null", source);
        Assert.DoesNotContain(".Where(player =>", source);
    }

    [Fact]
    public void Host_frame_refresh_ignores_out_of_order_responses()
    {
        var source = File.ReadAllText(FindFile(
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "contributor-frames.js"));

        Assert.Contains("let playerFrameRefreshSequence = 0;", source);
        Assert.Contains("const refreshSequence = ++playerFrameRefreshSequence;", source);
        Assert.Contains("refreshSequence !== playerFrameRefreshSequence", source);
        Assert.Contains("cache: \"no-store\"", source);
        Assert.Contains(
            "attributeFilter: [\"data-avatar-id\", \"data-avatar-frame-eligible\"]",
            source);
    }

    [Fact]
    public void Ukrainian_frame_ui_uses_avatar_frame_name()
    {
        var source = File.ReadAllText(FindFile(
            "src",
            "BadWolfQuiz.Web",
            "Resources",
            "Localization",
            "ContributorResource.uk.resx"));

        Assert.Contains(">Рамка аватара</value>", source);
        Assert.Contains(">Увімкнути рамку аватара</value>", source);
        Assert.DoesNotContain("Рамка контриб'ютора", source);
        Assert.DoesNotContain("рамку контриб'ютора", source);
    }

    [Fact]
    public void Join_page_uses_browser_day_and_game_scoped_storage_alias()
    {
        var page = File.ReadAllText(FindFile(
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Join",
            "Index.cshtml"));
        var model = File.ReadAllText(FindFile(
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Join",
            "Index.cshtml.cs"));

        Assert.Contains("Input.ClientDayOfMonth", page);
        Assert.Contains("new Date().getDate()", page);
        Assert.Contains("player-storage-name", page);
        Assert.Contains("PlayerContributorEasterEgg.ResolveForJoin", model);
        Assert.Contains("player.ApplyTemporaryContributorAlias", model);
        Assert.Contains("existingGame?.Session.AllPlayers", model);
    }

    private static GameSession CreateSession()
    {
        var clue = new ContentBlockSnapshot(
            1,
            ContentBlockKind.Text,
            "Question",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            1,
            false);
        var question = new QuizQuestionSnapshot(
            100,
            10,
            0,
            100,
            false,
            "Category",
            false,
            [clue],
            []);
        var quiz = new QuizSnapshot(
            1,
            "Test Quiz",
            [new QuizRoundSnapshot(1, "Round 1", 0, [question])]);
        return GameSession.Create(quiz);
    }

    private static string FindFile(params string[] pathParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var parts = new[] { directory.FullName }.Concat(pathParts).ToArray();
            var candidate = Path.Combine(parts);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate {Path.Combine(pathParts)} from the test output directory.");
    }
}
