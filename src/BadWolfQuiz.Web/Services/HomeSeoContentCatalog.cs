using System.Collections.Frozen;

namespace BadWolfQuiz.Web.Services;

public static class HomeSeoContentCatalog
{
    private static readonly FrozenDictionary<string, HomeSeoContent> Content =
        new Dictionary<string, HomeSeoContent>(StringComparer.Ordinal)
        {
            ["uk"] = new(
                "Онлайн-квіз гра для друзів і компаній",
                "Bad Wolf Quiz — безкоштовна онлайн-гра для живих квізів і вікторин. Один гравець проводить гру, а інші приєднуються зі своїх пристроїв і відповідають у реальному часі.",
                "Створюй власні квізи",
                "Додавай текст, зображення, аудіо та відео, налаштовуй раунди й збирай власну квіз-гру для вечірки, друзів або спільноти.",
                "Грай у готові вікторини",
                "Обирай публічні квізи, запускай готову гру та запрошуй учасників за кодом або QR-кодом."),
            ["en"] = new(
                "A free online quiz game for friends and groups",
                "Bad Wolf Quiz is a free live quiz and trivia game. One player hosts the game while everyone else joins from their own device and answers in real time.",
                "Create your own quiz games",
                "Build rounds with text, images, audio, and video, then turn them into a custom trivia game for a party, friends, or a community.",
                "Play ready-made trivia quizzes",
                "Browse public quizzes, start a ready-to-play game, and invite players with a code or QR code."),
            ["it"] = new(
                "Un gioco quiz online gratuito per amici e gruppi",
                "Bad Wolf Quiz è un gioco quiz e trivia dal vivo gratuito. Un giocatore conduce la partita, mentre gli altri partecipano dal proprio dispositivo e rispondono in tempo reale.",
                "Crea i tuoi giochi a quiz",
                "Crea round con testo, immagini, audio e video e prepara un gioco a quiz personalizzato per una festa, gli amici o una community.",
                "Gioca con quiz già pronti",
                "Esplora i quiz pubblici, avvia una partita pronta e invita i giocatori con un codice o un QR code.")
        }.ToFrozenDictionary(StringComparer.Ordinal);

    public static bool TryGet(string? culture, out HomeSeoContent content)
    {
        if (culture is not null && Content.TryGetValue(culture, out content))
        {
            return true;
        }

        content = default;
        return false;
    }
}

public readonly record struct HomeSeoContent(
    string Title,
    string Introduction,
    string CreateTitle,
    string CreateText,
    string PlayTitle,
    string PlayText);
