using System.Security.Claims;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Services;

public sealed class HostAccountService(
    QuizDbContext db,
    IPasswordHasher<HostAccount> passwordHasher)
{
    public async Task<HostRegistrationResult> RegisterAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (await db.Hosts.AnyAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken))
        {
            return HostRegistrationResult.EmailAlreadyUsed;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var isFirstHost = !await db.Hosts.AnyAsync(cancellationToken);
        var host = new HostAccount
        {
            Email = email.Trim(),
            NormalizedEmail = normalizedEmail
        };
        host.PasswordHash = passwordHasher.HashPassword(host, password);
        db.Hosts.Add(host);
        await db.SaveChangesAsync(cancellationToken);

        if (isFirstHost)
        {
            await db.Quizzes.IgnoreQueryFilters()
                .Where(x => x.HostId == null)
                .ExecuteUpdateAsync(update => update.SetProperty(x => x.HostId, host.Id), cancellationToken);
            await db.GameSessions.IgnoreQueryFilters()
                .Where(x => x.HostId == null)
                .ExecuteUpdateAsync(update => update.SetProperty(x => x.HostId, host.Id), cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return new HostRegistrationResult(host);
    }

    public async Task<HostAccount?> ValidateCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        var host = await db.Hosts.SingleOrDefaultAsync(
            x => x.NormalizedEmail == normalizedEmail,
            cancellationToken);
        if (host is null)
        {
            return null;
        }

        var result = passwordHasher.VerifyHashedPassword(host, host.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed)
        {
            return null;
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            host.PasswordHash = passwordHasher.HashPassword(host, password);
            await db.SaveChangesAsync(cancellationToken);
        }

        return host;
    }

    public static ClaimsPrincipal CreatePrincipal(HostAccount host)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, host.Id),
                new Claim(ClaimTypes.Name, host.Email),
                new Claim(ClaimTypes.Email, host.Email)
            ],
            CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();
}

public sealed record HostRegistrationResult
{
    public static HostRegistrationResult EmailAlreadyUsed { get; } = new(null, true);
    public HostRegistrationResult(HostAccount host) : this(host, false) { }
    private HostRegistrationResult(HostAccount? host, bool emailAlreadyUsed)
    {
        Host = host;
        IsEmailAlreadyUsed = emailAlreadyUsed;
    }
    public HostAccount? Host { get; }
    public bool IsEmailAlreadyUsed { get; }
}
