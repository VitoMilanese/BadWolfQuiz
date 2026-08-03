using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;

namespace BadWolfQuiz.Web.Tests;

public sealed class ActiveGamePersistenceServiceTests : IDisposable
{
    private readonly string _contentRoot = Path.Combine(
        Path.GetTempPath(),
        $"bad-wolf-quiz-persistence-{Guid.NewGuid():N}");

    [Fact]
    public async Task JudgeQuestionAnswer_persists_once_without_reserializing_unchanged_media()
    {
        Directory.CreateDirectory(_contentRoot);
        var environment = new TestWebHostEnvironment(_contentRoot);
        var store = new ActiveGameStore(environment);
        var registry = new GameSessionRegistry(new FixedGameCodeGenerator());
        var game = registry.Create(CreateMediaQuiz(), GameSessionSettings.Default, "host-1");
        var player = registry.JoinPlayer(game.PublicCode, "Rose").Player!;
        registry.StartGame(game.PublicCode);
        registry.SelectQuestion(game.PublicCode, 100);
        registry.ActivateQuestionBuzzer(game.PublicCode, 100);
        var service = new ActiveGamePersistenceService(
            registry,
            store,
            TimeProvider.System,
            NullLogger<ActiveGamePersistenceService>.Instance,
            new CrashLog(environment));

        await service.PersistIfChangedAsync();
        await service.PersistIfChangedAsync();

        Assert.Equal(1, store.WriteCount);

        registry.JudgeQuestionAnswer(game.PublicCode, 100, player.Id, false);
        await service.PersistIfChangedAsync();
        await service.PersistIfChangedAsync();

        Assert.Equal(2, store.WriteCount);
        Assert.True(new FileInfo(Path.Combine(
            _contentRoot,
            "App_Data",
            "active-games.json")).Length > 4 * 1024 * 1024);
    }

    public void Dispose()
    {
        if (Directory.Exists(_contentRoot))
        {
            Directory.Delete(_contentRoot, recursive: true);
        }
    }

    private static QuizSnapshot CreateMediaQuiz()
    {
        var media = new byte[4 * 1024 * 1024];
        var question = new QuizQuestionSnapshot(
            100,
            10,
            0,
            200,
            false,
            "Media",
            questionBlocks:
            [
                new ContentBlockSnapshot(
                    1000,
                    ContentBlockKind.Image,
                    null,
                    null,
                    null,
                    null,
                    null,
                    media,
                    "image/png",
                    "large.png",
                    0,
                    false)
            ]);

        return new QuizSnapshot(
            1,
            "Media quiz",
            [new QuizRoundSnapshot(1, "Round 1", 0, [question])]);
    }

    private sealed class FixedGameCodeGenerator : IGameCodeGenerator
    {
        public string Create() => "ABC123";
    }

    private sealed class TestWebHostEnvironment(string contentRootPath)
        : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "BadWolfQuiz.Web.Tests";

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string WebRootPath { get; set; } = contentRootPath;

        public string EnvironmentName { get; set; } = "Testing";

        public string ContentRootPath { get; set; } = contentRootPath;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
