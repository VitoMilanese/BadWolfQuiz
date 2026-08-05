using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Services;

public interface ISqliteVacuumService
{
    Task<bool> VacuumMainAsync(CancellationToken cancellationToken = default);
    Task VacuumArchiveAsync(CancellationToken cancellationToken = default);
}

public sealed class SqliteVacuumService(
    IDbContextFactory<QuizDbContext> quizDbFactory,
    IDbContextFactory<ArchiveDbContext> archiveDbFactory) : ISqliteVacuumService
{
    public async Task<bool> VacuumMainAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await quizDbFactory.CreateDbContextAsync(cancellationToken);
        if (await db.GameSessions.IgnoreQueryFilters().AnyAsync(x => x.Status != GameSessionStatus.Finished, cancellationToken))
            return false;
        await db.Database.ExecuteSqlRawAsync("VACUUM;", cancellationToken);
        return true;
    }

    public async Task VacuumArchiveAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await archiveDbFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync("VACUUM;", cancellationToken);
    }
}
