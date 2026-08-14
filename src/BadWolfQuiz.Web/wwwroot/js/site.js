for (const message of document.querySelectorAll("[data-auto-dismiss]")) {
    window.setTimeout(() => {
        message.classList.add("message-hidden");
        message.addEventListener("transitionend", () => message.remove(), { once: true });
    }, 4000);
}

const initializeActionMenus = () => {
    document.querySelectorAll("details.action-menu").forEach(menu => {
        if (menu.dataset.actionMenuInitialized === "true") {
            return;
        }

        menu.dataset.actionMenuInitialized = "true";
        menu.addEventListener("toggle", () => {
            if (!menu.open) {
                return;
            }

            document.querySelectorAll("details.action-menu[open]").forEach(other => {
                if (other !== menu) {
                    other.removeAttribute("open");
                }
            });
        });
    });
};

initializeActionMenus();

document.addEventListener("click", event => {
    const selectedItem = event.target.closest?.(".action-menu-item");
    selectedItem?.closest("details.action-menu")?.removeAttribute("open");

    document.querySelectorAll("details.action-menu[open]").forEach(menu => {
        if (!menu.contains(event.target)) {
            menu.removeAttribute("open");
        }
    });
});

document.addEventListener("keydown", event => {
    if (event.key === "Escape") {
        document.querySelectorAll("details.action-menu[open]").forEach(menu => {
            menu.removeAttribute("open");
        });
    }
});


document.querySelectorAll("[data-auto-rating-form]").forEach(form => {
    form.addEventListener("submit", event => event.preventDefault());
    const inputs = form.querySelectorAll('input[name="score"]');
    const state = form.querySelector("[data-rating-save-state]");

    const saveRating = async score => {
            const formData = new FormData(form);
            formData.set("score", score);
            inputs.forEach(item => item.disabled = true);
            if (state) {
                state.textContent = "";
            }

            try {
                const response = await fetch(form.action, {
                    method: "POST",
                    body: formData,
                    headers: { "X-Requested-With": "XMLHttpRequest" }
                });
                if (!response.ok) {
                    throw new Error("Rating was not saved.");
                }
                form.classList.add("is-saved");
            } catch {
                if (state) {
                    state.textContent = form.dataset.ratingErrorLabel;
                }
            } finally {
                inputs.forEach(item => item.disabled = false);
            }
    };

    inputs.forEach(input => {
        input.addEventListener("change", () => saveRating(input.value));
        const label = form.querySelector(`label[for="${input.id}"]`);
        label?.addEventListener("click", event => {
            if (!input.checked) {
                return;
            }

            event.preventDefault();
            input.checked = false;
            saveRating("0");
        });
    });
});

const languageButton = document.getElementById("languageButton");
const languageMenu = document.getElementById("languageMenu");

languageButton?.addEventListener("click", event => {
    event.stopPropagation();
    document.querySelectorAll("details.action-menu[open]").forEach(menu => {
        menu.removeAttribute("open");
    });
    const isOpen = languageMenu.classList.toggle("open");
    languageButton.setAttribute("aria-expanded", isOpen.toString());
});

languageMenu?.addEventListener("click", event => event.stopPropagation());

document.addEventListener("click", () => {
    languageMenu?.classList.remove("open");
    languageButton?.setAttribute("aria-expanded", "false");
});

document.addEventListener("keydown", event => {
    if (event.key === "Escape") {
        languageMenu?.classList.remove("open");
        languageButton?.setAttribute("aria-expanded", "false");
    }
});

let gameRoundIntroRoutesConfigured = false;

const configureGameRoundIntroRoutes = () => {
    const gameBoard = document.querySelector(".host-game-board[data-game-id]");
    const pathGameId = window.location.pathname.split("/").filter(Boolean).at(-1);
    const gameId = gameBoard?.dataset.gameId || pathGameId;

    if (!gameId || !window.location.pathname.includes("/Admin/Games/Lobby/")) {
        return;
    }
    if (gameRoundIntroRoutesConfigured) {
        return;
    }
    gameRoundIntroRoutesConfigured = true;

    const encodedGameId = encodeURIComponent(gameId);
    const runningIntroBase = `/Admin/Games/RunningRoundIntro/${encodedGameId}`;
    const finalTransitionBase = `/Admin/Games/FinalQuestionTransition/${encodedGameId}`;
    const startButton = document.querySelector('.lobby-start-button[form="start-game-form"]');
    if (startButton) {
        startButton.formAction = `/Admin/Games/RoundIntro/${encodedGameId}?handler=Prepare`;
    }

    const getFormHandler = form => {
        if (!(form instanceof HTMLFormElement) || !form.action) {
            return null;
        }

        return new URL(form.action, window.location.origin).searchParams.get("handler");
    };

    const openFinalTransition = force => {
        const target = force
            ? `${finalTransitionBase}?force=true`
            : finalTransitionBase;
        if (window.BadWolfHostFlowNavigation) {
            window.BadWolfHostFlowNavigation.navigate(target)
                .catch(error => {
                    console.error(error);
                    window.location.assign(target);
                });
            return;
        }

        window.location.assign(target);
    };

    const routeRoundForm = form => {
        if (!(form instanceof HTMLFormElement) || !form.action) {
            return;
        }

        const action = new URL(form.action, window.location.origin);
        const handler = action.searchParams.get("handler");

        if (form.id === "force-advance-round-form" || handler === "ForceAdvanceRound") {
            form.action = `${runningIntroBase}?handler=ForceAdvance`;
            return;
        }

        if (handler === "PreviousRound") {
            form.action = `${runningIntroBase}?handler=Previous`;
            return;
        }

        if (handler === "ReturnToUnfinishedRound") {
            form.action = `${runningIntroBase}?handler=ReturnToUnfinished`;
            return;
        }

        if (handler === "AdvanceRound") {
            form.action = `${runningIntroBase}?handler=Advance`;
        }
    };

    const submitRoutedForm = form => {
        if (!(form instanceof HTMLFormElement)) {
            return;
        }

        routeRoundForm(form);
        form.requestSubmit();
    };

    const advanceEmptyRoundSummary = () => {
        const summary = document.querySelector(".host-game-board .round-summary");
        if (!summary || summary.querySelector(".round-podium-player")) {
            return false;
        }

        const form = summary.querySelector("form");
        if (!(form instanceof HTMLFormElement)) {
            return false;
        }

        const action = new URL(form.action, window.location.origin);
        if (action.searchParams.get("handler") !== "AdvanceRound" &&
            !action.pathname.includes("/RunningRoundIntro/")) {
            return false;
        }

        if (summary.dataset.autoAdvanceStarted === "true") {
            return true;
        }

        summary.dataset.autoAdvanceStarted = "true";
        window.setTimeout(() => submitRoutedForm(form), 0);
        return true;
    };

    document.querySelectorAll("form").forEach(routeRoundForm);

    document.addEventListener("click", event => {
        const target = event.target instanceof Element ? event.target : null;

        const categoryPreview = target?.closest("[data-category-preview-url]");
        if (categoryPreview) {
            event.preventDefault();
            event.stopImmediatePropagation();
            const targetUrl = categoryPreview.dataset.categoryPreviewUrl;
            if (window.BadWolfHostFlowNavigation && targetUrl) {
                window.BadWolfHostFlowNavigation.navigate(targetUrl)
                    .catch(error => {
                        console.error(error);
                        window.location.assign(targetUrl);
                    });
            } else if (targetUrl) {
                window.location.assign(targetUrl);
            }
            return;
        }

        if (target?.closest("[data-open-natural-final-warning]")) {
            event.preventDefault();
            event.stopImmediatePropagation();
            const dialog = document.getElementById("natural-final-warning-dialog");
            if (dialog instanceof HTMLDialogElement && !dialog.open) {
                dialog.showModal();
            }
            return;
        }

        const forceAdvanceButton = target?.closest(
            "[data-confirm-force-advance-round]");
        if (forceAdvanceButton) {
            const form = document.getElementById("force-advance-round-form");
            if (!(form instanceof HTMLFormElement) || forceAdvanceButton.disabled) {
                return;
            }

            event.preventDefault();
            event.stopImmediatePropagation();
            forceAdvanceButton.disabled = true;
            document.getElementById("force-advance-round-dialog")?.close();
            routeRoundForm(form);

            fetch(form.action, {
                method: "POST",
                body: new FormData(form),
                headers: { Accept: "text/html" }
            })
                .then(async response => {
                    if (!response.ok) {
                        throw new Error(response.statusText);
                    }

                    const responseUrl = response.url ||
                        `${runningIntroBase}?returning=true`;
                    if (window.BadWolfHostFlowNavigation) {
                        await window.BadWolfHostFlowNavigation.navigate(responseUrl);
                    } else {
                        window.location.assign(responseUrl);
                    }
                })
                .catch(error => {
                    console.error(error);
                    window.location.reload();
                })
                .finally(() => {
                    forceAdvanceButton.disabled = false;
                });
            return;
        }

        const submitter = target?.closest("button, input[type='submit']");
        if (!submitter?.form) {
            return;
        }

        routeRoundForm(submitter.form);
        const action = new URL(submitter.form.action, window.location.origin);
        if (action.pathname.includes("/Admin/Games/RunningRoundIntro/")) {
            submitter.formAction = submitter.form.action;
        }
    }, true);

    document.addEventListener("keydown", event => {
        if (event.key !== "Enter" && event.key !== " ") {
            return;
        }

        const categoryPreview = event.target instanceof Element
            ? event.target.closest("[data-category-preview-url]")
            : null;
        if (!categoryPreview) {
            return;
        }

        event.preventDefault();
        const targetUrl = categoryPreview.dataset.categoryPreviewUrl;
        if (window.BadWolfHostFlowNavigation && targetUrl) {
            window.BadWolfHostFlowNavigation.navigate(targetUrl)
                .catch(error => {
                    console.error(error);
                    window.location.assign(targetUrl);
                });
        } else if (targetUrl) {
            window.location.assign(targetUrl);
        }
    });

    document.addEventListener("submit", event => {
        const form = event.target;
        const handler = getFormHandler(form);

        if (handler === "PrepareFinalQuestionLeaderboard") {
            event.preventDefault();
            event.stopImmediatePropagation();
            const confirmButton = document.querySelector(
                "[data-confirm-force-advance-final]");
            confirmButton?.setAttribute("disabled", "disabled");
            document.getElementById("force-advance-final-dialog")?.close();

            fetch(form.action, {
                method: "POST",
                body: new FormData(form),
                headers: { Accept: "text/html" }
            })
                .then(async response => {
                    if (!response.ok) {
                        throw new Error(response.statusText);
                    }

                    const responseUrl = response.url || window.location.href;
                    if (window.BadWolfHostFlowNavigation) {
                        await window.BadWolfHostFlowNavigation.navigate(responseUrl);
                    } else {
                        window.location.assign(responseUrl);
                    }
                })
                .catch(error => {
                    console.error(error);
                    window.location.reload();
                })
                .finally(() => {
                    confirmButton?.removeAttribute("disabled");
                });
            return;
        }

        if ((handler === "StartFinalQuestion" ||
             handler === "ForceAdvanceToFinalQuestion") &&
            !form.matches("[data-final-question-transition-form]")) {
            event.preventDefault();
            event.stopImmediatePropagation();
            form.closest("dialog")?.close();
            openFinalTransition(handler === "ForceAdvanceToFinalQuestion");
            return;
        }

        if (handler === "Previous" ||
            handler === "PreviousRound" ||
            handler === "ReturnToUnfinished" ||
            handler === "ReturnToUnfinishedRound") {
            event.preventDefault();
            event.stopImmediatePropagation();
            const submitter = event.submitter instanceof HTMLElement
                ? event.submitter
                : null;
            submitter?.setAttribute("disabled", "disabled");
            form.closest("dialog")?.close();
            routeRoundForm(form);

            fetch(form.action, {
                method: "POST",
                body: new FormData(form),
                headers: { Accept: "text/html" }
            })
                .then(async response => {
                    if (!response.ok) {
                        throw new Error(response.statusText);
                    }

                    const responseUrl = response.url ||
                        `${runningIntroBase}?returning=true`;
                    if (window.BadWolfHostFlowNavigation) {
                        await window.BadWolfHostFlowNavigation.navigate(responseUrl);
                    } else {
                        window.location.assign(responseUrl);
                    }
                })
                .catch(error => {
                    console.error(error);
                    window.location.reload();
                })
                .finally(() => {
                    submitter?.removeAttribute("disabled");
                });
            return;
        }

        routeRoundForm(form);
    }, true);

    if (advanceEmptyRoundSummary()) {
        return;
    }

    const observer = new MutationObserver(() => {
        document.querySelectorAll("form").forEach(routeRoundForm);
        advanceEmptyRoundSummary();
    });
    observer.observe(document.body, { childList: true, subtree: true });
};

let hostGameplayFormNavigationConfigured = false;

const configureHostGameplayFormNavigation = () => {
    const board = document.querySelector(".host-game-board[data-game-id]");
    if (!board || !window.BadWolfHostGameplay) {
        return;
    }
    if (hostGameplayFormNavigationConfigured) {
        return;
    }
    hostGameplayFormNavigationConfigured = true;

    const lobbyUrl = new URL(window.location.href);
    const gameId = board.dataset.gameId;
    const flowPaths = new Set([
        `/Admin/Games/RoundIntro/${encodeURIComponent(gameId)}`,
        `/Admin/Games/RunningRoundIntro/${encodeURIComponent(gameId)}`,
        `/Admin/Games/FinalQuestionTransition/${encodeURIComponent(gameId)}`
    ].map(path => path.toLowerCase()));
    const viewSelector = "[data-host-gameplay-view]";
    const boardSelector = "[data-host-gameplay-board]";
    const transientSelector = "[data-host-gameplay-transient]";
    let submissionInProgress = false;
    let finalTransitionTimer = null;
    let gameplayErrorDismissHandle = null;

    const showHostGameplayError = message => {
        const target = document.getElementById("game-board-error");
        if (!target) {
            window.alert(message);
            return;
        }

        window.clearTimeout(gameplayErrorDismissHandle);
        target.textContent = message;
        target.hidden = false;
        target.classList.remove("message-hidden");
        gameplayErrorDismissHandle = window.setTimeout(() => {
            target.classList.add("message-hidden");
            window.setTimeout(() => {
                target.hidden = true;
                target.classList.remove("message-hidden");
            }, 300);
        }, 3000);
    };

    const syncPersistentHostChrome = () => {
        const view = document.querySelector(viewSelector);
        const hidesPlayerPanel = view !== null &&
            view.querySelector(
                ".question-review-preview, [data-game-intro-page], [data-final-question-transition]") !== null;
        board.classList.toggle(
            "host-gameplay-presentation-mode",
            hidesPlayerPanel);
    };

    const syncBlockedPlayers = parsed => {
        const currentDialog = document.getElementById("blocked-players-dialog");
        const nextDialog = parsed.getElementById("blocked-players-dialog");
        const currentCard = currentDialog?.querySelector(":scope > .dialog-card");
        const nextCard = nextDialog?.querySelector(":scope > .dialog-card");
        if (!currentCard || !nextCard) {
            return;
        }

        for (const child of Array.from(currentCard.children)) {
            if (!child.classList.contains("dialog-heading")) {
                child.remove();
            }
        }
        for (const child of Array.from(nextCard.children)) {
            if (!child.classList.contains("dialog-heading")) {
                currentCard.append(document.importNode(child, true));
            }
        }
    };

    const isLobbyUrl = url =>
        url.origin === lobbyUrl.origin &&
        url.pathname.toLowerCase() === lobbyUrl.pathname.toLowerCase();

    const canNavigate = url =>
        url.origin === lobbyUrl.origin &&
        (isLobbyUrl(url) || flowPaths.has(url.pathname.toLowerCase()));

    const getQuestionId = question => {
        if (!(question instanceof Element)) {
            return null;
        }

        if (question.dataset.sourceQuestionId) {
            return question.dataset.sourceQuestionId;
        }

        if (question instanceof HTMLAnchorElement) {
            const href = question.getAttribute("href");
            return href
                ? new URL(href, lobbyUrl.href)
                    .searchParams.get("previewQuestionId")
                : null;
        }

        return null;
    };

    const getQuestionContainer = question =>
        question.closest("form.question-selection-form") ?? question;

    const fitBoardTitles = grid => {
        if (!grid) {
            return;
        }

        for (const title of grid.querySelectorAll(".host-board-column h3")) {
            let size = 30;
            title.style.setProperty("--category-title-size", `${size}px`);
            while (size > 12 &&
                (title.scrollWidth > title.clientWidth ||
                 title.scrollHeight > title.clientHeight)) {
                size -= 1;
                title.style.setProperty("--category-title-size", `${size}px`);
            }
        }
    };

    const syncBoardQuestions = nextBoard => {
        const currentBoard = document.querySelector(boardSelector);
        const currentGrid = currentBoard?.querySelector(".host-board-grid");
        const nextGrid = nextBoard?.querySelector(".host-board-grid");
        if (!currentBoard || !nextBoard || !currentGrid || !nextGrid) {
            return;
        }

        const currentRoundId = currentGrid.dataset.sourceRoundId;
        const nextRoundId = nextGrid.dataset.sourceRoundId;
        const currentCategoryIds = Array.from(currentGrid.querySelectorAll(
            "[data-category-context]"), category => category.dataset.sourceCategoryId);
        const nextCategoryIds = Array.from(nextGrid.querySelectorAll(
            "[data-category-context]"), category => category.dataset.sourceCategoryId);
        const sameRound = currentRoundId && nextRoundId
            ? currentRoundId === nextRoundId
            : currentCategoryIds.length === nextCategoryIds.length &&
                currentCategoryIds.every((id, index) => id === nextCategoryIds[index]);

        const replaceGrid = () => {
            currentGrid.replaceChildren(
                ...Array.from(nextGrid.childNodes, node =>
                    document.importNode(node, true)));
            if (nextRoundId) {
                currentGrid.dataset.sourceRoundId = nextRoundId;
            } else {
                delete currentGrid.dataset.sourceRoundId;
            }
            const nextStyle = nextGrid.getAttribute("style");
            if (nextStyle === null) {
                currentGrid.removeAttribute("style");
            } else {
                currentGrid.setAttribute("style", nextStyle);
            }
            fitBoardTitles(currentGrid);
        };

        if (!sameRound) {
            replaceGrid();
            return;
        }

        const currentById = new Map(
            Array.from(currentBoard.querySelectorAll(".host-board-question"))
                .map(question => [getQuestionId(question), question])
                .filter(([id]) => id));
        const nextById = new Map(
            Array.from(nextBoard.querySelectorAll(".host-board-question"))
                .map(question => [getQuestionId(question), question])
                .filter(([id]) => id));

        if (currentById.size !== nextById.size ||
            Array.from(nextById.keys()).some(id => !currentById.has(id))) {
            replaceGrid();
            return;
        }

        for (const [questionId, nextQuestion] of nextById) {
            const currentQuestion = currentById.get(questionId);
            if (!currentQuestion) {
                continue;
            }

            getQuestionContainer(currentQuestion).replaceWith(
                document.importNode(
                    getQuestionContainer(nextQuestion),
                    true));
        }

        for (const column of currentBoard.querySelectorAll(
            ".host-board-column")) {
            const category = column.querySelector("[data-category-context]");
            if (category) {
                category.dataset.hasAvailableQuestions = column.querySelector(
                    ".host-board-question.status-available[data-question-context]")
                    ? "true"
                    : "false";
            }
        }
        fitBoardTitles(currentGrid);
    };

    const syncHeaderState = () => {
        const view = document.querySelector(viewSelector);
        if (!view) {
            return;
        }

        const hasActiveQuestion =
            view.querySelector(".current-question-summary") !== null;
        const isRoundSummary = view.querySelector(".round-summary") !== null;
        const isExternalFlow = view.querySelector(
            "[data-game-intro-page], [data-final-question-transition]") !== null;
        const menu = document.querySelector(".board-action-menu");
        if (menu) {
            menu.hidden = isRoundSummary || isExternalFlow;
        }

        for (const action of document.querySelectorAll(
            ".board-action-menu a[href*='/Admin/Games/AnswerHistory'], " +
            ".board-action-menu form[action*='handler=RandomActivePlayer']")) {
            action.hidden = hasActiveQuestion || isExternalFlow;
        }
    };

    const initializeExternalFlow = currentView => {
        if (finalTransitionTimer !== null) {
            window.clearTimeout(finalTransitionTimer);
            finalTransitionTimer = null;
        }

        const finalForm = currentView.querySelector(
            "[data-final-question-transition-form]");
        if (finalForm instanceof HTMLFormElement) {
            finalTransitionTimer = window.setTimeout(() => {
                finalTransitionTimer = null;
                finalForm.requestSubmit();
            }, 3000);
        }
    };

    const renderExternalFlow = parsed => {
        const currentView = document.querySelector(viewSelector);
        const currentBoard = document.querySelector(boardSelector);
        const currentTransient = document.querySelector(transientSelector);
        const flow = parsed.querySelector(
            "[data-game-intro-page], [data-final-question-transition]");
        if (!currentView || !currentBoard || !flow) {
            return false;
        }

        window.BadWolfHostGameplay?.cancelPending?.();
        const styles = Array.from(parsed.querySelectorAll(
            "main.page-shell > style"));
        currentView.replaceChildren(
            document.importNode(flow, true),
            ...styles.map(style => document.importNode(style, true)));
        currentBoard.hidden = true;
        currentTransient?.replaceChildren();
        syncHeaderState();
        initializeExternalFlow(currentView);
        document.dispatchEvent(new CustomEvent(
            "badwolf:host-gameplay-updated"));
        return true;
    };

    const applyMarkup = async (markup, url) => {
        const targetUrl = new URL(url, lobbyUrl.href);
        if (!canNavigate(targetUrl)) {
            throw new Error(
                "Host gameplay navigation cannot leave the current game flow.");
        }

        const parsed = new DOMParser().parseFromString(markup, "text/html");
        syncBlockedPlayers(parsed);
        const nextView = parsed.querySelector(viewSelector);
        const nextBoard = parsed.querySelector(boardSelector);
        if (nextView && nextBoard) {
            syncBoardQuestions(nextBoard);
            await window.BadWolfHostGameplay.navigate(targetUrl.href, "none");
            return;
        }

        if (renderExternalFlow(parsed)) {
            return;
        }

        throw new Error("The host gameplay response is unsupported.");
    };

    const navigate = async url => {
        const targetUrl = new URL(url, lobbyUrl.href);
        if (!canNavigate(targetUrl)) {
            window.location.assign(targetUrl.href);
            return;
        }

        const response = await fetch(targetUrl.href, {
            method: "GET",
            credentials: "same-origin",
            headers: {
                Accept: "text/html",
                "X-Requested-With": "XMLHttpRequest"
            },
            cache: "no-store"
        });
        if (!response.ok) {
            throw new Error(response.statusText);
        }

        await applyMarkup(await response.text(), response.url || targetUrl.href);
    };

    window.BadWolfHostFlowNavigation = {
        applyMarkup,
        canNavigate,
        navigate
    };

    const isGameplayForm = form =>
        form.matches(".question-selection-form") ||
        form.id === "remove-player-form" ||
        form.closest("#blocked-players-dialog") !== null ||
        form.closest(viewSelector) !== null;

    document.addEventListener("click", event => {
        const target = event.target instanceof Element ? event.target : null;
        const blockPlayer = target?.closest("[data-confirm-block-player]");
        const removePlayer = target?.closest("[data-confirm-remove-player]");
        if (blockPlayer || removePlayer) {
            const form = document.getElementById("remove-player-form");
            const blockInput = document.getElementById("remove-player-block");
            if (form instanceof HTMLFormElement &&
                blockInput instanceof HTMLInputElement) {
                event.preventDefault();
                event.stopImmediatePropagation();
                blockInput.value = blockPlayer ? "true" : "false";
                document.getElementById("remove-player-dialog")?.close();
                form.requestSubmit();
            }
            return;
        }

        const flowLink = target?.closest(
            `${viewSelector} [data-game-intro-page] a`);
        if (!flowLink ||
            flowLink.target ||
            flowLink.hasAttribute("download") ||
            event.button !== 0 ||
            event.metaKey ||
            event.ctrlKey ||
            event.shiftKey ||
            event.altKey) {
            return;
        }

        const targetUrl = new URL(flowLink.href, lobbyUrl.href);
        if (!canNavigate(targetUrl)) {
            return;
        }

        event.preventDefault();
        event.stopImmediatePropagation();
        const flowPage = flowLink.closest("[data-game-intro-page]");
        const navigateToFlow = () => navigate(targetUrl)
            .catch(error => {
                console.error("Host game flow navigation failed.", error);
                window.location.assign(targetUrl.href);
            });
        if (flowPage &&
            !window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
            flowPage.classList.add("is-leaving");
            window.setTimeout(navigateToFlow, 165);
        } else {
            void navigateToFlow();
        }
    }, true);

    document.addEventListener("submit", async event => {
        const form = event.target instanceof HTMLFormElement
            ? event.target
            : null;
        if (event.defaultPrevented ||
            !form ||
            !isGameplayForm(form) ||
            submissionInProgress) {
            return;
        }

        const submitter = event.submitter instanceof HTMLElement
            ? event.submitter
            : null;
        const submitterHasFormAction =
            submitter?.hasAttribute("formaction") === true;
        const action = new URL(
            submitterHasFormAction ? submitter.formAction : form.action,
            window.location.href);

        if (!canNavigate(action)) {
            return;
        }

        event.preventDefault();
        event.stopImmediatePropagation();
        submissionInProgress = true;

        const button = submitter ?? form.querySelector("button[type='submit']");
        button?.setAttribute("disabled", "disabled");

        try {
            const formData = new FormData(form);
            if (submitter?.name) {
                formData.append(submitter.name, submitter.value);
            }

            const expectsJson = form.matches(".question-selection-form");
            const response = await fetch(action.href, {
                method: "POST",
                body: formData,
                credentials: "same-origin",
                headers: expectsJson
                    ? {
                        Accept: "application/json",
                        "X-Requested-With": "XMLHttpRequest"
                    }
                    : { Accept: "text/html" }
            });

            if (expectsJson) {
                const contentType = response.headers.get("content-type") ?? "";
                const result = contentType.includes("application/json")
                    ? await response.json()
                    : null;

                if (!response.ok || !result?.success) {
                    showHostGameplayError(
                        result?.error ?? response.statusText);
                    return;
                }

                await window.BadWolfHostGameplay.refresh();
                return;
            }

            if (!response.ok) {
                throw new Error(response.statusText);
            }

            const responseUrl = new URL(
                response.url || action.href,
                window.location.href);
            if (!canNavigate(responseUrl)) {
                window.location.assign(responseUrl.href);
                return;
            }

            await applyMarkup(await response.text(), responseUrl);
        } catch (error) {
            console.error("Host gameplay command failed.", error);
            window.location.reload();
        } finally {
            button?.removeAttribute("disabled");
            submissionInProgress = false;
        }
    }, true);

    document.addEventListener(
        "badwolf:host-gameplay-updated",
        syncPersistentHostChrome);
    syncPersistentHostChrome();
};

const configureDynamicHostShell = () => {
    initializeActionMenus();
    configureGameRoundIntroRoutes();
    configureHostGameplayFormNavigation();
};

document.addEventListener("badwolf:host-shell-mounted", configureDynamicHostShell);

if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", configureGameRoundIntroRoutes, { once: true });
    document.addEventListener("DOMContentLoaded", configureHostGameplayFormNavigation, { once: true });
} else {
    configureGameRoundIntroRoutes();
    configureHostGameplayFormNavigation();
}
