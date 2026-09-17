using BadWolfQuiz.Web.Models;
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
    width: 2.45rem !important;
    min-width: 2.45rem !important;
    height: 2.45rem;
    min-height: 2.45rem;
    padding: 0 !important;
    margin-inline-start: 0.35rem;
    display: inline-flex !important;
    align-items: center;
    justify-content: center;
    flex: 0 0 auto;
    box-sizing: border-box;
    line-height: 1;
}

.question-hint-action-icon {
    display: block;
    width: 1.15rem;
    height: 1.15rem;
    flex: 0 0 auto;
    overflow: visible;
}

.question-hint-reveal-button .question-hint-action-icon {
    transform: translateY(-0.05rem);
}

.question-hint-action-button:disabled {
    opacity: 0.45;
    cursor: wait;
}

.host-game-board > .message.message-error {
    min-height: 2.5rem;
    margin: 0.55rem 0.65rem 0;
    padding-block: 0.55rem;
    display: flex;
    align-items: center;
    box-sizing: border-box;
    line-height: 1.25;
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
        if (!(form instanceof HTMLFormElement)) {
            return;
        }

        event.preventDefault();
        event.stopPropagation();
        setBusy(true);

        try {
            const response = await fetch(target.formAction || form.action, {
                method: (target.formMethod || form.method || "post").toUpperCase(),
                body: new FormData(form),
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
    });
})();
""");
        return script;
    }
}
