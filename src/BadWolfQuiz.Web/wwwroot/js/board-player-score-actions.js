(() => {
    "use strict";

    if (window.badWolfBoardPlayerScoreActionsInstalled) {
        return;
    }

    window.badWolfBoardPlayerScoreActionsInstalled = true;

    const rowSelector =
        ".board-player-sidebar .board-player-list [data-sidebar-player-id]";
    const controlsSelector = "[data-board-player-score-actions]";
    const actionSelector = "[data-board-player-score-mode]";
    const contextActionSelector =
        "#player-score-context-menu [data-quick-score-mode]";
    const iconSelector = ".score-adjustment-action-icon";

    const style = document.createElement("style");
    style.textContent = `
        .board-player-score-controls {
            display: inline-flex;
            align-items: center;
            justify-content: flex-end;
            gap: 4px;
            min-width: 0;
        }

        .board-player-score-controls .board-player-score {
            min-width: 3.5ch;
            text-align: right;
        }

        .board-player-score-buttons {
            display: none;
            align-items: center;
            gap: 2px;
            flex: 0 0 auto;
        }

        .board-player-sidebar .board-player-list [data-sidebar-player-id]:hover
            .board-player-score-buttons {
            display: inline-flex;
        }

        .board-player-score-controls .board-player-score-action {
            width: 26px;
            height: 26px;
            min-width: 26px;
            min-height: 26px;
            display: inline-grid;
            place-items: center;
            padding: 0;
            border-radius: 6px;
        }

        .score-adjustment-action-icon {
            position: relative;
            display: block;
            width: 10px;
            height: 10px;
            pointer-events: none;
        }

        .score-adjustment-action-icon::before,
        .score-adjustment-action-icon::after {
            content: "";
            position: absolute;
            left: 50%;
            top: 50%;
            width: 10px;
            height: 2px;
            border-radius: 999px;
            background: currentColor;
            transform: translate(-50%, -50%);
        }

        .score-adjustment-action-icon::after {
            width: 2px;
            height: 10px;
        }

        [data-board-player-score-mode="subtract"]
            .score-adjustment-action-icon::after,
        #player-score-context-menu [data-quick-score-mode="subtract"]
            .score-adjustment-action-icon::after {
            display: none;
        }

        .board-player-score-controls
            .board-player-score-action[data-board-player-score-mode="add"] {
            color: color-mix(in srgb, #3fb950 76%, var(--text));
        }

        .board-player-score-controls
            .board-player-score-action[data-board-player-score-mode="subtract"] {
            color: color-mix(in srgb, #ef233c 82%, var(--text));
        }
    `;
    document.head.appendChild(style);

    const getActionLabel = mode => {
        const sourceAction = document.querySelector(
            `#player-score-context-menu [data-quick-score-mode="${mode}"]`);
        return sourceAction?.getAttribute("aria-label") ||
            sourceAction?.getAttribute("title") ||
            (mode === "add" ? "+" : "−");
    };

    const createIcon = () => {
        const icon = document.createElement("span");
        icon.className = "score-adjustment-action-icon";
        icon.setAttribute("aria-hidden", "true");
        return icon;
    };

    const createAction = mode => {
        const button = document.createElement("button");
        const label = getActionLabel(mode);
        button.className =
            "button button-secondary icon-button board-player-score-action";
        button.type = "button";
        button.dataset.boardPlayerScoreMode = mode;
        button.title = label;
        button.setAttribute("aria-label", label);
        button.appendChild(createIcon());
        return button;
    };

    const decorateRow = row => {
        if (!(row instanceof HTMLElement) || row.querySelector(controlsSelector)) {
            return;
        }

        const score = row.querySelector(".board-player-score");
        if (!(score instanceof HTMLElement)) {
            return;
        }

        const controls = document.createElement("span");
        controls.className = "board-player-score-controls";
        controls.dataset.boardPlayerScoreActions = "";

        const buttons = document.createElement("span");
        buttons.className = "board-player-score-buttons";
        buttons.append(
            createAction("add"),
            createAction("subtract"));

        controls.append(score, buttons);
        row.appendChild(controls);
    };

    const decorateRows = root => {
        if (root instanceof Element && root.matches(rowSelector)) {
            decorateRow(root);
        }

        root.querySelectorAll?.(rowSelector).forEach(decorateRow);
    };

    const decorateContextAction = action => {
        if (!(action instanceof HTMLButtonElement) || action.querySelector(iconSelector)) {
            return;
        }

        action.replaceChildren(createIcon());
    };

    const decorateContextActions = root => {
        if (root instanceof Element && root.matches(contextActionSelector)) {
            decorateContextAction(root);
        }

        root.querySelectorAll?.(contextActionSelector).forEach(decorateContextAction);
    };

    const openQuickScore = (button, row) => {
        const mode = button.dataset.boardPlayerScoreMode;
        const playerId = row.dataset.sidebarPlayerId ?? "";
        const playerName = row.querySelector(".board-player-name")
            ?.textContent?.trim() ?? "";
        const quickScore = window.BadWolfQuickScore;
        const contextAction = document.querySelector(
            `#player-score-context-menu [data-quick-score-mode="${mode}"]`);

        if (!playerId || !quickScore?.show || !(contextAction instanceof HTMLElement)) {
            return;
        }

        const playerProxy = document.createElement("span");
        playerProxy.dataset.playerId = playerId;
        playerProxy.dataset.playerName = playerName;

        const rect = button.getBoundingClientRect();
        quickScore.show(
            {
                preventDefault() {},
                stopPropagation() {},
                clientX: rect.left,
                clientY: rect.bottom
            },
            playerProxy);
        contextAction.click();
    };

    document.addEventListener("click", event => {
        const target = event.target instanceof Element ? event.target : null;
        const button = target?.closest(actionSelector);
        const row = button?.closest(rowSelector);
        if (!(button instanceof HTMLButtonElement) || !(row instanceof HTMLElement)) {
            return;
        }

        event.preventDefault();
        event.stopPropagation();
        openQuickScore(button, row);
    });

    decorateRows(document);
    decorateContextActions(document);

    new MutationObserver(mutations => {
        for (const mutation of mutations) {
            for (const node of mutation.addedNodes) {
                if (node instanceof Element) {
                    decorateRows(node);
                    decorateContextActions(node);
                }
            }
        }
    }).observe(document.documentElement, {
        childList: true,
        subtree: true
    });
})();
