using System.Globalization;
using System.Xml.Linq;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Services;

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
    [InlineData("ContributorResource")]
    [InlineData("QuizCloneResource")]
    [InlineData("UnfinishedGameResource")]
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


    [Fact]
    public void Novelty_server_side_localizers_return_only_ukraine_for_user_facing_text()
    {
        var previousCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("ru-RU");

            Assert.Equal(NoveltyValue, ContentBlockContainerText.Title);
            Assert.Equal(NoveltyValue, ContentBlockContainerText.HorizontalContent);
            Assert.Equal(NoveltyValue, ContentBlockContainerText.EmptyHint);
            Assert.Equal(NoveltyValue, MultipleChoiceAnswerOptionsText.Title);
            Assert.Equal(NoveltyValue, MultipleChoiceAnswerOptionsText.FirstCorrectHint);
            Assert.Equal(NoveltyValue, MultipleChoiceAnswerOptionsText.EmptyHint);
            Assert.Equal(NoveltyValue, MultipleChoiceAnswerOptionsText.HostQuestionType);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
        }

        var defaultPreview = SocialPreviewMetadataCatalog.GetDefault("ru-RU");
        var joinPreview = SocialPreviewMetadataCatalog.GetJoin("ru-RU");

        Assert.Equal(NoveltyValue, defaultPreview.Title);
        Assert.Equal(NoveltyValue, defaultPreview.Description);
        Assert.Equal(NoveltyValue, joinPreview.Title);
        Assert.Equal(NoveltyValue, joinPreview.Description);

        var finalTransition = File.ReadAllText(FindWebFile(
            "Pages", "Admin", "Games", "FinalQuestionTransition.cshtml.cs"));
        Assert.Contains(
            "\"ru\" => \"Україна\"",
            finalTransition,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("all-player-question.js", "ru: {")]
    [InlineData("editor-reset-button.js", "ru: {")]
    [InlineData("host-multiple-choice.js", "ru: {")]
    [InlineData("host-question-controls.js", "ru: {")]
    [InlineData("question-copy-action.js", "ru: {")]
    [InlineData("quiz-clone-action.js", "ru: {")]
    [InlineData("youtube-antibot-fallback.js", "ru: {")]
    [InlineData("multiple-choice-answer-options.js", "culture.startsWith(\"ru\")")]
    [InlineData("quiz-description-editor-links.js", "language.startsWith(\"ru\")")]
    public void Novelty_client_localization_objects_only_use_ukraine(
        string fileName,
        string marker)
    {
        var source = File.ReadAllText(FindWebFile("wwwroot", "js", fileName));
        var objectBody = ExtractObjectBody(source, marker);
        var quotedValues = ExtractDoubleQuotedStrings(objectBody);

        Assert.NotEmpty(quotedValues);
        Assert.All(quotedValues, value => Assert.Equal(NoveltyValue, value));
    }

    [Theory]
    [InlineData("quiz-editor-dialog-loading.js")]
    [InlineData("quiz-editor-question-price-input.js")]
    public void Novelty_client_scalar_localizations_use_ukraine(string fileName)
    {
        var source = File.ReadAllText(FindWebFile("wwwroot", "js", fileName));

        Assert.Contains(
            "ru: \"Україна\"",
            source,
            StringComparison.Ordinal);
    }

    private static string ExtractObjectBody(string source, string marker)
    {
        var markerIndex = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(markerIndex >= 0, $"Could not find localization marker '{marker}'.");

        var openBrace = source.IndexOf('{', markerIndex);
        Assert.True(openBrace >= 0, $"Could not find object start after '{marker}'.");

        var depth = 0;
        var inDoubleQuotedString = false;
        var escaped = false;
        for (var index = openBrace; index < source.Length; index++)
        {
            var character = source[index];
            if (inDoubleQuotedString)
            {
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (character == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (character == '\"')
                {
                    inDoubleQuotedString = false;
                }

                continue;
            }

            if (character == '\"')
            {
                inDoubleQuotedString = true;
                continue;
            }

            if (character == '{')
            {
                depth++;
            }
            else if (character == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return source[(openBrace + 1)..index];
                }
            }
        }

        throw new InvalidDataException($"Could not find object end after '{marker}'.");
    }

    private static IReadOnlyList<string> ExtractDoubleQuotedStrings(string source)
    {
        var values = new List<string>();
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index] != '\"')
            {
                continue;
            }

            var value = new System.Text.StringBuilder();
            var escaped = false;
            for (index++; index < source.Length; index++)
            {
                var character = source[index];
                if (escaped)
                {
                    value.Append(character);
                    escaped = false;
                    continue;
                }

                if (character == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (character == '\"')
                {
                    values.Add(value.ToString());
                    break;
                }

                value.Append(character);
            }
        }

        return values;
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
