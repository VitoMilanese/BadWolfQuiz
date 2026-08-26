using System.Globalization;

namespace BadWolfQuiz.Web.Localization;

public static class MultipleChoiceAnswerOptionsText
{
    public static string Title => Resolve().Title;

    public static string FirstCorrectHint => Resolve().FirstCorrectHint;

    public static string EmptyHint => Resolve().EmptyHint;

    public static string HostQuestionType => Resolve().HostQuestionType;

    private static Text Resolve()
    {
        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
        {
            "uk" => new(
                "Варіанти відповіді",
                "Перший варіант — правильний",
                "Додайте варіанти відповіді",
                "Вибір відповіді — обирає хост"),
            "it" => new(
                "Opzioni di risposta",
                "La prima opzione è corretta",
                "Aggiungi opzioni di risposta",
                "Scelta multipla — seleziona il conduttore"),
            "ru" => new(
                "Варианты ответа",
                "Первый вариант — правильный",
                "Добавьте варианты ответа",
                "Выбор ответа — выбирает хост"),
            _ => new(
                "Answer options",
                "The first option is correct",
                "Add answer options",
                "Multiple choice — host selects")
        };
    }

    private sealed record Text(
        string Title,
        string FirstCorrectHint,
        string EmptyHint,
        string HostQuestionType);
}
