(() => {
    "use strict";

    const resizerSelector = "[data-board-host-card-resizer]";
    const hostSlotSelector = "[data-board-host-slot]";
    const boardSelector = ".host-game-board[data-game-code]";
    const gameplayUpdatedEvent = "badwolf:host-gameplay-updated";

    let activePointerId = null;
    let liveSize = null;
    let storageKey = null;

    const resolveHostCard = () => {
        const board = document.querySelector(boardSelector);
        const hostSlot = document.querySelector(hostSlotSelector);
        const gameCode = board?.dataset.gameCode?.trim();
        if (!board || !hostSlot || !gameCode) {
            return null;
        }

        storageKey ??= `badwolfquiz:${gameCode}:host-card-size`;
        return { hostSlot };
    };

    const captureLiveSize = () => {
        if (activePointerId === null) {
            return;
        }

        const context = resolveHostCard();
        if (!context || !storageKey) {
            return;
        }

        const bounds = context.hostSlot.getBoundingClientRect();
        liveSize = {
            width: Math.round(bounds.width),
            height: Math.round(bounds.height)
        };

        // Lobby.cshtml may re-enter its persisted-size restoration while a pointer resize
        // is still active (for example from window resize or a gameplay refresh). Keep the
        // persisted snapshot synchronized with the live drag so that restoration can never
        // resurrect the previous, larger dimensions.
        localStorage.setItem(storageKey, JSON.stringify(liveSize));
    };

    const reapplyLiveSize = () => {
        if (activePointerId === null || !liveSize) {
            return;
        }

        const context = resolveHostCard();
        if (!context) {
            return;
        }

        context.hostSlot.style.setProperty(
            "--board-host-width",
            `${liveSize.width}px`);
        context.hostSlot.style.setProperty(
            "--board-host-height",
            `${liveSize.height}px`);
    };

    document.addEventListener("pointerdown", event => {
        const target = event.target instanceof Element
            ? event.target.closest(resizerSelector)
            : null;
        if (!target) {
            return;
        }

        activePointerId = event.pointerId;
        liveSize = null;
        storageKey = null;
        captureLiveSize();
    }, true);

    // Pointer capture keeps resize moves targeted at the host-card handle. This bubbling
    // listener therefore runs after Lobby.cshtml has applied the new width/height.
    document.addEventListener("pointermove", event => {
        if (event.pointerId !== activePointerId) {
            return;
        }

        captureLiveSize();
    });

    const finishResize = event => {
        if (event.pointerId !== activePointerId) {
            return;
        }

        captureLiveSize();
        activePointerId = null;
        liveSize = null;
        storageKey = null;
    };

    document.addEventListener("pointerup", finishResize);
    document.addEventListener("pointercancel", finishResize);

    window.addEventListener("resize", () => {
        if (activePointerId === null) {
            return;
        }

        // Lobby's resize listener schedules persisted restoration in requestAnimationFrame.
        // Schedule after it so the live drag remains authoritative for this frame.
        requestAnimationFrame(reapplyLiveSize);
    });

    document.addEventListener(gameplayUpdatedEvent, () => {
        if (activePointerId === null) {
            return;
        }

        // The gameplay-updated handler in Lobby first schedules relocation, which in turn
        // schedules persisted restoration on the following frame. Reapply after both frames.
        requestAnimationFrame(() => requestAnimationFrame(reapplyLiveSize));
    });
})();
