using System.Globalization;

namespace BadWolfQuiz.Web.Localization;

public sealed record WordRingsRoomText
{
    public string Cooperative { get; init; } = "Co-op";
    public string CreateRoom { get; init; } = "Create co-op room";
    public string JoinRoom { get; init; } = "Join room";
    public string PlayerName { get; init; } = "Player name";
    public string TargetScore { get; init; } = "Play to";
    public string PartialScore { get; init; } = "Award 0.5 points for a partially correct placement";
    public string PartialDescription { get; init; } = "A placement that matches at least one required ring but not the whole region is worth half a point.";
    public string RoomCode { get; init; } = "Room";
    public string CopyLink { get; init; } = "Copy room link";
    public string StartGame { get; init; } = "Start game";
    public string WaitingForPlayers { get; init; } = "Waiting for players. The host can start when at least two players are in the room.";
    public string YourTurn { get; init; } = "Your turn";
    public string OtherTurn { get; init; } = "Turn: {0}";
    public string TeamScoreTemplate { get; init; } = "Team: {0} / {1}";
    public string Players { get; init; } = "Players";
    public string Host { get; init; } = "Host";
    public string Close { get; init; } = "Close";
    public string Cancel { get; init; } = "Cancel";
    public string LeaveRoom { get; init; } = "Leave room";
    public string RoomError { get; init; } = "Could not complete the room operation.";
    public string NeedMorePlayers { get; init; } = "At least two players are required.";
    public string InvalidName { get; init; } = "Enter a player name.";
    public string WinTitle { get; init; } = "Victory!";
    public string LoseTitle { get; init; } = "Defeat";
    public string SoloWinTemplate { get; init; } = "You placed {0} words correctly and reached the target of {1}.";
    public string SoloLoseTemplate { get; init; } = "The 20-word limit is over. Correct placements: {0} of {1}.";
    public string CoopWinTemplate { get; init; } = "The team scored {0} of {1} points.";
    public string CoopLoseTemplate { get; init; } = "All player word sets are exhausted. The team scored {0} of {1} points.";
    public string PartialAwardTemplate { get; init; } = "Partially correct: +0.5";
    public string CorrectNoPointTemplate { get; init; } = "Correct. An outside-rings point was already awarded this turn.";
    public string ScoreAwardTemplate { get; init; } = "Correct: +{0}";
    public string RoomSettings { get; init; } = "Room settings";
    public string JoinExisting { get; init; } = "Join an existing room";
    public string EnterCode { get; init; } = "Room code";
    public string CreateAction { get; init; } = "Create";
    public string JoinAction { get; init; } = "Join";

    public static WordRingsRoomText ForCulture(CultureInfo culture) =>
        culture.TwoLetterISOLanguageName.ToLowerInvariant() switch
        {
            "uk" => Ukrainian,
            "it" => Italian,
            "ru" => Russian,
            _ => English
        };

    private static WordRingsRoomText English { get; } = new();

    private static WordRingsRoomText Ukrainian { get; } = new()
    {
        Cooperative = "Кооператив",
        CreateRoom = "Створити кооперативну кімнату",
        JoinRoom = "Приєднатися до кімнати",
        PlayerName = "Ім'я гравця",
        TargetScore = "Грати до",
        PartialScore = "Давати 0,5 бала за частково правильне розміщення",
        PartialDescription = "Якщо область збігається хоча б з одним потрібним кільцем, але не повністю, гравець отримує пів бала.",
        RoomCode = "Кімната",
        CopyLink = "Скопіювати посилання на кімнату",
        StartGame = "Почати гру",
        WaitingForPlayers = "Очікування гравців. Власник може почати гру, коли в кімнаті буде щонайменше двоє гравців.",
        YourTurn = "Ваш хід",
        OtherTurn = "Хід: {0}",
        TeamScoreTemplate = "Команда: {0} / {1}",
        Players = "Гравці",
        Host = "Власник",
        Close = "Закрити",
        Cancel = "Скасувати",
        LeaveRoom = "Вийти з кімнати",
        RoomError = "Не вдалося виконати операцію з кімнатою.",
        NeedMorePlayers = "Для початку гри потрібно щонайменше двоє гравців.",
        InvalidName = "Введіть ім'я гравця.",
        WinTitle = "Перемога!",
        LoseTitle = "Поразка",
        SoloWinTemplate = "Правильно розставлено {0} слів. Ціль {1} досягнута.",
        SoloLoseTemplate = "20 слів закінчилися. Правильно розставлено {0} з потрібних {1}.",
        CoopWinTemplate = "Команда набрала {0} з {1} балів.",
        CoopLoseTemplate = "Усі набори слів гравців закінчилися. Команда набрала {0} з {1} балів.",
        PartialAwardTemplate = "Частково правильно: +0,5",
        CorrectNoPointTemplate = "Правильно. Бал за слово поза кільцями в цьому ході вже був отриманий.",
        ScoreAwardTemplate = "Правильно: +{0}",
        RoomSettings = "Налаштування кімнати",
        JoinExisting = "Приєднатися до існуючої кімнати",
        EnterCode = "Код кімнати",
        CreateAction = "Створити",
        JoinAction = "Приєднатися"
    };

    private static WordRingsRoomText Italian { get; } = new()
    {
        Cooperative = "Cooperativa",
        CreateRoom = "Crea stanza cooperativa",
        JoinRoom = "Entra nella stanza",
        PlayerName = "Nome giocatore",
        TargetScore = "Gioca fino a",
        PartialScore = "Assegna 0,5 punti per un posizionamento parzialmente corretto",
        PartialDescription = "Se la posizione corrisponde ad almeno un cerchio richiesto ma non all'intera area, vale mezzo punto.",
        RoomCode = "Stanza",
        CopyLink = "Copia link della stanza",
        StartGame = "Avvia partita",
        WaitingForPlayers = "In attesa dei giocatori. L'host può iniziare con almeno due giocatori.",
        YourTurn = "Il tuo turno",
        OtherTurn = "Turno: {0}",
        TeamScoreTemplate = "Squadra: {0} / {1}",
        Players = "Giocatori",
        Host = "Host",
        Close = "Chiudi",
        Cancel = "Annulla",
        LeaveRoom = "Esci dalla stanza",
        RoomError = "Impossibile completare l'operazione della stanza.",
        NeedMorePlayers = "Servono almeno due giocatori.",
        InvalidName = "Inserisci il nome del giocatore.",
        WinTitle = "Vittoria!",
        LoseTitle = "Sconfitta",
        SoloWinTemplate = "Hai posizionato correttamente {0} parole e raggiunto l'obiettivo di {1}.",
        SoloLoseTemplate = "Le 20 parole sono terminate. Posizionamenti corretti: {0} su {1}.",
        CoopWinTemplate = "La squadra ha ottenuto {0} punti su {1}.",
        CoopLoseTemplate = "Tutti i set di parole sono terminati. La squadra ha ottenuto {0} punti su {1}.",
        PartialAwardTemplate = "Parzialmente corretto: +0,5",
        CorrectNoPointTemplate = "Corretto. Il punto fuori dai cerchi è già stato assegnato in questo turno.",
        ScoreAwardTemplate = "Corretto: +{0}",
        RoomSettings = "Impostazioni stanza",
        JoinExisting = "Entra in una stanza esistente",
        EnterCode = "Codice stanza",
        CreateAction = "Crea",
        JoinAction = "Entra"
    };

    private static WordRingsRoomText Russian { get; } = new()
    {
        Cooperative = "Україна",
        CreateRoom = "Україна",
        JoinRoom = "Україна",
        PlayerName = "Україна",
        TargetScore = "Україна",
        PartialScore = "Україна",
        PartialDescription = "Україна",
        RoomCode = "Україна",
        CopyLink = "Україна",
        StartGame = "Україна",
        WaitingForPlayers = "Україна",
        YourTurn = "Україна",
        OtherTurn = "Україна",
        TeamScoreTemplate = "Україна",
        Players = "Україна",
        Host = "Україна",
        Close = "Україна",
        Cancel = "Україна",
        LeaveRoom = "Україна",
        RoomError = "Україна",
        NeedMorePlayers = "Україна",
        InvalidName = "Україна",
        WinTitle = "Україна",
        LoseTitle = "Україна",
        SoloWinTemplate = "Україна",
        SoloLoseTemplate = "Україна",
        CoopWinTemplate = "Україна",
        CoopLoseTemplate = "Україна",
        PartialAwardTemplate = "Україна",
        CorrectNoPointTemplate = "Україна",
        ScoreAwardTemplate = "Україна",
        RoomSettings = "Україна",
        JoinExisting = "Україна",
        EnterCode = "Україна",
        CreateAction = "Україна",
        JoinAction = "Україна"
    };
}
