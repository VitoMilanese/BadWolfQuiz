(() => {
    "use strict";

    const card = document.querySelector(
        ".question-inbox-card:only-child.is-awaiting-reply");
    const panel = card?.querySelector(".question-inbox-actions-panel");
    if (!(card instanceof HTMLElement) || !(panel instanceof HTMLElement)) {
        return;
    }

    const footer = document.querySelector(".portal-footer");
    const visualViewport = window.visualViewport;

    card.classList.add("has-floating-reply");
    panel.classList.add("is-floating-reply");

    const updatePlacement = () => {
        let visibleFooterHeight = 0;
        if (footer instanceof HTMLElement) {
            const rect = footer.getBoundingClientRect();
            const viewportHeight = visualViewport?.height ?? window.innerHeight;
            visibleFooterHeight = Math.max(
                0,
                Math.min(rect.bottom, viewportHeight) - Math.max(rect.top, 0));
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
            `${Math.ceil(visibleFooterHeight)}px`);
        panel.style.setProperty(
            "--question-inbox-floating-left",
            `${Math.round(left)}px`);
        panel.style.setProperty(
            "--question-inbox-floating-width",
            `${Math.round(width)}px`);
    };

    updatePlacement();
    window.addEventListener("resize", updatePlacement, { passive: true });
    visualViewport?.addEventListener("resize", updatePlacement, { passive: true });
    visualViewport?.addEventListener("scroll", updatePlacement, { passive: true });

    if (typeof ResizeObserver === "function") {
        const observer = new ResizeObserver(updatePlacement);
        observer.observe(card);
        if (footer instanceof HTMLElement) {
            observer.observe(footer);
        }
    }
})();
