using System.Globalization;

namespace BadWolfQuiz.Web.Localization;

public static class ContentBlockContainerText
{
    public static string Title => Resolve().Title;

    public static string HorizontalMedia => Resolve().HorizontalMedia;

    public static string EmptyHint => Resolve().EmptyHint;

    private static ContainerText Resolve()
    {
        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
        {
            "uk" => new(
                "Контейнер",
                "Горизонтальні медіа",
                "Додайте зображення, YouTube або аудіо"),
            "it" => new(
                "Contenitore",
                "Media orizzontali",
                "Aggiungi immagini, YouTube o audio"),
            "ru" => new(
                "Контейнер",
                "Горизонтальные медиа",
                "Добавьте изображения, YouTube или аудио"),
            _ => new(
                "Container",
                "Horizontal media",
                "Add images, YouTube, or audio")
        };
    }

    private sealed record ContainerText(
        string Title,
        string HorizontalMedia,
        string EmptyHint);
}
