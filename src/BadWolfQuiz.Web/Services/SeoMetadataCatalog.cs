using System.Collections.Frozen;

namespace BadWolfQuiz.Web.Services;

public static class SeoMetadataCatalog
{
    private static readonly FrozenDictionary<(string Page, string Culture), SeoMetadata> Metadata =
        new Dictionary<(string, string), SeoMetadata>
        {
            [("/Index", "uk")] = new(
                "Bad Wolf Quiz — безкоштовна онлайн-квіз гра та вікторина",
                "Bad Wolf Quiz — безкоштовна онлайн-гра для створення й проведення квізів та вікторин у реальному часі. Створюй питання, збирай друзів і грай разом.",
                "uk_UA"),
            [("/Index", "en")] = new(
                "Bad Wolf Quiz — free online quiz & trivia game",
                "Bad Wolf Quiz is a free real-time quiz and trivia game for friends and groups. Create your own quizzes, join games, and play together online.",
                "en_US"),
            [("/Index", "it")] = new(
                "Bad Wolf Quiz — gioco quiz online gratuito",
                "Bad Wolf Quiz è un gioco quiz online gratuito in tempo reale. Crea i tuoi quiz, invita gli amici e gioca insieme.",
                "it_IT"),
            [("/Faq", "uk")] = new(
                "FAQ — Bad Wolf Quiz",
                "Відповіді на поширені запитання про створення, проведення та участь у квізах Bad Wolf Quiz.",
                "uk_UA"),
            [("/Faq", "en")] = new(
                "FAQ — Bad Wolf Quiz",
                "Answers to common questions about creating, hosting, and joining Bad Wolf Quiz games.",
                "en_US"),
            [("/Faq", "it")] = new(
                "FAQ — Bad Wolf Quiz",
                "Risposte alle domande più comuni su creazione, conduzione e partecipazione ai quiz Bad Wolf Quiz.",
                "it_IT"),
            [("/About", "uk")] = new(
                "Про Bad Wolf Quiz",
                "Дізнайся більше про Bad Wolf Quiz — платформу для живих онлайн-квізів, створену для гри з друзями та спільнотами.",
                "uk_UA"),
            [("/About", "en")] = new(
                "About Bad Wolf Quiz",
                "Learn more about Bad Wolf Quiz, a platform for live online quizzes built for playing with friends and communities.",
                "en_US"),
            [("/About", "it")] = new(
                "Informazioni su Bad Wolf Quiz",
                "Scopri Bad Wolf Quiz, una piattaforma per quiz online dal vivo pensata per giocare con amici e community.",
                "it_IT"),
            [("/PublicQuizzes", "uk")] = new(
                "Публічні квізи — Bad Wolf Quiz",
                "Переглядай публічні квізи Bad Wolf Quiz та знаходь готові ігри для проведення з друзями.",
                "uk_UA"),
            [("/PublicQuizzes", "en")] = new(
                "Public quizzes — Bad Wolf Quiz",
                "Browse public Bad Wolf Quiz quizzes and find ready-to-play games to host with friends.",
                "en_US"),
            [("/PublicQuizzes", "it")] = new(
                "Quiz pubblici — Bad Wolf Quiz",
                "Esplora i quiz pubblici di Bad Wolf Quiz e trova giochi pronti da organizzare con gli amici.",
                "it_IT")
        }.ToFrozenDictionary();

    public static bool TryGet(string? page, string? culture, out SeoMetadata metadata)
    {
        if (page is not null &&
            culture is not null &&
            Metadata.TryGetValue((page, culture), out metadata))
        {
            return true;
        }

        metadata = default;
        return false;
    }

    public static bool IsIndexableRequest(
        string? page,
        string? routeCulture,
        string? uiCulture) =>
        page is not null &&
        routeCulture is not null &&
        uiCulture is not null &&
        string.Equals(routeCulture, uiCulture, StringComparison.Ordinal) &&
        SeoRouteCatalog.IsSeoCulture(routeCulture) &&
        Metadata.ContainsKey((page, routeCulture));

    public static string BuildAbsoluteUrl(string page, string culture)
    {
        var route = SeoRouteCatalog.IndexablePages.Single(item => item.Page == page);
        var suffix = string.IsNullOrWhiteSpace(route.Path) ? string.Empty : $"/{route.Path}";
        return $"{SeoDiscoveryDocuments.PublicBaseUrl}/{culture}{suffix}";
    }
}

public readonly record struct SeoMetadata(
    string Title,
    string Description,
    string OpenGraphLocale);
