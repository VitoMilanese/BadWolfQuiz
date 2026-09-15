namespace BadWolfQuiz.Web.Services;

public static class SocialPreviewMetadataCatalog
{
    public static SocialPreviewMetadata Resolve(string? page, string? culture)
    {
        if (string.Equals(page, "/Join/Index", StringComparison.OrdinalIgnoreCase))
        {
            return GetJoin(culture);
        }

        if (string.Equals(page, "/WordRings", StringComparison.OrdinalIgnoreCase))
        {
            var normalizedCulture = NormalizeCulture(culture);
            return normalizedCulture == "ru"
                ? GetDefault(culture)
                : GetWordRings(culture);
        }

        return GetDefault(culture);
    }

    public static SocialPreviewMetadata GetDefault(string? culture)
    {
        var normalizedCulture = NormalizeCulture(culture);
        if (normalizedCulture != "ru" &&
            SeoMetadataCatalog.TryGet("/Index", normalizedCulture, out var metadata))
        {
            return new SocialPreviewMetadata(
                metadata.Title,
                metadata.Description,
                metadata.OpenGraphLocale,
                "site");
        }

        return new SocialPreviewMetadata(
            "Україна",
            "Україна",
            "ru_RU",
            "site");
    }

    public static SocialPreviewMetadata GetJoin(string? culture) =>
        NormalizeCulture(culture) switch
        {
            "uk" => new SocialPreviewMetadata(
                "Приєднуйся до гри — Bad Wolf Quiz",
                "Відкрий посилання, введи своє ім’я та приєднуйся до гри.",
                "uk_UA",
                "join"),
            "it" => new SocialPreviewMetadata(
                "Partecipa alla partita — Bad Wolf Quiz",
                "Apri il link, inserisci il tuo nome e partecipa alla partita.",
                "it_IT",
                "join"),
            "ru" => new SocialPreviewMetadata(
                "Україна",
                "Україна",
                "ru_RU",
                "join"),
            _ => new SocialPreviewMetadata(
                "Join the game — Bad Wolf Quiz",
                "Open the link, enter your name, and join the game.",
                "en_US",
                "join")
        };

    public static SocialPreviewMetadata GetWordRings(string? culture) =>
        NormalizeCulture(culture) switch
        {
            "uk" => new SocialPreviewMetadata(
                "Слівце в кільце — Bad Wolf Quiz",
                "Розгадай три приховані правила та розклади слова у правильні перетини кілець.",
                "uk_UA",
                "word-rings-uk"),
            "it" => new SocialPreviewMetadata(
                "Parola nel cerchio — Bad Wolf Quiz",
                "Scopri tre regole nascoste e sistema ogni parola nelle corrette intersezioni dei cerchi.",
                "it_IT",
                "word-rings-it"),
            _ => new SocialPreviewMetadata(
                "Word into the ring — Bad Wolf Quiz",
                "Discover three hidden rules and place every word into the correct ring intersections.",
                "en_US",
                "word-rings-en")
        };

    public static string NormalizeCulture(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
        {
            return "en";
        }

        var normalized = culture.Trim().ToLowerInvariant();
        var separatorIndex = normalized.IndexOf('-');
        if (separatorIndex < 0)
        {
            separatorIndex = normalized.IndexOf('_');
        }

        if (separatorIndex > 0)
        {
            normalized = normalized[..separatorIndex];
        }

        return normalized switch
        {
            "uk" => "uk",
            "it" => "it",
            "ru" => "ru",
            _ => "en"
        };
    }
}

public readonly record struct SocialPreviewMetadata(
    string Title,
    string Description,
    string OpenGraphLocale,
    string ImageVariant);
