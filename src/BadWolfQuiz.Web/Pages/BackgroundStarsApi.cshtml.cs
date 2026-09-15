using System.Collections.Concurrent;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BadWolfQuiz.Web.Pages;

[IgnoreAntiforgeryToken]
public sealed class BackgroundStarsApiModel(
    GameSessionRegistry sessionRegistry,
    GameSettingsStore settingsStore,
    MinigameRoomStore minigameRooms,
    CurrentHost currentHost,
    IWebHostEnvironment environment) : PageModel
{
    private const string GuessKind = "guess";
    private const string WordRingsKind = "word-rings";
    private const string QuizKind = "quiz";
    private static readonly TimeSpan RegistrationLifetime = TimeSpan.FromHours(3);
    private static readonly ConcurrentDictionary<string, MinigameAppearanceRegistration> Registrations =
        new(StringComparer.OrdinalIgnoreCase);

    public async Task<IActionResult> OnGetAsync(
        string? kind,
        string? code,
        CancellationToken cancellationToken)
    {
        var normalizedKind = NormalizeKind(kind);
        var normalizedCode = NormalizeCode(code);
        if (normalizedKind is null || normalizedCode is null)
        {
            return BadRequest();
        }

        Response.Headers.CacheControl = "no-store";

        if (normalizedKind == QuizKind)
        {
            var game = sessionRegistry.Find(normalizedCode);
            if (game is null)
            {
                return new JsonResult(new
                {
                    success = true,
                    known = false,
                    animatedStarsEnabled = true
                });
            }

            var settings = !string.IsNullOrWhiteSpace(game.HostId)
                ? await settingsStore.LoadAsync(game.HostId, cancellationToken)
                : game.Session.Settings;
            return new JsonResult(new
            {
                success = true,
                known = true,
                animatedStarsEnabled = settings.AnimatedStarsEnabled
            });
        }

        CleanupExpiredRegistrations();
        if (!Registrations.TryGetValue(
                RegistrationKey(normalizedKind, normalizedCode),
                out var registration))
        {
            return new JsonResult(new
            {
                success = true,
                known = false,
                animatedStarsEnabled = true
            });
        }

        var enabled = registration.FallbackEnabled;
        if (!string.IsNullOrWhiteSpace(registration.HostId))
        {
            var settings = await settingsStore.LoadAsync(
                registration.HostId,
                cancellationToken);
            enabled = settings.AnimatedStarsEnabled;
        }

        return new JsonResult(new
        {
            success = true,
            known = true,
            animatedStarsEnabled = enabled
        });
    }

    public async Task<IActionResult> OnPostRegisterAsync(
        string? kind,
        string? code,
        string? playerToken,
        bool animatedStarsEnabled,
        CancellationToken cancellationToken)
    {
        var normalizedKind = NormalizeKind(kind);
        var normalizedCode = NormalizeCode(code);
        var normalizedToken = string.IsNullOrWhiteSpace(playerToken)
            ? null
            : playerToken.Trim();
        if (normalizedKind is not (GuessKind or WordRingsKind) ||
            normalizedCode is null ||
            normalizedToken is null)
        {
            return BadRequest();
        }

        if (!IsRoomCreator(normalizedKind, normalizedCode, normalizedToken))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var hostId = currentHost.Id;
        var effectiveEnabled = animatedStarsEnabled;
        if (!string.IsNullOrWhiteSpace(hostId))
        {
            var settings = await settingsStore.LoadAsync(hostId, cancellationToken);
            effectiveEnabled = settings.AnimatedStarsEnabled;
        }

        CleanupExpiredRegistrations();
        Registrations[RegistrationKey(normalizedKind, normalizedCode)] = new(
            effectiveEnabled,
            hostId,
            DateTimeOffset.UtcNow);

        Response.Headers.CacheControl = "no-store";
        return new JsonResult(new
        {
            success = true,
            registered = true,
            animatedStarsEnabled = effectiveEnabled
        });
    }

    private bool IsRoomCreator(
        string kind,
        string roomCode,
        string playerToken)
    {
        try
        {
            if (kind == GuessKind)
            {
                return minigameRooms
                    .GetState(roomCode, playerToken, touchActivity: false)
                    .PlayerNumber == 1;
            }

            if (kind == WordRingsKind)
            {
                return WordRingsRoomHostCoordinator
                    .Get(environment)
                    .GetRoomState(roomCode, playerToken)
                    .IsHost;
            }
        }
        catch (MinigameRoomException)
        {
            return false;
        }
        catch (WordRingsRoomException)
        {
            return false;
        }

        return false;
    }

    private static string? NormalizeKind(string? kind)
    {
        var normalized = (kind ?? string.Empty).Trim().ToLowerInvariant();
        return normalized is GuessKind or WordRingsKind or QuizKind
            ? normalized
            : null;
    }

    private static string? NormalizeCode(string? code)
    {
        var normalized = (code ?? string.Empty).Trim().ToUpperInvariant();
        return normalized.Length == 6 && normalized.All(char.IsLetterOrDigit)
            ? normalized
            : null;
    }

    private static string RegistrationKey(string kind, string code) =>
        $"{kind}:{code}";

    private static void CleanupExpiredRegistrations()
    {
        var threshold = DateTimeOffset.UtcNow - RegistrationLifetime;
        foreach (var item in Registrations)
        {
            if (item.Value.UpdatedUtc < threshold)
            {
                Registrations.TryRemove(item.Key, out _);
            }
        }
    }

    private sealed record MinigameAppearanceRegistration(
        bool FallbackEnabled,
        string? HostId,
        DateTimeOffset UpdatedUtc);
}