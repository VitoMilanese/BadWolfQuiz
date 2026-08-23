using System.ComponentModel.DataAnnotations;

namespace BadWolfQuiz.Web.Models;

public enum ArchivedMediaRole
{
    QuestionBlock = 1,
    AnswerBlock = 2,
    FinalQuestionBlock = 3,
    FinalAnswerBlock = 4,
    FinalDescriptionBlock = 5
}

public enum ArchiveOperationState
{
    Creating = 1,
    Verified = 2,
    Completed = 3,
    Failed = 4,
    Superseded = 5
}

public sealed class ArchivedQuizMedia
{
    public long Id { get; set; }
    public Guid OperationId { get; set; }
    public int QuizId { get; set; }
    [Required, MaxLength(36)]
    public string HostId { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public ArchivedMediaRole Role { get; set; }
    [MaxLength(256)]
    public string? ContentType { get; set; }
    [MaxLength(512)]
    public string? OriginalFileName { get; set; }
    public byte[] Data { get; set; } = [];
    public long Length { get; set; }
    [Required, MaxLength(64)]
    public string Sha256 { get; set; } = string.Empty;
    public DateTime ArchivedAtUtc { get; set; }
}

public sealed class ArchiveOperation
{
    public Guid Id { get; set; }
    public int QuizId { get; set; }
    [Required, MaxLength(36)]
    public string HostId { get; set; } = string.Empty;
    public ArchiveOperationState State { get; set; }
    public int MediaCount { get; set; }
    public long MediaBytes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? RestoredAtUtc { get; set; }
    public DateTime? OrphanedAtUtc { get; set; }
    [MaxLength(1000)]
    public string? FailureReason { get; set; }
}
