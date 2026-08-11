using System.Globalization;
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

namespace BadWolfQuiz.Web.Pages.Admin.Games;

public sealed class RoundIntroModel(
    QuizDbContext db,
    GameSessionRegistry sessionRegistry,
    CurrentHost currentHost,
    AvatarCatalog avatarCatalog,
    MediaUploadProcessor mediaUploadProcessor,
    PremiumHostAccess premiumHostAccess,
    IHubContext<GameHub> gameHub,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    [BindProperty]
    public GameSettingsInput SettingsInput { get; set; } = new();

    [BindProperty]
    public IFormFile? HostImage { get; set; }

    public Guid GameId { get; private set; }
    public string Heading { get; private set; } = string.Empty;
    public IReadOnlyList<ContentBlockSnapshot> Blocks { get; private set; } = [];
    public int CategoryCount { get; private set; }
    public int? NextCategoryIndex { get; private set; }
    public bool IsFinalCategory { get; private set; }

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

    public IActionResult OnGet(Guid id, int? category)
    {
        var game = FindOwned(id);
        if (game is null)
        {
            return NotFound();
        }

        if (game.Session.Status != GameSessionStatus.Lobby)
        {
            return RedirectToPage("Lobby", new { id });
        }

        LoadIntro(game, id, category);
        return Page();
    }

    public IActionResult OnGetContentBlock(Guid id, int sourceContentBlockId)
    {
        var game = FindOwned(id);
        if (game is null)
        {
            return NotFound();
        }

        var round = game.Session.CurrentRound;
        var block = round.DescriptionBlocks
            .Concat(round.CategoryIntros.SelectMany(category => category.DescriptionBlocks))
            .SingleOrDefault(item => item.SourceContentBlockId == sourceContentBlockId);

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

    public async Task<IActionResult> OnPostPrepareAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var game = FindOwned(id);
        if (game is null)
        {
            return NotFound();
        }

        if (game.Session.Status != GameSessionStatus.Lobby)
        {
            return RedirectToPage("Lobby", new { id });
        }

        var settings = await BuildSettingsAsync(game.Session.Settings, cancellationToken);
        if (settings is null)
        {
            return RedirectToPage("Lobby", new { id });
        }

        try
        {
            sessionRegistry.UpdateSettings(game.PublicCode, settings);
        }
        catch (GameRuleViolationException)
        {
            TempData["ErrorMessage"] = localizer["GameSettings_GameLocked"].Value;
            return RedirectToPage("Lobby", new { id });
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostStartAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var game = FindOwned(id);
        if (game is null)
        {
            return NotFound();
        }

        if (game.Session.Status != GameSessionStatus.Lobby)
        {
            return RedirectToPage("Lobby", new { id });
        }

        try
        {
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
            return RedirectToPage("Lobby", new { id });
        }

        await gameHub.Clients
            .Group(GameHub.GroupName(game.PublicCode))
            .SendAsync(
                "GameStatusChanged",
                GameHub.CreateStatusUpdate(game),
                cancellationToken);

        return RedirectToPage("Lobby", new { id });
    }

    private void LoadIntro(
        GameSessionRegistration game,
        Guid id,
        int? categoryIndex)
    {
        GameId = id;
        var session = game.Session;
        var round = session.CurrentRound;
        var categories = round.CategoryIntros
            .OrderBy(category => category.SortOrder)
            .ToArray();
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

        var category = categories[categoryIndex.Value];
        Heading = ResolveCategoryHeading(category, categoryIndex.Value + 1);
        Blocks = FilterDisplayBlocks(category.DescriptionBlocks);
        IsFinalCategory = categoryIndex.Value == categories.Length - 1;
        NextCategoryIndex = IsFinalCategory ? null : categoryIndex.Value + 1;
    }

    private IReadOnlyList<ContentBlockSnapshot> FilterDisplayBlocks(
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

    private GameSessionRegistration? FindOwned(Guid id) =>
        sessionRegistry.FindOwned(new GameSessionId(id), currentHost.RequiredId);

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
}
