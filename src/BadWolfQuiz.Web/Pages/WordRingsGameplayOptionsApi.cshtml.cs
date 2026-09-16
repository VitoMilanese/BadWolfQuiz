using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BadWolfQuiz.Web.Pages;

public sealed class WordRingsGameplayOptionsApiModel(IWebHostEnvironment environment) : PageModel
{
    private WordRingsGameplayOptionsCoordinator Gameplay => WordRingsGameplayOptionsCoordinator.Get(environment);

    public IActionResult OnPostConfigureRoom(
        string? roomCode,
        string? playerToken,
        int targetScore,
        int handSize) =>
        Execute(() => new
        {
            success = true,
            options = Gameplay.ConfigureRoom(roomCode, playerToken, targetScore, handSize)
        });

    public IActionResult OnPostOptions(string? roomCode, string? playerToken) =>
        Execute(() => new
        {
            success = true,
            options = Gameplay.GetOptions(roomCode, playerToken)
        });

    public IActionResult OnPostCurrentState(string? roomCode, string? playerToken) =>
        Execute(() => new
        {
            success = true,
            state = Gameplay.GetDecoratedRoomState(roomCode, playerToken)
        });

    public IActionResult OnPostPrepareRound(string? roomCode, string? playerToken) =>
        Execute(() => new
        {
            success = true,
            state = Gameplay.PrepareRound(roomCode, playerToken)
        });

    public IActionResult OnPostSelectFirstPlayer(string? roomCode, string? playerToken) =>
        Execute(() => new
        {
            success = true,
            state = Gameplay.SelectFirstPlayerAfterSeeds(roomCode, playerToken)
        });

    public IActionResult OnPostCaptureWordCounts(string? roomCode, string? playerToken) =>
        Execute(() => new
        {
            success = true,
            capture = Gameplay.CaptureWordCounts(roomCode, playerToken)
        });

    public IActionResult OnPostRestoreWordCounts(
        string? roomCode,
        string? playerToken,
        Guid captureId,
        bool allowCallerReturnedWord = false) =>
        Execute(() =>
        {
            Gameplay.RestoreWordCounts(roomCode, playerToken, captureId, allowCallerReturnedWord);
            return new { success = true };
        });

    public IActionResult OnPostFinalizePlayerExhaustion(
        string? roomCode,
        string? playerToken,
        Guid playerId) =>
        Execute(() => new
        {
            success = true,
            state = Gameplay.FinalizePlayerExhaustion(roomCode, playerToken, playerId)
        });

    public IActionResult OnPostFinalizePlacementExhaustion(
        string? roomCode,
        string? playerToken,
        long placementId) =>
        Execute(() => new
        {
            success = true,
            state = Gameplay.FinalizePlacementExhaustion(roomCode, playerToken, placementId)
        });

    public IActionResult OnPostNormalizeActionCard(
        string? roomCode,
        string? playerToken,
        int cardId,
        string? targetName) =>
        Execute(() => new
        {
            success = true,
            actionCards = Gameplay.NormalizeActionCardEffects(roomCode, playerToken, cardId, targetName)
        });

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
}
