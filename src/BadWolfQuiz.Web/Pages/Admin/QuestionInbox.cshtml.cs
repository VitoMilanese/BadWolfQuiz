using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace BadWolfQuiz.Web.Pages.Admin;

public sealed class QuestionInboxModel(
    QuizDbContext db,
    UserQuestionDeletionService deletionService,
    DiscordQuestionBotService questionBot,
    DiscordQuestionBotSettingsRepository questionBotSettingsRepository,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    public IReadOnlyList<UserQuestion> Questions { get; private set; } = [];
    public IReadOnlyList<DiscordGuildOption> QuestionBotGuilds { get; private set; } = [];
    public IReadOnlyList<DiscordTextChannelOption> QuestionBotChannels { get; private set; } = [];

    [BindProperty(SupportsGet = true)]
    public int? QuestionId { get; set; }

    [BindProperty]
    public string? QuestionBotGuildId { get; set; }

    [BindProperty]
    public string? QuestionBotChannelId { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
    }

    public JsonResult OnGetQuestionBotChannels(string guildId)
    {
        return new JsonResult(questionBot.GetTextChannels(guildId));
    }

    public async Task<IActionResult> OnPostSaveQuestionBotAsync(CancellationToken cancellationToken)
    {
        var guild = QuestionBotGuildId is null
            ? null
            : questionBot.GetGuilds().SingleOrDefault(x => x.Id == QuestionBotGuildId);

        var channel = QuestionBotGuildId is null || QuestionBotChannelId is null
            ? null
            : questionBot.GetTextChannels(QuestionBotGuildId)
                .SingleOrDefault(x => x.Id == QuestionBotChannelId);

        if (guild is null || channel is null)
        {
            TempData["QuestionBotError"] = localizer["QuestionBot_SelectServerAndChannel"].Value;
            return RedirectToPage(new { questionId = QuestionId, openQuestionBotSettings = true });
        }

        await questionBotSettingsRepository.SaveAsync(
            guild.Id,
            guild.Name,
            channel.Id,
            channel.Name,
            cancellationToken);

        TempData["QuestionBotSuccess"] = localizer["QuestionBot_Saved"].Value;
        return RedirectToPage(new { questionId = QuestionId, openQuestionBotSettings = true });
    }

    public async Task<IActionResult> OnPostAnswerAsync(
        int id,
        string answer,
        CancellationToken cancellationToken)
    {
        answer = answer?.Trim() ?? string.Empty;

        if (answer.Length is < 1 or > 5000)
        {
            ModelState.AddModelError(
                string.Empty,
                "Відповідь має містити від 1 до 5000 символів.");

            await LoadAsync(cancellationToken);
            return Page();
        }

        var question = await db.UserQuestions
            .Include(x => x.Messages)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (question is null)
        {
            return NotFound();
        }

        var now = DateTimeOffset.UtcNow;
        question.Messages.Add(new UserQuestionMessage
        {
            AuthorType = UserQuestionAuthorType.Developer,
            Text = answer,
            CreatedAtUtc = now
        });
        question.UpdatedAtUtc = now;

        await db.SaveChangesAsync(cancellationToken);
        return RedirectToPage(new { questionId = id });
    }

    public async Task<IActionResult> OnPostDeleteAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var deleted = await deletionService.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            return NotFound();
        }

        return RedirectToPage();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        await LoadQuestionsAsync(cancellationToken);
        await LoadQuestionBotSettingsAsync(cancellationToken);
    }

    private async Task LoadQuestionBotSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await questionBotSettingsRepository.GetAsync(cancellationToken);
        QuestionBotGuildId = settings?.GuildId;
        QuestionBotChannelId = settings?.ChannelId;
        QuestionBotGuilds = questionBot.GetGuilds();
        QuestionBotChannels = QuestionBotGuildId is null
            ? []
            : questionBot.GetTextChannels(QuestionBotGuildId);
    }

    private async Task LoadQuestionsAsync(CancellationToken cancellationToken)
    {
        var query = db.UserQuestions
            .AsNoTracking()
            .Include(x => x.Messages)
            .AsQueryable();

        if (QuestionId is not null)
        {
            query = query.Where(x => x.Id == QuestionId.Value);
        }

        var questions = await query.ToListAsync(cancellationToken);

        Questions = questions
            .OrderBy(x =>
                x.Messages
                    .OrderByDescending(message => message.CreatedAtUtc)
                    .ThenByDescending(message => message.Id)
                    .FirstOrDefault()?.AuthorType
                == UserQuestionAuthorType.Developer)
            .ThenByDescending(x => x.UpdatedAtUtc)
            .ToList();
    }
}
