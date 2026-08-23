(() => {
    "use strict";

    if (window.badWolfHostQuestionControlsInstalled) {
        return;
    }

    window.badWolfHostQuestionControlsInstalled = true;

    const normalizeText = value =>
        (value ?? "").replace(/\s+/g, " ").trim();

    const localizedText = {
        uk: {
            showUnplayed: "Показати ще не зіграні питання",
            noPlayed: "Немає зіграних питань.",
            noPlayers: "Немає доступних гравців."
        },
        ru: {
            showUnplayed: "Показать ещё не сыгранные вопросы",
            noPlayed: "Нет сыгранных вопросов.",
            noPlayers: "Нет доступных игроков."
        },
        it: {
            showUnplayed: "Mostra anche le domande non ancora giocate",
            noPlayed: "Non ci sono domande già giocate.",
            noPlayers: "Nessun giocatore disponibile."
        },
        en: {
            showUnplayed: "Show unplayed questions",
            noPlayed: "There are no played questions.",
            noPlayers: "No eligible players."
        }
    };
    const language = document.documentElement.lang?.toLowerCase() ?? "en";
    const text = localizedText[language] ??
        localizedText[language.split("-")[0]] ??
        localizedText.en;
    let questionMetadata = [];
    let resolvedQuestion = null;

    const getMetadataUrl = () => {
        const gameId = document.querySelector(".host-game-board[data-game-id]")
            ?.dataset.gameId;
        return gameId
            ? `/Admin/Games/QuickScoreQuestions/${encodeURIComponent(gameId)}`
            : null;
    };

    const loadQuestionMetadata = async () => {
        const metadataUrl = getMetadataUrl();
        if (!metadataUrl) {
            throw new Error("Quick score game identifier is unavailable.");
        }

        const response = await fetch(metadataUrl, {
            method: "GET",
            headers: {
                "Accept": "application/json",
                "X-Requested-With": "XMLHttpRequest"
            },
            cache: "no-store"
        });
        if (!response.ok) {
            throw new Error(response.statusText);
        }

        const result = await response.json();
        if (!result?.success || !Array.isArray(result.questions)) {
            throw new Error("Quick score question metadata is unavailable.");
        }

        questionMetadata = result.questions;
        return questionMetadata;
    };

    const initializeQuickScoreQuestionList = () => {
        if (!getMetadataUrl()) {
            return;
        }

        const dialog = document.getElementById("quick-score-dialog");
        const sourceSelect = dialog?.querySelector("[data-quick-score-question]");
        const sourceLabel = sourceSelect?.closest("label");
        const submitButton = dialog?.querySelector("[data-quick-score-submit]");
        const warning = dialog?.querySelector("[data-quick-score-warning]");
        if (!dialog || !sourceSelect || !sourceLabel || !submitButton || !warning ||
            sourceSelect.dataset.playOrderInitialized === "true") {
            return;
        }

        sourceSelect.dataset.playOrderInitialized = "true";

        const showUnplayedLabel = document.createElement("label");
        showUnplayedLabel.className =
            "answer-history-correct quick-score-show-unplayed";
        const showUnplayed = document.createElement("input");
        showUnplayed.type = "checkbox";
        showUnplayed.dataset.quickScoreShowUnplayed = "";
        const showUnplayedText = document.createElement("span");
        showUnplayedText.textContent = text.showUnplayed;
        showUnplayedLabel.append(showUnplayed, showUnplayedText);
        sourceLabel.after(showUnplayedLabel);

        const createOption = question => {
            const option = document.createElement("option");
            option.value = question.sourceQuestionId.toString();
            option.textContent = question.label;
            option.dataset.points = question.points.toString();
            option.dataset.status = question.status;
            option.dataset.played = question.played ? "true" : "false";
            option.dataset.openSequence = question.openSequence?.toString() ?? "";
            option.dataset.attemptedPlayerIds =
                (question.attemptedPlayerIds ?? []).join(",");
            return option;
        };

        const renderQuestions = preserveSelection => {
            const previousSelection = preserveSelection ? sourceSelect.value : "";
            const visibleQuestions = questionMetadata.filter(question =>
                question.played || showUnplayed.checked);

            sourceSelect.replaceChildren(...visibleQuestions.map(createOption));

            if (previousSelection && visibleQuestions.some(question =>
                question.sourceQuestionId.toString() === previousSelection)) {
                sourceSelect.value = previousSelection;
            } else if (sourceSelect.options.length > 0) {
                sourceSelect.selectedIndex = 0;
            } else {
                sourceSelect.selectedIndex = -1;
            }

            const hasQuestion = sourceSelect.selectedIndex >= 0;
            submitButton.disabled = !hasQuestion;
            warning.hidden = hasQuestion;
            warning.textContent = hasQuestion ? "" : text.noPlayed;

            if (hasQuestion) {
                sourceSelect.dispatchEvent(new Event("change", { bubbles: true }));
            }
        };

        const refresh = async resetShowUnplayed => {
            sourceSelect.disabled = true;
            showUnplayed.disabled = true;
            try {
                await loadQuestionMetadata();
                if (resetShowUnplayed) {
                    showUnplayed.checked = false;
                }
                renderQuestions(false);
            } catch (error) {
                console.error("Unable to refresh quick score questions.", error);
            } finally {
                sourceSelect.disabled = false;
                showUnplayed.disabled = false;
            }
        };

        showUnplayed.addEventListener("change", () => renderQuestions(true));

        new MutationObserver(() => {
            if (dialog.open) {
                void refresh(true);
            }
        }).observe(dialog, {
            attributes: true,
            attributeFilter: ["open"]
        });

        // The server-rendered select contains the legacy all-questions list.
        // Clear it immediately so the persistent shell can never expose that
        // stale list while current play-order metadata is loading.
        sourceSelect.replaceChildren();
        submitButton.disabled = true;
        void refresh(true);
    };

    const sourceQuestionIdFor = question => {
        try {
            const url = new URL(question.href, window.location.href);
            return url.searchParams.get("previewQuestionId") ?? "";
        } catch {
            return "";
        }
    };

    const restoreAvailableQuestionMenu = () => {
        resolvedQuestion = null;

        const menu = document.getElementById("question-context-menu");
        const closeAction = menu?.querySelector("[data-question-close]");
        if (menu) {
            menu.style.removeProperty("grid-template-columns");
        }
        closeAction?.style.removeProperty("display");

        const giftResolve = document.querySelector(
            "#question-gift-dialog [data-question-gift-resolve]");
        giftResolve?.closest("label")?.removeAttribute("hidden");
    };

    const positionResolvedQuestionMenu = (menu, event) => {
        menu.hidden = false;
        const rect = menu.getBoundingClientRect();
        const left = Math.min(
            event.clientX,
            Math.max(8, window.innerWidth - rect.width - 8));
        const top = Math.min(
            event.clientY,
            Math.max(8, window.innerHeight - rect.height - 8));
        menu.style.left = `${Math.max(8, left)}px`;
        menu.style.top = `${Math.max(8, top)}px`;
    };

    const syncResolvedGiftPlayers = (giftPlayer, giftWarning, giftSubmit, question) => {
        const attempted = new Set(
            (question?.attemptedPlayerIds ?? []).map(id => id.toString()));
        let first = null;

        for (const option of giftPlayer.options) {
            option.disabled = attempted.has(option.value);
            if (!option.disabled && !first) {
                first = option;
            }
        }

        if (first) {
            giftPlayer.value = first.value;
        }

        giftWarning.hidden = !!first;
        giftWarning.textContent = first ? "" : text.noPlayers;
        giftSubmit.disabled = !first;
        return !!first;
    };

    const installResolvedQuestionGiftMenu = () => {
        document.addEventListener("contextmenu", event => {
            const target = event.target instanceof Element ? event.target : null;
            const resolved = target?.closest(
                ".host-board-question.status-resolved[data-question-resolved]");
            if (resolved) {
                const menu = document.getElementById("question-context-menu");
                const giftAction = menu?.querySelector("[data-question-gift]");
                const closeAction = menu?.querySelector("[data-question-close]");
                if (!menu || !giftAction) {
                    return;
                }

                event.preventDefault();
                event.stopImmediatePropagation();
                resolvedQuestion = resolved;

                if (closeAction) {
                    closeAction.style.setProperty("display", "none");
                }
                menu.style.gridTemplateColumns = "42px";
                positionResolvedQuestionMenu(menu, event);
                return;
            }

            if (target?.closest(
                ".host-board-question.status-available[data-question-context]")) {
                restoreAvailableQuestionMenu();
            }
        }, true);

        document.addEventListener("click", async event => {
            if (!resolvedQuestion) {
                return;
            }

            const target = event.target instanceof Element ? event.target : null;
            const giftAction = target?.closest("[data-question-gift]");
            const menu = giftAction?.closest("#question-context-menu");
            if (!giftAction || !menu) {
                return;
            }

            event.preventDefault();
            event.stopImmediatePropagation();
            menu.hidden = true;

            const giftDialog = document.getElementById("question-gift-dialog");
            const giftId = giftDialog?.querySelector("[data-question-gift-id]");
            const giftLabel = giftDialog?.querySelector("[data-question-gift-label]");
            const giftPlayer = giftDialog?.querySelector("[data-question-gift-player]");
            const giftValue = giftDialog?.querySelector("[data-question-gift-value]");
            const giftResolve = giftDialog?.querySelector("[data-question-gift-resolve]");
            const giftWarning = giftDialog?.querySelector("[data-question-gift-warning]");
            const giftSubmit = giftDialog?.querySelector("[data-question-gift-submit]");
            const giftResolveOption = giftResolve?.closest("label");
            if (!giftDialog || !giftId || !giftLabel || !giftPlayer ||
                !giftValue || !giftResolve || !giftResolveOption ||
                !giftWarning || !giftSubmit) {
                return;
            }

            const questionElement = resolvedQuestion;
            if (!questionElement?.isConnected) {
                restoreAvailableQuestionMenu();
                return;
            }

            const sourceQuestionId = sourceQuestionIdFor(questionElement);
            if (!sourceQuestionId) {
                return;
            }

            try {
                const questions = await loadQuestionMetadata();
                const question = questions.find(item =>
                    item.sourceQuestionId.toString() === sourceQuestionId);

                giftId.value = sourceQuestionId;
                giftLabel.value = question?.label ||
                    normalizeText(questionElement.textContent);
                giftValue.value = question?.points?.toString() ||
                    normalizeText(questionElement.textContent) ||
                    "100";
                giftResolve.checked = false;
                giftResolveOption.hidden = true;
                syncResolvedGiftPlayers(
                    giftPlayer,
                    giftWarning,
                    giftSubmit,
                    question);
                giftDialog.showModal();
            } catch (error) {
                console.error("Unable to load resolved question metadata.", error);
            }
        }, true);
    };

    installResolvedQuestionGiftMenu();

    const initializeCurrentDom = () => {
        initializeQuickScoreQuestionList();
    };

    initializeCurrentDom();

    new MutationObserver(() => initializeCurrentDom()).observe(
        document.documentElement,
        {
            childList: true,
            subtree: true
        });
})();