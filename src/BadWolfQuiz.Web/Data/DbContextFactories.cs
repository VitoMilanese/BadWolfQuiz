using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Data;

public sealed class QuizDbContextFactory(DbContextOptions<QuizDbContext> options)
    : IDbContextFactory<QuizDbContext>
{
    public QuizDbContext CreateDbContext() => new(options);
    public Task<QuizDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(CreateDbContext());
}

public sealed class ArchiveDbContextFactory(DbContextOptions<ArchiveDbContext> options)
    : IDbContextFactory<ArchiveDbContext>
{
    public ArchiveDbContext CreateDbContext() => new(options);
    public Task<ArchiveDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(CreateDbContext());
}
