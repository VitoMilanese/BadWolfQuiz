(() => {
    "use strict";

    if (window.badWolfPeerRatedLayoutInitialized) {
        return;
    }
    window.badWolfPeerRatedLayoutInitialized = true;

    const style = document.createElement("style");
    style.id = "peer-rated-all-player-layout-styles";
    style.textContent = `
.player-lobby > .peer-rated-player-panel {
    grid-column: 1 / -1 !important;
    width: 100% !important;
    max-width: none !important;
    min-width: 0;
    box-sizing: border-box;
    justify-self: stretch !important;
}

.player-lobby > .peer-rated-player-panel > [data-peer-controls],
.player-lobby > .peer-rated-player-panel .stack-form,
.player-lobby > .peer-rated-player-panel .peer-rated-review-card {
    width: 100%;
    max-width: none;
    min-width: 0;
    box-sizing: border-box;
}

.host-game-board.peer-rated-reviewing .peer-rated-question-context {
    width: auto !important;
    max-width: none !important;
    justify-self: stretch !important;
    padding: 0 !important;
    border: 0 !important;
    border-radius: 0 !important;
    background: transparent !important;
    box-shadow: none !important;
}

.host-game-board.peer-rated-reviewing
    .peer-rated-question-context > .game-content-blocks {
    width: 100% !important;
    max-width: none !important;
    min-width: 0;
}
`;
    document.head.appendChild(style);
})();
