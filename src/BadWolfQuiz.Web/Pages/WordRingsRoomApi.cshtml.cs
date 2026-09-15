using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Globalization;

namespace BadWolfQuiz.Web.Pages;

public sealed class WordRingsRoomApiModel(IWebHostEnvironment environment) : PageModel
{
    private WordRingsRoomStore Store => WordRingsRoomStore.Get(environment);
    private WordRingsRoomHostCoordinator HostCoordinator => WordRingsRoomHostCoordinator.Get(environment);
    private WordRingsActionCardCoordinator ActionCards => WordRingsActionCardCoordinator.Get(environment);

    public async Task<IActionResult> OnPostCreateRoom(
        string? playerName,
        int targetScore,
        bool partialScoreEnabled,
        bool hostChoosesRules,
        string? previousRoomCode,
        string? previousPlayerToken,
        int turnDurationSeconds,
        bool actionCardsEnabled = false,
        int actionCardCorrectWords = 3,
        int actionCardMaxHand = 3,
        CancellationToken cancellationToken = default)
    {
        var configuration = HttpContext.RequestServices.GetService(typeof(IConfiguration)) as IConfiguration;
        if (turnDurationSeconds == 15 && configuration?.GetValue<bool>("DebugMode") != true)
        {
            return new JsonResult(new
            {
                success = false,
                error = WordRingsRoomError.InvalidTurnDuration.ToString()
            });
        }

        byte[]? brandLogoData = null;
        string? brandLogoContentType = null;
        var currentHost = HttpContext.RequestServices.GetService(typeof(CurrentHost)) as CurrentHost;
        var settingsStore = HttpContext.RequestServices.GetService(typeof(GameSettingsStore)) as GameSettingsStore;
        if (currentHost?.Id is { Length: > 0 } hostId && settingsStore is not null)
        {
            var settings = await settingsStore.LoadAsync(hostId, cancellationToken);
            if (settings.BrandLogoData is not null &&
                !string.IsNullOrWhiteSpace(settings.BrandLogoContentType))
            {
                brandLogoData = settings.BrandLogoData;
                brandLogoContentType = settings.BrandLogoContentType;
            }
        }

        return Execute(() =>
        {
            var connection = HostCoordinator.CreateRoom(
                playerName,
                targetScore,
                partialScoreEnabled,
                hostChoosesRules,
                previousRoomCode,
                previousPlayerToken,
                brandLogoData,
                brandLogoContentType,
                turnDurationSeconds,
                currentHost?.Id);
            ActionCards.RegisterRoom(
                connection,
                actionCardsEnabled,
                actionCardCorrectWords,
                actionCardMaxHand,
                previousRoomCode);
            return new
            {
                success = true,
                connection
            };
        });
    }

    public IActionResult OnGetRoomBrandLogo(string? roomCode)
    {
        var logo = HostCoordinator.GetBrandLogo(roomCode);
        if (logo is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "no-store";
        return File(logo.Data, logo.ContentType);
    }

    public IActionResult OnPostJoinRoom(string? roomCode, string? playerName)
    {
        var currentHost = HttpContext.RequestServices.GetService(typeof(CurrentHost)) as CurrentHost;
        return Execute(() => new
        {
            success = true,
            connection = HostCoordinator.JoinRoom(roomCode, playerName, currentHost?.Id)
        });
    }

    public Task<IActionResult> OnPostRoomState(
        string? roomCode,
        string? playerToken,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () =>
        {
            var state = HostCoordinator.GetRoomState(roomCode, playerToken);
            await RecordCompletedRoundAchievementsAsync(state, cancellationToken);
            return new { success = true, state };
        });

    public IActionResult OnPostActionCardState(string? roomCode, string? playerToken) =>
        Execute(() => new
        {
            success = true,
            actionCards = ActionCards.GetState(roomCode, playerToken)
        });

    public IActionResult OnPostUseActionCard(
        string? roomCode,
        string? playerToken,
        int cardId,
        Guid? targetPlayerId,
        string? word) =>
        Execute(() => new
        {
            success = true,
            result = ActionCards.UseCard(roomCode, playerToken, cardId, targetPlayerId, word)
        });

    public IActionResult OnPostPrepareLeaveRoom(string? roomCode, string? playerToken) =>
        Execute(() =>
        {
            Store.PrepareToLeave(roomCode, playerToken);
            return new { success = true };
        });

    public IActionResult OnPostLeaveRoom(string? roomCode, string? playerToken) =>
        Execute(() =>
        {
            HostCoordinator.LeaveRoom(roomCode, playerToken);
            return new { success = true };
        });

    public IActionResult OnPostRoomHostState(string? roomCode, string? playerToken) =>
        Execute(() => new { success = true, state = HostCoordinator.GetHostState(roomCode, playerToken) });

    public IActionResult OnPostStartRoom(string? roomCode, string? playerToken) =>
        Execute(() =>
        {
            var state = HostCoordinator.StartGame(roomCode, playerToken);
            ActionCards.BeginRound(roomCode, playerToken);
            return new { success = true, state };
        });

    public IActionResult OnPostSelectRoomRule(string? roomCode, string? playerToken, string? ring, Guid ruleId) =>
        Execute(() => new { success = true, state = HostCoordinator.SelectRule(roomCode, playerToken, ring, ruleId) });

    public IActionResult OnPostRefreshRoomRules(string? roomCode, string? playerToken, string? ring) =>
        Execute(() => new { success = true, state = HostCoordinator.RefreshRules(roomCode, playerToken, ring) });

    public IActionResult OnPostSetRoomTurn(string? roomCode, string? playerToken, Guid playerId) =>
        Execute(() => new { success = true, state = HostCoordinator.SetCurrentPlayer(roomCode, playerToken, playerId) });

    public IActionResult OnPostKickRoomPlayer(string? roomCode, string? playerToken, Guid playerId) =>
        Execute(() => new { success = true, state = HostCoordinator.KickPlayer(roomCode, playerToken, playerId) });

    public IActionResult OnPostSetRoomJoinLock(string? roomCode, string? playerToken, bool locked) =>
        Execute(() => new { success = true, state = HostCoordinator.SetJoinLocked(roomCode, playerToken, locked) });

    public Task<IActionResult> OnPostSubmitRoomWord(
        string? roomCode,
        string? playerToken,
        string? word,
        string? membership,
        string? x,
        string? y,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () =>
        {
            ActionCards.EnsureWordUsable(roomCode, playerToken, word);
            var protectedFailure = ActionCards.TryInterceptFailedPlacement(
                roomCode,
                playerToken,
                word,
                membership);
            if (protectedFailure is not null)
            {
                return new
                {
                    success = true,
                    result = protectedFailure,
                    actionCardImmune = true
                };
            }

            var result = HostCoordinator.SubmitPlacement(
                roomCode,
                playerToken,
                word,
                membership,
                ParseCoordinate(x),
                ParseCoordinate(y));

            if (result.IsPending)
            {
                ActionCards.RecordHostedSubmission(result.State.RoomCode, result.State.PlayerId, result.Word);
            }
            else
            {
                ActionCards.RecordPlacementAttempt(
                    result.State.RoomCode,
                    result.State.PlayerId,
                    result.Word,
                    result.IsCorrect);

                var identity = HostCoordinator.GetAchievementParticipant(result.State.RoomCode, result.State.PlayerId);
                var placement = result.State.Placements.LastOrDefault(item =>
                    item.PlayerId == result.State.PlayerId &&
                    string.Equals(item.Word, result.Word, StringComparison.OrdinalIgnoreCase));
                await RecordPlacementAchievementAsync(
                    result.State.RoomCode,
                    identity,
                    placement?.Id.ToString(CultureInfo.InvariantCulture) ?? $"{result.State.Version}:{result.Word}",
                    result.IsCorrect,
                    result.IsCorrect ? result.ExpectedMembership : result.ActualMembership,
                    cancellationToken);
            }
            await RecordCompletedRoundAchievementsAsync(result.State, cancellationToken);
            return new { success = true, result };
        });

    public IActionResult OnPostMoveRoomPlacement(
        string? roomCode,
        string? playerToken,
        long placementId,
        string? membership,
        string? x,
        string? y) =>
        Execute(() => new
        {
            success = true,
            state = HostCoordinator.MovePlacement(
                roomCode,
                playerToken,
                placementId,
                membership,
                ParseCoordinate(x),
                ParseCoordinate(y))
        });

    public IActionResult OnPostPlaceRoomSeed(
        string? roomCode,
        string? playerToken,
        string? word,
        string? membership,
        string? x,
        string? y) =>
        Execute(() => new
        {
            success = true,
            state = HostCoordinator.PlaceSeedWord(
                roomCode,
                playerToken,
                word,
                membership,
                ParseCoordinate(x),
                ParseCoordinate(y))
        });

    public IActionResult OnPostConfirmRoomSeeds(string? roomCode, string? playerToken) =>
        Execute(() => new
        {
            success = true,
            state = HostCoordinator.ConfirmSeedSetup(roomCode, playerToken)
        });

    public Task<IActionResult> OnPostResolveRoomPlacement(
        string? roomCode,
        string? playerToken,
        long placementId,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () =>
        {
            var pending = HostCoordinator.GetHostState(roomCode, playerToken).PendingPlacement;
            var identity = pending is null
                ? null
                : HostCoordinator.GetAchievementParticipant(roomCode, pending.PlayerId);

            if (ActionCards.TryCancelHostedFailureWithImmunity(roomCode, playerToken, placementId, pending))
            {
                return new
                {
                    success = true,
                    state = HostCoordinator.GetHostState(roomCode, playerToken),
                    actionCardImmune = true
                };
            }

            var result = HostCoordinator.ResolvePlacementWithResult(roomCode, playerToken, placementId);
            if (pending is not null)
            {
                ActionCards.RecordPlacementAttempt(
                    result.State.RoomCode,
                    pending.PlayerId,
                    pending.Word,
                    result.IsCorrect);
            }
            await RecordPlacementAchievementAsync(
                result.State.RoomCode,
                identity,
                placementId.ToString(CultureInfo.InvariantCulture),
                result.IsCorrect,
                result.ExpectedMembership,
                cancellationToken);
            await RecordCompletedRoundAchievementsAsync(result.State, cancellationToken);
            return new
            {
                success = true,
                state = HostCoordinator.GetHostState(roomCode, playerToken)
            };
        });

    public Task<IActionResult> OnPostRecordSoloAchievement(
        string? sessionId,
        string? eventId,
        string? actualMembership,
        string? expectedMembership,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () =>
        {
            var currentHost = HttpContext.RequestServices.GetService(typeof(CurrentHost)) as CurrentHost;
            var db = HttpContext.RequestServices.GetService(typeof(QuizDbContext)) as QuizDbContext;
            if (db is null || string.IsNullOrWhiteSpace(currentHost?.Id))
            {
                return new { success = true };
            }

            var service = new WordRingsAchievementService(db);
            await service.UnlockSoloAiAsync(currentHost.Id, cancellationToken);
            var actual = CanonicalMembership(actualMembership);
            var expected = CanonicalMembership(expectedMembership);
            await service.RecordPlacementAsync(
                currentHost.Id,
                null,
                User.Identity?.Name ?? "Word Rings",
                $"solo:{sessionId}",
                eventId ?? string.Empty,
                string.Equals(actual, expected, StringComparison.Ordinal),
                expected,
                cancellationToken);
            return new { success = true };
        });

    private async Task RecordPlacementAchievementAsync(
        string roomCode,
        WordRingsAchievementParticipant? identity,
        string eventId,
        bool fullyCorrect,
        string membership,
        CancellationToken cancellationToken)
    {
        if (identity is null || identity.RoundNumber <= 0)
        {
            return;
        }
        var db = HttpContext.RequestServices.GetService(typeof(QuizDbContext)) as QuizDbContext;
        if (db is null)
        {
            return;
        }
        await new WordRingsAchievementService(db).RecordPlacementAsync(
            identity.AccountId,
            identity.HostId,
            identity.PlayerName,
            $"room:{roomCode}:round:{identity.RoundNumber}",
            eventId,
            fullyCorrect,
            membership,
            cancellationToken);
    }

    private async Task RecordCompletedRoundAchievementsAsync(
        WordRingsRoomSnapshot state,
        CancellationToken cancellationToken)
    {
        var completed = HostCoordinator.TryClaimCompletedRoundAchievement(state);
        if (completed is null)
        {
            return;
        }
        var db = HttpContext.RequestServices.GetService(typeof(QuizDbContext)) as QuizDbContext;
        if (db is null)
        {
            return;
        }

        if (completed.DedicatedHostMode)
        {
            await new WordRingsAchievementService(db).RecordHostedGameAsync(
                completed.Host.AccountId,
                completed.Host.HostId,
                completed.Host.PlayerName,
                $"room:{completed.RoomCode}:round:{completed.RoundNumber}",
                cancellationToken);
            return;
        }

        if (completed.PlayingParticipantCount >= 2 &&
            completed.WinnerPlayerId == completed.Host.PlayerId)
        {
            await new PlayerAchievementService(db).UnlockPlayerAsync(
                completed.Host.AccountId,
                completed.Host.HostId,
                completed.Host.PlayerName,
                "WordRingsPlayingHostWin",
                cancellationToken: cancellationToken);
        }
    }

    private static string CanonicalMembership(string? membership)
    {
        var value = (membership ?? string.Empty).Trim().ToUpperInvariant();
        return new string(value
            .Where(character => character is 'A' or 'B' or 'C')
            .Distinct()
            .OrderBy(character => character)
            .ToArray());
    }

    private static double ParseCoordinate(string? value)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var coordinate) ||
            !double.IsFinite(coordinate))
        {
            throw new WordRingsRoomException(WordRingsRoomError.InvalidPlacement);
        }
        return coordinate;
    }

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