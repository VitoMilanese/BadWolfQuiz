using System.ComponentModel.DataAnnotations;
using BadWolfQuiz.Game.Definitions;

namespace BadWolfQuiz.Web.Models;

public enum ContentBlockType
{
    Text = 1,
    Image = 2,
    Audio = 3,
    Video = 4,
    YouTube = 5,
    Container = 6,
    AnswerOptions = 7
}

public enum BuzzActivationMode
{
    UseRoundDefault = 0,
    Manual = 1,
    Immediately = 2,
    AfterMedia = 3,
    AfterDelay = 4,
    Disabled = 5
}

public enum GameSessionStatus
{
    Lobby = 1,
    Running = 2,
    Finished = 3
}

public enum GameQuestionStatus
{
    Pending = 1,
    ShowingQuestion = 2,
    BuzzOpen = 3,
    Locked = 4,
    ShowingAnswer = 5,
    Finished = 6
}

public enum QuizMediaState
{
    Active = 0,
    Archiving = 1,
    Archived = 2,
    Restoring = 3,
    Failed = 4
}

public sealed class HostAccount
{
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString("D");

    [Required, MaxLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(254)]
    public string NormalizedEmail { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(80)]
    public string? DisplayName { get; set; }

    [MaxLength(64)]
    public string? PasswordResetTokenHash { get; set; }
    public DateTime? PasswordResetTokenExpiresAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<Quiz> Quizzes { get; set; } = new List<Quiz>();
    public ICollection<GameSession> GameSessions { get; set; } = new List<GameSession>();
    public HostDiscordConnection? DiscordConnection { get; set; }
}

public sealed class HostDiscordConnection
{
    public int Id { get; set; }

    [Required, MaxLength(36)]
    public string HostId { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string DiscordUserId { get; set; } = string.Empty;

    [MaxLength(80)]
    public string? DiscordUserName { get; set; }

    [MaxLength(20)]
    public string? GuildId { get; set; }

    [MaxLength(100)]
    public string? GuildName { get; set; }

    [MaxLength(20)]
    public string? VoiceChannelId { get; set; }

    [MaxLength(100)]
    public string? VoiceChannelName { get; set; }

    public bool AutoMuteDuringMedia { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public HostAccount Host { get; set; } = null!;
}

public sealed class Quiz
{
    public int Id { get; set; }
    [MaxLength(36)]
    public string? HostId { get; set; }

    [Required, MaxLength(160)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsArchived { get; set; }
    public QuizMediaState MediaState { get; set; } = QuizMediaState.Active;
    public Guid? CurrentArchiveOperationId { get; set; }
    public int ArchivedMediaCount { get; set; }
    public long ArchivedMediaBytes { get; set; }
    public DateTime? MediaArchivedAtUtc { get; set; }
    public DateTime? MediaRestoredAtUtc { get; set; }
    [MaxLength(1000)]
    public string? MediaArchiveFailureReason { get; set; }
    public bool PreventAutomaticArchiving { get; set; }
    public DateTime? LastPlayedAtUtc { get; set; }
    public bool IsPublic { get; set; }
    public DateTime? PublishedAtUtc { get; set; }

    public ICollection<QuizRound> Rounds { get; set; } = new List<QuizRound>();
    public ICollection<FinalDescriptionContentBlock> FinalDescriptionBlocks { get; set; } =
        new List<FinalDescriptionContentBlock>();
    public ICollection<FinalQuestionContentBlock> FinalQuestionBlocks { get; set; } =
        new List<FinalQuestionContentBlock>();
    public ICollection<FinalAnswerContentBlock> FinalAnswerBlocks { get; set; } =
        new List<FinalAnswerContentBlock>();
    public ICollection<GameSession> Sessions { get; set; } = new List<GameSession>();
    public ICollection<QuizRating> Ratings { get; set; } = new List<QuizRating>();
    public HostAccount? Host { get; set; }
}

public sealed class QuizRating
{
    public int Id { get; set; }
    public int QuizId { get; set; }
    public int GameSessionId { get; set; }
    [Required, MaxLength(100)]
    public string RaterKey { get; set; } = string.Empty;
    [Range(0, 5)]
    public int Score { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Quiz Quiz { get; set; } = null!;
    public GameSession GameSession { get; set; } = null!;
}

public sealed class QuizRound
{
    public int Id { get; set; }
    public int QuizId { get; set; }

    [Required, MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    public int SortOrder { get; set; }
    public int DefaultTimeLimitSeconds { get; set; } = 30;
    public BuzzActivationMode DefaultBuzzMode { get; set; } = BuzzActivationMode.Manual;
    public bool UseRandomWagerQuestions { get; set; }
    public int RandomWagerQuestionCount { get; set; }
    public bool UseRandomAnonymousSharedWagerQuestions { get; set; }
    public int RandomAnonymousSharedWagerQuestionCount { get; set; }

    public Quiz Quiz { get; set; } = null!;
    public ICollection<QuizRoundRow> Rows { get; set; } = new List<QuizRoundRow>();
    public ICollection<QuizCategory> Categories { get; set; } = new List<QuizCategory>();
    public ICollection<RoundDescriptionContentBlock> DescriptionBlocks { get; set; } =
        new List<RoundDescriptionContentBlock>();
}

public sealed class QuizRoundRow
{
    public int Id { get; set; }
    public int QuizRoundId { get; set; }
    public int RowIndex { get; set; }
    public int Points { get; set; }

    public QuizRound Round { get; set; } = null!;
}

public sealed class QuizCategory
{
    public int Id { get; set; }
    public int QuizRoundId { get; set; }

    [Required, MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public QuizRound Round { get; set; } = null!;
    public ICollection<QuizQuestion> Questions { get; set; } = new List<QuizQuestion>();
    public ICollection<CategoryDescriptionContentBlock> DescriptionBlocks { get; set; } =
        new List<CategoryDescriptionContentBlock>();
}

public sealed class QuizQuestion
{
    public int Id { get; set; }
    public int QuizCategoryId { get; set; }
    public int RowIndex { get; set; }
    public int? TimeLimitSecondsOverride { get; set; }
    public BuzzActivationMode BuzzModeOverride { get; set; } = BuzzActivationMode.UseRoundDefault;
    public int BuzzDelaySeconds { get; set; }
    public bool IsSpecial { get; set; }
    public QuestionPresentationType PresentationType { get; set; }
    public bool ExcludeFromRandomWagerSelection { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public QuizCategory Category { get; set; } = null!;
    public ICollection<QuestionContentBlock> QuestionBlocks { get; set; } = new List<QuestionContentBlock>();
    public ICollection<AnswerContentBlock> AnswerBlocks { get; set; } = new List<AnswerContentBlock>();
}

public abstract class ContentBlockBase
{
    public int Id { get; set; }
    public ContentBlockType BlockType { get; set; }
    public string? TextContent { get; set; }
    public string? TopCaption { get; set; }
    public string? BottomCaption { get; set; }
    public string? MediaPath { get; set; }
    public string? ExternalUrl { get; set; }
    public int SortOrder { get; set; }
    public bool AudioOnly { get; set; }
    public bool Autoplay { get; set; }
    public byte[]? FileData { get; set; }
    public string? FileContentType { get; set; }
    public string? FileName { get; set; }
}

public sealed class FinalDescriptionContentBlock : ContentBlockBase
{
    public int QuizId { get; set; }
    public Quiz Quiz { get; set; } = null!;
}

public sealed class FinalQuestionContentBlock : ContentBlockBase
{
    public int QuizId { get; set; }
    public Quiz Quiz { get; set; } = null!;
}

public sealed class FinalAnswerContentBlock : ContentBlockBase
{
    public int QuizId { get; set; }
    public Quiz Quiz { get; set; } = null!;
}

public sealed class RoundDescriptionContentBlock : ContentBlockBase
{
    public int QuizRoundId { get; set; }
    public QuizRound Round { get; set; } = null!;
}

public sealed class CategoryDescriptionContentBlock : ContentBlockBase
{
    public int QuizCategoryId { get; set; }
    public QuizCategory Category { get; set; } = null!;
}

public sealed class QuestionContentBlock : ContentBlockBase
{
    public int QuizQuestionId { get; set; }
    public QuizQuestion Question { get; set; } = null!;
}

public sealed class AnswerContentBlock : ContentBlockBase
{
    public int QuizQuestionId { get; set; }
    public QuizQuestion Question { get; set; } = null!;
}

public sealed class GameSession
{
    public int Id { get; set; }
    public int QuizId { get; set; }
    [MaxLength(36)]
    public string? HostId { get; set; }

    [Required, MaxLength(12)]
    public string PublicCode { get; set; } = string.Empty;

    public GameSessionStatus Status { get; set; } = GameSessionStatus.Lobby;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }

    public Quiz Quiz { get; set; } = null!;
    public HostAccount? Host { get; set; }
    public ICollection<GamePlayer> Players { get; set; } = new List<GamePlayer>();
    public ICollection<GameQuestion> Questions { get; set; } = new List<GameQuestion>();
}

public sealed class GamePlayer
{
    public int Id { get; set; }
    public int GameSessionId { get; set; }

    [Required, MaxLength(60)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string ReconnectToken { get; set; } = string.Empty;

    public int TotalScore { get; set; }
    public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastSeenAtUtc { get; set; }
    public bool IsActive { get; set; } = true;

    public GameSession Session { get; set; } = null!;
    public ICollection<PlayerQuestionResult> Results { get; set; } = new List<PlayerQuestionResult>();
}

public sealed class GameQuestion
{
    public int Id { get; set; }
    public int GameSessionId { get; set; }
    public int QuizQuestionId { get; set; }
    public GameQuestionStatus Status { get; set; } = GameQuestionStatus.Pending;
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? BuzzOpenedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public int? FirstBuzzPlayerId { get; set; }

    public GameSession Session { get; set; } = null!;
    public QuizQuestion QuizQuestion { get; set; } = null!;
    public ICollection<PlayerBuzz> Buzzes { get; set; } = new List<PlayerBuzz>();
    public ICollection<PlayerQuestionResult> Results { get; set; } = new List<PlayerQuestionResult>();
}

public sealed class PlayerBuzz
{
    public int Id { get; set; }
    public int GameQuestionId { get; set; }
    public int GamePlayerId { get; set; }
    public DateTime ServerReceivedAtUtc { get; set; } = DateTime.UtcNow;
    public int BuzzOrder { get; set; }
    public bool WasFirst { get; set; }

    public GameQuestion GameQuestion { get; set; } = null!;
    public GamePlayer Player { get; set; } = null!;
}

public sealed class PlayerQuestionResult
{
    public int Id { get; set; }
    public int GameQuestionId { get; set; }
    public int GamePlayerId { get; set; }
    public bool? IsCorrect { get; set; }
    public int PointsAwarded { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public GameQuestion GameQuestion { get; set; } = null!;
    public GamePlayer Player { get; set; } = null!;
}
