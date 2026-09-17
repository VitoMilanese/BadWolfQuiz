using BadWolfQuiz.Web.Pages.Admin.Games;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("section", Attributes = "data-game-id")]
public sealed class QuestionHintGameplayClientAssetsTagHelper : TagHelper
{
    private static readonly object AssetsKey = new();

    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    public override int Order => 1000;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var cssClass =
            context.AllAttributes.TryGetAttribute("class", out var classAttribute)
                ? classAttribute.Value?.ToString()
                : null;
        if (!HasCssClass(cssClass, "host-game-board") ||
            ViewContext.ViewData.Model is not LobbyModel)
        {
            return;
        }

        if (ViewContext.HttpContext.Items.ContainsKey(AssetsKey))
        {
            return;
        }

        ViewContext.HttpContext.Items[AssetsKey] = true;
        output.PostElement.AppendHtml(BuildStyle());
        output.PostElement.AppendHtml(BuildScript());
    }

    private static bool HasCssClass(string? cssClass, string value) =>
        (cssClass ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Contains(value, StringComparer.Ordinal);

    private static TagBuilder BuildStyle()
    {
        var style = new TagBuilder("style");
        style.Attributes["data-question-hint-client-styles"] = string.Empty;
        style.InnerHtml.AppendHtml("""
.question-hint-action-button {
    width: 2.25rem !important;
    min-width: 2.25rem !important;
    max-width: 2.25rem !important;
    height: 2.25rem !important;
    min-height: 2.25rem !important;
    max-height: 2.25rem !important;
    padding: 0 !important;
    margin: 0 !important;
    display: inline-flex !important;
    align-items: center;
    justify-content: center;
    flex: 0 0 2.25rem;
    box-sizing: border-box;
    line-height: 1;
}

.question-hint-action-icon {
    display: block;
    width: 1.05rem;
    height: 1.05rem;
    flex: 0 0 auto;
    overflow: visible;
}

.question-hint-reveal-button .question-hint-action-icon {
    transform: translateY(-0.04rem);
}

.question-hint-action-button:disabled {
    opacity: 0.42;
    cursor: wait;
    filter: saturate(0.6);
}

.host-game-board > .message.message-error {
    position: relative;
    z-index: 1;
    min-height: 2.75rem;
    margin: 0.75rem 0.65rem 0 !important;
    padding: 0.65rem 0.8rem !important;
    display: flex;
    align-items: center;
    box-sizing: border-box;
    line-height: 1.3;
    overflow: visible;
}

.host-game-board > .message.message-error[hidden] {
    display: none !important;
}
""");
        return style;
    }

    private static TagBuilder BuildScript()
    {
        var script = new TagBuilder("script");
        script.Attributes["data-question-hint-client-script"] = string.Empty;
        script.InnerHtml.AppendHtml("""
(() => {
    if (window.badWolfQuestionHintControlsInitialized) {
        return;
    }

    window.badWolfQuestionHintControlsInitialized = true;
    const selector = ".question-hint-action-button";

    const setBusy = busy => {
        for (const button of document.querySelectorAll(selector)) {
            button.disabled = busy;
            if (busy) {
                button.setAttribute("aria-busy", "true");
            } else {
                button.removeAttribute("aria-busy");
            }
        }
    };

    const hideError = () => {
        const error = document.getElementById("game-board-error");
        if (!error) {
            return;
        }

        error.hidden = true;
        error.textContent = "";
    };

    const showError = message => {
        const error = document.getElementById("game-board-error");
        if (!error) {
            console.error(message);
            return;
        }

        error.textContent = message;
        error.hidden = false;
    };

    const responseError = async response => {
        const contentType = response.headers.get("content-type") ?? "";
        if (contentType.includes("application/json")) {
            const payload = await response.json().catch(() => null);
            return payload?.error ?? payload?.message ?? response.statusText;
        }

        return response.statusText;
    };

    document.addEventListener("click", async event => {
        const target = event.target instanceof Element
            ? event.target.closest(selector)
            : null;
        if (!(target instanceof HTMLButtonElement) || target.disabled) {
            return;
        }

        const form = target.form;
        const actionUrl = target.dataset.questionHintActionUrl;
        if (!(form instanceof HTMLFormElement) || !actionUrl) {
            return;
        }

        event.preventDefault();
        event.stopPropagation();
        setBusy(true);
        hideError();

        try {
            const response = await fetch(actionUrl, {
                method: "POST",
                body: new FormData(form),
                credentials: "same-origin",
                headers: {
                    "Accept": "application/json, text/html",
                    "X-Requested-With": "XMLHttpRequest"
                },
                cache: "no-store"
            });

            if (!response.ok) {
                throw new Error(await responseError(response));
            }

            if (window.BadWolfHostGameplay?.refresh) {
                await window.BadWolfHostGameplay.refresh();
            } else {
                window.location.reload();
            }
        } catch (error) {
            console.error("Question hint reveal failed.", error);
            showError(error?.message ?? "Question hint reveal failed.");
        } finally {
            setBusy(false);
        }
    }, true);
})();
""");
        return script;
    }
}
