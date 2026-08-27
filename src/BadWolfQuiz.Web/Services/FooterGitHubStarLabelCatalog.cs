using System.Globalization;

namespace BadWolfQuiz.Web.Services;

public static class FooterGitHubStarLabelCatalog
{
    public static FooterGitHubStarLabel Resolve(CultureInfo culture) =>
        culture.TwoLetterISOLanguageName switch
        {
            "uk" => new("Поставити зірку Bad Wolf Quiz на", true, true),
            "it" => new("Metti una stella a Bad Wolf Quiz su", true, true),
            "ru" => new("Україна", false, false),
            _ => new("Star Bad Wolf Quiz on", true, true)
        };
}

public sealed record FooterGitHubStarLabel(
    string Prefix,
    bool ShowLeadingStar,
    bool ShowGitHubBrand);
