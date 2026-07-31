using BadWolfQuiz.Web.Models;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Data;

public sealed class QuizDbContext(
    DbContextOptions<QuizDbContext> options,
    IHttpContextAccessor? httpContextAccessor = null) : DbContext(options)
{
    private string? CurrentHostId => httpContextAccessor?.HttpContext?.User
        .FindFirstValue(ClaimTypes.NameIdentifier);

    public DbSet<HostAccount> Hosts => Set<HostAccount>();
    public DbSet<Quiz> Quizzes => Set<Quiz>();
    public DbSet<QuizRound> QuizRounds => Set<QuizRound>();
    public DbSet<QuizRoundRow> QuizRoundRows => Set<QuizRoundRow>();
    public DbSet<QuizCategory> QuizCategories => Set<QuizCategory>();
    public DbSet<QuizQuestion> QuizQuestions => Set<QuizQuestion>();
    public DbSet<FinalQuestionContentBlock> FinalQuestionContentBlocks =>
        Set<FinalQuestionContentBlock>();
    public DbSet<FinalAnswerContentBlock> FinalAnswerContentBlocks =>
        Set<FinalAnswerContentBlock>();
    public DbSet<QuestionContentBlock> QuestionContentBlocks => Set<QuestionContentBlock>();
    public DbSet<AnswerContentBlock> AnswerContentBlocks => Set<AnswerContentBlock>();
    public DbSet<GameSession> GameSessions => Set<GameSession>();
    public DbSet<GamePlayer> GamePlayers => Set<GamePlayer>();
    public DbSet<GameQuestion> GameQuestions => Set<GameQuestion>();
    public DbSet<PlayerBuzz> PlayerBuzzes => Set<PlayerBuzz>();
    public DbSet<PlayerQuestionResult> PlayerQuestionResults => Set<PlayerQuestionResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<HostAccount>()
            .HasIndex(x => x.NormalizedEmail)
            .IsUnique();

        modelBuilder.Entity<Quiz>()
            .HasOne(x => x.Host)
            .WithMany(x => x.Quizzes)
            .HasForeignKey(x => x.HostId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GameSession>()
            .HasOne(x => x.Host)
            .WithMany(x => x.GameSessions)
            .HasForeignKey(x => x.HostId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Quiz>()
            .HasQueryFilter(x => x.HostId == CurrentHostId);
        modelBuilder.Entity<QuizRound>()
            .HasQueryFilter(x => x.Quiz.HostId == CurrentHostId);
        modelBuilder.Entity<QuizRoundRow>()
            .HasQueryFilter(x => x.Round.Quiz.HostId == CurrentHostId);
        modelBuilder.Entity<QuizCategory>()
            .HasQueryFilter(x => x.Round.Quiz.HostId == CurrentHostId);
        modelBuilder.Entity<QuizQuestion>()
            .HasQueryFilter(x => x.Category.Round.Quiz.HostId == CurrentHostId);
        modelBuilder.Entity<FinalQuestionContentBlock>()
            .HasQueryFilter(x => x.Quiz.HostId == CurrentHostId);
        modelBuilder.Entity<FinalAnswerContentBlock>()
            .HasQueryFilter(x => x.Quiz.HostId == CurrentHostId);
        modelBuilder.Entity<QuestionContentBlock>()
            .HasQueryFilter(x => x.Question.Category.Round.Quiz.HostId == CurrentHostId);
        modelBuilder.Entity<AnswerContentBlock>()
            .HasQueryFilter(x => x.Question.Category.Round.Quiz.HostId == CurrentHostId);
        modelBuilder.Entity<GameSession>()
            .HasQueryFilter(x => x.HostId == CurrentHostId);
        modelBuilder.Entity<GamePlayer>()
            .HasQueryFilter(x => x.Session.HostId == CurrentHostId);
        modelBuilder.Entity<GameQuestion>()
            .HasQueryFilter(x => x.Session.HostId == CurrentHostId);
        modelBuilder.Entity<PlayerBuzz>()
            .HasQueryFilter(x => x.GameQuestion.Session.HostId == CurrentHostId);
        modelBuilder.Entity<PlayerQuestionResult>()
            .HasQueryFilter(x => x.GameQuestion.Session.HostId == CurrentHostId);

        modelBuilder.Entity<QuizRound>()
            .HasIndex(x => new { x.QuizId, x.SortOrder })
            .IsUnique();

        modelBuilder.Entity<QuizRoundRow>()
            .HasIndex(x => new { x.QuizRoundId, x.RowIndex })
            .IsUnique();

        modelBuilder.Entity<QuizCategory>()
            .HasIndex(x => new { x.QuizRoundId, x.SortOrder })
            .IsUnique();

        modelBuilder.Entity<QuizQuestion>()
            .HasIndex(x => new { x.QuizCategoryId, x.RowIndex })
            .IsUnique();

        modelBuilder.Entity<GameSession>()
            .HasIndex(x => x.PublicCode)
            .IsUnique();

        modelBuilder.Entity<GamePlayer>()
            .HasIndex(x => new { x.GameSessionId, x.Name })
            .IsUnique();

        modelBuilder.Entity<PlayerBuzz>()
            .HasIndex(x => new { x.GameQuestionId, x.GamePlayerId })
            .IsUnique();

        modelBuilder.Entity<PlayerQuestionResult>()
            .HasIndex(x => new { x.GameQuestionId, x.GamePlayerId })
            .IsUnique();
    }
}
