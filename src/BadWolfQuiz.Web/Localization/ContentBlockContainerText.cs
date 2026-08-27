using System.Globalization;

namespace BadWolfQuiz.Web.Localization;

public static class ContentBlockContainerText
{
    public static string Title => Resolve().Title;

    public static string HorizontalContent => Resolve().HorizontalContent;

    public static string EmptyHint => Resolve().EmptyHint;

    private static ContainerText Resolve()
    {
        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
        {
            "uk" => new(
                "Контейнер",
                "Горизонтальний контент",
                "Додайте текст, зображення, YouTube або аудіо"),
            "it" => new(
                "Contenitore",
                "Contenuto orizzontale",
                "Aggiungi testo, immagini, YouTube o audio"),
            "ru" => new(
                "Україна",
                "Україна",
                "Україна"),
            _ => new(
                "Container",
                "Horizontal content",
                "Add text, images, YouTube, or audio")
        };
    }

    private sealed record ContainerText(
        string Title,
        string HorizontalContent,
        string EmptyHint);
}
