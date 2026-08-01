using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Hubs;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Localization;
using QRCoder;

namespace BadWolfQuiz.Web.Pages.Admin.Games;

public sealed class LobbyModel(
    GameSessionRegistry sessionRegistry,
    GameHistoryStore gameHistoryStore,
    CurrentHost currentHost,
    JoinUrlBuilder joinUrlBuilder,
    AvatarCatalog avatarCatalog,
    IHubContext<GameHub> gameHub,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    public GameSessionRegistration Game { get; private set; } = null!;

    public IReadOnlyList<GamePlayer> Players { get; private set; } = [];

    public IReadOnlySet<GamePlayerId> ActivePlayerIds { get; private set; } =
        new HashSet<GamePlayerId>();

    public IReadOnlyList<GameBoardCategory> BoardCategories { get; private set; } = [];

    public RuntimeQuestion? CurrentQuestion { get; private set; }

    public RuntimeQuestion? PreviewQuestion { get; private set; }

    public bool IsPreviewingAnswer { get; private set; }

    public WagerLimits? QuestionWagerLimits { get; private set; }

    [BindProperty]
    public GameSettingsInput SettingsInput { get; set; } = new();

    [BindProperty]
    public IFormFile? HostImage { get; set; }

    public bool IsRoundSummaryVisible { get; private set; }

    public IReadOnlyList<RoundLeaderboardEntry> RoundLeaders { get; private set; } = [];

    public IReadOnlyList<GameResultStanding> FinalStandings { get; private set; } = [];

    public IActionResult OnGet(
        Guid id,
        int? previewQuestionId,
        bool previewAnswer = false)
    {
        return LoadPage(id, previewQuestionId, previewAnswer);
    }

    public IActionResult OnGetContentBlock(
        Guid id,
        int sourceQuestionId,
        int sourceContentBlockId,
        bool answer)
    {
        var game = sessionRegistry.FindOwned(new GameSessionId(id), currentHost.RequiredId);

        if (game is null)
        {
            return NotFound();
        }

        var question = game.Session.Board.Questions.SingleOrDefault(
            item => item.SourceQuestionId == sourceQuestionId);
        var blocks = answer
            ? question?.AnswerBlocks
            : question?.QuestionBlocks;
        var block = blocks?.SingleOrDefault(
            item => item.SourceContentBlockId == sourceContentBlockId);

        if (block?.FileData is null ||
            string.IsNullOrWhiteSpace(block.FileContentType))
        {
            return NotFound();
        }

        return string.IsNullOrWhiteSpace(block.FileName)
            ? File(block.FileData, block.FileContentType)
            : File(block.FileData, block.FileContentType, block.FileName);
    }

    public IActionResult OnGetFinalContentBlock(
        Guid id,
        int sourceContentBlockId,
        bool answer)
    {
        var game = sessionRegistry.FindOwned(new GameSessionId(id), currentHost.RequiredId);
        var blocks = answer
            ? game?.Session.FinalQuestion?.Definition.AnswerBlocks
            : game?.Session.FinalQuestion?.Definition.QuestionBlocks;
        var block = blocks?.SingleOrDefault(
            item => item.SourceContentBlockId == sourceContentBlockId);

        if (block?.FileData is null ||
            string.IsNullOrWhiteSpace(block.FileContentType))
        {
            return NotFound();
        }

        return string.IsNullOrWhiteSpace(block.FileName)
            ? File(block.FileData, block.FileContentType)
            : File(block.FileData, block.FileContentType, block.FileName);
    }

    public IActionResult OnGetJoinQrCode(Guid id)
    {
        var game = sessionRegistry.FindOwned(
            new GameSessionId(id),
            currentHost.RequiredId);
        if (game is null)
        {
            return NotFound();
        }

        var joinUrl = joinUrlBuilder.Build(Request, game.PublicCode);
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(
            joinUrl,
            QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(data);
        return File(qrCode.GetGraphic(16), "image/png");
    }

    public IActionResult OnGetHostCardImage(Guid id)
    {
        var game = sessionRegistry.FindOwned(new GameSessionId(id), currentHost.RequiredId);
        var settings = game?.Session.Settings;
        Response.Headers.CacheControl = "no-store";
        return settings?.HostImageData is not null &&
               !string.IsNullOrWhiteSpace(settings.HostImageContentType)
            ? File(settings.HostImageData, settings.HostImageContentType)
            : NotFound();
    }

    public static string? GetYouTubeEmbedUrl(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return value;
        }

        if (uri.AbsolutePath.StartsWith("/embed/", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        string? videoId = null;

        if (uri.Host.EndsWith("youtu.be", StringComparison.OrdinalIgnoreCase))
        {
            videoId = uri.AbsolutePath.Trim('/');
        }
        else if (uri.Host.Contains("youtube.com", StringComparison.OrdinalIgnoreCase))
        {
            videoId = uri.Query
                .TrimStart('?')
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Split('=', 2))
                .FirstOrDefault(item =>
                    item.Length == 2 &&
                    string.Equals(item[0], "v", StringComparison.OrdinalIgnoreCase))?
                .ElementAtOrDefault(1);
        }

        return string.IsNullOrWhiteSpace(videoId)
            ? value
            : $"https://www.youtube.com/embed/{Uri.EscapeDataString(videoId)}";
    }

    public async Task<IActionResult> OnPostUpdateSettingsAsync(
        Guid id, CancellationToken cancellationToken)
    {
        var game = sessionRegistry.FindOwned(new GameSessionId(id), currentHost.RequiredId);

        if (game is null)
        {
            return NotFound();
        }

        var settings = await BuildSettingsAsync(game.Session.Settings, cancellationToken);
        if (settings is null)
        {
            return RedirectToPage(new { id });
        }

        try
        {
            sessionRegistry.UpdateSettings(
                game.PublicCode,
                settings);
            TempData["SuccessMessage"] =
                localizer["GameSettings_GameSaved"].Value;
        }
        catch (GameRuleViolationException)
        {
            TempData["ErrorMessage"] =
                localizer["GameSettings_GameLocked"].Value;
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostStartAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.FindOwned(new GameSessionId(id), currentHost.RequiredId);

        if (game is null)
        {
            return NotFound();
        }

        var settings = await BuildSettingsAsync(game.Session.Settings, cancellationToken);
        if (settings is null)
        {
            return RedirectToPage(new { id });
        }

        try
        {
            sessionRegistry.UpdateSettings(
                game.PublicCode,
                settings);
            sessionRegistry.StartGame(game.PublicCode);
        }
        catch (GameRuleViolationException)
        {
            TempData["ErrorMessage"] = localizer["GameLobby_StartRequiresPlayer"].Value;
            return RedirectToPage(new { id });
        }

        await gameHub.Clients
            .Group(GameHub.GroupName(game.PublicCode))
            .SendAsync(
                "GameStatusChanged",
                GameHub.CreateStatusUpdate(game),
                cancellationToken);

        return RedirectToPage(new { id });
    }

    private async Task<GameSessionSettings?> BuildSettingsAsync(
        GameSessionSettings existing,
        CancellationToken cancellationToken)
    {
        var imageData = existing.HostImageData;
        var imageContentType = existing.HostImageContentType;

        if (HostImage is not null)
        {
            if (!HostImage.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
                HostImage.Length is <= 0 or > 5 * 1024 * 1024)
            {
                TempData["ErrorMessage"] = localizer["HostCard_InvalidImage"].Value;
                return null;
            }

            await using var stream = new MemoryStream();
            await HostImage.CopyToAsync(stream, cancellationToken);
            imageData = stream.ToArray();
            imageContentType = HostImage.ContentType;
            SettingsInput.HostVisualSource = HostVisualSource.Image;
        }

        if (!SettingsInput.IsValid)
        {
            TempData["ErrorMessage"] = localizer["GameSettings_InvalidDuration"].Value;
            return null;
        }

        if (SettingsInput.HostVisualSource == HostVisualSource.Avatar &&
            !avatarCatalog.IsValid(SettingsInput.HostAvatarId))
        {
            TempData["ErrorMessage"] = localizer["HostCard_InvalidSettings"].Value;
            return null;
        }

        return SettingsInput.ToRuntimeSettings(
            imageData,
            imageContentType,
            existing.BrandLogoData,
            existing.BrandLogoContentType);
    }

    public async Task<IActionResult> OnPostStartWagerAnswerTimerAsync(
        Guid id,
        int sourceQuestionId,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.FindOwned(new GameSessionId(id), currentHost.RequiredId);

        if (game is null)
        {
            return NotFound();
        }

        try
        {
            sessionRegistry.StartWagerAnswerTimer(
                game.PublicCode,
                sourceQuestionId);
            await BroadcastTimerAsync(game, cancellationToken);
        }
        catch (GameRuleViolationException)
        {
            TempData["ErrorMessage"] =
                localizer["GameTimer_StartRejected"].Value;
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostPauseQuestionTimerAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.FindOwned(new GameSessionId(id), currentHost.RequiredId);

        if (game is null)
        {
            return NotFound();
        }

        try
        {
            sessionRegistry.PauseQuestionTimer(game.PublicCode);
            await BroadcastTimerAsync(game, cancellationToken);
        }
        catch (GameRuleViolationException)
        {
            TempData["ErrorMessage"] =
                localizer["GameTimer_PauseRejected"].Value;
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostResumeQuestionTimerAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.FindOwned(new GameSessionId(id), currentHost.RequiredId);

        if (game is null)
        {
            return NotFound();
        }

        try
        {
            sessionRegistry.ResumeQuestionTimer(game.PublicCode);
            await BroadcastTimerAsync(game, cancellationToken);
        }
        catch (GameRuleViolationException)
        {
            TempData["ErrorMessage"] =
                localizer["GameTimer_ResumeRejected"].Value;
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostAdvanceRoundAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.FindOwned(new GameSessionId(id), currentHost.RequiredId);

        if (game is null)
        {
            return NotFound();
        }

        try
        {
            sessionRegistry.AdvanceToNextRound(game.PublicCode);
        }
        catch (GameRuleViolationException)
        {
            TempData["ErrorMessage"] =
                localizer["GameBoard_AdvanceRoundRejected"].Value;
        }

        await BroadcastPlayersAsync(game, cancellationToken);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostStartFinalQuestionAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await ExecuteFinalHostCommand(
            id,
            game => sessionRegistry.StartFinalQuestion(game.PublicCode),
            cancellationToken);
    }

    public async Task<IActionResult> OnPostLockFinalWagersAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await ExecuteFinalHostCommand(
            id,
            game => sessionRegistry.LockFinalWagers(game.PublicCode),
            cancellationToken);
    }

    public async Task<IActionResult> OnPostLockFinalAnswersAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await ExecuteFinalHostCommand(
            id,
            game => sessionRegistry.LockFinalAnswers(game.PublicCode),
            cancellationToken);
    }

    public async Task<IActionResult> OnPostJudgeFinalAnswerAsync(
        Guid id,
        Guid playerId,
        bool isCorrect,
        CancellationToken cancellationToken)
    {
        return await ExecuteFinalHostCommand(
            id,
            game => sessionRegistry.JudgeFinalAnswer(
                game.PublicCode,
                new GamePlayerId(playerId),
                isCorrect),
            cancellationToken);
    }

    public async Task<IActionResult> OnPostSelectQuestionAsync(
        Guid id,
        int sourceQuestionId,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.FindOwned(new GameSessionId(id), currentHost.RequiredId);

        if (game is null)
        {
            return NotFound();
        }

        try
        {
            sessionRegistry.SelectQuestion(game.PublicCode, sourceQuestionId);
            await BroadcastBuzzerAsync(game, cancellationToken);
            await BroadcastTimerAsync(game, cancellationToken);
        }
        catch (GameRuleViolationException)
        {
            TempData["ErrorMessage"] = localizer["GameBoard_SelectionRejected"].Value;
        }

        return RedirectToPage(new { id });
    }

    public IActionResult OnPostSubmitQuestionWager(
        Guid id,
        int sourceQuestionId,
        int amount)
    {
        var game = sessionRegistry.FindOwned(new GameSessionId(id), currentHost.RequiredId);

        if (game is null)
        {
            return NotFound();
        }

        try
        {
            sessionRegistry.SubmitQuestionWager(
                game.PublicCode,
                sourceQuestionId,
                amount);
        }
        catch (GameRuleViolationException)
        {
            TempData["ErrorMessage"] =
                localizer["GameBoard_WagerRejected"].Value;
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostActivateBuzzerAsync(
        Guid id,
        int sourceQuestionId,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.FindOwned(new GameSessionId(id), currentHost.RequiredId);

        if (game is null)
        {
            return NotFound();
        }

        try
        {
            sessionRegistry.ActivateQuestionBuzzer(
                game.PublicCode,
                sourceQuestionId);

            await BroadcastBuzzerAsync(game, cancellationToken);
        }
        catch (GameRuleViolationException)
        {
            TempData["ErrorMessage"] =
                localizer["GameBoard_BuzzerActivationRejected"].Value;
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostJudgeQuestionAnswerAsync(
        Guid id,
        int sourceQuestionId,
        Guid playerId,
        bool isCorrect,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.FindOwned(new GameSessionId(id), currentHost.RequiredId);

        if (game is null)
        {
            return NotFound();
        }

        try
        {
            sessionRegistry.JudgeQuestionAnswer(
                game.PublicCode,
                sourceQuestionId,
                new GamePlayerId(playerId),
                isCorrect);
        }
        catch (GameRuleViolationException)
        {
            TempData["ErrorMessage"] =
                localizer["GameBoard_JudgmentRejected"].Value;
        }

        await BroadcastPlayersAsync(game, cancellationToken);
        await BroadcastBuzzerAsync(game, cancellationToken);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostResolveQuestionAsync(
        Guid id,
        int sourceQuestionId,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.FindOwned(new GameSessionId(id), currentHost.RequiredId);

        if (game is null)
        {
            return NotFound();
        }

        try
        {
            sessionRegistry.ResolveQuestionWithoutCorrectAnswer(
                game.PublicCode,
                sourceQuestionId);
        }
        catch (GameRuleViolationException)
        {
            TempData["ErrorMessage"] =
                localizer["GameBoard_JudgmentRejected"].Value;
        }

        await BroadcastPlayersAsync(game, cancellationToken);
        await BroadcastBuzzerAsync(game, cancellationToken);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCloseAnswerAsync(
        Guid id,
        int sourceQuestionId,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.FindOwned(new GameSessionId(id), currentHost.RequiredId);

        if (game is null)
        {
            return NotFound();
        }

        try
        {
            sessionRegistry.CloseQuestionAnswer(
                game.PublicCode,
                sourceQuestionId);
            await gameHistoryStore.SaveCompletedGameAsync(
                game,
                cancellationToken);
            await BroadcastBuzzerAsync(game, cancellationToken);
        }
        catch (GameRuleViolationException)
        {
            TempData["ErrorMessage"] =
                localizer["GameBoard_CloseAnswerRejected"].Value;
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostSetActivePlayerAsync(
        Guid id,
        Guid playerId,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.FindOwned(new GameSessionId(id), currentHost.RequiredId);

        if (game is null)
        {
            return NotFound();
        }

        try
        {
            sessionRegistry.SetActivePlayer(
                game.PublicCode,
                new GamePlayerId(playerId));
        }
        catch (GameRuleViolationException)
        {
            TempData["ErrorMessage"] =
                localizer["GameBoard_ActivePlayerRejected"].Value;
        }

        await BroadcastPlayersAsync(game, cancellationToken);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRandomActivePlayerAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.FindOwned(new GameSessionId(id), currentHost.RequiredId);

        if (game is null)
        {
            return NotFound();
        }

        try
        {
            sessionRegistry.SelectRandomActivePlayer(game.PublicCode);
        }
        catch (GameRuleViolationException)
        {
            TempData["ErrorMessage"] =
                localizer["GameBoard_ActivePlayerRejected"].Value;
        }

        await BroadcastPlayersAsync(game, cancellationToken);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostApproveRejoinAsync(
        Guid id,
        Guid playerId,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.FindOwned(new GameSessionId(id), currentHost.RequiredId);

        if (game is null)
        {
            return NotFound();
        }

        var approval = sessionRegistry.ApprovePlayerRejoin(
            game.PublicCode,
            new GamePlayerId(playerId));

        if (approval is null)
        {
            return NotFound();
        }

        if (approval.ConnectionIds.Count > 0)
        {
            await gameHub.Clients
                .Clients(approval.ConnectionIds)
                .SendAsync("RejoinApproved", cancellationToken);
        }

        await gameHub.Clients
            .Group(GameHub.GroupName(game.PublicCode))
            .SendAsync(
                "PlayersChanged",
                GameHub.CreatePlayersUpdate(sessionRegistry, game),
                cancellationToken);

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRemovePlayerAsync(
        Guid id,
        Guid playerId,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.FindOwned(new GameSessionId(id), currentHost.RequiredId);

        if (game is null)
        {
            return NotFound();
        }

        try
        {
            var removal = sessionRegistry.RemovePlayer(
                game.PublicCode,
                new GamePlayerId(playerId));

            if (removal is null)
            {
                return NotFound();
            }

            if (removal.ConnectionIds.Count > 0)
            {
                await gameHub.Clients
                    .Clients(removal.ConnectionIds)
                    .SendAsync("RemovedFromGame", cancellationToken);
            }

            await BroadcastPlayersAsync(game, cancellationToken);
        }
        catch (GameRuleViolationException)
        {
            TempData["ErrorMessage"] =
                localizer["GameBoard_RemovePlayerRejected"].Value;
        }

        return RedirectToPage(new { id });
    }

    public IActionResult OnPostTogglePlayerJoining(Guid id)
    {
        var game = sessionRegistry.FindOwned(new GameSessionId(id), currentHost.RequiredId);

        if (game is null)
        {
            return NotFound();
        }

        try
        {
            sessionRegistry.ToggleNewPlayerJoining(game.PublicCode);
        }
        catch (GameRuleViolationException)
        {
            TempData["ErrorMessage"] =
                localizer["GameBoard_PlayerJoiningToggleRejected"].Value;
        }

        return RedirectToPage(new { id });
    }

    private Task BroadcastTimerAsync(
        GameSessionRegistration game,
        CancellationToken cancellationToken)
    {
        return gameHub.Clients
            .Group(GameHub.GroupName(game.PublicCode))
            .SendAsync(
                "TimerStateChanged",
                GameHub.CreateTimerUpdate(game),
                cancellationToken);
    }

    private Task BroadcastBuzzerAsync(
        GameSessionRegistration game,
        CancellationToken cancellationToken)
    {
        var update = GameHub.CreateBuzzerUpdate(game);

        return update is null
            ? Task.CompletedTask
            : gameHub.Clients
                .Group(GameHub.GroupName(game.PublicCode))
                .SendAsync("BuzzerStateChanged", update, cancellationToken);
    }

    private Task BroadcastPlayersAsync(
        GameSessionRegistration game,
        CancellationToken cancellationToken)
    {
        return gameHub.Clients
            .Group(GameHub.GroupName(game.PublicCode))
            .SendAsync(
                "PlayersChanged",
                GameHub.CreatePlayersUpdate(sessionRegistry, game),
                cancellationToken);
    }

    private async Task<IActionResult> ExecuteFinalHostCommand(
        Guid id,
        Action<GameSessionRegistration> command,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.FindOwned(new GameSessionId(id), currentHost.RequiredId);

        if (game is null)
        {
            return NotFound();
        }

        try
        {
            command(game);
            await gameHistoryStore.SaveCompletedGameAsync(
                game,
                cancellationToken);
        }
        catch (GameRuleViolationException)
        {
            TempData["ErrorMessage"] =
                localizer["FinalQuestion_ActionRejected"].Value;
        }

        await gameHub.Clients
            .Group(GameHub.GroupName(game.PublicCode))
            .SendAsync(
                "GameStatusChanged",
                GameHub.CreateStatusUpdate(game),
                cancellationToken);
        await gameHub.Clients
            .Group(GameHub.GroupName(game.PublicCode))
            .SendAsync("FinalQuestionProgressChanged", cancellationToken);
        await BroadcastPlayersAsync(game, cancellationToken);

        return RedirectToPage(new { id });
    }

    private IActionResult LoadPage(
        Guid id,
        int? previewQuestionId = null,
        bool previewAnswer = false)
    {
        var game = sessionRegistry.FindOwned(new GameSessionId(id), currentHost.RequiredId);

        if (game is null)
        {
            return NotFound();
        }

        Game = game;
        Players = sessionRegistry.GetPlayers(game);
        ActivePlayerIds = sessionRegistry.GetPlayerLobbyEntries(game)
            .Where(player => player.Presence == PlayerPresenceStatus.Active)
            .Select(player => player.Id)
            .ToHashSet();
        SettingsInput = GameSettingsInput.From(game.Session.Settings);

        if (game.Session.Status == GameSessionStatus.Completed)
        {
            FinalStandings = game.Session.GetFinalStandings();
        }

        if (game.Session.Status is not GameSessionStatus.Running)
        {
            return Page();
        }

        var currentRound = game.Session.CurrentRound;

        var roundQuestions = game.Session.Board.Questions
            .Where(question => question.SourceRoundId == currentRound.SourceRoundId)
            .ToArray();

        BoardCategories = roundQuestions
            .GroupBy(question => new
            {
                question.SourceCategoryId,
                question.CategoryTitle
            })
            .Select(group => new GameBoardCategory(
                group.Key.SourceCategoryId,
                group.Key.CategoryTitle,
                group.OrderBy(question => question.RowIndex).ToArray()))
            .ToArray();

        CurrentQuestion = roundQuestions.FirstOrDefault(question =>
            question.Status is not RuntimeQuestionStatus.Available and
                not RuntimeQuestionStatus.Resolved);

        if (CurrentQuestion is null && previewQuestionId.HasValue)
        {
            PreviewQuestion = roundQuestions.SingleOrDefault(question =>
                question.SourceQuestionId == previewQuestionId.Value &&
                question.Status == RuntimeQuestionStatus.Resolved);
            IsPreviewingAnswer = PreviewQuestion is not null && previewAnswer;
        }

        IsRoundSummaryVisible =
            CurrentQuestion is null &&
            PreviewQuestion is null &&
            game.Session.IsCurrentRoundComplete;

        if (IsRoundSummaryVisible)
        {
            RoundLeaders = game.Session.GetCurrentRoundStandings()
                .Take(3)
                .Select(standing => new RoundLeaderboardEntry(
                    standing.Position,
                    standing.PlayerId,
                    standing.PlayerName,
                    standing.Score))
                .ToArray();
        }

        if (CurrentQuestion?.Status == RuntimeQuestionStatus.AwaitingWager)
        {
            QuestionWagerLimits = game.Session.GetQuestionWagerLimits(
                CurrentQuestion.SourceQuestionId);
        }

        return Page();
    }
}


public sealed record GameBoardCategory(
    int SourceCategoryId,
    string Title,
    IReadOnlyList<RuntimeQuestion> Questions);


public sealed record RoundLeaderboardEntry(
    int Position,
    GamePlayerId PlayerId,
    string PlayerName,
    int Score);

public sealed record GameContentPreviewModel(
    Guid GameSessionId,
    int SourceQuestionId,
    bool IsAnswer,
    IReadOnlyList<ContentBlockSnapshot> Blocks);
