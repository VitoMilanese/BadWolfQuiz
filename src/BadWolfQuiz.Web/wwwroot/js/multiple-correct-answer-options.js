(() => {
    "use strict";

    if (window.badWolfMultipleCorrectAnswerOptionsLoaded) {
        return;
    }
    window.badWolfMultipleCorrectAnswerOptionsLoaded = true;

    const culture = (document.documentElement.lang || "en").toLowerCase();
    const text = culture.startsWith("uk")
        ? {
            correct: "Правильна",
            hint: "Позначте один або кілька правильних варіантів. Будь-який позначений варіант зараховується як правильна відповідь."
        }
        : culture.startsWith("it")
            ? {
                correct: "Corretta",
                hint: "Contrassegna una o più opzioni corrette. Qualsiasi opzione contrassegnata viene accettata come risposta corretta."
            }
            : culture.startsWith("ru")
                ? {
                    correct: "Україна",
                    hint: "Україна"
                }
                : {
                    correct: "Correct",
                    hint: "Mark one or more correct options. Any marked option is accepted as a correct answer."
                };

    const style = document.createElement("style");
    style.id = "multiple-correct-answer-options-styles";
    style.textContent = `
form.multiple-correct-answer-options-active
    .multiple-choice-answer-option-correct-badge {
    display: none !important;
}
.multiple-correct-answer-option-toggle {
    display: inline-flex;
    align-items: center;
    gap: .35rem;
    margin-right: auto;
    padding: .2rem .5rem;
    border: 1px solid color-mix(in srgb, #2e7d32 70%, var(--line));
    border-radius: 999px;
    color: color-mix(in srgb, #66bb6a 84%, var(--text));
    font-size: .78rem;
    font-weight: 800;
    cursor: pointer;
    user-select: none;
}
.multiple-correct-answer-option-toggle input {
    margin: 0;
}
.multiple-correct-answer-option-toggle:has(input:disabled) {
    cursor: default;
    opacity: .85;
}
.multiple-choice-answer-option-child.multiple-correct-answer-option-selected {
    border-left-color: #2e7d32 !important;
    box-shadow: inset 0 0 0 1px color-mix(in srgb, #2e7d32 55%, transparent);
}
`;
    document.head.appendChild(style);

    const isCard = element =>
        element instanceof HTMLElement &&
        element.classList.contains("content-block-card");

    const parseState = (value, fallbackCount) => {
        const raw = (value ?? "").trim();
        const parts = raw.split("|", 2);
        const parsedCount = Number.parseInt(parts[0] ?? "", 10);
        const count = Number.isInteger(parsedCount) && parsedCount >= 0
            ? parsedCount
            : Math.max(0, fallbackCount);

        if (parts.length < 2) {
            return {
                count,
                correctIndexes: count > 0 ? [0] : []
            };
        }

        const correctIndexes = parts[1]
            .split(",")
            .map(token => Number.parseInt(token.trim(), 10))
            .filter(index => Number.isInteger(index) && index >= 0 && index < count);
        return {
            count,
            correctIndexes: Array.from(new Set(correctIndexes)).sort((a, b) => a - b)
        };
    };

    const serializeState = (count, correctIndexes) => {
        const normalized = Array.from(new Set(correctIndexes))
            .filter(index => Number.isInteger(index) && index >= 0 && index < count)
            .sort((a, b) => a - b);
        if (normalized.length === 1 && normalized[0] === 0) {
            return String(count);
        }
        return `${count}|${normalized.join(",")}`;
    };

    const initialize = () => {
        const form = document.querySelector("form[data-ajax-question-editor]");
        const select = document.getElementById("Input_PresentationType");
        const answerSection = document.getElementById("answer-blocks");
        const api = window.BadWolfMultipleChoiceAnswerOptions;
        if (!(form instanceof HTMLFormElement) ||
            !(select instanceof HTMLSelectElement) ||
            !(answerSection instanceof HTMLElement) ||
            !api) {
            window.setTimeout(initialize, 25);
            return;
        }

        const getMarker = () => api.getMarker?.() ?? null;
        const getOptionCards = () => (api.getOptionCards?.() ?? []).filter(isCard);
        const getAdditionalCards = () => (api.getAdditionalCards?.() ?? []).filter(isCard);
        const isAllPlayerChoice = () => select.value === "3";
        let syncTimer = null;
        let previewCapture = null;

        const getStateInput = marker => marker?.querySelector(
            ".multiple-choice-answer-options-count") ?? null;

        const seedCardState = () => {
            const marker = getMarker();
            const options = getOptionCards();
            if (!marker || options.length === 0) {
                return;
            }

            if (marker.dataset.multipleCorrectStateInitialized !== "true") {
                const stateInput = getStateInput(marker);
                const stored = marker.dataset.answerOptionsStoredState ||
                    stateInput?.value ||
                    String(options.length);
                const state = parseState(stored, options.length);
                const correctSet = new Set(state.correctIndexes);
                options.forEach((card, index) => {
                    card.dataset.multipleCorrectAnswerOption =
                        correctSet.has(index) ? "true" : "false";
                });
                marker.dataset.multipleCorrectStateInitialized = "true";
            } else {
                options.forEach(card => {
                    if (!Object.hasOwn(card.dataset, "multipleCorrectAnswerOption")) {
                        card.dataset.multipleCorrectAnswerOption = "false";
                    }
                });
            }

            if (!options.some(card =>
                    card.dataset.multipleCorrectAnswerOption === "true")) {
                options[0].dataset.multipleCorrectAnswerOption = "true";
            }
        };

        const removeToggle = card => {
            card.querySelector(".multiple-correct-answer-option-toggle")?.remove();
            card.classList.remove("multiple-correct-answer-option-selected");
        };

        const renderOptionUi = () => {
            const marker = getMarker();
            const options = getOptionCards();
            form.classList.toggle(
                "multiple-correct-answer-options-active",
                isAllPlayerChoice());

            const toolbarHint = marker?.querySelector(
                ".content-block-container-toolbar > span");
            if (toolbarHint instanceof HTMLElement) {
                if (!toolbarHint.dataset.singleCorrectHint) {
                    toolbarHint.dataset.singleCorrectHint = toolbarHint.textContent ?? "";
                }
                toolbarHint.textContent = isAllPlayerChoice()
                    ? text.hint
                    : toolbarHint.dataset.singleCorrectHint;
            }

            const typeHelp = select.closest(".question-type-setting")
                ?.querySelectorAll(".multiple-choice-answer-options-help");
            if (isAllPlayerChoice() && typeHelp && typeHelp.length >= 2) {
                typeHelp[1].textContent = text.hint;
            }

            if (!isAllPlayerChoice()) {
                options.forEach(removeToggle);
                return;
            }

            seedCardState();
            const correctCount = options.filter(card =>
                card.dataset.multipleCorrectAnswerOption === "true").length;

            options.forEach(card => {
                const isCorrect = card.dataset.multipleCorrectAnswerOption === "true";
                card.classList.toggle(
                    "multiple-correct-answer-option-selected",
                    isCorrect);
                const toolbar = card.querySelector(".content-block-toolbar");
                if (!(toolbar instanceof HTMLElement)) {
                    return;
                }

                let label = toolbar.querySelector(
                    ".multiple-correct-answer-option-toggle");
                if (!(label instanceof HTMLLabelElement)) {
                    label = document.createElement("label");
                    label.className = "multiple-correct-answer-option-toggle";
                    const checkbox = document.createElement("input");
                    checkbox.type = "checkbox";
                    checkbox.setAttribute("aria-label", text.correct);
                    const caption = document.createElement("span");
                    caption.textContent = text.correct;
                    label.append(checkbox, caption);
                    toolbar.prepend(label);

                    checkbox.addEventListener("change", () => {
                        if (!checkbox.checked) {
                            const otherCorrect = getOptionCards().some(option =>
                                option !== card &&
                                option.dataset.multipleCorrectAnswerOption === "true");
                            if (!otherCorrect) {
                                checkbox.checked = true;
                                return;
                            }
                        }

                        card.dataset.multipleCorrectAnswerOption =
                            checkbox.checked ? "true" : "false";
                        syncState();
                    });
                }

                const checkbox = label.querySelector('input[type="checkbox"]');
                if (checkbox instanceof HTMLInputElement) {
                    checkbox.checked = isCorrect;
                    checkbox.disabled = isCorrect && correctCount <= 1;
                }
            });
        };

        const syncState = () => {
            const marker = getMarker();
            const options = getOptionCards();
            if (!marker) {
                return;
            }

            if (!isAllPlayerChoice()) {
                renderOptionUi();
                return;
            }

            seedCardState();
            const correctIndexes = options
                .map((card, index) =>
                    card.dataset.multipleCorrectAnswerOption === "true"
                        ? index
                        : -1)
                .filter(index => index >= 0);
            const value = serializeState(options.length, correctIndexes);
            const stateInput = getStateInput(marker);
            if (stateInput instanceof HTMLInputElement) {
                stateInput.value = value;
            }
            marker.dataset.answerOptionsStoredState = value;
            renderOptionUi();
        };

        const scheduleSync = () => {
            if (syncTimer !== null) {
                window.clearTimeout(syncTimer);
            }
            syncTimer = window.setTimeout(() => {
                syncTimer = null;
                syncState();
            }, 0);
        };

        const cardProducesPreview = card => {
            switch (card?.dataset.blockType) {
                case "Text":
                    return Boolean(card.querySelector('[name$=".TextContent"]')
                        ?.value?.trim());
                case "Image": {
                    const preview = card.querySelector(".unified-file-preview");
                    const image = card.querySelector(
                        ".unified-image-preview-element");
                    return Boolean(preview && !preview.hidden && image?.getAttribute("src"));
                }
                case "Audio": {
                    const preview = card.querySelector(".unified-file-preview");
                    const audio = card.querySelector(
                        ".unified-audio-preview-element");
                    return Boolean(preview && !preview.hidden && audio?.getAttribute("src"));
                }
                case "Video":
                case "YouTube":
                    return Boolean(card.querySelector('[name$=".ExternalUrl"]')
                        ?.value?.trim());
                default:
                    return false;
            }
        };

        const captureAnswerPreview = () => {
            if (!isAllPlayerChoice()) {
                previewCapture = null;
                return;
            }
            const previewRoot = document.getElementById("question-preview-content");
            if (!previewRoot) {
                previewCapture = null;
                return;
            }

            const leaves = Array.from(previewRoot.querySelectorAll(
                ".question-preview-text, .question-preview-media"));
            const previewByCard = new Map();
            let leafIndex = 0;
            answerSection.querySelectorAll(".content-block-card").forEach(card => {
                if (card.dataset.blockType === "Container" ||
                    card.dataset.blockType === "AnswerOptions" ||
                    !cardProducesPreview(card)) {
                    return;
                }
                const leaf = leaves[leafIndex++];
                if (leaf) {
                    previewByCard.set(card, leaf);
                }
            });
            previewCapture = { previewRoot, previewByCard, leaves };
        };

        const rebuildAnswerPreview = () => {
            if (!isAllPlayerChoice() || !previewCapture) {
                return;
            }
            seedCardState();
            const { previewRoot, previewByCard, leaves } = previewCapture;
            leaves.forEach(leaf => leaf.classList.remove(
                "all-player-answer-option",
                "all-player-answer-option-correct",
                "all-player-answer-option-incorrect"));

            const fragment = document.createDocumentFragment();
            for (const card of getOptionCards()) {
                if (card.dataset.multipleCorrectAnswerOption !== "true") {
                    continue;
                }
                const preview = previewByCard.get(card);
                if (preview) {
                    preview.classList.add(
                        "all-player-answer-option",
                        "all-player-answer-option-correct");
                    fragment.appendChild(preview);
                }
            }

            for (const card of getAdditionalCards()) {
                if (card.dataset.blockType === "Container") {
                    const childCards = Array.from(card.querySelector(
                        "[data-content-block-container-children]")?.children ?? [])
                        .filter(isCard);
                    const childPreviews = childCards
                        .map(child => previewByCard.get(child))
                        .filter(Boolean);
                    if (childPreviews.length > 0) {
                        const layout = document.createElement("div");
                        layout.className = "content-block-container-layout";
                        layout.style.setProperty(
                            "--content-block-container-columns",
                            String(childPreviews.length));
                        childPreviews.forEach(preview => layout.appendChild(preview));
                        fragment.appendChild(layout);
                    }
                    continue;
                }

                const preview = previewByCard.get(card);
                if (preview) {
                    fragment.appendChild(preview);
                }
            }

            previewRoot.classList.remove("all-player-answer-grid");
            previewRoot.replaceChildren(fragment);
            previewCapture = null;
        };

        document.addEventListener("click", event => {
            const button = event.target.closest("[data-open-question-preview]");
            if (!button || button.dataset.openQuestionPreview !== "answer" ||
                !isAllPlayerChoice()) {
                return;
            }
            syncState();
            window.setTimeout(captureAnswerPreview, 10);
            window.setTimeout(rebuildAnswerPreview, 60);
        }, true);

        select.addEventListener("change", scheduleSync);
        answerSection.addEventListener("input", scheduleSync);
        answerSection.addEventListener("change", scheduleSync);

        form.addEventListener("submit", () => {
            syncState();
        }, true);

        const answerList = answerSection.querySelector("[data-content-block-list]");
        if (answerList) {
            new MutationObserver(scheduleSync).observe(answerList, {
                childList: true,
                subtree: true
            });
        }

        scheduleSync();
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initialize, { once: true });
    } else {
        initialize();
    }
})();