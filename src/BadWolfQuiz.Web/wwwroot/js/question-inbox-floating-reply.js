(() => {
    "use strict";

    const initialize = () => {
        const card = document.querySelector(
            ".question-inbox-card:only-child.is-awaiting-reply");
        const panel = card?.querySelector(".question-inbox-actions-panel");
        if (!(card instanceof HTMLElement) || !(panel instanceof HTMLElement)) {
            return;
        }

        const visualViewport = window.visualViewport;

        card.classList.add("has-floating-reply");
        panel.classList.add("is-floating-reply");

        const updatePlacement = () => {
            const viewportTop = visualViewport?.offsetTop ?? 0;
            const viewportHeight = visualViewport?.height ?? window.innerHeight;
            const viewportBottom = viewportTop + viewportHeight;
            const footer = document.querySelector(".portal-footer");

            let footerOffset = 0;
            if (footer instanceof HTMLElement) {
                const footerRect = footer.getBoundingClientRect();
                const footerTop = Math.min(
                    Math.max(footerRect.top, viewportTop),
                    viewportBottom);
                footerOffset = Math.max(0, viewportBottom - footerTop);
            }

            const cardRect = card.getBoundingClientRect();
            const viewportWidth = visualViewport?.width ?? window.innerWidth;
            const horizontalGap = 12;
            const width = Math.max(
                0,
                Math.min(cardRect.width, viewportWidth - (horizontalGap * 2)));
            const left = Math.min(
                Math.max(cardRect.left, horizontalGap),
                Math.max(horizontalGap, viewportWidth - width - horizontalGap));

            panel.style.setProperty(
                "--question-inbox-footer-offset",
                `${Math.ceil(footerOffset)}px`);
            panel.style.setProperty(
                "--question-inbox-floating-left",
                `${Math.round(left)}px`);
            panel.style.setProperty(
                "--question-inbox-floating-width",
                `${Math.round(width)}px`);
        };

        updatePlacement();
        window.addEventListener("resize", updatePlacement, { passive: true });
        window.addEventListener("load", updatePlacement, { once: true });
        visualViewport?.addEventListener("resize", updatePlacement, { passive: true });
        visualViewport?.addEventListener("scroll", updatePlacement, { passive: true });

        if (typeof ResizeObserver === "function") {
            const observer = new ResizeObserver(updatePlacement);
            observer.observe(card);

            const footer = document.querySelector(".portal-footer");
            if (footer instanceof HTMLElement) {
                observer.observe(footer);
            }
        }
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initialize, { once: true });
    } else {
        initialize();
    }
})();
