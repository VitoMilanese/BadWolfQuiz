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

.question-presentation:has(> .question-hints-panel) {
    min-height: 0;
}

.question-presentation:has(> .question-hints-panel) > .game-content-blocks {
    min-height: 0;
    overflow: auto;
}

.question-presentation > .question-hints-panel {
    position: relative;
    z-index: 1;
    flex: 0 0 auto;
    width: min(100%, 84rem);
    max-height: min(34dvh, 20rem);
    margin: clamp(0.55rem, 1vh, 0.9rem) auto 0;
    padding: clamp(0.55rem, 0.9vw, 0.8rem);
    overflow-x: hidden;
    overflow-y: auto;
    border: 1px solid var(--line);
    border-radius: 0.9rem 0.9rem 0 0;
    background: var(--panel-2);
    box-shadow: 0 -0.8rem 2rem rgb(0 0 0 / 12%);
    animation: question-hints-panel-rise 180ms ease-out both;
}

.question-hints-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(min(16rem, 100%), 1fr));
    gap: 0.7rem;
}

.question-hint-item {
    min-width: 0;
    display: grid;
    place-items: center;
    gap: 0.35rem;
    padding: clamp(0.55rem, 0.9vw, 0.8rem);
    border: 1px solid var(--line);
    border-radius: 0.75rem;
    background: var(--panel);
    text-align: center;
}

.question-hint-text,
.question-hint-caption {
    margin: 0;
    overflow-wrap: anywhere;
}

.question-hint-text {
    font-size: clamp(1.15rem, 2vw, 2rem);
}

.question-hint-caption {
    color: var(--muted);
    font-size: 0.9rem;
}

.question-hint-image {
    display: block;
    width: auto;
    max-width: 100%;
    max-height: min(20dvh, 11rem);
    object-fit: contain;
}

@keyframes question-hints-panel-rise {
    from { transform: translateY(1.25rem); opacity: 0; }
    to { transform: translateY(0); opacity: 1; }
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
    let latestRevealState = null;

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

    const responsePayload = async response => {
        const contentType = response.headers.get("content-type") ?? "";
        if (!contentType.includes("application/json")) {
            return null;
        }

        return response.json().catch(() => null);
    };

    const appendCaption = (item, value, cssClass) => {
        if (!value) {
            return;
        }

        const caption = document.createElement("p");
        caption.className = `question-hint-caption ${cssClass}`;
        caption.textContent = value;
        item.append(caption);
    };

    const findQuestionPresentation = sourceQuestionId => {
        const blocks = document.querySelector(
            `[data-host-gameplay-view] .question-presentation [data-source-question-id="${sourceQuestionId}"]`);
        return blocks?.closest(".question-presentation") ?? null;
    };

    const renderHintPanel = payload => {
        const presentation = findQuestionPresentation(payload?.sourceQuestionId);
        if (!presentation || !Array.isArray(payload?.revealedHints)) {
            return;
        }

        const panel = document.createElement("section");
        panel.className = "question-hints-panel player-all-player-panel";
        panel.dataset.questionHintsPanel = "";
        panel.dataset.sourceQuestionId = payload.sourceQuestionId.toString();
        panel.setAttribute("aria-label", payload.hintsLabel ?? "Hints");

        const grid = document.createElement("div");
        grid.className = "question-hints-grid";

        for (const hint of payload.revealedHints) {
            const item = document.createElement("article");
            item.className = "question-hint-item";

            appendCaption(item, hint.topCaption, "question-hint-caption-top");

            if (hint.blockType === "image" && hint.imageUrl) {
                const image = document.createElement("img");
                image.className = "game-content-image question-hint-image";
                image.src = hint.imageUrl;
                image.alt = hint.topCaption ?? hint.bottomCaption ?? "";
                image.loading = "eager";
                item.append(image);
            } else {
                const text = document.createElement("p");
                text.className = "game-content-text question-hint-text";
                text.textContent = hint.textContent ?? "";
                item.append(text);
            }

            appendCaption(item, hint.bottomCaption, "question-hint-caption-bottom");
            grid.append(item);
        }

        panel.append(grid);

        const existing = presentation.querySelector(
            ":scope > [data-question-hints-panel]");
        if (existing) {
            existing.replaceWith(panel);
        } else {
            presentation.append(panel);
        }
    };

    const syncButtons = payload => {
        if (!payload || !findQuestionPresentation(payload.sourceQuestionId)) {
            return;
        }

        if (payload.revealedHintCount >= payload.totalHintCount) {
            for (const button of document.querySelectorAll(selector)) {
                button.remove();
            }
            return;
        }

        if (!payload.showAnotherHintLabel) {
            return;
        }

        for (const button of document.querySelectorAll(
            ".question-hint-reveal-button")) {
            button.title = payload.showAnotherHintLabel;
            button.setAttribute("aria-label", payload.showAnotherHintLabel);
        }
    };

    const syncReward = payload => {
        const presentation = findQuestionPresentation(payload?.sourceQuestionId);
        const heading = presentation?.querySelector("[data-question-heading]");
        if (!heading || !Number.isFinite(payload?.rewardValue)) {
            return;
        }

        heading.dataset.currentReward = payload.rewardValue.toString();
        const template = heading.dataset.rewardTemplate;
        if (template) {
            heading.textContent = template.replace(
                "__REWARD__",
                payload.rewardValue.toString());
        }
    };

    const applyRevealState = payload => {
        renderHintPanel(payload);
        syncButtons(payload);
        syncReward(payload);
    };

    document.addEventListener("badwolf:host-gameplay-updated", () => {
        if (!latestRevealState) {
            return;
        }

        if (!findQuestionPresentation(latestRevealState.sourceQuestionId)) {
            latestRevealState = null;
            return;
        }

        applyRevealState(latestRevealState);
    });

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
        event.stopImmediatePropagation();
        setBusy(true);
        hideError();

        try {
            const response = await fetch(actionUrl, {
                method: "POST",
                body: new FormData(form),
                credentials: "same-origin",
                headers: {
                    "Accept": "application/json",
                    "X-Requested-With": "XMLHttpRequest"
                },
                cache: "no-store"
            });
            const payload = await responsePayload(response);

            if (!response.ok) {
                throw new Error(
                    payload?.error ?? payload?.message ?? response.statusText);
            }
            if (!payload) {
                throw new Error("Question hint reveal returned no state.");
            }

            latestRevealState = payload;
            applyRevealState(payload);
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
