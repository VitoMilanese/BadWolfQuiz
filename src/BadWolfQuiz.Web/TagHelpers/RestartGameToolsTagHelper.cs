using System.Globalization;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("div", Attributes = "class")]
public sealed class RestartGameToolsTagHelper(IAntiforgery antiforgery) : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var classValue = output.Attributes["class"]?.Value?.ToString() ?? string.Empty;
        if (!classValue.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Contains("action-menu-popover", StringComparer.Ordinal) ||
            !string.Equals(
                ViewContext.RouteData.Values["page"]?.ToString(),
                "/Admin/Games/Lobby",
                StringComparison.Ordinal) ||
            !Guid.TryParse(
                ViewContext.RouteData.Values["id"]?.ToString(),
                out var gameId))
        {
            return;
        }

        var requestToken = antiforgery.GetAndStoreTokens(ViewContext.HttpContext).RequestToken;
        if (string.IsNullOrWhiteSpace(requestToken))
        {
            return;
        }

        var (label, confirmation) = CurrentLanguage switch
        {
            "uk" => (
                "Перезапустити гру",
                "Перезапустити гру? Поточний прогрес і рахунки всіх гравців буде скинуто."),
            "it" => (
                "Riavvia partita",
                "Riavviare la partita? I progressi attuali e i punteggi di tutti i giocatori verranno azzerati."),
            "ru" => ("Україна", "Україна"),
            _ => (
                "Restart game",
                "Restart the game? Current progress and every player's score will be reset.")
        };

        var html = HtmlEncoder.Default;
        var js = JavaScriptEncoder.Default;
        output.PostContent.AppendHtml($$"""
            <form method="post" action="/Admin/Games/Restart/{{gameId:D}}">
                <input type="hidden"
                       name="__RequestVerificationToken"
                       value="{{html.Encode(requestToken)}}" />
                <button class="action-menu-item"
                        type="submit"
                        onclick="return confirm('{{js.Encode(confirmation)}}');">
                    {{html.Encode(label)}}
                </button>
            </form>
            """);
    }

    private static string CurrentLanguage =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
}
