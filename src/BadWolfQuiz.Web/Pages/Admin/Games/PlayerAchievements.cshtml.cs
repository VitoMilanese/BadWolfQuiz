using System.Globalization;
using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace BadWolfQuiz.Web.Pages.Admin.Games;

public sealed class PlayerAchievementsModel(
    QuizDbContext db,
    GameSessionRegistry sessionRegistry,
    CurrentHost currentHost,
    IStringLocalizer<AchievementResource> localizer,
    IStringLocalizer<PlayerAchievementHistoryResource> historyLocalizer,
    IOptions<FooterOptions> footerOptions) : PageModel
{
    public const string GameMode = "game";
    public const string PlayersMode = "players";
    public const string PendingMode = "pending";

    public GameSessionRegistration Game { get; private set; } = null!;
    public string Mode { get; private set; } = GameMode;
    public IReadOnlyList<PlayerAchievementHistoryPlayer> Players { get; private set; } = [];
    public Guid? SelectedPlayerId { get; private set; }
    public IReadOnlyList<PlayerAchievementHistoryEntry> Entries { get; private set; } = [];
    public IReadOnlyList<PlayerAchievementPendingConfirmation> PendingConfirmations { get; private set; } = [];
    public bool HasPendingConfirmations => PendingConfirmations.Count > 0;
    public int CurrentModeCount => Mode == PendingMode ? PendingConfirmations.Count : Entries.Count;

    public async Task<IActionResult> OnGetAsync(
        Guid id,
        Guid? playerId,
        string? mode,
        Guid? selectedPlayerId,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.FindOwned(
            new GameSessionId(id),
            currentHost.RequiredId);
        if (game is null)
        {
            return NotFound();
        }

        if (playerId.HasValue)
        {
            return await BuildPlayerDialogJsonAsync(
                game,
                playerId.Value,
                cancellationToken);
        }

        return await LoadHistoryPageAsync(
            game,
            mode,
            selectedPlayerId,
            cancellationToken);
    }

    public async Task<IActionResult> OnPostConfirmIdentityAsync(
        Guid id,
        Guid playerId,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.FindOwned(
            new GameSessionId(id),
            currentHost.RequiredId);
        if (game is null)
        {
            return NotFound();
        }

        var runtimePlayer = sessionRegistry
            .GetPlayers(game)
            .FirstOrDefault(item => item.Id == new GamePlayerId(playerId));
        if (runtimePlayer is null)
        {
            return NotFound();
        }

        // The account identity is always resolved from authoritative runtime state.
        // It is intentionally never accepted from the browser.
        var accountId = PlayerAchievementRuntimeState.GetPlayerAccountId(game, runtimePlayer.Id);
        if (string.IsNullOrWhiteSpace(accountId))
        {
            return BadRequest(new
            {
                message = historyLocalizer["History_ConfirmError"].Value
            });
        }

        var confirmationService = new PlayerAchievementHistoryConfirmationService(db);
        var status = await confirmationService.ConfirmAsync(
            accountId,
            game.HostId,
            runtimePlayer.Name,
            cancellationToken);
        if (status == PlayerAchievementHistoryConfirmationStatus.ConflictingAccountClaim)
        {
            return new JsonResult(new
            {
                message = historyLocalizer["History_ConfirmConflict"].Value
            })
            {
                StatusCode = StatusCodes.Status409Conflict
            };
        }
        if (status == PlayerAchievementHistoryConfirmationStatus.InvalidIdentity)
        {
            return BadRequest(new
            {
                message = historyLocalizer["History_ConfirmError"].Value
            });
        }

        var remaining = await LoadPendingConfirmationsAsync(game, cancellationToken);
        var nextMode = remaining.Count > 0 ? PendingMode : PlayersMode;
        var nextUrl = Url.Page(
            "/Admin/Games/PlayerAchievements",
            new
            {
                id = game.Session.Id.Value,
                mode = nextMode,
                selectedPlayerId = playerId
            });
        return new JsonResult(new
        {
            success = true,
            confirmed = status == PlayerAchievementHistoryConfirmationStatus.Confirmed,
            nextUrl
        });
    }

    public static string ToUtcIso(DateTime value) =>
        EnsureUtc(value).ToString("O", CultureInfo.InvariantCulture);

    public static string FormatUtcFallback(DateTime value) =>
        $"{EnsureUtc(value).ToString("g", CultureInfo.CurrentCulture)} UTC";

    private async Task<IActionResult> BuildPlayerDialogJsonAsync(
        GameSessionRegistration game,
        Guid playerId,
        CancellationToken cancellationToken)
    {
        var player = sessionRegistry
            .GetPlayers(game)
            .FirstOrDefault(item => item.Id == new GamePlayerId(playerId));
        if (player is null)
        {
            return NotFound();
        }

        var accountId = PlayerAchievementRuntimeState.GetPlayerAccountId(game, player.Id);
        var isContributor = player.HasTemporaryContributorPrivileges ||
            ContributorRecognition.IsContributor(
                footerOptions.Value,
                player.OriginalName);

        if (!isContributor && !string.IsNullOrWhiteSpace(accountId))
        {
            var accountDisplayName = await db.Hosts
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(host => host.Id == accountId)
                .Select(host => host.DisplayName)
                .SingleOrDefaultAsync(cancellationToken);
            isContributor = ContributorRecognition.IsContributor(
                footerOptions.Value,
                accountDisplayName);
        }

        var achievements = await new PlayerAchievementService(db).LoadForPlayerAsync(
            game.HostId,
            player.Name,
            game.PublicCode,
            accountId,
            isContributor,
            cancellationToken);

        var unlockedCount = achievements.Count(item => item.IsUnlocked);
        return new JsonResult(new
        {
            playerName = player.Name,
            title = localizer["Achievements_Title"].Value,
            subtitle = localizer["Achievements_Subtitle"].Value,
            closeLabel = localizer["Achievements_Close"].Value,
            unlockedCountText = localizer[
                "Achievements_UnlockedCount",
                unlockedCount,
                achievements.Count].Value,
            secretTitle = localizer["Achievements_SecretTitle"].Value,
            secretDescription = localizer["Achievements_SecretDescription"].Value,
            achievements = achievements.Select(item => new
            {
                item.Code,
                item.Icon,
                item.IsUnlocked,
                item.IsSecret,
                item.IsNewInCurrentGame,
                item.Progress,
                item.Target,
                name = localizer[$"{item.Code}_Name"].Value,
                description = localizer[$"{item.Code}_Description"].Value
            })
        });
    }

    private async Task<IActionResult> LoadHistoryPageAsync(
        GameSessionRegistration game,
        string? mode,
        Guid? selectedPlayerId,
        CancellationToken cancellationToken)
    {
        Game = game;

        Players = sessionRegistry
            .GetPlayers(game)
            .OrderBy(player => player.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(player => new PlayerAchievementHistoryPlayer(
                player.Id.Value,
                player.Name,
                PlayerAchievementRuntimeState.GetPlayerAccountId(game, player.Id)))
            .ToArray();

        var selectedPlayer = Players.FirstOrDefault(player =>
                selectedPlayerId.HasValue && player.Id == selectedPlayerId.Value)
            ?? Players.FirstOrDefault();
        SelectedPlayerId = selectedPlayer?.Id;

        PendingConfirmations = await LoadPendingConfirmationsAsync(game, cancellationToken);
        Mode = string.Equals(mode, PendingMode, StringComparison.OrdinalIgnoreCase) && HasPendingConfirmations
            ? PendingMode
            : string.Equals(mode, PlayersMode, StringComparison.OrdinalIgnoreCase)
                ? PlayersMode
                : GameMode;

        if (Mode == PendingMode)
        {
            Entries = [];
            return Page();
        }

        var achievementService = new PlayerAchievementService(db);
        if (Mode == PlayersMode)
        {
            if (selectedPlayer is null)
            {
                Entries = [];
                return Page();
            }

            var persisted = await achievementService.LoadPersistedUnlocksAsync(
                selectedPlayer.AccountId,
                game.HostId,
                selectedPlayer.Name,
                cancellationToken: cancellationToken);
            Entries = persisted
                .Select(item => CreateHistoryEntry(selectedPlayer, item))
                .ToArray();
            return Page();
        }

        var storedGameSessionId = await db.GameSessions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(session =>
                session.PublicCode == game.PublicCode &&
                session.HostId == currentHost.RequiredId)
            .Select(session => (int?)session.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (!storedGameSessionId.HasValue)
        {
            Entries = [];
            return Page();
        }

        var entries = new List<PlayerAchievementHistoryEntry>();
        foreach (var player in Players)
        {
            var persisted = await achievementService.LoadPersistedUnlocksAsync(
                player.AccountId,
                game.HostId,
                player.Name,
                storedGameSessionId.Value,
                cancellationToken);
            entries.AddRange(persisted.Select(item => CreateHistoryEntry(player, item)));
        }

        Entries = entries
            .OrderByDescending(item => item.UnlockedAtUtc)
            .ThenByDescending(item => item.RecordId)
            .ThenBy(item => item.PlayerName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        return Page();
    }

    private async Task<IReadOnlyList<PlayerAchievementPendingConfirmation>> LoadPendingConfirmationsAsync(
        GameSessionRegistration game,
        CancellationToken cancellationToken)
    {
        var confirmationService = new PlayerAchievementHistoryConfirmationService(db);
        var pending = new List<PlayerAchievementPendingConfirmation>();
        foreach (var player in sessionRegistry
                     .GetPlayers(game)
                     .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var accountId = PlayerAchievementRuntimeState.GetPlayerAccountId(game, player.Id);
            if (string.IsNullOrWhiteSpace(accountId))
            {
                continue;
            }

            var preview = await confirmationService.LoadPendingAsync(
                accountId,
                game.HostId,
                player.Name,
                cancellationToken);
            if (preview is null)
            {
                continue;
            }

            var historyPlayer = new PlayerAchievementHistoryPlayer(
                player.Id.Value,
                player.Name,
                accountId);
            pending.Add(new PlayerAchievementPendingConfirmation(
                player.Id.Value,
                player.Name,
                preview.PendingGameCount,
                preview.PendingAchievements
                    .Select(item => CreateHistoryEntry(historyPlayer, item))
                    .ToArray()));
        }

        return pending;
    }

    private PlayerAchievementHistoryEntry CreateHistoryEntry(
        PlayerAchievementHistoryPlayer player,
        PlayerAchievement achievement)
    {
        var definition = PlayerAchievementService.Catalog.FirstOrDefault(item =>
            string.Equals(item.Code, achievement.AchievementCode, StringComparison.Ordinal));
        var name = localizer[$"{achievement.AchievementCode}_Name"];
        var description = localizer[$"{achievement.AchievementCode}_Description"];

        return new PlayerAchievementHistoryEntry(
            achievement.Id,
            player.Id,
            player.Name,
            achievement.AchievementCode,
            name.ResourceNotFound ? achievement.AchievementCode : name.Value,
            description.ResourceNotFound
                ? historyLocalizer["History_UnknownDescription"].Value
                : description.Value,
            definition is null
                ? null
                : $"/images/achievements/{Uri.EscapeDataString(achievement.AchievementCode)}.png",
            achievement.UnlockedAtUtc,
            achievement.SourceGameSessionId);
    }

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}

public sealed record PlayerAchievementHistoryPlayer(
    Guid Id,
    string Name,
    string? AccountId);

public sealed record PlayerAchievementPendingConfirmation(
    Guid PlayerId,
    string PlayerName,
    int PendingGameCount,
    IReadOnlyList<PlayerAchievementHistoryEntry> Achievements);

public sealed record PlayerAchievementHistoryEntry(
    int RecordId,
    Guid PlayerId,
    string PlayerName,
    string AchievementCode,
    string Name,
    string Description,
    string? ArtworkUrl,
    DateTime UnlockedAtUtc,
    int? SourceGameSessionId);
