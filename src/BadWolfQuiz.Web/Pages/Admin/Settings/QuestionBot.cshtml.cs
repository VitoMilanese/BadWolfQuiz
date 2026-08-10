using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BadWolfQuiz.Web.Localization;
using Microsoft.Extensions.Localization;

namespace BadWolfQuiz.Web.Pages.Admin.Settings;

public sealed class QuestionBotModel(
    DiscordQuestionBotService bot,
    DiscordQuestionBotSettingsRepository repository,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    public IReadOnlyList<DiscordGuildOption> Guilds { get; private set; } = [];
    public IReadOnlyList<DiscordTextChannelOption> Channels { get; private set; } = [];

    [BindProperty]
    public string? GuildId { get; set; }

    [BindProperty]
    public string? ChannelId { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
    }

    public JsonResult OnGetChannels(string guildId)
    {
        return new JsonResult(bot.GetTextChannels(guildId));
    }

    public async Task<IActionResult> OnPostSaveAsync(
        bool embedded,
        CancellationToken cancellationToken)
    {
        var guild = GuildId is null
            ? null
            : bot.GetGuilds().SingleOrDefault(x => x.Id == GuildId);

        var channel = GuildId is null || ChannelId is null
            ? null
            : bot.GetTextChannels(GuildId).SingleOrDefault(x => x.Id == ChannelId);

        if (guild is null || channel is null)
        {
            TempData["QuestionBotError"] = localizer["QuestionBot_SelectServerAndChannel"].Value;
            return RedirectToPage(new { embedded });
        }

        await repository.SaveAsync(
            guild.Id,
            guild.Name,
            channel.Id,
            channel.Name,
            cancellationToken);

        TempData["QuestionBotSuccess"] = localizer["QuestionBot_Saved"].Value;
        return RedirectToPage(new { embedded });
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var settings = await repository.GetAsync(cancellationToken);
        GuildId = settings?.GuildId;
        ChannelId = settings?.ChannelId;
        Guilds = bot.GetGuilds();
        Channels = GuildId is null ? [] : bot.GetTextChannels(GuildId);
    }
}
