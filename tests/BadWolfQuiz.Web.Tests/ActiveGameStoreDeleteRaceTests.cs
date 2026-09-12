using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace BadWolfQuiz.Web.Tests;

public sealed class ActiveGameStoreDeleteRaceTests : IDisposable
{
    private readonly string _contentRoot = Path.Combine(
        Path.GetTempPath(),
        $"bad-wolf-quiz-delete-race-{Guid.NewGuid():N}");

    [Fact]
    public async Task Delete_is_idempotent_and_blocks_an_older_inflight_snapshot()
    {
        Directory.CreateDirectory(_contentRoot);
        var store = new ActiveGameStore(new TestWebHostEnvironment(_contentRoot));
        var quiz = new QuizSnapshot(
            1,
            "Delete race",
            [new QuizRoundSnapshot(
                1,
                "Round 1",
                0,
                [new QuizQuestionSnapshot(100, 10, 0, 200, false, "Question")])]);
        var session = GameSession.Create(quiz);
        var staleSnapshot = new ActiveGameSnapshot(
            "ABC123",
            "host-1",
            true,
            quiz,
            GameSessionSettings.Default,
            session.CaptureState());

        // Simulates the persistence loop winning the race and removing the
        // stored snapshot before the explicit delete request reaches the store.
        Assert.Empty(store.GetAll());
        Assert.True(await store.RemoveAsync("host-1", quiz.SourceQuizId));

        // An older snapshot captured before the deletion must not be resurrected.
        await store.ReplaceAsync([staleSnapshot]);
        Assert.Empty(store.GetAll());
    }

    public void Dispose()
    {
        if (Directory.Exists(_contentRoot))
        {
            Directory.Delete(_contentRoot, recursive: true);
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
