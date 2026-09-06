using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace BadWolfQuiz.Web.Pages.Admin;

[Authorize(Policy = "MasterHost")]
public sealed class MinigameResourceSyncModel(
    IDbContextFactory<QuizDbContext> dbFactory,
    IStringLocalizer<MinigameEditorResource> localizer,
    ILogger<MinigameResourceSyncModel> logger) : PageModel
{
    private MinigameResourceSyncService SyncService => new(dbFactory);

    public IActionResult OnGet() => NotFound();

    public async Task<IActionResult> OnPostSynchronizeAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await SyncService.SynchronizeAsync(cancellationToken);
            if (!result.Success)
            {
                return BadRequest(new
                {
                    success = false,
                    message = GetSyncErrorMessage(result)
                });
            }

            return new JsonResult(new
            {
                success = true,
                addedCount = result.AddedCount,
                updatedCount = result.UpdatedCount,
                missingGames = result.MissingGames,
                message = localizer[
                    "ResourceSyncCompleted",
                    result.AddedCount,
                    result.UpdatedCount].Value
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to synchronize minigame resource games.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = localizer["ResourceSyncUnexpectedError"].Value
            });
        }
    }

    public async Task<IActionResult> OnPostDeleteMissingAsync(
        int[]? gameIds,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await SyncService.DeleteMissingGamesAsync(
                gameIds ?? [],
                cancellationToken);
            if (!result.Success)
            {
                return BadRequest(new
                {
                    success = false,
                    message = GetDeleteErrorMessage(result)
                });
            }

            return new JsonResult(new
            {
                success = true,
                deletedCount = result.DeletedCount,
                message = localizer[
                    "ResourceSyncDeleted",
                    result.DeletedCount].Value
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to delete minigame games missing from resources.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = localizer["ResourceSyncUnexpectedError"].Value
            });
        }
    }

    private string GetSyncErrorMessage(MinigameResourceSyncResult result) =>
        result.Error switch
        {
            MinigameResourceSyncError.FolderNotFound =>
                localizer["ResourceSyncFolderMissing"].Value,
            MinigameResourceSyncError.InvalidGameName =>
                localizer["ResourceSyncInvalidGameName", result.ErrorItem ?? string.Empty].Value,
            MinigameResourceSyncError.DuplicateGameName =>
                localizer["ResourceSyncDuplicateGame", result.ErrorItem ?? string.Empty].Value,
            MinigameResourceSyncError.DuplicateAnswerFile =>
                localizer["ResourceSyncDuplicateAnswerFile", result.ErrorItem ?? string.Empty].Value,
            MinigameResourceSyncError.InvalidAnswerFile when result.InvalidLineNumber > 0 =>
                localizer[
                    "ResourceSyncInvalidAnswerLine",
                    result.ErrorItem ?? string.Empty,
                    result.InvalidLineNumber].Value,
            MinigameResourceSyncError.InvalidAnswerFile =>
                localizer[
                    "ResourceSyncWrongAnswerCount",
                    result.ErrorItem ?? string.Empty,
                    result.ExpectedAnswerCount,
                    result.ActualAnswerCount].Value,
            MinigameResourceSyncError.ResourceReadFailed =>
                localizer["ResourceSyncReadFailed", result.ErrorItem ?? string.Empty].Value,
            _ => localizer["ResourceSyncUnexpectedError"].Value
        };

    private string GetDeleteErrorMessage(MinigameResourceDeleteResult result) =>
        result.Error switch
        {
            MinigameResourceSyncError.FolderNotFound =>
                localizer["ResourceSyncFolderMissing"].Value,
            MinigameResourceSyncError.InvalidGameName =>
                localizer["ResourceSyncInvalidGameName", result.ErrorItem ?? string.Empty].Value,
            MinigameResourceSyncError.DuplicateGameName =>
                localizer["ResourceSyncDuplicateGame", result.ErrorItem ?? string.Empty].Value,
            _ => localizer["ResourceSyncUnexpectedError"].Value
        };
}
