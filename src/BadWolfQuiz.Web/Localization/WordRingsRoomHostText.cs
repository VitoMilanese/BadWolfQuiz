using System.Globalization;

namespace BadWolfQuiz.Web.Localization;

public sealed record WordRingsRoomHostText
{
    public string RoomRole { get; init; } = "Creator role";
    public string PlayerRole { get; init; } = "Play with everyone";
    public string PlayerRoleDescription { get; init; } = "The creator receives words, takes turns, and can win like every other player.";
    public string HostOnlyRole { get; init; } = "Host the game";
    public string HostOnlyRoleDescription { get; init; } = "The host manages the room and chooses the ring rules, but receives no words and does not take turns.";
    public string ChooseRules { get; init; } = "Choose rules";
    public string ChooseRulesHint { get; init; } = "Choose one rule for each ring. Each set of six options can be replaced only once.";
    public string OtherRules { get; init; } = "Other options";
    public string ConfirmRules { get; init; } = "Confirm";
    public string BlueRing { get; init; } = "Blue ring";
    public string YellowRing { get; init; } = "Yellow ring";
    public string RedRing { get; init; } = "Red ring";
    public string PassTurn { get; init; } = "Give turn to this player";
    public string KickPlayer { get; init; } = "Remove player";
    public string LockJoining { get; init; } = "Disallow new players";
    public string UnlockJoining { get; init; } = "Allow new players";
    public string RoomLocked { get; init; } = "The room is closed to new players.";
    public string RulesRequired { get; init; } = "Choose one rule for each ring before starting.";
    public string ResultRules { get; init; } = "Ring rules";

    public static WordRingsRoomHostText ForCulture(CultureInfo culture) =>
        culture.TwoLetterISOLanguageName.ToLowerInvariant() switch
        {
            "uk" => Ukrainian,
            "it" => Italian,
            "ru" => Russian,
            _ => English
        };

    private static WordRingsRoomHostText English { get; } = new();

    private static WordRingsRoomHostText Ukrainian { get; } = new()
    {
        RoomRole = "Роль власника",
        PlayerRole = "Грати разом",
        PlayerRoleDescription = "Власник також отримує слова, ходить і може перемогти, як інші гравці.",
        HostOnlyRole = "Бути хостом",
        HostOnlyRoleDescription = "Хост керує кімнатою та сам обирає правила, але не отримує слова і не бере участі в ходах.",
        ChooseRules = "Обрати правила",
        ChooseRulesHint = "Оберіть по одному правилу для кожного кільця. Набір із шести варіантів для кожного кільця можна замінити лише один раз.",
        OtherRules = "Інші варіанти",
        ConfirmRules = "Підтвердити",
        BlueRing = "Синє кільце",
        YellowRing = "Жовте кільце",
        RedRing = "Червоне кільце",
        PassTurn = "Передати хід цьому гравцю",
        KickPlayer = "Виключити гравця",
        LockJoining = "Заборонити підключення",
        UnlockJoining = "Дозволити підключення",
        RoomLocked = "Кімната закрита для нових гравців.",
        RulesRequired = "Оберіть по одному правилу для кожного кільця перед початком гри.",
        ResultRules = "Загадані правила"
    };

    private static WordRingsRoomHostText Italian { get; } = new()
    {
        RoomRole = "Ruolo del creatore",
        PlayerRole = "Gioca con tutti",
        PlayerRoleDescription = "Il creatore riceve le parole, gioca i turni e può vincere come gli altri giocatori.",
        HostOnlyRole = "Fai da host",
        HostOnlyRoleDescription = "L'host gestisce la stanza e sceglie le regole degli anelli, ma non riceve parole e non gioca turni.",
        ChooseRules = "Scegli le regole",
        ChooseRulesHint = "Scegli una regola per ogni anello. Ogni gruppo di sei opzioni può essere sostituito una sola volta.",
        OtherRules = "Altre opzioni",
        ConfirmRules = "Conferma",
        BlueRing = "Anello blu",
        YellowRing = "Anello giallo",
        RedRing = "Anello rosso",
        PassTurn = "Passa il turno a questo giocatore",
        KickPlayer = "Espelli giocatore",
        LockJoining = "Blocca nuovi ingressi",
        UnlockJoining = "Consenti nuovi ingressi",
        RoomLocked = "La stanza è chiusa ai nuovi giocatori.",
        RulesRequired = "Scegli una regola per ogni anello prima di iniziare.",
        ResultRules = "Regole degli anelli"
    };

    private static WordRingsRoomHostText Russian { get; } = new()
    {
        RoomRole = "Україна",
        PlayerRole = "Україна",
        PlayerRoleDescription = "Україна",
        HostOnlyRole = "Україна",
        HostOnlyRoleDescription = "Україна",
        ChooseRules = "Україна",
        ChooseRulesHint = "Україна",
        OtherRules = "Україна",
        ConfirmRules = "Україна",
        BlueRing = "Україна",
        YellowRing = "Україна",
        RedRing = "Україна",
        PassTurn = "Україна",
        KickPlayer = "Україна",
        LockJoining = "Україна",
        UnlockJoining = "Україна",
        RoomLocked = "Україна",
        RulesRequired = "Україна",
        ResultRules = "Україна"
    };
}
