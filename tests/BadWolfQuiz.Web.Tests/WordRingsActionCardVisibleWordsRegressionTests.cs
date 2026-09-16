using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace BadWolfQuiz.Web.Tests;

public sealed class WordRingsActionCardVisibleWordsRegressionTests
{
    [Fact]
    public void Action_card_ui_uses_only_words_currently_rendered_in_the_bank()
    {
        var script = ReadWebFile("wwwroot", "js", "word-rings-action-cards-visible-words.js");
        var tagHelper = ReadWebFile("TagHelpers", "WordRingsActionCardsAssetsTagHelper.cs");

        Assert.Contains("uk: ['КПС ', 'До картки ']", script, StringComparison.Ordinal);
        Assert.Contains("en: ['KPS ', 'Next card ']", script, StringComparison.Ordinal);
        Assert.Contains("it: ['KPS ', 'Prossima carta ']", script, StringComparison.Ordinal);
        Assert.Contains("wordList.querySelectorAll('.word-rings-word[data-word]')", script, StringComparison.Ordinal);
        Assert.Contains("window.getComputedStyle(token).display !== 'none'", script, StringComparison.Ordinal);
        Assert.Contains("visibleWords.has(normalizeWord(option.value))", script, StringComparison.Ordinal);
        Assert.Contains("wordrings:bank-rendered", script, StringComparison.Ordinal);
        Assert.DoesNotContain("multiplayerSnapshot", script, StringComparison.Ordinal);

        Assert.Contains("word-rings-action-cards-visible-words.js?v=1", tagHelper, StringComparison.Ordinal);
        Assert.True(
            tagHelper.IndexOf("word-rings-action-cards-v2.js?v=5", StringComparison.Ordinal) <
            tagHelper.IndexOf("word-rings-action-cards-visible-words.js?v=1", StringComparison.Ordinal));
    }

    [Fact]
    public void Block_and_hint_never_select_queued_words()
    {
        var root = Path.Combine(Path.GetTempPath(), $"badwolf-word-rings-visible-card-words-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var environment = new TestWebHostEnvironment(root);
            var rooms = WordRingsRoomHostCoordinator.Get(environment);
            var cards = WordRingsActionCardCoordinator.Get(environment);
            var patch = WordRingsActionCardPatchCoordinator.Get(environment);

            var host = rooms.CreateRoom("Host", 15, partialScoreEnabled: false, hostChoosesRules: false);
            var guest = rooms.JoinRoom(host.RoomCode, "Guest");
            cards.RegisterRoom(host, enabled: true, correctWordsPerCard: 2, maximumCards: 4);
            _ = rooms.StartGame(host.RoomCode, host.PlayerToken);
            cards.BeginRound(host.RoomCode, host.PlayerToken);

            var guestRoom = rooms.GetRoomState(host.RoomCode, guest.PlayerToken);
            Assert.NotEmpty(guestRoom.BankWords);
            Assert.NotEmpty(guestRoom.QueuedWords);

            patch.GrantDebugCard(host.RoomCode, host.PlayerToken, host.State.PlayerId, (int)WordRingsActionCardKind.Block);
            _ = cards.UseCard(
                host.RoomCode,
                host.PlayerToken,
                (int)WordRingsActionCardKind.Block,
                guest.State.PlayerId,
                null);

            var guestCards = cards.GetState(host.RoomCode, guest.PlayerToken);
            Assert.NotEmpty(guestCards.BlockedWords);
            Assert.All(guestCards.BlockedWords, word =>
            {
                Assert.Contains(word, guestRoom.BankWords, StringComparer.OrdinalIgnoreCase);
                Assert.DoesNotContain(word, guestRoom.QueuedWords, StringComparer.OrdinalIgnoreCase);
            });

            patch.GrantDebugCard(host.RoomCode, host.PlayerToken, host.State.PlayerId, (int)WordRingsActionCardKind.Hint);
            var hostRoom = rooms.GetRoomState(host.RoomCode, host.PlayerToken);
            Assert.NotEmpty(hostRoom.QueuedWords);
            _ = cards.UseCard(
                host.RoomCode,
                host.PlayerToken,
                (int)WordRingsActionCardKind.Hint,
                host.State.PlayerId,
                null);

            var hostCards = cards.GetState(host.RoomCode, host.PlayerToken);
            Assert.NotNull(hostCards.HintWord);
            Assert.Contains(hostCards.HintWord!, hostRoom.BankWords, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain(hostCards.HintWord!, hostRoom.QueuedWords, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
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
            if (File.Exists(Path.Combine(directory.FullName, "BadWolfQuiz.sln"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("BadWolfQuiz repository root was not found.");
    }

    private sealed class TestWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "BadWolfQuiz.Web.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = contentRootPath;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(contentRootPath);
    }
}
