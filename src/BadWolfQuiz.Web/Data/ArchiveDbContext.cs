using BadWolfQuiz.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Data;

public sealed class ArchiveDbContext(DbContextOptions<ArchiveDbContext> options)
    : DbContext(options)
{
    public DbSet<ArchivedQuizMedia> ArchivedQuizMedia => Set<ArchivedQuizMedia>();
    public DbSet<ArchiveOperation> ArchiveOperations => Set<ArchiveOperation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ArchivedQuizMedia>().HasIndex(x => x.QuizId);
        modelBuilder.Entity<ArchivedQuizMedia>().HasIndex(x => x.HostId);
        modelBuilder.Entity<ArchivedQuizMedia>().HasIndex(x => x.OperationId);
        modelBuilder.Entity<ArchivedQuizMedia>().HasIndex(x => new { x.QuizId, x.HostId });
        modelBuilder.Entity<ArchivedQuizMedia>()
            .HasIndex(x => new { x.OperationId, x.EntityId, x.Role })
            .IsUnique();
        modelBuilder.Entity<ArchiveOperation>().HasIndex(x => new { x.QuizId, x.HostId });
    }
}
