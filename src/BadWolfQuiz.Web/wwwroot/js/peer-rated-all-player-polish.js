(() => {
    "use strict";

    if (window.badWolfPeerRatedPolishInitialized) {
        return;
    }
    window.badWolfPeerRatedPolishInitialized = true;

    const metadataPath = "/api/peer-rated-question-metadata";
    const peerApiPath = "/api/peer-rated-all-player-question";
    const metadataCache = new Map();
    const culture = (document.documentElement.lang || "en")
        .slice(0, 2)
        .toLowerCase();
    const answerKeyText = {
        en: { question: "Question", showQuestion: "Show question" },
        uk: { question: "Питання", showQuestion: "Показати питання" },
        it: { question: "Domanda", showQuestion: "Mostra domanda" },
        ru: { question: "Україна", showQuestion: "Україна" }
    }[culture] ?? { question: "Question", showQuestion: "Show question" };

    const style = document.createElement("style");
    style.id = "peer-rated-all-player-polish-styles";
    style.textContent = `
.player-lobby.peer-rated-player-active .player-buzzer-panel,
.player-buzzer-panel[data-hidden-by-peer-rated="true"] {
    display: none !important;
}
.host-game-board.peer-rated-question-shell-active .question-controls {
    display: none !important;
}
.host-game-board.peer-rated-returning-to-board .current-question-summary {
    visibility: hidden !important;
}
[data-peer-rated-answer-ui-hidden="true"],
.question-review-preview [data-peer-rated-answer-preview-hidden="true"] {
    display: none !important;
}
`;
    document.head.appendChild(style);

    const setClassState = (element, className, active) => {
        if (!(element instanceof Element) ||
            element.classList.contains(className) === active) {
            return;
        }
        element.classList.toggle(className, active);
    };

    let hostBoundsFrame = 0;
    let hostBoundsSettleHandle = 0;
    let hostBoundsObservedSummary = null;
    let hostBoundsObservedScoreboard = null;
    const hostBoundsResizeObserver = typeof ResizeObserver === "function"
        ? new ResizeObserver(() => schedulePeerRatedHostBounds())
        : null;

    const applyPeerRatedHostBounds = () => {
        const board = document.querySelector(".host-game-board[data-game-code]");
        const summary = board?.querySelector(".current-question-summary");
        const ui = summary?.querySelector(".peer-rated-host-ui");
        if (!(board instanceof HTMLElement) ||
            !(summary instanceof HTMLElement) ||
            !(ui instanceof HTMLElement)) {
            return;
        }

        const scoreboard = board.querySelector(".game-scoreboard");
        let bottomGap = 0;
        if (scoreboard instanceof HTMLElement) {
            const summaryRect = summary.getBoundingClientRect();
            const scoreboardRect = scoreboard.getBoundingClientRect();
            if (scoreboardRect.width > 0 && scoreboardRect.height > 0) {
                bottomGap = Math.max(
                    0,
                    Math.ceil(summaryRect.bottom - scoreboardRect.top));
            }
        }

        const nextBottom = `${bottomGap}px`;
        if (ui.style.bottom !== nextBottom) {
            ui.style.bottom = nextBottom;
        }
    };

    const syncHostBoundsResizeObserver = () => {
        if (!hostBoundsResizeObserver) {
            return;
        }
        const board = document.querySelector(".host-game-board[data-game-code]");
        const summary = board?.querySelector(".current-question-summary");
        const scoreboard = board?.querySelector(".game-scoreboard");
        const nextSummary = summary instanceof HTMLElement ? summary : null;
        const nextScoreboard = scoreboard instanceof HTMLElement ? scoreboard : null;
        if (nextSummary === hostBoundsObservedSummary &&
            nextScoreboard === hostBoundsObservedScoreboard) {
            return;
        }

        hostBoundsResizeObserver.disconnect();
        hostBoundsObservedSummary = nextSummary;
        hostBoundsObservedScoreboard = nextScoreboard;
        if (hostBoundsObservedSummary) {
            hostBoundsResizeObserver.observe(hostBoundsObservedSummary);
        }
        if (hostBoundsObservedScoreboard) {
            hostBoundsResizeObserver.observe(hostBoundsObservedScoreboard);
        }
    };

    function schedulePeerRatedHostBounds() {
        if (hostBoundsFrame && typeof window.cancelAnimationFrame === "function") {
            window.cancelAnimationFrame(hostBoundsFrame);
        }
        const apply = () => {
            hostBoundsFrame = 0;
            syncHostBoundsResizeObserver();
            applyPeerRatedHostBounds();
        };
        if (typeof window.requestAnimationFrame === "function") {
            hostBoundsFrame = window.requestAnimationFrame(apply);
        } else {
            apply();
        }

        window.clearTimeout(hostBoundsSettleHandle);
        hostBoundsSettleHandle = window.setTimeout(() => {
            syncHostBoundsResizeObserver();
            applyPeerRatedHostBounds();
        }, 80);
    }

    const getMetadata = async (gameElement, sourceQuestionId) => {
        if (!(gameElement instanceof HTMLElement) || !Number.isInteger(sourceQuestionId)) {
            return null;
        }
        const code = gameElement.dataset.gameCode;
        if (!code) {
            return null;
        }

        const key = `${code}:${sourceQuestionId}`;
        if (metadataCache.has(key)) {
            return metadataCache.get(key);
        }

        const request = fetch(
            `${metadataPath}?code=${encodeURIComponent(code)}` +
            `&sourceQuestionId=${encodeURIComponent(sourceQuestionId)}`,
            {
                credentials: "same-origin",
                headers: { Accept: "application/json" }
            })
            .then(response => response.ok ? response.json() : null)
            .catch(() => null);
        metadataCache.set(key, request);
        return request;
    };

    const syncPlayerBuzzer = () => {
        const lobby = document.querySelector(".player-lobby");
        if (!(lobby instanceof HTMLElement)) {
            return;
        }

        const peerPanel = lobby.querySelector(".peer-rated-player-panel");
        const active = peerPanel instanceof HTMLElement && !peerPanel.hidden;
        setClassState(lobby, "peer-rated-player-active", active);

        const buzzer = lobby.querySelector(".player-buzzer-panel");
        if (active && buzzer instanceof HTMLElement) {
            if (!buzzer.hidden) {
                buzzer.hidden = true;
            }
            if (buzzer.getAttribute("aria-hidden") !== "true") {
                buzzer.setAttribute("aria-hidden", "true");
            }
        } else if (buzzer instanceof HTMLElement &&
            buzzer.dataset.hiddenByPeerRated !== "true" &&
            buzzer.hasAttribute("aria-hidden")) {
            buzzer.removeAttribute("aria-hidden");
        }
    };

    const syncEditorAnswerUi = () => {
        const form = document.querySelector("form.question-editor");
        const presentation = document.getElementById("Input_PresentationType");
        if (!(form instanceof HTMLFormElement) ||
            !(presentation instanceof HTMLSelectElement)) {
            return;
        }

        const peerRated = presentation.value === "5";
        const answerSection = document.getElementById("answer-blocks");
        const answerHeading = answerSection?.previousElementSibling;
        const answerValidation = answerSection?.nextElementSibling;
        const answerPreview = form.querySelector(
            '[data-open-question-preview="answer"]');

        for (const element of [answerHeading, answerSection, answerValidation, answerPreview]) {
            if (!(element instanceof HTMLElement)) {
                continue;
            }
            if (peerRated) {
                if (element.dataset.peerRatedAnswerUiHidden !== "true") {
                    element.dataset.peerRatedAnswerUiHidden = "true";
                }
                if (element.getAttribute("aria-hidden") !== "true") {
                    element.setAttribute("aria-hidden", "true");
                }
            } else {
                if (element.dataset.peerRatedAnswerUiHidden !== undefined) {
                    delete element.dataset.peerRatedAnswerUiHidden;
                }
                if (element.hasAttribute("aria-hidden")) {
                    element.removeAttribute("aria-hidden");
                }
            }
        }
    };

    const parseQuestionId = value => {
        const parsed = Number.parseInt(value ?? "", 10);
        return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
    };

    const getCurrentQuestionId = board => {
        const blockContainer = board.querySelector(
            ".current-question-summary [data-source-question-id]");
        return parseQuestionId(blockContainer?.getAttribute("data-source-question-id"));
    };

    const getPreviewQuestionId = () => {
        const parameters = new URLSearchParams(window.location.search);
        return parseQuestionId(parameters.get("previewQuestionId"));
    };

    const removePreviewAnswerParameter = () => {
        const url = new URL(window.location.href);
        if (!url.searchParams.has("previewAnswer")) {
            return false;
        }
        url.searchParams.delete("previewAnswer");
        window.location.replace(url.toString());
        return true;
    };

    const relabelPeerAnswerAsQuestion = board => {
        const presentation = board.querySelector(
            ".current-question-summary .answer-presentation");
        if (!(presentation instanceof HTMLElement)) {
            return;
        }

        setClassState(presentation, "answer-presentation", false);
        setClassState(presentation, "question-presentation", true);

        const heading = presentation.querySelector("[data-question-heading]");
        if (!(heading instanceof HTMLElement)) {
            return;
        }
        const template = heading.dataset.rewardTemplate;
        const reward = heading.dataset.rewardFullValue;
        if (template && reward) {
            const nextText = template.replace("__REWARD__", reward);
            if (heading.textContent !== nextText) {
                heading.textContent = nextText;
            }
        }
    };

    let hostSyncRevision = 0;
    const syncHostUi = async () => {
        const board = document.querySelector(".host-game-board[data-game-code]");
        if (!(board instanceof HTMLElement)) {
            return;
        }

        const revision = ++hostSyncRevision;
        const previewQuestionId = getPreviewQuestionId();
        if (previewQuestionId) {
            const metadata = await getMetadata(board, previewQuestionId);
            if (revision !== hostSyncRevision || !metadata?.peerRated) {
                return;
            }

            setClassState(board, "peer-rated-question-shell-active", false);
            const preview = document.querySelector(".question-review-preview");
            preview?.querySelectorAll('a[href*="previewAnswer"]')
                .forEach(link => {
                    if (link instanceof HTMLElement) {
                        if (link.dataset.peerRatedAnswerPreviewHidden !== "true") {
                            link.dataset.peerRatedAnswerPreviewHidden = "true";
                        }
                        if (link.getAttribute("aria-hidden") !== "true") {
                            link.setAttribute("aria-hidden", "true");
                        }
                    }
                });

            const parameters = new URLSearchParams(window.location.search);
            if (parameters.has("previewAnswer")) {
                removePreviewAnswerParameter();
            }
            return;
        }

        const sourceQuestionId = getCurrentQuestionId(board);
        if (!sourceQuestionId) {
            setClassState(board, "peer-rated-question-shell-active", false);
            return;
        }

        const metadata = await getMetadata(board, sourceQuestionId);
        if (revision !== hostSyncRevision) {
            return;
        }
        setClassState(
            board,
            "peer-rated-question-shell-active",
            Boolean(metadata?.peerRated));
        if (metadata?.peerRated) {
            relabelPeerAnswerAsQuestion(board);
        }
    };

    let answerKeySyncRevision = 0;
    const syncAnswerKey = async () => {
        const page = document.querySelector(
            ".answer-key-page[data-game-code][data-source-question-id]");
        if (!(page instanceof HTMLElement)) {
            return;
        }
        const sourceQuestionId = parseQuestionId(page.dataset.sourceQuestionId);
        if (!sourceQuestionId) {
            return;
        }

        const revision = ++answerKeySyncRevision;
        const metadata = await getMetadata(page, sourceQuestionId);
        if (revision !== answerKeySyncRevision || !metadata?.peerRated) {
            return;
        }

        const header = document.querySelector(".answer-key-header-context h2");
        if (header instanceof HTMLElement && header.textContent !== answerKeyText.question) {
            header.textContent = answerKeyText.question;
        }
        const toggle = document.querySelector("[data-answer-key-visibility-toggle]");
        if (toggle instanceof HTMLElement &&
            toggle.getAttribute("aria-label") !== answerKeyText.question) {
            toggle.setAttribute("aria-label", answerKeyText.question);
        }
        const placeholder = document.querySelector("[data-answer-key-hidden-placeholder]");
        const placeholderTitle = placeholder?.querySelector("strong");
        const placeholderAction = placeholder?.querySelector("span");
        if (placeholderTitle instanceof HTMLElement &&
            placeholderTitle.textContent !== answerKeyText.question) {
            placeholderTitle.textContent = answerKeyText.question;
        }
        if (placeholderAction instanceof HTMLElement &&
            placeholderAction.textContent !== answerKeyText.showQuestion) {
            placeholderAction.textContent = answerKeyText.showQuestion;
        }
        const content = document.querySelector("[data-answer-key-content]");
        if (content instanceof HTMLElement) {
            setClassState(content, "answer-presentation", false);
            setClassState(content, "question-presentation", true);
        }
    };

    const isReturnToBoardRequest = input => {
        const rawUrl = input instanceof Request ? input.url : String(input ?? "");
        try {
            const url = new URL(rawUrl, window.location.origin);
            return url.pathname === peerApiPath &&
                url.searchParams.get("handler") === "ReturnToBoard";
        } catch {
            return rawUrl.includes(peerApiPath) &&
                rawUrl.includes("handler=ReturnToBoard");
        }
    };

    const setReturningToBoard = active => {
        const board = document.querySelector(".host-game-board[data-game-code]");
        if (board instanceof HTMLElement) {
            setClassState(board, "peer-rated-returning-to-board", active);
        }
    };

    if (!window.badWolfPeerRatedReturnFetchWrapped) {
        window.badWolfPeerRatedReturnFetchWrapped = true;
        const originalFetch = window.fetch.bind(window);
        window.fetch = async (...args) => {
            const returnToBoard = isReturnToBoardRequest(args[0]);
            if (returnToBoard) {
                setReturningToBoard(true);
            }
            try {
                const response = await originalFetch(...args);
                if (returnToBoard) {
                    if (!response.ok) {
                        setReturningToBoard(false);
                    } else {
                        void response.clone().json()
                            .then(payload => {
                                if (payload?.success) {
                                    window.setTimeout(() => window.location.reload(), 100);
                                } else {
                                    setReturningToBoard(false);
                                }
                            })
                            .catch(() => setReturningToBoard(false));
                    }
                }
                return response;
            } catch (error) {
                if (returnToBoard) {
                    setReturningToBoard(false);
                }
                throw error;
            }
        };
    }

    const sync = () => {
        syncPlayerBuzzer();
        syncEditorAnswerUi();
        void syncHostUi();
        void syncAnswerKey();
        schedulePeerRatedHostBounds();
    };

    document.addEventListener("change", event => {
        if (event.target instanceof HTMLSelectElement &&
            event.target.id === "Input_PresentationType") {
            syncEditorAnswerUi();
        }
    });
    document.addEventListener("badwolf:host-gameplay-updated", () => {
        void syncHostUi();
        schedulePeerRatedHostBounds();
    });
    document.addEventListener("badwolf:host-shell-mounted", () => {
        void syncHostUi();
        schedulePeerRatedHostBounds();
    });
    window.addEventListener("resize", schedulePeerRatedHostBounds, { passive: true });
    window.visualViewport?.addEventListener(
        "resize",
        schedulePeerRatedHostBounds,
        { passive: true });
    window.addEventListener("pageshow", sync);

    sync();
    new MutationObserver(() => sync())
        .observe(document.documentElement, {
            childList: true,
            subtree: true
        });
})();