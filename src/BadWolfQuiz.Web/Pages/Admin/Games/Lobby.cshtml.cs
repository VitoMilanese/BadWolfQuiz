using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Hubs;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using QRCoder;

namespace BadWolfQuiz.Web.Pages.Admin.Games;

public sealed class LobbyModel(
    QuizDbContext db,
    GameSessionRegistry sessionRegistry,
    GameHistoryStore gameHistoryStore,
    CurrentHost currentHost,
    JoinUrlBuilder joinUrlBuilder,
    AvatarCatalog avatarCatalog,
    QuizRatingService quizRatingService,
    MediaUploadProcessor mediaUploadProcessor,
    PremiumHostAccess premiumHostAccess,
    DiscordConnectionRepository discordRepository,
    DiscordMuteCoordinator discordMuteCoordinator,
    IDiscordVoiceGateway discordGateway,
    IHubContext<GameHub> gameHub,
    IConfiguration configuration,
    IStringLocalizer<SharedResource> localizer,
    ILogger<LobbyModel> logger) : PageModel
{
    public GameSessionRegistration Game { get; private set; } = null!;

    public IReadOnlyList<PlayerLobbyEntry> Players { get; private set; } = [];

    public IReadOnlyList<GamePlayer> BlockedPlayers { get; private set; } = [];

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
    public bool CanRateQuiz { get; private set; }
    public int? ExistingRating { get; private set; }
    public int MaximumImageUploadMegabytes =>
        mediaUploadProcessor.MaximumImageUploadMegabytes(
            premiumHostAccess.IsPremium(currentHost.RequiredId));
    public BadWolfQuiz.Web.Models.HostDiscordConnection? DiscordConnection { get; private set; }
    public bool IsDiscordVoiceReady { get; private set; }

    public string QuestionTimerWarningSound =>
        string.Equals(
            configuration["Game:QuestionTimerWarningSound"],
            "Rising",
            StringComparison.OrdinalIgnoreCase)
            ? "Rising"
            : "Alternating";

    public string CorrectAnswerSound => NormalizeAnswerFeedbackSound(
        configuration["Game:CorrectAnswerSound"],
        "Triumph",
        "Arcade",
        "Chime");

    public string IncorrectAnswerSound => NormalizeAnswerFeedbackSound(
        configuration["Game:IncorrectAnswerSound"],
        "Descent",
        "ArcadeFall",
        "ChimeFall");

    public AnswerResultOverlay? AnswerResultOverlay { get; private set; }

    private IReadOnlyDictionary<int, bool> questionContentBlockAutoplay =
        new Dictionary<int, bool>();
    private IReadOnlyDictionary<int, bool> answerContentBlockAutoplay =
        new Dictionary<int, bool>();
    private IReadOnlyDictionary<int, bool> finalQuestionContentBlockAutoplay =
        new Dictionary<int, bool>();
    private IReadOnlyDictionary<int, bool> finalAnswerContentBlockAutoplay =
        new Dictionary<int, bool>();

    public bool ResolveContentBlockAutoplay(
        ContentBlockSnapshot block,
        bool answer = false,
        bool final = false)
    {
        var values = final
            ? answer
                ? finalAnswerContentBlockAutoplay
                : finalQuestionContentBlockAutoplay
            : answer
                ? answerContentBlockAutoplay
                : questionContentBlockAutoplay;

        return values.TryGetValue(block.SourceContentBlockId, out var autoplay)
            ? autoplay
            : block.Autoplay;
    }

    private static string NormalizeAnswerFeedbackSound(
        string? configured,
        string fallback,
        params string[] supported) =>
        supported.FirstOrDefault(option =>
            string.Equals(option, configured, StringComparison.OrdinalIgnoreCase))
        ?? fallback;

    public async Task<IActionResult> OnGetAsync(
        Guid id,
        int? previewQuestionId,
        bool previewAnswer = false,
        CancellationToken cancellationToken = default)
    {
        var result = LoadPage(id, previewQuestionId, previewAnswer);
        if (result is PageResult)
        {
            await LoadContentBlockAutoplayOverridesAsync(cancellationToken);

            AnswerResultOverlay =
                sessionRegistry.ConsumeAnswerResultOverlay(Game);

            DiscordConnection = await discordRepository.GetAsync(cancellationToken);
            IsDiscordVoiceReady = DiscordConnection is not null &&
                discordGateway.GetHealth(
                    DiscordConnection.GuildId,
                    DiscordConnection.VoiceChannelId).IsReady;
            var rating = await quizRatingService.GetHostRatingStateAsync(
                Game,
                currentHost.RequiredId,
                cancellationToken);
            CanRateQuiz = rating.IsAvailable;
            ExistingRating = rating.Score;
        }
        return result;
    }

    private async Task LoadContentBlockAutoplayOverridesAsync(
        CancellationToken cancellationToken)
    {
        var quizId = Game.Session.Quiz.SourceQuizId;

        questionContentBlockAutoplay = await db.QuestionContentBlocks
            .AsNoTracking()
            .Where(block => block.Question.Category.Round.QuizId == quizId)
            .ToDictionaryAsync(
                block => block.Id,
                block => block.Autoplay,
                cancellationToken);
        answerContentBlockAutoplay = await db.AnswerContentBlocks
            .AsNoTracking()
            .Where(block => block.Question.Category.Round.QuizId == quizId)
            .ToDictionaryAsync(
                block => block.Id,
                block => block.Autoplay,
                cancellationToken);
        finalQuestionContentBlockAutoplay = await db.FinalQuestionContentBlocks
            .AsNoTracking()
            .Where(block => block.QuizId == quizId)
            .ToDictionaryAsync(
                block => block.Id,
                block => block.Autoplay,
                cancellationToken);
        finalAnswerContentBlockAutoplay = await db.FinalAnswerContentBlocks
            .AsNoTracking()
            .Where(block => block.QuizId == quizId)
            .ToDictionaryAsync(
                block => block.Id,
                block => block.Autoplay,
                cancellationToken);
    }

    public async Task<IActionResult> OnPostDiscordMuteAsync(
        Guid id,
        bool muted,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.FindOwned(new GameSessionId(id), currentHost.RequiredId);
        if (game is null)
        {
            return NotFound();
        }

        var connection = await discordRepository.GetAsync(cancellationToken);
        if (connection is null ||
            !discordGateway.GetHealth(connection.GuildId, connection.VoiceChannelId).IsReady)
        {
            return new JsonResult(new { error = localizer["Discord_NotReady"].Value })
                { StatusCode = 409 };
        }

        var result = await discordMuteCoordinator.SetManualAsync(
            id, currentHost.RequiredId, connection, muted, cancellationToken);
        return DiscordResult(result);
    }

    public async Task<IActionResult> OnPostDiscordMediaAsync(
        Guid id,
        bool active,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.FindOwned(new GameSessionId(id), currentHost.RequiredId);
        if (game is null)
        {
            return NotFound();
        }

        var connection = await discordRepository.GetAsync(cancellationToken);
        if (connection is null || (active && !connection.AutoMuteDuringMedia))
        {
            return new JsonResult(new { ignored = true });
        }

        var result = await discordMuteCoordinator.SetAutomaticAsync(
            id, currentHost.RequiredId, connection, active, cancellationToken);
        return DiscordResult(result);
    }

    private IActionResult DiscordResult(DiscordMuteResult result)
    {
        var payload = new
        {
            result.TargetCount,
            result.SucceededCount,
            result.FailedCount,
            result.SkippedCount,
            message = result.FailedCount == 0
                ? localizer["Discord_OperationComplete", result.SucceededCount, result.SkippedCount].Value
                : localizer["Discord_OperationPartial", result.SucceededCount, result.FailedCount, result.SkippedCount].Value
        };
        return new JsonResult(payload);
    }

    public async Task<IActionResult> OnPostRateQuizAsync(
        Guid id,
        int score,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.FindOwned(
            new GameSessionId(id),
            currentHost.RequiredId);
        if (game is null)
        {
            return NotFound();
        }

        var result = await quizRatingService.RateHostAsync(
            game,
            currentHost.RequiredId,
            score,
            cancellationToken);
        return new JsonResult(new { saved = result == QuizRatingResult.Saved })
        {
            StatusCode = result == QuizRatingResult.Saved ? 200 : 409
        };
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

        return new FileContentResult(block.FileData, block.FileContentType)
        {
            EnableRangeProcessing = true
        };
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

        return new FileContentResult(block.FileData, block.FileContentType)
        {
            EnableRangeProcessing = true
        };
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

        string? videoId = null;

        if (uri.Host.EndsWith("youtu.be", StringComparison.OrdinalIgnoreCase))
        {
            videoId = uri.AbsolutePath
                .Trim('/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();
        }
        else if (uri.Host.EndsWith("youtube.com", StringComparison.OrdinalIgnoreCase) ||
                 uri.Host.EndsWith("youtube-nocookie.com", StringComparison.OrdinalIgnoreCase))
        {
            var pathSegments = uri.AbsolutePath
                .Trim('/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (pathSegments.Length >= 2 &&
                (string.Equals(pathSegments[0], "embed", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(pathSegments[0], "shorts", StringComparison.OrdinalIgnoreCase)))
            {
                videoId = pathSegments[1];
            }
            else
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
        }

        return string.IsNullOrWhiteSpace(videoId)
            ? value
            : $"https://www.youtube-nocookie.com/embed/{Uri.EscapeDataString(videoId)}?enablejsapi=1";
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
            await db.Quizzes
                .Where(quiz => quiz.Id == game.Session.Quiz.SourceQuizId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(quiz => quiz.LastPlayedAtUtc, DateTime.UtcNow),
                    cancellationToken);
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
            try
            {
                var media = await mediaUploadProcessor.ProcessImageAsync(
                    HostImage,
                    premiumHostAccess.IsPremium(currentHost.RequiredId),
                    cancellationToken);
                imageData = media.Data;
                imageContentType = media.ContentType;
                SettingsInput.HostVisualSource = HostVisualSource.Image;
            }
            catch (MediaUploadException exception)
            {
                TempData["ErrorMessage"] =
                    localizer[exception.ResourceKey, exception.ResourceArguments].Value;
                return null;
            }
        }

        if ((SettingsInput.HostVisualSource == HostVisualSource.Avatar &&
             !avatarCatalog.IsValid(SettingsInput.HostAvatarId)) ||
            (SettingsInput.HostVisualSource == HostVisualSource.WebcamUrl &&
             !GameSettingsInput.IsValidWebcamUrl(SettingsInput.HostWebcamUrl)))
        {
            TempData["ErrorMessage"] = localizer["HostCard_InvalidSettings"].Value;
            return null;
        }

        if (!SettingsInput.IsValid)
        {
            TempData["ErrorMessage"] = localizer["GameSettings_InvalidDuration"].Value;
            return null;
        }

        return SettingsInput.ToRuntimeSettings(
            imageData,
            imageContentType,
            existing.BrandLogoData,
            existing.BrandLogoContentType,
            existing.SiteThemeId,
            existing.CustomThemeColors);
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
            logger.LogWarning(
                "Host gameplay session lookup failed. Handler={Handler} GameSessionId={GameSessionId} HostId={HostId} TraceIdentifier={TraceIdentifier}",
                "PauseQuestionTimer",
                id,
                currentHost.RequiredId,
                HttpContext.TraceIdentifier);
            return NotFound();
        }

        try
        {
            sessionRegistry.PauseQuestionTimer(game.PublicCode);
            await BroadcastTimerAsync(game, cancellationToken);
        }
        catch (GameRuleViolationException)
        {
            var errorMessage = localizer["GameTimer_PauseRejected"].Value;

            if (IsAjaxRequest())
            {
                return BadRequest(new { success = false, error = errorMessage });
            }

            TempData["ErrorMessage"] = errorMessage;
        }

        if (IsAjaxRequest())
        {
            return new JsonResult(new { success = true });
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
            logger.LogWarning(
                "Host gameplay session lookup failed. Handler={Handler} GameSessionId={GameSessionId} HostId={HostId} TraceIdentifier={TraceIdentifier}",
                "ResumeQuestionTimer",
                id,
                currentHost.RequiredId,
                HttpContext.TraceIdentifier);
            return NotFound();
        }

        try
        {
            sessionRegistry.ResumeQuestionTimer(game.PublicCode);
            await BroadcastTimerAsync(game, cancellationToken);
        }
        catch (GameRuleViolationException)
        {
            var errorMessage = localizer["GameTimer_ResumeRejected"].Value;

            if (IsAjaxRequest())
            {
                return BadRequest(new { success = false, error = errorMessage });
            }

            TempData["ErrorMessage"] = errorMessage;
        }

        if (IsAjaxRequest())
        {
            return new JsonResult(new { success = true });
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostAddQuestionTimerTimeAsync(
        Guid id,
        int seconds,
        CancellationToken cancellationToken)
    {
        if (seconds is not (10 or 15 or 20 or 30))
        {
            return BadRequest();
        }

        var game = sessionRegistry.FindOwned(new GameSessionId(id), currentHost.RequiredId);

        if (game is null)
        {
            return NotFound();
        }

        try
        {
            sessionRegistry.AddQuestionTimerTime(
                game.PublicCode,
                TimeSpan.FromSeconds(seconds));
            await BroadcastTimerAsync(game, cancellationToken);
        }
        catch (GameRuleViolationException)
        {
            if (IsAjaxRequest())
            {
                return BadRequest(new { success = false });
            }

            return BadRequest();
        }

        if (IsAjaxRequest())
        {
            return new JsonResult(new { success = true });
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
        await StopAutomaticDiscordMuteAsync(id, cancellationToken);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostPreviousRoundAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.FindOwned(
            new GameSessionId(id),
            currentHost.RequiredId);

        if (game is null)
        {
            return NotFound();
        }

        try
        {
            sessionRegistry.ReturnToPreviousUnfinishedRound(game.PublicCode);
        }
        catch (GameRuleViolationException)
        {
            TempData["ErrorMessage"] =
                localizer["GameBoard_PreviousRoundRejected"].Value;
        }

        await BroadcastPlayersAsync(game, cancellationToken);
        await BroadcastBuzzerAsync(game, cancellationToken);
        await BroadcastTimerAsync(game, cancellationToken);
        await StopAutomaticDiscordMuteAsync(id, cancellationToken);
        return LocalRedirect($"/Admin/Games/RunningRoundIntro/{id:D}?returning=true");
    }

    public async Task<IActionResult> OnPostReturnToUnfinishedRoundAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.FindOwned(
            new GameSessionId(id),
            currentHost.RequiredId);

        if (game is null)
        {
            return NotFound();
        }

        var showLeaderboard =
            game.Session.Players.Count > 0 &&
            !game.Session.IsUnfinishedRoundReturnPending;

        try
        {
            if (showLeaderboard)
            {
                sessionRegistry.PrepareReturnToNearestUnfinishedRoundExcludingCurrent(
                    game.PublicCode);
            }
            else
            {
                sessionRegistry.ReturnToNearestUnfinishedRoundExcludingCurrent(
                    game.PublicCode);
            }
        }
        catch (GameRuleViolationException)
        {
            TempData["ErrorMessage"] =
                localizer["GameBoard_PreviousRoundRejected"].Value;
        }

        await BroadcastPlayersAsync(game, cancellationToken);
        await BroadcastBuzzerAsync(game, cancellationToken);
        await BroadcastTimerAsync(game, cancellationToken);
        await StopAutomaticDiscordMuteAsync(id, cancellationToken);
        return showLeaderboard
            ? RedirectToPage(new { id })
            : LocalRedirect($"/Admin/Games/RunningRoundIntro/{id:D}?returning=true");
    }

    public async Task<IActionResult> OnPostForceAdvanceRoundAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.FindOwned(
            new GameSessionId(id),
            currentHost.RequiredId);

        if (game is null)
        {
            return NotFound();
        }

        try
        {
            sessionRegistry.ForceCompleteCurrentRound(game.PublicCode);

            if (game.Session.Players.Count == 0)
            {
                sessionRegistry.AdvanceToNextRound(game.PublicCode);
            }
        }
        catch (GameRuleViolationException)
        {
            TempData["ErrorMessage"] =
                localizer["GameBoard_AdvanceRoundRejected"].Value;
        }

        await BroadcastPlayersAsync(game, cancellationToken);
        await BroadcastBuzzerAsync(game, cancellationToken);
        await BroadcastTimerAsync(game, cancellationToken);
        await StopAutomaticDiscordMuteAsync(id, cancellationToken);

        return RedirectToPage(new { id });
    }

    public IActionResult OnPostStartNaturalFinalTransition(Guid id)
    {
        var game = sessionRegistry.FindOwned(
            new GameSessionId(id),
            currentHost.RequiredId);

        if (game is null)
        {
            return NotFound();
        }

        if (game.Session.Status != GameSessionStatus.Running ||
            game.Session.Quiz.FinalQuestion is null ||
            !game.Session.IsCurrentRoundComplete ||
            game.Session.HasNextUnfinishedRound ||
            game.Session.HasAnyUnfinishedRegularRound)
        {
            TempData["ErrorMessage"] = localizer["FinalQuestion_ActionRejected"].Value;
            return RedirectToPage(new { id });
        }

        return RedirectToPage("FinalQuestionTransition", new { id, force = false });
    }

    public async Task<IActionResult> OnPostPrepareFinalQuestionLeaderboardAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.FindOwned(
            new GameSessionId(id),
            currentHost.RequiredId);

        if (game is null)
        {
            return NotFound();
        }

        if (game.Session.Players.Count == 0)
        {
            return RedirectToPage("FinalQuestionTransition", new { id, force = true });
        }

        try
        {
            sessionRegistry.PrepareFinalQuestionAdvance(game.PublicCode);
        }
        catch (GameRuleViolationException)
        {
            TempData["ErrorMessage"] = localizer["FinalQuestion_ActionRejected"].Value;
        }

        await BroadcastPlayersAsync(game, cancellationToken);
        await BroadcastBuzzerAsync(game, cancellationToken);
        await BroadcastTimerAsync(game, cancellationToken);
        await StopAutomaticDiscordMuteAsync(id, cancellationToken);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostForceAdvanceToFinalQuestionAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await ExecuteFinalHostCommand(
            id,
            game => sessionRegistry.ForceAdvanceToFinalQuestion(game.PublicCode),
            cancellationToken);
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

    public async Task<IActionResult> OnPostSubmitMinimumFinalWagerAsync(
        Guid id,
        Guid playerId,
        CancellationToken cancellationToken)
    {
        return await ExecuteFinalHostCommand(
            id,
            game => sessionRegistry.SubmitMinimumFinalWagerForPlayer(
                game.PublicCode,
                new GamePlayerId(playerId)),
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

    public async Task<IActionResult> OnPostSubmitEmptyFinalAnswerAsync(
        Guid id,
        Guid playerId,
        CancellationToken cancellationToken)
    {
        return await ExecuteFinalHostCommand(
            id,
            game => sessionRegistry.SubmitEmptyFinalAnswerForPlayer(
                game.PublicCode,
                new GamePlayerId(playerId)),
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
            logger.LogWarning(
                "Host gameplay session lookup failed. Handler={Handler} GameSessionId={GameSessionId} HostId={HostId} TraceIdentifier={TraceIdentifier}",
                "SelectQuestion",
                id,
                currentHost.RequiredId,
                HttpContext.TraceIdentifier);
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
            var errorMessage =
                localizer["GameBoard_SelectionRejected"].Value;

            if (IsAjaxRequest())
            {
                return BadRequest(new
                {
                    success = false,
                    error = errorMessage
                });
            }

            TempData["ErrorMessage"] = errorMessage;
        }

        if (IsAjaxRequest())
        {
            return new JsonResult(new
            {
                success = true
            });
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

    public async Task<IActionResult> OnPostRevealClueAsync(
        Guid id,
        int sourceQuestionId,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.FindOwned(
            new GameSessionId(id),
            currentHost.RequiredId);

        if (game is null)
        {
            return NotFound();
        }

        var revealedClueCount = 0;
        var canRevealClue = false;

        try
        {
            var question = sessionRegistry.RevealNextClue(
                game.PublicCode,
                sourceQuestionId);

            if (question is null)
            {
                return NotFound();
            }

            revealedClueCount = question.RevealedClueCount;
            canRevealClue = question.CanRevealClue;

            await gameHub.Clients
                .Group(GameHub.GroupName(game.PublicCode))
                .SendAsync(
                    "QuestionClueRevealed",
                    new
                    {
                        sourceQuestionId,
                        revealedClueCount,
                        canRevealClue
                    },
                    cancellationToken);
        }
        catch (GameRuleViolationException)
        {
            var errorMessage =
                localizer["GameBoard_RevealClueRejected"].Value;

            if (IsAjaxRequest())
            {
                return BadRequest(new
                {
                    success = false,
                    error = errorMessage
                });
            }

            TempData["ErrorMessage"] = errorMessage;
        }

        if (IsAjaxRequest())
        {
            return new JsonResult(new
            {
                success = true,
                sourceQuestionId,
                revealedClueCount,
                canRevealClue
            });
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
            logger.LogWarning(
                "Host gameplay session lookup failed. Handler={Handler} GameSessionId={GameSessionId} HostId={HostId} TraceIdentifier={TraceIdentifier}",
                "JudgeQuestionAnswer",
                id,
                currentHost.RequiredId,
                HttpContext.TraceIdentifier);
            return NotFound();
        }

        try
        {
            var attempt = sessionRegistry.JudgeQuestionAnswer(
                game.PublicCode,
                sourceQuestionId,
                new GamePlayerId(playerId),
                isCorrect);

            if (attempt is not null)
            {
                var player = game.Session.Players.Single(
                    item => item.Id == attempt.PlayerId);

                sessionRegistry.SetAnswerResultOverlay(
                    game,
                    player,
                    attempt,
                    isCorrect ? "correct" : "incorrect");
            }
        }
        catch (GameRuleViolationException)
        {
            var errorMessage = localizer["GameBoard_JudgmentRejected"].Value;

            if (IsAjaxRequest())
            {
                return BadRequest(new { success = false, error = errorMessage });
            }

            TempData["ErrorMessage"] = errorMessage;
        }

        await BroadcastPlayersAsync(game, cancellationToken);
        await BroadcastBuzzerAsync(game, cancellationToken);
        await StopAutomaticDiscordMuteAsync(id, cancellationToken);

        if (IsAjaxRequest())
        {
            return new JsonResult(new { success = true });
        }

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
        await StopAutomaticDiscordMuteAsync(id, cancellationToken);
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
            var quizCompleted = await gameHistoryStore.SaveCompletedGameAsync(
                game,
                cancellationToken);
            await BroadcastBuzzerAsync(game, cancellationToken);
            if (quizCompleted)
            {
                var connection = await discordRepository.GetAsync(cancellationToken);
                if (connection is not null)
                {
                    await discordMuteCoordinator.CleanupAsync(
                        id, currentHost.RequiredId, connection, cancellationToken);
                }
                await gameHub.Clients
                    .Group(GameHub.GroupName(game.PublicCode))
                    .SendAsync("QuizCompleted", cancellationToken);
            }
            else
            {
                await StopAutomaticDiscordMuteAsync(id, cancellationToken);
            }
        }
        catch (GameRuleViolationException)
        {
            TempData["ErrorMessage"] =
                localizer["GameBoard_CloseAnswerRejected"].Value;
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostGiftQuestionAsync(
        Guid id,
        int sourceQuestionId,
        Guid playerId,
        int value,
        bool resolveQuestionIfAvailable,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.FindOwned(new GameSessionId(id), currentHost.RequiredId);

        if (game is null)
        {
            return NotFound();
        }

        try
        {
            if (value <= 0)
            {
                var errorMessage = localizer["GameBoard_QuickScoreInvalidValue"].Value;
                if (IsAjaxRequest())
                {
                    return BadRequest(new { success = false, error = errorMessage });
                }

                TempData["ErrorMessage"] = errorMessage;
                return RedirectToPage(new { id });
            }

            sessionRegistry.AddQuestionAnswerHistoryEntry(
                game.PublicCode,
                sourceQuestionId,
                new GamePlayerId(playerId),
                true,
                value,
                resolveQuestionIfAvailable);

            await gameHistoryStore.SaveCompletedGameAsync(game, cancellationToken);
        }
        catch (GameRuleViolationException exception)
        {
            var errorMessage = exception.Message switch
            {
                "This player already has an answer entry for the selected question." =>
                    localizer["AnswerHistory_PlayerAlreadyRecorded"].Value,
                "Answer history cannot be added to a question that has not been played." =>
                    localizer["AnswerHistory_QuestionNotPlayed"].Value,
                "An answer history value cannot be negative." =>
                    localizer["AnswerHistory_InvalidValue"].Value,
                _ => localizer["AnswerHistory_Rejected"].Value
            };

            if (IsAjaxRequest())
            {
                return BadRequest(new { success = false, error = errorMessage });
            }

            TempData["ErrorMessage"] = errorMessage;
        }

        await BroadcastPlayersAsync(game, cancellationToken);

        if (IsAjaxRequest())
        {
            var question = game.Session.Board.Questions.Single(item =>
                item.SourceQuestionId == sourceQuestionId);
            return new JsonResult(new
            {
                success = true,
                sourceQuestionId,
                playerId,
                questionResolved = question.Status == RuntimeQuestionStatus.Resolved,
                roundComplete = game.Session.IsCurrentRoundComplete
            });
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCloseAvailableQuestionAsync(
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
            sessionRegistry.CloseAvailableQuestion(game.PublicCode, sourceQuestionId);
            await gameHistoryStore.SaveCompletedGameAsync(game, cancellationToken);
        }
        catch (GameRuleViolationException)
        {
            var errorMessage = localizer["GameBoard_SelectionRejected"].Value;
            if (IsAjaxRequest())
            {
                return BadRequest(new { success = false, error = errorMessage });
            }

            TempData["ErrorMessage"] = errorMessage;
        }

        if (IsAjaxRequest())
        {
            return new JsonResult(new
            {
                success = true,
                sourceQuestionId,
                roundComplete = game.Session.IsCurrentRoundComplete
            });
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCloseAvailableCategoryQuestionsAsync(
        Guid id,
        int sourceCategoryId,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.FindOwned(new GameSessionId(id), currentHost.RequiredId);

        if (game is null)
        {
            return NotFound();
        }

        var resolvedQuestionIds = game.Session.Board.Questions
            .Where(question =>
                question.SourceRoundId == game.Session.CurrentRound.SourceRoundId &&
                question.SourceCategoryId == sourceCategoryId &&
                question.Status == RuntimeQuestionStatus.Available)
            .Select(question => question.SourceQuestionId)
            .ToArray();

        try
        {
            sessionRegistry.CloseAvailableCategoryQuestions(
                game.PublicCode,
                sourceCategoryId);
            await gameHistoryStore.SaveCompletedGameAsync(game, cancellationToken);
        }
        catch (GameRuleViolationException)
        {
            var errorMessage = localizer["GameBoard_SelectionRejected"].Value;
            if (IsAjaxRequest())
            {
                return BadRequest(new { success = false, error = errorMessage });
            }

            TempData["ErrorMessage"] = errorMessage;
        }

        if (IsAjaxRequest())
        {
            return new JsonResult(new
            {
                success = true,
                sourceCategoryId,
                resolvedQuestionIds,
                roundComplete = game.Session.IsCurrentRoundComplete
            });
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostQuickScoreAdjustmentAsync(
        Guid id,
        int sourceQuestionId,
        Guid playerId,
        bool isCorrect,
        int value,
        bool resolveQuestionIfAvailable,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.FindOwned(new GameSessionId(id), currentHost.RequiredId);

        if (game is null)
        {
            return NotFound();
        }

        try
        {
            if (value <= 0)
            {
                var errorMessage = localizer["GameBoard_QuickScoreInvalidValue"].Value;
                if (IsAjaxRequest())
                {
                    return BadRequest(new { success = false, error = errorMessage });
                }

                TempData["ErrorMessage"] = errorMessage;
                return RedirectToPage(new { id });
            }

            sessionRegistry.AdjustQuestionAnswerHistoryEntry(
                game.PublicCode,
                sourceQuestionId,
                new GamePlayerId(playerId),
                isCorrect ? value : -value,
                resolveQuestionIfAvailable);

            await gameHistoryStore.SaveCompletedGameAsync(game, cancellationToken);
        }
        catch (GameRuleViolationException exception)
        {
            var errorMessage = exception.Message switch
            {
                "This player already has an answer entry for the selected question." =>
                    localizer["AnswerHistory_PlayerAlreadyRecorded"].Value,
                "Answer history cannot be added to a question that has not been played." =>
                    localizer["AnswerHistory_QuestionNotPlayed"].Value,
                "An answer history value cannot be negative." =>
                    localizer["AnswerHistory_InvalidValue"].Value,
                _ => localizer["AnswerHistory_Rejected"].Value
            };

            if (IsAjaxRequest())
            {
                return BadRequest(new { success = false, error = errorMessage });
            }

            TempData["ErrorMessage"] = errorMessage;
        }

        await BroadcastPlayersAsync(game, cancellationToken);

        if (IsAjaxRequest())
        {
            var question = game.Session.Board.Questions.Single(item =>
                item.SourceQuestionId == sourceQuestionId);
            return new JsonResult(new
            {
                success = true,
                sourceQuestionId,
                playerId,
                questionResolved = question.Status == RuntimeQuestionStatus.Resolved,
                roundComplete = game.Session.IsCurrentRoundComplete
            });
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
            logger.LogWarning(
                "Host gameplay session lookup failed. Handler={Handler} GameSessionId={GameSessionId} HostId={HostId} TraceIdentifier={TraceIdentifier}",
                "RandomActivePlayer",
                id,
                currentHost.RequiredId,
                HttpContext.TraceIdentifier);
            return NotFound();
        }

        try
        {
            sessionRegistry.SelectRandomActivePlayer(game.PublicCode);
        }
        catch (GameRuleViolationException)
        {
            var errorMessage = localizer["GameBoard_ActivePlayerRejected"].Value;

            if (IsAjaxRequest())
            {
                return BadRequest(new { success = false, error = errorMessage });
            }

            TempData["ErrorMessage"] = errorMessage;
        }

        await BroadcastPlayersAsync(game, cancellationToken);

        if (IsAjaxRequest())
        {
            return new JsonResult(new { success = true });
        }

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
            await gameHub.Clients
                .Clients(approval.ConnectionIds)
                .SendAsync(
                    "GameStatusChanged",
                    GameHub.CreateStatusUpdate(game),
                    cancellationToken);
            await gameHub.Clients
                .Clients(approval.ConnectionIds)
                .SendAsync(
                    "BuzzerStateChanged",
                    GameHub.CreateBuzzerUpdate(game),
                    cancellationToken);
            await gameHub.Clients
                .Clients(approval.ConnectionIds)
                .SendAsync(
                    "TimerStateChanged",
                    GameHub.CreateTimerUpdate(game),
                    cancellationToken);
        }

        await gameHub.Clients
            .Group(GameHub.GroupName(game.PublicCode))
            .SendAsync(
                "PlayersChanged",
                GameHub.CreatePlayersUpdate(sessionRegistry, game),
                cancellationToken);

        if (IsAjaxRequest())
        {
            return new JsonResult(new { success = true });
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRemovePlayerAsync(
        Guid id,
        Guid playerId,
        bool blockPlayer,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.FindOwned(new GameSessionId(id), currentHost.RequiredId);

        if (game is null)
        {
            return NotFound();
        }

        var previousStatus = game.Session.Status;

        try
        {
            var gamePlayerId = new GamePlayerId(playerId);
            var removal = sessionRegistry.RemovePlayer(
                game.PublicCode,
                gamePlayerId);

            if (removal is null)
            {
                return NotFound();
            }

            if (!blockPlayer &&
                !sessionRegistry.UnblockPlayer(game.PublicCode, gamePlayerId))
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

            if (previousStatus != game.Session.Status)
            {
                await gameHub.Clients
                    .Group(GameHub.GroupName(game.PublicCode))
                    .SendAsync(
                        "GameStatusChanged",
                        GameHub.CreateStatusUpdate(game),
                        cancellationToken);
                await gameHub.Clients
                    .Group(GameHub.GroupName(game.PublicCode))
                    .SendAsync("FinalQuestionProgressChanged", cancellationToken);
            }
        }
        catch (GameRuleViolationException)
        {
            TempData["ErrorMessage"] =
                localizer["GameBoard_RemovePlayerRejected"].Value;
        }

        return RedirectToPage(new { id });
    }

    public IActionResult OnPostUnblockPlayer(Guid id, Guid playerId)
    {
        var game = sessionRegistry.FindOwned(new GameSessionId(id), currentHost.RequiredId);

        if (game is null)
        {
            return NotFound();
        }

        if (!sessionRegistry.UnblockPlayer(
                game.PublicCode,
                new GamePlayerId(playerId)))
        {
            TempData["ErrorMessage"] =
                localizer["GameBoard_UnblockPlayerRejected"].Value;
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
            var errorMessage =
                localizer["GameBoard_PlayerJoiningToggleRejected"].Value;

            if (IsAjaxRequest())
            {
                return BadRequest(new { success = false, error = errorMessage });
            }

            TempData["ErrorMessage"] = errorMessage;
        }

        if (IsAjaxRequest())
        {
            return new JsonResult(new
            {
                success = true,
                allowsNewPlayers = game.AllowsNewPlayers
            });
        }

        return RedirectToPage(new { id });
    }

    private bool IsAjaxRequest()
    {
        return string.Equals(
            Request.Headers["X-Requested-With"],
            "XMLHttpRequest",
            StringComparison.OrdinalIgnoreCase);
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

    private async Task StopAutomaticDiscordMuteAsync(
        Guid gameId,
        CancellationToken cancellationToken)
    {
        var connection = await discordRepository.GetAsync(cancellationToken);
        if (connection is not null)
        {
            await discordMuteCoordinator.SetAutomaticAsync(
                gameId, currentHost.RequiredId, connection, false, cancellationToken);
        }
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

        var isAjaxRequest = IsAjaxRequest();
        string? errorMessage = null;

        try
        {
            command(game);
            await gameHistoryStore.SaveCompletedGameAsync(
                game,
                cancellationToken);
        }
        catch (GameRuleViolationException)
        {
            errorMessage = localizer["FinalQuestion_ActionRejected"].Value;
            if (!isAjaxRequest)
            {
                TempData["ErrorMessage"] = errorMessage;
            }
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

        var discordConnection = await discordRepository.GetAsync(cancellationToken);
        if (discordConnection is not null)
        {
            if (game.Session.Status == BadWolfQuiz.Game.Runtime.GameSessionStatus.Completed)
            {
                await discordMuteCoordinator.CleanupAsync(
                    id, currentHost.RequiredId, discordConnection, cancellationToken);
            }
            else
            {
                await discordMuteCoordinator.SetAutomaticAsync(
                    id, currentHost.RequiredId, discordConnection, false, cancellationToken);
            }
        }

        if (isAjaxRequest)
        {
            return errorMessage is null
                ? new JsonResult(new { success = true })
                : BadRequest(new { success = false, error = errorMessage });
        }

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
        Players = sessionRegistry.GetPlayerLobbyEntries(game);
        BlockedPlayers = sessionRegistry.GetBlockedPlayers(game);
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
    QuestionPresentationType PresentationType,
    bool IsAnswer,
    IReadOnlyList<ContentBlockSnapshot> Blocks);
