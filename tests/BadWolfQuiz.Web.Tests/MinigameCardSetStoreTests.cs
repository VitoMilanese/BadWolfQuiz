using BadWolfQuiz.Web.Pages;
using BadWolfQuiz.Web.Services;

namespace BadWolfQuiz.Web.Tests;

public sealed class MinigameCardSetStoreTests
{
    [Fact]
    public void Requested_set_uses_the_requested_number_of_unique_cards()
    {
        using var directory = new TemporaryDirectory();
        for (var index = 1; index <= 12; index++)
        {
            directory.CreateFile($"Card-{index}.png");
        }
        var store = new MinigameCardSetStore(directory.Path, 10);

        var cards = store.GenerateCards(10);

        Assert.Equal(10, cards.Count);
        Assert.Equal(10, cards.Select(card => card.FileName).Distinct().Count());
        Assert.Equal(12, store.AvailableCardCount);
        Assert.Equal(10, store.DefaultCardCount);
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

        Assert.Equal(2, store.AvailableCardCount);
    }

    [Fact]
    public void Card_resolution_rejects_path_traversal()
    {
        using var directory = new TemporaryDirectory();
        directory.CreateFile("A.png");
        var store = new MinigameCardSetStore(directory.Path, 10);

        Assert.True(store.TryResolveCard("A.png", out var path, out var contentType));
        Assert.Equal("image/png", contentType);
        Assert.Equal(System.IO.Path.Combine(directory.Path, "A.png"), path);
        Assert.False(store.TryResolveCard("../A.png", out _, out _));
        Assert.False(store.TryResolveCard("..\\A.png", out _, out _));
    }

    [Fact]
    public void Page_exposes_available_and_default_card_counts()
    {
        using var directory = new TemporaryDirectory();
        for (var index = 1; index <= 14; index++)
        {
            directory.CreateFile($"Card-{index}.png");
        }
        var store = new MinigameCardSetStore(directory.Path, 10);
        var page = new MinigamesModel(store);

        page.OnGet();

        Assert.Equal(14, page.AvailableCardCount);
        Assert.Equal(10, page.DefaultCardCount);
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
