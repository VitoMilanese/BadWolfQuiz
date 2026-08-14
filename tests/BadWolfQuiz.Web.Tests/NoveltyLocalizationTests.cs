using System.Globalization;
using System.Xml.Linq;

namespace BadWolfQuiz.Web.Tests;

public sealed class NoveltyLocalizationTests
{
    private const string Culture = "ru";
    private const string NoveltyValue = "Україна";

    [Fact]
    public void Novelty_culture_is_a_valid_supported_culture()
    {
        var culture = CultureInfo.GetCultureInfo(Culture);
        Assert.Equal(Culture, culture.Name);

        var program = File.ReadAllText(FindWebFile("Program.cs"));
        var setLanguage = File.ReadAllText(FindWebFile("Pages", "SetLanguage.cshtml.cs"));
        var project = File.ReadAllText(FindWebFile("BadWolfQuiz.Web.csproj"));

        Assert.Contains("new CultureInfo(\"ru\")", program);
        Assert.Contains("\"ru\"", setLanguage);
        Assert.Contains("<SatelliteResourceLanguages>uk;ru;en;it</SatelliteResourceLanguages>", project);
    }

    [Fact]
    public void Novelty_culture_is_available_in_the_language_selector()
    {
        var layout = File.ReadAllText(FindWebFile("Pages", "Shared", "_Layout.cshtml"));
        var playerLobby = File.ReadAllText(FindWebFile("Pages", "Player", "Lobby.cshtml"));

        Assert.Contains("CultureInfo.CurrentUICulture.Name", layout);
        Assert.Contains("asp-route-culture=\"ru\"", layout);
        Assert.Contains("currentCulture == \"ru\"", layout);
        Assert.Contains("🇷🇺 Кацапська", layout);
        Assert.Contains("asp-route-culture=\"ru\"", playerLobby);
        Assert.Contains("currentCulture == \"ru\"", playerLobby);
        Assert.Contains("🇷🇺 Кацапська", playerLobby);
    }

    [Theory]
    [InlineData("SharedResource")]
    [InlineData("ProductVersionResource")]
    [InlineData("PlayerAdmissionResource")]
    public void Novelty_resource_has_every_base_key_and_only_ukraine_values(string resourceName)
    {
        var basePath = FindWebFile("Resources", "Localization", $"{resourceName}.resx");
        var noveltyPath = FindWebFile("Resources", "Localization", $"{resourceName}.{Culture}.resx");

        var baseEntries = ReadEntries(basePath);
        var noveltyEntries = ReadEntries(noveltyPath);

        Assert.Equal(baseEntries.Keys.OrderBy(key => key), noveltyEntries.Keys.OrderBy(key => key));
        Assert.NotEmpty(noveltyEntries);
        Assert.All(noveltyEntries.Values, value => Assert.Equal(NoveltyValue, value));
    }

    private static Dictionary<string, string> ReadEntries(string path) =>
        XDocument.Load(path)
            .Root!
            .Elements("data")
            .ToDictionary(
                element => element.Attribute("name")!.Value,
                element => element.Element("value")?.Value ?? string.Empty,
                StringComparer.Ordinal);

    private static string FindWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                new[] { directory.FullName, "src", "BadWolfQuiz.Web" }
                    .Concat(parts)
                    .ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate BadWolfQuiz.Web file: {Path.Combine(parts)}");
    }
}
