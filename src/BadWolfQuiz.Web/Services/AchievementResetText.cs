using System.Globalization;

namespace BadWolfQuiz.Web.Services;

public static class AchievementResetText
{
    public static AchievementResetLabels Current => For(CultureInfo.CurrentUICulture);

    public static AchievementResetLabels For(CultureInfo culture) =>
        culture.TwoLetterISOLanguageName switch
        {
            "uk" => new(
                "Скинути досягнення",
                "Скинути досягнення?",
                "Скинути досягнення «{0}»? Воно знову стане заблокованим, і його можна буде отримати повторно.",
                "Скасувати",
                "Скинути",
                "Не вдалося скинути досягнення. Спробуйте ще раз."),
            "ru" => new(
                "Сбросить достижение",
                "Сбросить достижение?",
                "Сбросить достижение «{0}»? Оно снова станет заблокированным, и его можно будет получить повторно.",
                "Отмена",
                "Сбросить",
                "Не удалось сбросить достижение. Попробуйте ещё раз."),
            "it" => new(
                "Reimposta obiettivo",
                "Reimpostare l'obiettivo?",
                "Reimpostare l'obiettivo «{0}»? Tornerà bloccato e potrà essere ottenuto di nuovo.",
                "Annulla",
                "Reimposta",
                "Impossibile reimpostare l'obiettivo. Riprova."),
            _ => new(
                "Reset achievement",
                "Reset achievement?",
                "Reset achievement “{0}”? It will become locked again and can be earned again.",
                "Cancel",
                "Reset",
                "The achievement could not be reset. Please try again.")
        };
}

public sealed record AchievementResetLabels(
    string Action,
    string Title,
    string MessageTemplate,
    string Cancel,
    string Confirm,
    string Error);
