using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Globalization;

namespace BadWolfQuiz.Web.Pages;

public sealed class WordRingsActionCardsPatchApiModel(
    IWebHostEnvironment environment,
    IConfiguration configuration) : PageModel
{
    private WordRingsRoomHostCoordinator Host => WordRingsRoomHostCoordinator.Get(environment);
    private WordRingsActionCardCoordinator Cards => WordRingsActionCardCoordinator.Get(environment);
    private WordRingsActionCardPatchCoordinator Patch => WordRingsActionCardPatchCoordinator.Get(environment);

    public IActionResult OnPostState(string? roomCode, string? playerToken) =>
        Execute(() =>
        {
            var room = Host.GetRoomState(roomCode, playerToken);
            try
            {
                _ = Cards.GetState(room.RoomCode, playerToken);
            }
            catch (InvalidOperationException exception) when (IsUnconfiguredActionCards(exception))
            {
                // Rooms created before action-card metadata existed simply have no patch state.
            }
            var patch = Patch.GetState(room.RoomCode, room.PlayerId);
            var players = room.Players
                .Where(player => !(room.DedicatedHostMode && player.IsHost))
                .Select(player => new { player.Id, player.Name })
                .ToArray();
            return new
            {
                success = true,
                patch.Notice,
                patch.PendingImmunityDecision,
                patch.AnyPendingImmunityDecision,
                debugMode = configuration.GetValue<bool>("DebugMode"),
                room.IsHost,
                players
            };
        });

    public IActionResult OnPostUseActionCard(
        string? roomCode,
        string? playerToken,
        int cardId,
        Guid? targetPlayerId,
        string? word) =>
        Execute(() =>
        {
            var before = Host.GetRoomState(roomCode, playerToken);
            var result = Cards.UseCard(roomCode, playerToken, cardId, targetPlayerId, word);
            Patch.RecordCardUse(before, cardId, result);
            _ = Patch.EnsureTargetReachable(roomCode, playerToken);
            return new { success = true, result };
        });

    public IActionResult OnPostRecordSubmissionLifecycle(
        string? roomCode,
        string? playerToken,
        long? placementId) =>
        Execute(() =>
        {
            var state = Host.GetRoomState(roomCode, playerToken);
            var placement = placementId is long id
                ? state.Placements.FirstOrDefault(item => item.Id == id && item.PlayerId == state.PlayerId)
                : null;
            Patch.ApplySubmissionLifecycle(
                state.RoomCode,
                state.PlayerId,
                placement is { IsPending: false, IsCorrect: true });
            return new { success = true };
        });

    public IActionResult OnPostTryQueueHostedImmunity(
        string? roomCode,
        string? playerToken,
        long placementId) =>
        Execute(() =>
        {
            var result = Patch.TryQueueHostedImmunity(roomCode, playerToken, placementId);
            return new
            {
                success = true,
                queued = result.Queued,
                state = result.State
            };
        });

    public IActionResult OnPostRecordHostedResolutionLifecycle(
        string? roomCode,
        string? playerToken,
        long placementId) =>
        Execute(() =>
        {
            Patch.ApplyHostedResolutionLifecycle(roomCode, playerToken, placementId);
            return new { success = true };
        });

    public IActionResult OnPostEnsureTargetWords(string? roomCode, string? playerToken) =>
        Execute(() => new
        {
            success = true,
            state = Patch.EnsureTargetReachable(roomCode, playerToken)
        });

    public async Task<IActionResult> OnPostResolveImmunityDecision(
        string? roomCode,
        string? playerToken,
        long decisionId,
        bool returnWord,
        CancellationToken cancellationToken = default) =>
        await ExecuteAsync(async () =>
        {
            var resolution = Patch.ResolveImmunityDecision(roomCode, playerToken, decisionId, returnWord);
            if (resolution.PlacementResult is { } result)
            {
                await RecordPlacementAchievementAsync(
                    result.State.RoomCode,
                    resolution.PlayerId,
                    resolution.PlacementId,
                    result.IsCorrect,
                    result.ExpectedMembership,
                    cancellationToken);
            }
            var state = Host.GetRoomState(roomCode, playerToken);
            return new
            {
                success = true,
                returnedWord = resolution.ReturnedWord,
                state
            };
        });

    public IActionResult OnPostGrantDebugActionCard(
        string? roomCode,
        string? playerToken,
        Guid playerId,
        int cardId) =>
        Execute(() =>
        {
            if (!configuration.GetValue<bool>("DebugMode"))
            {
                throw new InvalidOperationException("DebugModeDisabled");
            }
            Patch.GrantDebugCard(roomCode, playerToken, playerId, cardId);
            return new { success = true };
        });

    private async Task RecordPlacementAchievementAsync(
        string roomCode,
        Guid playerId,
        long placementId,
        bool fullyCorrect,
        string membership,
        CancellationToken cancellationToken)
    {
        var identity = Host.GetAchievementParticipant(roomCode, playerId);
        if (identity is null || identity.RoundNumber <= 0) return;
        var db = HttpContext.RequestServices.GetService(typeof(QuizDbContext)) as QuizDbContext;
        if (db is null) return;

        await new WordRingsAchievementService(db).RecordPlacementAsync(
            identity.AccountId,
            identity.HostId,
            identity.PlayerName,
            $"room:{roomCode}:round:{identity.RoundNumber}",
            placementId.ToString(CultureInfo.InvariantCulture),
            fullyCorrect,
            membership,
            cancellationToken);
    }

    private static bool IsUnconfiguredActionCards(InvalidOperationException exception) =>
        string.Equals(exception.Message, "ActionCardRoomNotConfigured", StringComparison.Ordinal);

    private IActionResult Execute(Func<object> operation)
    {
        try
        {
            return new JsonResult(operation());
        }
        catch (WordRingsRoomException exception)
        {
            return new JsonResult(new { success = false, error = exception.Error.ToString() });
        }
        catch (InvalidOperationException exception)
        {
            return new JsonResult(new { success = false, error = exception.Message });
        }
    }

    private async Task<IActionResult> ExecuteAsync(Func<Task<object>> operation)
    {
        try
        {
            return new JsonResult(await operation());
        }
        catch (WordRingsRoomException exception)
        {
            return new JsonResult(new { success = false, error = exception.Error.ToString() });
        }
        catch (InvalidOperationException exception)
        {
            return new JsonResult(new { success = false, error = exception.Message });
        }
    }
}
