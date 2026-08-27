(() => {
    "use strict";

    if (window.badWolfQuizEditorDialogLoadingInitialized) {
        return;
    }

    window.badWolfQuizEditorDialogLoadingInitialized = true;

    const language = (document.documentElement.lang || "en")
        .split("-")[0]
        .toLowerCase();
    const loadingLabels = {
        en: "Loading available destinations…",
        uk: "Завантаження доступних місць…",
        ru: "Україна",
        it: "Caricamento destinazioni disponibili…"
    };
    const loadingLabel = loadingLabels[language] ?? loadingLabels.en;

    const style = document.createElement("style");
    style.id = "quiz-editor-dialog-loading-styles";
    style.textContent = `
dialog[data-question-copy-dialog] .alert:not(.alert-error):not([hidden]),
.quiz-editor-dialog-loading:not([hidden]) {
    display: flex;
    align-items: center;
    gap: 0.75rem;
}

dialog[data-question-copy-dialog] .alert:not(.alert-error):not([hidden])::before,
.quiz-editor-dialog-loading:not([hidden])::before {
    content: "";
    width: 20px;
    height: 20px;
    flex: 0 0 20px;
    box-sizing: border-box;
    border-radius: 50%;
    border: 3px solid color-mix(in srgb, var(--text) 18%, transparent);
    border-top-color: var(--red-bright);
    border-right-color: color-mix(in srgb, var(--red-bright) 68%, transparent);
    animation: quiz-editor-dialog-loading-spin 0.8s linear infinite;
}

.quiz-editor-dialog-loading {
    margin: 0.25rem 0 1rem;
    color: var(--muted);
}

@keyframes quiz-editor-dialog-loading-spin {
    to {
        transform: rotate(360deg);
    }
}

@media (prefers-reduced-motion: reduce) {
    dialog[data-question-copy-dialog] .alert:not(.alert-error):not([hidden])::before,
    .quiz-editor-dialog-loading:not([hidden])::before {
        animation: none;
        transform: rotate(38deg);
    }
}`;
    document.head.appendChild(style);

    const runAfterPaint = callback => {
        if (typeof window.requestAnimationFrame !== "function" ||
            document.visibilityState !== "visible") {
            window.setTimeout(callback, 34);
            return;
        }

        window.requestAnimationFrame(() => {
            window.requestAnimationFrame(callback);
        });
    };

    const ensureLoadingIndicator = dialog => {
        let indicator = dialog.querySelector("[data-dialog-destination-loading]");
        if (indicator) {
            return indicator;
        }

        indicator = document.createElement("div");
        indicator.className = "quiz-editor-dialog-loading";
        indicator.dataset.dialogDestinationLoading = "true";
        indicator.setAttribute("role", "status");
        indicator.setAttribute("aria-live", "polite");
        indicator.textContent = loadingLabel;
        indicator.hidden = true;

        const heading = dialog.querySelector(".dialog-heading");
        heading?.insertAdjacentElement("afterend", indicator);
        return indicator;
    };

    const setLoading = (dialog, loading, controls) => {
        const indicator = ensureLoadingIndicator(dialog);
        indicator.hidden = !loading;
        dialog.toggleAttribute("aria-busy", loading);

        if (loading) {
            controls.forEach(control => {
                if (control) {
                    control.disabled = true;
                }
            });
        }
    };

    const prepareDialog = (dialog, controls, populate) => {
        setLoading(dialog, true, controls);
        if (!dialog.open) {
            dialog.showModal();
        }

        runAfterPaint(() => {
            try {
                populate();
            } finally {
                setLoading(dialog, false, controls);
            }
        });
    };

    const categoryDialog = document.getElementById("exchange-category-dialog");
    if (categoryDialog) {
        window.openExchangeCategoryDialog = (
            categoryId,
            categoryTitle,
            sourceRoundId) => {
            const sourceIdInput = document.getElementById(
                "exchange-category-source-id");
            const sourceTitle = document.getElementById(
                "exchange-category-source-title");
            const targetRoundSelect = document.getElementById(
                "exchange-category-target-round");
            const targetCategorySelect = document.getElementById(
                "exchange-category-target-category");
            const submitButton = document.getElementById(
                "exchange-category-submit-button");
            const emptyMessage = document.getElementById(
                "exchange-category-empty-message");

            if (!sourceIdInput || !sourceTitle || !targetRoundSelect ||
                !targetCategorySelect || !submitButton) {
                return;
            }

            sourceIdInput.value = String(categoryId);
            sourceTitle.textContent = categoryTitle;
            targetRoundSelect.replaceChildren();
            targetCategorySelect.replaceChildren();
            if (emptyMessage) {
                emptyMessage.style.display = "none";
            }

            prepareDialog(
                categoryDialog,
                [targetRoundSelect, targetCategorySelect, submitButton],
                () => {
                    const availableRounds = exchangeCategoryRounds.filter(
                        roundItem => roundItem.id !== sourceRoundId &&
                            roundItem.categories.length > 0);

                    for (const roundItem of availableRounds) {
                        const option = document.createElement("option");
                        option.value = roundItem.id.toString();
                        option.textContent = roundItem.title;
                        targetRoundSelect.appendChild(option);
                    }

                    refreshExchangeTargetCategories();
                });
        };
    }

    const questionDialog = document.getElementById("exchange-question-dialog");
    if (questionDialog) {
        window.openExchangeQuestionDialog = (
            questionId,
            questionTitle,
            sourceRoundId) => {
            const sourceIdInput = document.getElementById(
                "exchange-question-source-id");
            const sourceTitle = document.getElementById(
                "exchange-question-source-title");
            const targetRoundSelect = document.getElementById(
                "exchange-question-target-round");
            const targetQuestionSelect = document.getElementById(
                "exchange-question-target-question");
            const submitButton = document.getElementById(
                "exchange-question-submit-button");
            const emptyMessage = document.getElementById(
                "exchange-question-empty-message");

            if (!sourceIdInput || !sourceTitle || !targetRoundSelect ||
                !targetQuestionSelect || !submitButton) {
                return;
            }

            sourceIdInput.value = String(questionId);
            sourceTitle.textContent = questionTitle;
            targetRoundSelect.replaceChildren();
            targetQuestionSelect.replaceChildren();
            if (emptyMessage) {
                emptyMessage.style.display = "none";
            }

            prepareDialog(
                questionDialog,
                [targetRoundSelect, targetQuestionSelect, submitButton],
                () => {
                    const availableRounds = exchangeQuestionRounds.filter(
                        roundItem => roundItem.id !== sourceRoundId &&
                            roundItem.questions.length > 0);

                    for (const roundItem of availableRounds) {
                        const option = document.createElement("option");
                        option.value = roundItem.id.toString();
                        option.textContent = roundItem.title;
                        targetRoundSelect.appendChild(option);
                    }

                    refreshExchangeTargetQuestions();
                });
        };
    }
})();
