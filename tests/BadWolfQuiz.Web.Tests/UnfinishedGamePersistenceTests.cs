using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;

namespace BadWolfQuiz.Web.Tests;

public sealed class UnfinishedGamePersistenceTests : IDisposable
{
    private readonly string _contentRoot = Path.Combine(
        Path.GetTempPath(),
        $"bad-wolf-quiz-unfinished-{Guid.NewGuid():N}");

    [Fact]
    public async Task Game_is_not_persisted_until_first_question_is_opened()
    {
        var (store, registry, service) = CreateServices("ABC123");
        var game = registry.Create(
            CreateQuiz(),
            GameSessionSettings.Default,
            "host-1");
        registry.JoinPlayer(game.PublicCode, "Rose");

        await service.PersistIfChangedAsync();
        Assert.Empty(store.GetAll());

        registry.StartGame(game.PublicCode);
        await service.PersistIfChangedAsync();
        Assert.Empty(store.GetAll());

        registry.SelectQuestion(game.PublicCode, 100);
        await service.PersistIfChangedAsync();

        var snapshot = Assert.Single(store.GetAll());
        Assert.Equal(game.Session.Id, snapshot.SessionState.Id);
        Assert.Equal(
            RuntimeQuestionStatus.Selected,
            snapshot.SessionState.Questions.Single().Status);
    }

    [Fact]
    public async Task Game_forced_directly_to_final_is_persisted_and_restored_without_opened_regular_question()
    {
        var (store, registry, service) = CreateServices("ABC123");
        var game = registry.Create(
            CreateQuiz(includeFinalQuestion: true),
            GameSessionSettings.Default,
            "host-1");
        registry.JoinPlayer(game.PublicCode, "Rose");
        registry.StartGame(game.PublicCode);

        await service.PersistIfChangedAsync();
        Assert.Empty(store.GetAll());

        registry.ForceAdvanceToFinalQuestion(game.PublicCode);
        await service.PersistIfChangedAsync();

        var snapshot = Assert.Single(store.GetAll());
        Assert.Equal(game.Session.Id, snapshot.SessionState.Id);
        Assert.Equal(GameSessionStatus.FinalWagering, snapshot.SessionState.Status);
        Assert.All(
            snapshot.SessionState.Questions,
            question => Assert.Equal(
                RuntimeQuestionStatus.Available,
                question.Status));
        Assert.NotNull(snapshot.SessionState.FinalQuestion);

        var restoredRegistry = new GameSessionRegistry(
            new SequenceGameCodeGenerator("RESTORED"));
        var restoredService = CreatePersistenceService(restoredRegistry, store);
        await restoredService.StartAsync(CancellationToken.None);
        try
        {
            var restored = Assert.Single(restoredRegistry.GetAll());
            Assert.Equal(game.Session.Id, restored.Session.Id);
            Assert.Equal(GameSessionStatus.FinalWagering, restored.Session.Status);
            Assert.All(
                restored.Session.Board.Questions,
                question => Assert.Equal(
                    RuntimeQuestionStatus.Available,
                    question.Status));
            Assert.NotNull(restored.Session.FinalQuestion);
        }
        finally
        {
            await restoredService.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Replacement_lobby_preserves_old_snapshot_until_its_first_question_opens()
    {
        var (store, registry, service) = CreateServices("OLD111", "NEW222");
        var oldGame = registry.Create(
            CreateQuiz(),
            GameSessionSettings.Default,
            "host-1");
        registry.JoinPlayer(oldGame.PublicCode, "Rose");
        registry.StartGame(oldGame.PublicCode);
        registry.SelectQuestion(oldGame.PublicCode, 100);
        await service.PersistIfChangedAsync();
        Assert.Equal(oldGame.Session.Id, Assert.Single(store.GetAll()).SessionState.Id);

        var replacement = registry.Create(CreateQuiz(), GameSessionSettings.Default);
        replacement.AssignHost("host-1");
        registry.JoinPlayer(replacement.PublicCode, "Mickey");
        registry.StartGame(replacement.PublicCode);
        await service.PersistIfChangedAsync();

        Assert.Equal(oldGame.Session.Id, Assert.Single(store.GetAll()).SessionState.Id);

        registry.SelectQuestion(replacement.PublicCode, 100);
        await service.PersistIfChangedAsync();

        Assert.Equal(
            replacement.Session.Id,
            Assert.Single(store.GetAll()).SessionState.Id);

        registry.ResolveQuestionWithoutCorrectAnswer(replacement.PublicCode, 100);
        registry.CloseQuestionAnswer(replacement.PublicCode, 100);
        await service.PersistIfChangedAsync();
        Assert.Empty(store.GetAll());

        await service.PersistIfChangedAsync();
        Assert.Empty(store.GetAll());
    }

    [Fact]
    public async Task Explicit_delete_removes_snapshot_and_blocks_stale_rewrite()
    {
        var (store, registry, service) = CreateServices("ABC123");
        var game = registry.Create(
            CreateQuiz(),
            GameSessionSettings.Default,
            "host-1");
        registry.JoinPlayer(game.PublicCode, "Rose");
        registry.StartGame(game.PublicCode);
        registry.SelectQuestion(game.PublicCode, 100);
        await service.PersistIfChangedAsync();
        Assert.Single(store.GetAll());

        Assert.True(await store.RemoveAsync("host-1", 1));
        Assert.Empty(store.GetAll());

        game.MarkPersistenceChanged();
        await service.PersistIfChangedAsync();
        Assert.Empty(store.GetAll());
    }

    public void Dispose()
    {
        if (Directory.Exists(_contentRoot))
        {
            Directory.Delete(_contentRoot, recursive: true);
        }
    }

    private (ActiveGameStore Store, GameSessionRegistry Registry,
        ActiveGamePersistenceService Service) CreateServices(params string[] codes)
    {
        Directory.CreateDirectory(_contentRoot);
        var environment = new TestWebHostEnvironment(_contentRoot);
        var store = new ActiveGameStore(environment);
        var registry = new GameSessionRegistry(new SequenceGameCodeGenerator(codes));
        var service = CreatePersistenceService(registry, store);
        return (store, registry, service);
    }

    private ActiveGamePersistenceService CreatePersistenceService(
        GameSessionRegistry registry,
        ActiveGameStore store)
    {
        var environment = new TestWebHostEnvironment(_contentRoot);
        return new ActiveGamePersistenceService(
            registry,
            store,
            TimeProvider.System,
            NullLogger<ActiveGamePersistenceService>.Instance,
            new CrashLog(environment));
    }

    private static QuizSnapshot CreateQuiz(bool includeFinalQuestion = false)
    {
        var question = new QuizQuestionSnapshot(
            100,
            10,
            0,
            200,
            false,
            "Question");
        return new QuizSnapshot(
            1,
            "Unfinished lifecycle",
            [new QuizRoundSnapshot(1, "Round 1", 0, [question])],
            includeFinalQuestion ? new FinalQuestionSnapshot() : null);
    }

    private sealed class SequenceGameCodeGenerator(string[] codes)
        : IGameCodeGenerator
    {
        private int _index;

        public string Create()
        {
            if (_index >= codes.Length)
            {
                throw new InvalidOperationException("No test game codes remain.");
            }

            return codes[_index++];
        }
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
