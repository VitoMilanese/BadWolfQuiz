using System.Xml.Linq;
using BadWolfQuiz.Web.Services;

namespace BadWolfQuiz.Web.Tests;

public sealed class PlayerAchievementLocalizationRegressionTests
{
    private static readonly string[] ResourceFileNames =
    [
        "AchievementResource.resx",
        "AchievementResource.uk.resx",
        "AchievementResource.it.resx",
        "AchievementResource.ru.resx"
    ];

    public static IEnumerable<object[]> Resources =>
        ResourceFileNames.Select(fileName => new object[] { fileName });

    [Theory]
    [MemberData(nameof(Resources))]
    public void Achievement_resources_cover_entire_catalog(string fileName)
    {
        var values = LoadResource(fileName);
        var uiKeys = new[]
        {
            "Achievements_Title",
            "Achievements_PlayerLabel",
            "Achievements_Close",
            "Achievements_Subtitle",
            "Achievements_UnlockedCount",
            "Achievements_NewUnlocked",
            "Achievements_NewUnlockedMany",
            "Achievements_SecretTitle",
            "Achievements_SecretDescription"
        };

        Assert.All(uiKeys, key => AssertNonEmpty(values, fileName, key));
        Assert.All(PlayerAchievementService.Catalog, achievement =>
        {
            AssertNonEmpty(values, fileName, $"{achievement.Code}_Name");
            AssertNonEmpty(values, fileName, $"{achievement.Code}_Description");
        });
    }

    private static Dictionary<string, string> LoadResource(string fileName)
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "BadWolfQuiz.Web",
            "Resources",
            "Localization",
            fileName);
        var document = XDocument.Load(path);

        return document.Root!
            .Elements("data")
            .ToDictionary(
                element => element.Attribute("name")!.Value,
                element => element.Element("value")?.Value ?? string.Empty,
                StringComparer.Ordinal);
    }

    private static void AssertNonEmpty(
        IReadOnlyDictionary<string, string> values,
        string fileName,
        string key)
    {
        Assert.True(
            values.TryGetValue(key, out var value),
            $"{fileName} is missing achievement resource key '{key}'.");
        Assert.False(
            string.IsNullOrWhiteSpace(value),
            $"{fileName} contains an empty achievement resource value for '{key}'.");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "BadWolfQuiz.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the BadWolfQuiz repository root.");
    }
}
