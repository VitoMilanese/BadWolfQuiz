using System.Security.Claims;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Tests;

public sealed class HostAccountServiceTests
{
    [Fact]
    public async Task Registration_saves_optional_trimmed_host_name()
    {
        await using var fixture = await Fixture.CreateAsync();

        var named = await fixture.Accounts.RegisterAsync(
            "named@example.com",
            "password-123",
            "  Quiz club  ");
        var unnamed = await fixture.Accounts.RegisterAsync(
            "unnamed@example.com",
            "password-123",
            "   ");

        Assert.Equal("Quiz club", named.Host!.DisplayName);
        Assert.Null(unnamed.Host!.DisplayName);
    }

    [Fact]
    public async Task First_registered_host_claims_legacy_quizzes_and_games()
    {
        await using var fixture = await Fixture.CreateAsync();
        var quiz = new Quiz { Title = "Legacy" };
        quiz.Sessions.Add(new GameSession { PublicCode = "LEGACY" });
        fixture.Db.Quizzes.Add(quiz);
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Accounts.RegisterAsync("first@example.com", "password-123");

        Assert.NotNull(result.Host);
        Assert.Equal(result.Host!.Id, await fixture.Db.Quizzes.IgnoreQueryFilters()
            .Select(x => x.HostId).SingleAsync());
        Assert.Equal(result.Host.Id, await fixture.Db.GameSessions.IgnoreQueryFilters()
            .Select(x => x.HostId).SingleAsync());
    }

    [Fact]
    public async Task Later_host_does_not_take_existing_data()
    {
        await using var fixture = await Fixture.CreateAsync();
        var first = await fixture.Accounts.RegisterAsync("first@example.com", "password-123");
        fixture.Db.Quizzes.Add(new Quiz { HostId = first.Host!.Id, Title = "First" });
        await fixture.Db.SaveChangesAsync();

        await fixture.Accounts.RegisterAsync("second@example.com", "password-456");

        Assert.Equal(first.Host.Id, await fixture.Db.Quizzes.IgnoreQueryFilters()
            .Select(x => x.HostId).SingleAsync());
    }

    [Fact]
    public async Task Query_filters_hide_another_hosts_quizzes()
    {
        await using var fixture = await Fixture.CreateAsync("host-b");
        fixture.Db.Hosts.AddRange(
            new HostAccount { Id = "host-a", Email = "a@example.com", NormalizedEmail = "A@EXAMPLE.COM", PasswordHash = "x" },
            new HostAccount { Id = "host-b", Email = "b@example.com", NormalizedEmail = "B@EXAMPLE.COM", PasswordHash = "x" });
        fixture.Db.Quizzes.AddRange(
            new Quiz { HostId = "host-a", Title = "Hidden" },
            new Quiz { HostId = "host-b", Title = "Visible" });
        await fixture.Db.SaveChangesAsync();

        Assert.Equal("host-b", fixture.Db.CurrentHostId);
        Assert.Equal("Visible", (await fixture.Db.Quizzes.SingleAsync()).Title);
    }

    [Fact]
    public async Task Password_reset_token_is_single_use()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Accounts.RegisterAsync("host@example.com", "old-password");
        var token = await fixture.Accounts.CreatePasswordResetTokenAsync("host@example.com");

        Assert.NotNull(token);
        Assert.True(await fixture.Accounts.ResetPasswordAsync(
            "host@example.com", token!, "new-password"));
        Assert.False(await fixture.Accounts.ResetPasswordAsync(
            "host@example.com", token!, "another-password"));
        Assert.NotNull(await fixture.Accounts.ValidateCredentialsAsync(
            "host@example.com", "new-password"));
    }

    [Fact]
    public async Task Change_password_requires_current_password()
    {
        await using var fixture = await Fixture.CreateAsync();
        var registered = await fixture.Accounts.RegisterAsync(
            "host@example.com", "old-password");

        Assert.False(await fixture.Accounts.ChangePasswordAsync(
            registered.Host!.Id, "wrong-password", "new-password"));
        Assert.True(await fixture.Accounts.ChangePasswordAsync(
            registered.Host.Id, "old-password", "new-password"));
        Assert.NotNull(await fixture.Accounts.ValidateCredentialsAsync(
            "host@example.com", "new-password"));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private Fixture(SqliteConnection connection, QuizDbContext db)
        {
            this.connection = connection;
            Db = db;
            Accounts = new HostAccountService(db, new PasswordHasher<HostAccount>());
        }
        public QuizDbContext Db { get; }
        public HostAccountService Accounts { get; }

        public static async Task<Fixture> CreateAsync(string? hostId = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<QuizDbContext>().UseSqlite(connection).Options;
            IHttpContextAccessor? accessor = null;
            if (hostId is not null)
            {
                var context = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, hostId)], "test"))
                };
                accessor = new HttpContextAccessor { HttpContext = context };
            }
            var db = new QuizDbContext(options, accessor);
            await db.Database.EnsureCreatedAsync();
            return new Fixture(connection, db);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
