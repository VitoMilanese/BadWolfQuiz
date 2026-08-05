using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Services;
using BadWolfQuiz.Web.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace BadWolfQuiz.Web.Pages.Admin;

public sealed class MasterGamesModel(
    QuizDbContext db,
    GameSessionRegistry sessionRegistry,
    GameSessionLauncher gameSessionLauncher,
    CurrentHost currentHost,
    IConfiguration configuration,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    public IReadOnlyList<MasterActiveGameItem> Games { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!IsMasterHost())
        {
            return Forbid();
        }

        var activeGames = sessionRegistry.GetAll()
            .Where(game =>
                GameHub.IsHostConnected(game) &&
                sessionRegistry.HasConnectedPlayer(game))
            .Select(CaptureGame)
            .Where(game => game is not null)
            .Cast<RuntimeGameSnapshot>()
            .OrderBy(game => game.CreatedAtUtc)
            .ToArray();

        var hostIds = activeGames
            .Select(game => game.HostId)
            .Where(hostId => !string.IsNullOrWhiteSpace(hostId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var quizIds = activeGames
            .Select(game => game.QuizId)
            .Distinct()
            .ToArray();

        var hostNames = await db.Hosts
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(host => hostIds.Contains(host.Id))
            .ToDictionaryAsync(
                host => host.Id,
                host => host.DisplayName,
                StringComparer.Ordinal,
                cancellationToken);
        var quizzes = await db.Quizzes
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(quiz => quizIds.Contains(quiz.Id) && !quiz.IsArchived)
            .Select(quiz => new
            {
                quiz.Id,
                quiz.Title,
                quiz.HostId,
                quiz.IsPublic
            })
            .ToDictionaryAsync(quiz => quiz.Id, cancellationToken);

        Games = activeGames.Select(game =>
        {
            hostNames.TryGetValue(game.HostId ?? string.Empty, out var hostName);
            quizzes.TryGetValue(game.QuizId, out var quiz);
            var canPlay = quiz is not null &&
                          (quiz.IsPublic || string.Equals(
                              quiz.HostId,
                              currentHost.RequiredId,
                              StringComparison.Ordinal));
            return new MasterActiveGameItem(
                game.SessionId,
                game.HostId,
                hostName,
                quiz?.Title ?? game.QuizTitle,
                game.IsInLobby,
                game.RoundNumber,
                game.RoundTitle,
                game.Players,
                quiz?.Id,
                canPlay);
        }).ToArray();

        return Page();
    }

    public async Task<IActionResult> OnPostCreateGameAsync(
        int quizId,
        CancellationToken cancellationToken)
    {
        if (!IsMasterHost())
        {
            return Forbid();
        }

        try
        {
            var game = await gameSessionLauncher.CreateAsync(quizId, cancellationToken);
            return game is null
                ? NotFound()
                : RedirectToPage(
                    "/Admin/Games/Lobby",
                    new { id = game.Session.Id.Value });
        }
        catch (ArgumentException)
        {
            TempData["ErrorMessage"] = localizer["Error_QuizCannotStart"].Value;
        }
        catch (InvalidOperationException)
        {
            TempData["ErrorMessage"] = localizer["Error_QuizCannotStart"].Value;
        }

        return RedirectToPage();
    }

    private bool IsMasterHost() =>
        !string.IsNullOrWhiteSpace(configuration["MasterHostId"]) &&
        string.Equals(
            currentHost.Id,
            configuration["MasterHostId"]?.Trim(),
            StringComparison.Ordinal);

    private static RuntimeGameSnapshot? CaptureGame(GameSessionRegistration game)
    {
        lock (game)
        {
            var session = game.Session;
            if (session.Status == GameSessionStatus.Completed)
            {
                return null;
            }

            return new RuntimeGameSnapshot(
                session.Id.Value,
                game.HostId,
                session.Quiz.SourceQuizId,
                session.Quiz.Title,
                session.Status == GameSessionStatus.Lobby,
                session.Status == GameSessionStatus.Lobby
                    ? null
                    : session.CurrentRoundNumber,
                session.Status == GameSessionStatus.Lobby
                    ? null
                    : session.CurrentRound.Title,
                session.Players
                    .Select(player => new MasterActivePlayerItem(
                        player.Name,
                        player.Score))
                    .ToArray(),
                session.CreatedAtUtc);
        }
    }

    private sealed record RuntimeGameSnapshot(
        Guid SessionId,
        string? HostId,
        int QuizId,
        string QuizTitle,
        bool IsInLobby,
        int? RoundNumber,
        string? RoundTitle,
        IReadOnlyList<MasterActivePlayerItem> Players,
        DateTimeOffset CreatedAtUtc);
}

public sealed record MasterActiveGameItem(
    Guid SessionId,
    string? HostId,
    string? HostName,
    string? QuizTitle,
    bool IsInLobby,
    int? RoundNumber,
    string? RoundTitle,
    IReadOnlyList<MasterActivePlayerItem> Players,
    int? QuizId,
    bool CanPlay);

public sealed record MasterActivePlayerItem(string Name, int Score);
