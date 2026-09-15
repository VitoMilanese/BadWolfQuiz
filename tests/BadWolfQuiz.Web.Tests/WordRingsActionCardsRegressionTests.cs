using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace BadWolfQuiz.Web.Tests;

public sealed class WordRingsActionCardsRegressionTests
{
    [Fact]
    public void Fully_correct_words_award_unique_cards_up_to_the_configured_limit()
    {
        var root = Path.Combine(Path.GetTempPath(), $"badwolf-word-rings-action-cards-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var environment = new TestWebHostEnvironment(root);
            var rooms = WordRingsRoomHostCoordinator.Get(environment);
            var cards = WordRingsActionCardCoordinator.Get(environment);
            var host = rooms.CreateRoom("Host", 15, partialScoreEnabled: false, hostChoosesRules: false);
            _ = rooms.JoinRoom(host.RoomCode, "Guest");
            cards.RegisterRoom(host, enabled: true, correctWordsPerCard: 1, maximumCards: 2);
            _ = rooms.StartGame(host.RoomCode, host.PlayerToken);
            cards.BeginRound(host.RoomCode, host.PlayerToken);

            cards.RecordPlacementAttempt(host.RoomCode, host.State.PlayerId, "one", fullyCorrect: true);
            var first = cards.GetState(host.RoomCode, host.PlayerToken);
            Assert.True(first.Active);
            Assert.Single(first.Cards);

            cards.RecordPlacementAttempt(host.RoomCode, host.State.PlayerId, "two", fullyCorrect: true);
            cards.RecordPlacementAttempt(host.RoomCode, host.State.PlayerId, "three", fullyCorrect: true);
            var capped = cards.GetState(host.RoomCode, host.PlayerToken);

            Assert.Equal(2, capped.Cards.Count);
            Assert.Equal(2, capped.Cards.Select(card => card.Id).Distinct().Count());
            Assert.Equal(0, capped.CorrectProgress);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Non_full_placement_does_not_advance_kps_progress()
    {
        var root = Path.Combine(Path.GetTempPath(), $"badwolf-word-rings-action-kps-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var environment = new TestWebHostEnvironment(root);
            var rooms = WordRingsRoomHostCoordinator.Get(environment);
            var cards = WordRingsActionCardCoordinator.Get(environment);
            var host = rooms.CreateRoom("Host", 15, partialScoreEnabled: true, hostChoosesRules: false);
            _ = rooms.JoinRoom(host.RoomCode, "Guest");
            cards.RegisterRoom(host, enabled: true, correctWordsPerCard: 2, maximumCards: 3);
            _ = rooms.StartGame(host.RoomCode, host.PlayerToken);
            cards.BeginRound(host.RoomCode, host.PlayerToken);

            cards.RecordPlacementAttempt(host.RoomCode, host.State.PlayerId, "partial", fullyCorrect: false);
            var state = cards.GetState(host.RoomCode, host.PlayerToken);

            Assert.Empty(state.Cards);
            Assert.Equal(0, state.CorrectProgress);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Action_card_assets_cover_configuration_carousel_dialog_and_solo_subset()
    {
        var script = ReadWebFile("wwwroot", "js", "word-rings-action-cards.js");
        var css = ReadWebFile("wwwroot", "css", "word-rings-action-cards.css");
        var pointerDrag = ReadWebFile("wwwroot", "js", "word-rings-pointer-drag.js");
        var api = ReadWebFile("Pages", "WordRingsRoomApi.cshtml.cs");
        var imports = ReadWebFile("Pages", "_ViewImports.cshtml");
        var service = ReadWebFile("Services", "WordRingsActionCardCoordinator.cs");

        Assert.Contains("createRoomActionCards", script, StringComparison.Ordinal);
        Assert.Contains("actionCardCorrectWords", script, StringComparison.Ordinal);
        Assert.Contains("actionCardMaxHand", script, StringComparison.Ordinal);
        Assert.Contains("const soloPool = [2, 4, 5, 6, 8];", script, StringComparison.Ordinal);
        Assert.Contains("const outOfTurn = new Set([8, 10, 13]);", script, StringComparison.Ordinal);
        Assert.Contains("const passiveCards = new Set([5]);", script, StringComparison.Ordinal);
        Assert.Contains("['Тимчасове слово'", script, StringComparison.Ordinal);
        Assert.Contains("['Імунітет', 'Пасивна:", script, StringComparison.Ordinal);
        Assert.Contains("word-rings-action-carousel", script, StringComparison.Ordinal);
        Assert.Contains("word-rings-action-dialog", script, StringComparison.Ordinal);
        Assert.Contains("transform: scale(1.16)", css, StringComparison.Ordinal);
        Assert.Contains("is-passive:disabled", css, StringComparison.Ordinal);
        Assert.Contains("new MutationObserver", pointerDrag, StringComparison.Ordinal);
        Assert.Contains("OnPostUseActionCard", api, StringComparison.Ordinal);
        Assert.Contains("OnPostActionCardState", api, StringComparison.Ordinal);
        Assert.Contains("WordRingsActionCardsAssetsTagHelper", imports, StringComparison.Ordinal);
        Assert.Contains("public enum WordRingsActionCardKind", service, StringComparison.Ordinal);
        Assert.Contains("Temporary = 4", service, StringComparison.Ordinal);
        Assert.Contains("MaximumCards = 4", service, StringComparison.Ordinal);
    }

    [Fact]
    public void Corrected_card_semantics_are_guarded_in_the_server_runtime()
    {
        var service = ReadWebFile("Services", "WordRingsActionCardCoordinator.cs");

        Assert.Contains("private static readonly HashSet<WordRingsActionCardKind> ShieldedCards", service, StringComparison.Ordinal);
        Assert.Contains("WordRingsActionCardKind.Block, WordRingsActionCardKind.Timeout, WordRingsActionCardKind.Mask, WordRingsActionCardKind.Anagram", service, StringComparison.Ordinal);
        Assert.Contains("throw new InvalidOperationException(\"ActionCardPassive\")", service, StringComparison.Ordinal);
        Assert.Contains("return player.Cards.Remove(WordRingsActionCardKind.Immunity);", service, StringComparison.Ordinal);
        Assert.Contains("GrantTemporaryWord(code, state, targetId);", service, StringComparison.Ordinal);
        Assert.Contains("RemainingWords(target).Insert(0, temporaryWord);", service, StringComparison.Ordinal);
        Assert.Contains("target.SkipTurns = 0;", service, StringComparison.Ordinal);
        Assert.DoesNotContain("FailureImmunity { get; set; }", service, StringComparison.Ordinal);
        Assert.DoesNotContain("TemporaryCard { get; set; }", service, StringComparison.Ordinal);
    }

    private static string ReadWebFile(params string[] pathParts) =>
        File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "BadWolfQuiz.Web",
            Path.Combine(pathParts)));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "BadWolfQuiz.Web")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private sealed class TestWebHostEnvironment(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "BadWolfQuiz.Web.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = root;
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(root);
    }
}