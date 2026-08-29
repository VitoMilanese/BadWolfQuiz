using BadWolfQuiz.Web.Pages;
using BadWolfQuiz.Web.Services;

namespace BadWolfQuiz.Web.Tests;

public sealed class MinigameCardSetStoreTests
{
    [Fact]
    public void Current_set_is_shared_until_regeneration()
    {
        using var directory = new TemporaryDirectory();
        directory.CreateFile("A.png");
        directory.CreateFile("B.jpg");
        directory.CreateFile("C.webp");
        directory.CreateFile("D.gif");

        var store = new MinigameCardSetStore(directory.Path, 3);

        var first = store.GetCurrent();
        var second = store.GetCurrent();
        var regenerated = store.Regenerate();

        Assert.Same(first, second);
        Assert.Equal(3, first.Cards.Count);
        Assert.Equal(first.Version + 1, regenerated.Version);
        Assert.Equal(3, regenerated.Cards.Count);
        Assert.Equal(3, regenerated.Cards.Select(card => card.FileName).Distinct().Count());
    }

    [Fact]
    public void Discovery_uses_supported_top_level_images_only()
    {
        using var directory = new TemporaryDirectory();
        directory.CreateFile("A.png");
        directory.CreateFile("B.JPEG");
        directory.CreateFile("ignore.txt");
        var nested = Directory.CreateDirectory(System.IO.Path.Combine(directory.Path, "nested"));
        File.WriteAllText(System.IO.Path.Combine(nested.FullName, "C.png"), string.Empty);

        var store = new MinigameCardSetStore(directory.Path, 10);
        var state = store.GetCurrent();

        Assert.Equal(2, state.Cards.Count);
        Assert.Contains(state.Cards, card => card.FileName == "A.png" && card.DisplayName == "A");
        Assert.Contains(state.Cards, card => card.FileName == "B.JPEG" && card.DisplayName == "B");
    }

    [Fact]
    public void Card_resolution_rejects_path_traversal()
    {
        using var directory = new TemporaryDirectory();
        directory.CreateFile("A.png");
        var store = new MinigameCardSetStore(directory.Path, 1);

        Assert.True(store.TryResolveCard("A.png", out var path, out var contentType));
        Assert.Equal("image/png", contentType);
        Assert.Equal(System.IO.Path.Combine(directory.Path, "A.png"), path);
        Assert.False(store.TryResolveCard("../A.png", out _, out _));
        Assert.False(store.TryResolveCard("..\\A.png", out _, out _));
    }

    [Fact]
    public void Page_load_highlights_one_card_from_the_shared_set()
    {
        using var directory = new TemporaryDirectory();
        directory.CreateFile("A.png");
        directory.CreateFile("B.png");
        var store = new MinigameCardSetStore(directory.Path, 2);
        var page = new MinigamesModel(store);

        page.OnGet();

        Assert.Equal(store.GetCurrent().Version, page.StateVersion);
        Assert.Contains(page.Cards, card => card.FileName == page.HighlightedFileName);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"badwolfquiz-minigames-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void CreateFile(string fileName) =>
            File.WriteAllText(System.IO.Path.Combine(Path, fileName), string.Empty);

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
