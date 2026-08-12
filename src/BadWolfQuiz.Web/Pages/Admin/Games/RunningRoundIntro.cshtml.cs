using System.Globalization;
using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Hubs;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Localization;

namespace BadWolfQuiz.Web.Pages.Admin.Games;

public sealed class RunningRoundIntroModel(
    GameSessionRegistry sessionRegistry,
    CurrentHost currentHost,
    DiscordConnectionRepository discordRepository,
    DiscordMuteCoordinator discordMuteCoordinator,
    IHubContext<GameHub> gameHub,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    public Guid GameId { get; private set; }
    public string Heading { get; private set; } = string.Empty;
    public IReadOnlyList<ContentBlockSnapshot> Blocks { get; private set; } = [];
    public int CategoryCount { get; private set; }
    public int? CategoryIndex { get; private set; }
    public int? NextCategoryIndex { get; private set; }
    public bool IsFinalCategory { get; private set; }
    public bool IsReturning { get; private set; }

    public string SkipLabel => CurrentLanguage switch
    {
        "uk" => "Пропустити",
        "it" => "Salta",
        _ => "Skip"
    };

    public string NextLabel => CurrentLanguage switch
    {
        "uk" => "Далі",
        "it" => "Avanti",
        _ => "Next"
    };

    public string StartGameLabel => CurrentLanguage switch
    {
        "uk" => "Почати гру",
        "it" => "Avvia gioco",
        _ => "Start game"
    };

    private static string CurrentLanguage =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    public IActionResult OnGet(Guid id, int? category, bool returning = false)
    {
        var game = FindOwned(id);
        if (game is null)
        {
            return NotFound();
        }

        if (game.Session.Status != GameSessionStatus.Running)
        {
            return RedirectToPage("Lobby", new { id });
        }

        LoadIntro(game, id, category, returning);
        return Page();
    }

    public IActionResult OnGetContentBlock(
        Guid id,
        int? category,
        int sourceContentBlockId,
        bool returning = false)
    {
        var game = FindOwned(id);
        if (game is null)
        {
            return NotFound();
        }

        var round = game.Session.CurrentRound;
        IReadOnlyList<ContentBlockSnapshot> blocks;

        if (category.HasValue)
        {
            var categories = GetIntroCategories(game.Session, round, returning);
            if (category.Value < 0 || category.Value >= categories.Length)
            {
                return NotFound();
            }

            blocks = categories[category.Value].DescriptionBlocks;
        }
        else
        {
            blocks = round.DescriptionBlocks;
        }

        var block = blocks.SingleOrDefault(item =>
            item.SourceContentBlockId == sourceContentBlockId);

        if (block?.FileData is null ||
            block.FileData.Length == 0 ||
            string.IsNullOrWhiteSpace(block.FileContentType))
        {
            return NotFound();
        }

        return new FileContentResult(block.FileData, block.FileContentType)
        {
            EnableRangeProcessing = true
        };
    }

    public async Task<IActionResult> OnPostAdvanceAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var game = FindOwned(id);
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
            TempData["ErrorMessage"] = localizer["GameBoard_AdvanceRoundRejected"].Value;
            return RedirectToPage("Lobby", new { id });
        }

        await BroadcastPlayersAsync(game, cancellationToken);
        await StopAutomaticDiscordMuteAsync(id, cancellationToken);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostForceAdvanceAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var game = FindOwned(id);
        if (game is null)
        {
            return NotFound();
        }

        var advancedImmediately = false;

        try
        {
            sessionRegistry.ForceCompleteCurrentRound(game.PublicCode);

            if (game.Session.Players.Count == 0)
            {
                sessionRegistry.AdvanceToNextRound(game.PublicCode);
                advancedImmediately = true;
            }
        }
        catch (GameRuleViolationException)
        {
            TempData["ErrorMessage"] = localizer["GameBoard_AdvanceRoundRejected"].Value;
            return RedirectToPage("Lobby", new { id });
        }

        await BroadcastPlayersAsync(game, cancellationToken);
        await BroadcastBuzzerAsync(game, cancellationToken);
        await BroadcastTimerAsync(game, cancellationToken);
        await StopAutomaticDiscordMuteAsync(id, cancellationToken);

        return advancedImmediately
            ? RedirectToPage(new { id })
            : RedirectToPage("Lobby", new { id });
    }

    private void LoadIntro(
        GameSessionRegistration game,
        Guid id,
        int? categoryIndex,
        bool returning)
    {
        GameId = id;
        IsReturning = returning;
        var session = game.Session;
        var round = session.CurrentRound;
        var categories = GetIntroCategories(session, round, returning);
        CategoryCount = categories.Length;

        if (!categoryIndex.HasValue)
        {
            Heading = ResolveRoundHeading(round, session.CurrentRoundNumber);
            Blocks = FilterDisplayBlocks(round.DescriptionBlocks);
            NextCategoryIndex = categories.Length > 0 ? 0 : null;
            return;
        }

        if (categoryIndex.Value < 0 || categoryIndex.Value >= categories.Length)
        {
            Heading = ResolveRoundHeading(round, session.CurrentRoundNumber);
            Blocks = FilterDisplayBlocks(round.DescriptionBlocks);
            NextCategoryIndex = categories.Length > 0 ? 0 : null;
            return;
        }

        CategoryIndex = categoryIndex.Value;
        var category = categories[categoryIndex.Value];
        Heading = ResolveCategoryHeading(category, categoryIndex.Value + 1);
        Blocks = FilterDisplayBlocks(category.DescriptionBlocks);
        IsFinalCategory = categoryIndex.Value == categories.Length - 1;
        NextCategoryIndex = IsFinalCategory ? null : categoryIndex.Value + 1;
    }

    private static QuizCategoryIntroSnapshot[] GetIntroCategories(
        GameSession session,
        QuizRoundSnapshot round,
        bool returning) => round.CategoryIntros
        .Where(category => !returning || session.Board.Questions.Any(question =>
            question.SourceRoundId == round.SourceRoundId &&
            question.SourceCategoryId == category.SourceCategoryId &&
            question.Status != RuntimeQuestionStatus.Resolved))
        .OrderBy(category => category.SortOrder)
        .ToArray();

    private static IReadOnlyList<ContentBlockSnapshot> FilterDisplayBlocks(
        IEnumerable<ContentBlockSnapshot> blocks) => blocks
        .Where(block => block.Kind switch
        {
            ContentBlockKind.Text => !string.IsNullOrWhiteSpace(block.TextContent),
            ContentBlockKind.Image => block.FileData is { Length: > 0 } ||
                                      !string.IsNullOrWhiteSpace(block.MediaPath),
            _ => false
        })
        .OrderBy(block => block.SortOrder)
        .ToArray();

    private string ResolveRoundHeading(QuizRoundSnapshot round, int position)
    {
        var label = localizer["Label_Round"].Value;
        var title = round.Title.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            return $"{label} {position}";
        }

        if (title.All(char.IsDigit))
        {
            return $"{label} {title}";
        }

        return title;
    }

    private string ResolveCategoryHeading(
        QuizCategoryIntroSnapshot category,
        int position)
    {
        var label = localizer["Label_Category"].Value;
        var title = category.Title.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            return $"{label} {position}";
        }

        if (title.All(char.IsDigit))
        {
            return $"{label} {title}";
        }

        return StartsWithLabel(title, label)
            ? title
            : $"{label}: {title}";
    }

    private static bool StartsWithLabel(string title, string label)
    {
        if (!title.StartsWith(label, StringComparison.CurrentCultureIgnoreCase))
        {
            return false;
        }

        if (title.Length == label.Length)
        {
            return true;
        }

        var next = title[label.Length];
        return char.IsWhiteSpace(next) || char.IsPunctuation(next) || char.IsDigit(next);
    }

    private Task BroadcastTimerAsync(
        GameSessionRegistration game,
        CancellationToken cancellationToken) =>
        gameHub.Clients
            .Group(GameHub.GroupName(game.PublicCode))
            .SendAsync(
                "TimerStateChanged",
                GameHub.CreateTimerUpdate(game),
                cancellationToken);

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
        CancellationToken cancellationToken) =>
        gameHub.Clients
            .Group(GameHub.GroupName(game.PublicCode))
            .SendAsync(
                "PlayersChanged",
                GameHub.CreatePlayersUpdate(sessionRegistry, game),
                cancellationToken);

    private async Task StopAutomaticDiscordMuteAsync(
        Guid gameId,
        CancellationToken cancellationToken)
    {
        var connection = await discordRepository.GetAsync(cancellationToken);
        if (connection is not null)
        {
            await discordMuteCoordinator.SetAutomaticAsync(
                gameId,
                currentHost.RequiredId,
                connection,
                false,
                cancellationToken);
        }
    }

    private GameSessionRegistration? FindOwned(Guid id) =>
        sessionRegistry.FindOwned(new GameSessionId(id), currentHost.RequiredId);
}
