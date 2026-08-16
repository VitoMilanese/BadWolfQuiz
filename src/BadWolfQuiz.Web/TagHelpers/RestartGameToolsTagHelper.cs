using System.Globalization;
using System.Text.Encodings.Web;
using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("div", Attributes = "class")]
public sealed class RestartGameToolsTagHelper(
    IAntiforgery antiforgery,
    GameSessionRegistry sessionRegistry,
    CurrentHost currentHost) : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (!string.Equals(
                ViewContext.RouteData.Values["page"]?.ToString(),
                "/Admin/Games/Lobby",
                StringComparison.Ordinal) ||
            !Guid.TryParse(
                ViewContext.RouteData.Values["id"]?.ToString(),
                out var gameId))
        {
            return;
        }

        var classes = (output.Attributes["class"]?.Value?.ToString() ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var isToolsPopover = classes.Contains(
            "action-menu-popover",
            StringComparer.Ordinal);
        var isHeaderContext = classes.Contains(
            "game-header-context",
            StringComparer.Ordinal);

        if (!isToolsPopover && !isHeaderContext)
        {
            return;
        }

        var game = sessionRegistry.FindOwned(
            new GameSessionId(gameId),
            currentHost.RequiredId);
        if (game is null || !CanRestart(game.Session.Status))
        {
            return;
        }

        var labels = RestartLabels.ForCurrentLanguage();
        var html = HtmlEncoder.Default;

        if (isToolsPopover)
        {
            if (game.Session.Status == GameSessionStatus.Running)
            {
                output.PostContent.AppendHtml($$"""
                    <button class="action-menu-item"
                            type="button"
                            data-open-restart-game-dialog>
                        {{html.Encode(labels.Restart)}}
                    </button>
                    """);
            }

            return;
        }

        var requestToken = antiforgery.GetAndStoreTokens(ViewContext.HttpContext).RequestToken;
        if (string.IsNullOrWhiteSpace(requestToken))
        {
            return;
        }

        if (game.Session.Status != GameSessionStatus.Running)
        {
            output.PostContent.AppendHtml($$"""
                <details class="action-menu board-action-menu">
                    <summary class="button button-secondary action-menu-trigger">
                        {{html.Encode(labels.Tools)}}
                    </summary>
                    <div class="action-menu-popover">
                        <button class="action-menu-item"
                                type="button"
                                data-open-restart-game-dialog>
                            {{html.Encode(labels.Restart)}}
                        </button>
                    </div>
                </details>
                """);
        }

        output.PostContent.AppendHtml($$"""
            <dialog id="restart-game-dialog"
                    class="app-dialog"
                    aria-labelledby="restart-game-dialog-title">
                <form method="post"
                      action="/Admin/Games/Restart/{{gameId:D}}"
                      class="dialog-card dialog-card-danger">
                    <input type="hidden"
                           name="__RequestVerificationToken"
                           value="{{html.Encode(requestToken)}}" />

                    <div class="dialog-heading">
                        <h2 id="restart-game-dialog-title">{{html.Encode(labels.Title)}}</h2>
                        <button class="dialog-close"
                                type="button"
                                data-close-restart-game-dialog
                                aria-label="{{html.Encode(labels.Close)}}">×</button>
                    </div>

                    <p class="dialog-warning">{{html.Encode(labels.Warning)}}</p>

                    <div class="form-actions dialog-actions">
                        <button class="button button-secondary"
                                type="button"
                                data-close-restart-game-dialog>
                            {{html.Encode(labels.Cancel)}}
                        </button>
                        <button class="button button-danger" type="submit">
                            {{html.Encode(labels.Restart)}}
                        </button>
                    </div>
                </form>
            </dialog>

            <script>
                (() => {
                    const dialog = document.getElementById("restart-game-dialog");
                    if (!dialog) return;

                    for (const button of document.querySelectorAll(
                        "[data-open-restart-game-dialog]")) {
                        button.addEventListener("click", () => {
                            button.closest("details")?.removeAttribute("open");
                            dialog.showModal();
                        });
                    }

                    for (const button of dialog.querySelectorAll(
                        "[data-close-restart-game-dialog]")) {
                        button.addEventListener("click", () => dialog.close());
                    }

                    dialog.addEventListener("click", event => {
                        if (event.target === dialog) {
                            dialog.close();
                        }
                    });
                })();
            </script>
            """);
    }

    private static bool CanRestart(GameSessionStatus status) =>
        status is GameSessionStatus.Running or
            GameSessionStatus.FinalWagering or
            GameSessionStatus.FinalAnswering or
            GameSessionStatus.FinalJudging;

    private sealed record RestartLabels(
        string Tools,
        string Restart,
        string Title,
        string Warning,
        string Cancel,
        string Close)
    {
        public static RestartLabels ForCurrentLanguage() =>
            CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
            {
                "uk" => new(
                    "Інструменти",
                    "Перезапустити гру",
                    "Перезапустити гру?",
                    "Поточний прогрес гри та рахунки всіх гравців буде скинуто. Цю дію не можна скасувати.",
                    "Скасувати",
                    "Закрити"),
                "it" => new(
                    "Strumenti",
                    "Riavvia partita",
                    "Riavviare la partita?",
                    "I progressi attuali e i punteggi di tutti i giocatori verranno azzerati. Questa azione non può essere annullata.",
                    "Annulla",
                    "Chiudi"),
                "ru" => new(
                    "Україна",
                    "Україна",
                    "Україна",
                    "Україна",
                    "Україна",
                    "Україна"),
                _ => new(
                    "Tools",
                    "Restart game",
                    "Restart game?",
                    "Current game progress and every player's score will be reset. This action cannot be undone.",
                    "Cancel",
                    "Close")
            };
    }
}
