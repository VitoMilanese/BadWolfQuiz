(() => {
    if (window.badWolfGameplayEscapeShortcutsInitialized) {
        return;
    }

    window.badWolfGameplayEscapeShortcutsInitialized = true;

    const bootstrapUrl = document.currentScript?.src
        ? new URL(document.currentScript.src)
        : null;
    const getSharedAssetUrl = path => {
        const assetUrl = new URL(path, window.location.origin);
        if (!assetUrl.search && bootstrapUrl?.search) {
            assetUrl.search = bootstrapUrl.search;
        }
        return assetUrl;
    };
    const loadSharedScript = (path, options = {}) => {
        const script = document.createElement("script");
        script.src = getSharedAssetUrl(path).href;
        script.async = false;
        if (typeof options.onLoad === "function") {
            script.addEventListener("load", options.onLoad, { once: true });
        }
        if (typeof options.onError === "function") {
            script.addEventListener("error", options.onError, { once: true });
        }
        document.head.appendChild(script);
        return script;
    };
    const loadSharedStyle = path => {
        const stylesheet = document.createElement("link");
        stylesheet.rel = "stylesheet";
        stylesheet.href = getSharedAssetUrl(path).href;
        document.head.appendChild(stylesheet);
    };

    const installFinalQuestionProgressGuard = () => {
        const hubConnectionPrototype = window.signalR?.HubConnection?.prototype;
        if (!hubConnectionPrototype ||
            hubConnectionPrototype.badWolfFinalQuestionProgressGuardInstalled) {
            return;
        }

        const registerHandler = hubConnectionPrototype.on;
        if (typeof registerHandler !== "function") {
            return;
        }

        hubConnectionPrototype.badWolfFinalQuestionProgressGuardInstalled = true;

        const fallbackFormSelector =
            "form[action*='handler=SubmitMinimumFinalWager'], " +
            "form[action*='handler=SubmitEmptyFinalAnswer']";
        let hostSyncInProgress = false;
        let hostSyncPending = false;

        const syncLockButton = (currentBoard, nextBoard, handler) => {
            const selector =
                `form[action*='handler=${handler}'] button[type='submit']`;
            const currentButton = currentBoard.querySelector(selector);
            const nextButton = nextBoard.querySelector(selector);
            if (!(currentButton instanceof HTMLButtonElement) ||
                !(nextButton instanceof HTMLButtonElement)) {
                return;
            }

            currentButton.disabled = nextButton.disabled;
        };

        const syncFinalSubmissionRows = (currentBoard, nextBoard) => {
            const currentLists = Array.from(
                currentBoard.querySelectorAll(".final-submission-list"));
            const nextLists = Array.from(
                nextBoard.querySelectorAll(".final-submission-list"));
            if (currentLists.length !== nextLists.length) {
                return false;
            }

            for (let listIndex = 0; listIndex < currentLists.length; listIndex++) {
                const currentRows = Array.from(
                    currentLists[listIndex].querySelectorAll(":scope > li"));
                const nextRows = Array.from(
                    nextLists[listIndex].querySelectorAll(":scope > li"));
                if (currentRows.length !== nextRows.length) {
                    return false;
                }

                for (let rowIndex = 0; rowIndex < currentRows.length; rowIndex++) {
                    const currentRow = currentRows[rowIndex];
                    const nextRow = nextRows[rowIndex];
                    const currentStatus = currentRow.querySelector(
                        ":scope > strong + span");
                    const nextStatus = nextRow.querySelector(
                        ":scope > strong + span");
                    if (currentStatus && nextStatus) {
                        currentStatus.textContent = nextStatus.textContent;
                    }

                    const currentFallback = currentRow.querySelector(
                        fallbackFormSelector);
                    const nextFallback = nextRow.querySelector(
                        fallbackFormSelector);
                    if (currentFallback && !nextFallback) {
                        currentFallback.remove();
                    }
                }
            }

            syncLockButton(currentBoard, nextBoard, "LockFinalWagers");
            syncLockButton(currentBoard, nextBoard, "LockFinalAnswers");
            return true;
        };

        const syncHostFinalProgressOnce = async () => {
            const currentBoard = document.querySelector(
                ".host-game-board.final-question-host");
            if (!currentBoard) {
                return;
            }

            const response = await fetch(window.location.href, {
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

            const parsed = new DOMParser().parseFromString(
                await response.text(),
                "text/html");
            const nextBoard = parsed.querySelector(
                ".host-game-board.final-question-host");
            if (!nextBoard) {
                return;
            }

            if (currentBoard.dataset.gameStatus !== nextBoard.dataset.gameStatus) {
                return;
            }

            syncFinalSubmissionRows(currentBoard, nextBoard);
        };

        const requestHostFinalProgressSync = () => {
            if (hostSyncInProgress) {
                hostSyncPending = true;
                return;
            }

            hostSyncInProgress = true;
            void (async () => {
                do {
                    hostSyncPending = false;
                    try {
                        await syncHostFinalProgressOnce();
                    } catch (error) {
                        console.error(
                            "Final question progress sync failed.",
                            error);
                    }
                } while (hostSyncPending);
            })().finally(() => {
                hostSyncInProgress = false;
            });
        };

        hubConnectionPrototype.on = function(methodName, handler) {
            if (typeof methodName !== "string" ||
                methodName.toLowerCase() !== "finalquestionprogresschanged" ||
                typeof handler !== "function") {
                return registerHandler.apply(this, arguments);
            }

            return registerHandler.call(this, methodName, (...args) => {
                if (document.querySelector(".player-lobby[data-player-id]")) {
                    return;
                }

                if (document.querySelector(
                    ".host-game-board.final-question-host")) {
                    requestHostFinalProgressSync();
                    return;
                }

                return handler(...args);
            });
        };
    };

    installFinalQuestionProgressGuard();

    const brandLink = document.querySelector("a.brand[href]");
    if (brandLink instanceof HTMLAnchorElement) {
        brandLink.href = new URL("/", window.location.origin).href;
    }

    loadSharedStyle("/css/content-block-containers.css");
    loadSharedScript("/js/content-block-containers.js");
    const gameContentViewportFitVersion = "5";
    loadSharedStyle(
        `/css/game-content-viewport-fit.css?v=${gameContentViewportFitVersion}`);
    loadSharedScript(
        `/js/game-content-viewport-fit.js?v=${gameContentViewportFitVersion}`);

    const finalFallbackHandlers = new Set([
        "SubmitMinimumFinalWager",
        "SubmitEmptyFinalAnswer"
    ]);
    const pendingFinalFallbackClicks = [];
    const getFinalFallbackForm = button => {
        const form = button?.form;
        if (!(form instanceof HTMLFormElement) || !form.action) {
            return null;
        }

        const action = new URL(form.action, window.location.href);
        return finalFallbackHandlers.has(action.searchParams.get("handler"))
            ? form
            : null;
    };
    const replayPendingFinalFallbackClicks = () => {
        const pending = pendingFinalFallbackClicks.splice(0);
        for (const { button } of pending) {
            if (!(button instanceof HTMLButtonElement) || !button.isConnected) {
                continue;
            }

            button.disabled = false;
            button.removeAttribute("aria-busy");
            button.click();
        }
    };
    const releasePendingFinalFallbackClicks = () => {
        for (const { button } of pendingFinalFallbackClicks.splice(0)) {
            if (!(button instanceof HTMLButtonElement) || !button.isConnected) {
                continue;
            }

            button.disabled = false;
            button.removeAttribute("aria-busy");
        }
    };

    window.addEventListener("click", event => {
        if (window.badWolfFinalPlayerFallbackActionsInitialized) {
            return;
        }

        const button = event.target instanceof Element
            ? event.target.closest("button[type='submit']")
            : null;
        const form = getFinalFallbackForm(button);
        if (!(button instanceof HTMLButtonElement) ||
            !(form instanceof HTMLFormElement) ||
            button.disabled) {
            return;
        }

        event.preventDefault();
        event.stopImmediatePropagation();
        button.disabled = true;
        button.setAttribute("aria-busy", "true");
        pendingFinalFallbackClicks.push({ form, button });
    }, true);

    loadSharedScript("/js/final-player-fallback-actions.js?v=3", {
        onLoad: replayPendingFinalFallbackClicks,
        onError: () => {
            console.error("Final fallback actions could not be loaded.");
            releasePendingFinalFallbackClicks();
        }
    });

    const finalJudgingLocks = new Map();
    const releaseFinalJudgingLock = form => {
        const state = finalJudgingLocks.get(form);
        if (!state) {
            return;
        }

        window.clearTimeout(state.timeoutHandle);
        state.buttonObserver.disconnect();
        state.errorObserver?.disconnect();
        finalJudgingLocks.delete(form);

        if (!form.isConnected) {
            return;
        }

        if (!state.hadInert) {
            form.removeAttribute("inert");
        }
        delete form.dataset.finalJudgingSubmitting;

        for (const buttonState of state.buttonStates) {
            const { button } = buttonState;
            if (!button.isConnected) {
                continue;
            }

            button.disabled = buttonState.disabled;
            if (buttonState.ariaBusy === null) {
                button.removeAttribute("aria-busy");
            } else {
                button.setAttribute("aria-busy", buttonState.ariaBusy);
            }
        }
    };

    document.addEventListener("submit", event => {
        const form = event.target instanceof HTMLFormElement
            ? event.target
            : null;
        if (!form?.matches(".final-judging-actions")) {
            return;
        }

        if (form.dataset.finalJudgingSubmitting === "true") {
            event.preventDefault();
            event.stopImmediatePropagation();
            return;
        }

        const buttons = Array.from(
            form.querySelectorAll("button[name='isCorrect']"));
        if (buttons.length === 0) {
            return;
        }

        const submitter = event.submitter instanceof HTMLButtonElement
            ? event.submitter
            : null;
        const buttonStates = buttons.map(button => ({
            button,
            disabled: button.disabled,
            ariaBusy: button.getAttribute("aria-busy")
        }));
        const hadInert = form.hasAttribute("inert");

        form.dataset.finalJudgingSubmitting = "true";
        form.setAttribute("inert", "");
        for (const button of buttons) {
            button.disabled = true;
        }
        submitter?.setAttribute("aria-busy", "true");

        const buttonObserver = new MutationObserver(() => {
            if (form.dataset.finalJudgingSubmitting !== "true") {
                return;
            }

            for (const button of buttons) {
                if (!button.disabled) {
                    button.disabled = true;
                }
            }
        });
        buttonObserver.observe(form, {
            subtree: true,
            attributes: true,
            attributeFilter: ["disabled"]
        });

        const errorTarget = document.getElementById("game-board-error");
        const errorObserver = errorTarget
            ? new MutationObserver(() => {
                if (!errorTarget.hidden && errorTarget.textContent.trim()) {
                    releaseFinalJudgingLock(form);
                }
            })
            : null;
        errorObserver?.observe(errorTarget, {
            childList: true,
            subtree: true,
            attributes: true,
            attributeFilter: ["hidden", "class"]
        });

        const timeoutHandle = window.setTimeout(
            () => releaseFinalJudgingLock(form),
            15000);
        finalJudgingLocks.set(form, {
            hadInert,
            buttonStates,
            buttonObserver,
            errorObserver,
            timeoutHandle
        });
    }, true);

    document.addEventListener("badwolf:host-gameplay-updated", () => {
        for (const form of Array.from(finalJudgingLocks.keys())) {
            if (!form.isConnected) {
                releaseFinalJudgingLock(form);
            }
        }
    });

    const hostGameplayTarget = document.querySelector(".host-game-board");
    if (hostGameplayTarget) {
        loadSharedScript("/js/board-header-layout.js");
    }

    const playerNameMarqueeVersion = "3";
    let playerNameMarqueeAssetsLoaded = false;
    const loadPlayerNameMarqueeAssets = () => {
        if (playerNameMarqueeAssetsLoaded ||
            !document.querySelector(".host-game-board")) {
            return;
        }

        playerNameMarqueeAssetsLoaded = true;
        loadSharedStyle(
            `/css/player-name-marquee.css?v=${playerNameMarqueeVersion}`);
        loadSharedScript(
            `/js/player-name-marquee.js?v=${playerNameMarqueeVersion}`);
    };

    loadPlayerNameMarqueeAssets();
    for (const eventName of [
        "badwolf:host-shell-mounted",
        "badwolf:host-gameplay-updated"
    ]) {
        document.addEventListener(
            eventName,
            loadPlayerNameMarqueeAssets);
    }

    const contentBlockEditorTarget = document.querySelector(
        ".content-block-section");
    if (contentBlockEditorTarget) {
        loadSharedStyle("/css/content-block-reorder-buttons.css");
        loadSharedScript("/js/content-block-reorder-buttons.js");
    }

    const initializeEditorBrandNavigation = () => {
        if (!(brandLink instanceof HTMLAnchorElement)) {
            return;
        }

        const editorForm = document.querySelector("form.question-editor");
        const editorActions = editorForm?.querySelector(".editor-actions");
        if (!(editorForm instanceof HTMLFormElement) ||
            !(editorActions instanceof HTMLElement)) {
            return;
        }

        const proxyLink = document.createElement("a");
        proxyLink.href = brandLink.href;
        proxyLink.hidden = true;
        proxyLink.dataset.editorBrandNavigationProxy = "true";
        editorActions.appendChild(proxyLink);

        brandLink.addEventListener("click", event => {
            if (event.defaultPrevented ||
                event.button !== 0 ||
                event.altKey ||
                event.ctrlKey ||
                event.metaKey ||
                event.shiftKey) {
                return;
            }

            event.preventDefault();
            proxyLink.href = brandLink.href;
            proxyLink.click();
        });
    };

    const editorResetTarget = document.querySelector(
        "#question-editor-back-link, #final-question-editor-back-link, #description-editor-back");
    if (editorResetTarget) {
        loadSharedStyle("/css/editor-reset-button.css");
        loadSharedScript("/js/editor-reset-button.js", {
            onLoad: initializeEditorBrandNavigation
        });
    }

    const quizCloneTarget = document.querySelector(
        ".quiz-list .quiz-action-menu");
    if (quizCloneTarget) {
        loadSharedScript("/js/quiz-clone-action.js");
    }

    const editorSaveOverlayTarget = document.querySelector(
        "form.quiz-board-form, form.question-editor");
    if (editorSaveOverlayTarget) {
        loadSharedScript("/js/editor-save-overlay.js");
    }

    const hasBlockingUi = () =>
        document.querySelector(
            "dialog[open], details.action-menu[open], .language-menu.open, [role='menu']:not([hidden])") !== null;

    const findQuestionReviewReturnLink = () => {
        const links = document.querySelectorAll(
            ".question-review-preview .question-review-actions a[href]");

        for (const link of links) {
            const targetUrl = new URL(link.href, window.location.href);
            if (!targetUrl.searchParams.has("previewQuestionId")) {
                return link;
            }
        }

        return null;
    };

    document.addEventListener("keydown", event => {
        if (event.key !== "Escape" ||
            event.defaultPrevented ||
            event.isComposing ||
            event.repeat ||
            event.altKey ||
            event.ctrlKey ||
            event.metaKey ||
            event.shiftKey ||
            hasBlockingUi()) {
            return;
        }

        const initialIntroForm = document.querySelector(
            "form[data-game-intro-start]");
        if (initialIntroForm instanceof HTMLFormElement) {
            event.preventDefault();
            const submitter = initialIntroForm.querySelector(
                "button[type='submit'], input[type='submit']");
            if (submitter instanceof HTMLButtonElement ||
                submitter instanceof HTMLInputElement) {
                initialIntroForm.requestSubmit(submitter);
            } else {
                initialIntroForm.requestSubmit();
            }
            return;
        }

        const runningIntroFinish = document.querySelector(
            "[data-game-intro-page] .game-intro-actions a[href]:not([data-game-intro-next])");
        if (runningIntroFinish instanceof HTMLAnchorElement) {
            event.preventDefault();
            runningIntroFinish.click();
            return;
        }

        const questionReviewReturn = findQuestionReviewReturnLink();
        if (questionReviewReturn instanceof HTMLAnchorElement) {
            event.preventDefault();
            questionReviewReturn.click();
        }
    }, true);
})();
