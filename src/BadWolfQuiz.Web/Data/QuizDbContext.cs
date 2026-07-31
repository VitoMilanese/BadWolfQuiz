using BadWolfQuiz.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Data;

public sealed class QuizDbContext(DbContextOptions<QuizDbContext> options) : DbContext(options)
{
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
