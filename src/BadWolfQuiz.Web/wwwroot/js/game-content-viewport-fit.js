(() => {
    if (window.badWolfGameContentViewportFitInitialized) {
        return;
    }

    window.badWolfGameContentViewportFitInitialized = true;

    const containerSelector = [
        ".host-game-board .current-question-summary:not(.wager-mode) .game-content-blocks:not(.four-clue-grid):not(.all-player-answer-grid)",
        ".host-game-board .question-review-preview .game-content-blocks:not(.four-clue-grid):not(.all-player-answer-grid)"
    ].join(",");
    const imageSelector = ":scope > .game-content-block > img.game-content-image";
    const overflowTolerance = 2;
    let frameHandle = 0;

    const getMinimumImageHeight = () =>
        Math.max(120, Math.min(180, Math.round(window.innerHeight * 0.18)));

    const clearCompactSize = image => {
        image.style.removeProperty("--game-content-fit-height");
        image.style.removeProperty("width");
        image.style.removeProperty("height");
        image.style.removeProperty("max-width");
        image.style.removeProperty("max-height");
    };

    const clearImageFit = (image, clearExpanded = true) => {
        clearCompactSize(image);
        image.removeAttribute("data-game-content-fit-state");
        image.removeAttribute("data-game-content-fit-eligible");
        image.removeAttribute("aria-pressed");
        image.removeAttribute("role");
        image.removeAttribute("tabindex");
        if (clearExpanded) {
            delete image.dataset.gameContentFitExpanded;
        }
    };

    const markInteractive = image => {
        image.dataset.gameContentFitEligible = "true";
        image.setAttribute("role", "button");
        image.setAttribute("tabindex", "0");
    };

    const markReady = image => {
        image.dataset.gameContentFitReady = "true";
        image.removeAttribute("data-game-content-fit-pending");
        image.style.removeProperty("visibility");
    };

    const setCompactSize = (image, height) => {
        const roundedHeight = Math.round(height);
        image.style.setProperty(
            "--game-content-fit-height",
            `${roundedHeight}px`);
        image.style.setProperty("width", "auto", "important");
        image.style.setProperty("height", "auto", "important");
        image.style.setProperty("max-width", "100%", "important");
        image.style.setProperty(
            "max-height",
            `${roundedHeight}px`,
            "important");
        image.dataset.gameContentFitState = "compact";
    };

    const applyCompactHeight = (container, image, height) => {
        let nextHeight = height;
        const minimumHeight = getMinimumImageHeight();

        for (let attempt = 0; attempt < 2; attempt++) {
            setCompactSize(image, nextHeight);

            const remainingOverflow =
                container.scrollHeight - container.clientHeight;
            if (remainingOverflow <= overflowTolerance ||
                nextHeight <= minimumHeight) {
                break;
            }

            nextHeight = Math.max(
                minimumHeight,
                nextHeight - remainingOverflow - overflowTolerance);
        }
    };

    const fitContainer = container => {
        const images = Array.from(container.querySelectorAll(imageSelector));
        container.querySelectorAll(
            "img.game-content-image[data-game-content-fit-eligible='true']")
            .forEach(image => {
                if (!images.includes(image)) {
                    clearImageFit(image);
                    markReady(image);
                }
            });

        if (images.length !== 1) {
            images.forEach(image => {
                clearImageFit(image);
                markReady(image);
            });
            return;
        }

        const image = images[0];
        if (!image.complete || image.naturalWidth <= 0 || container.clientHeight <= 0) {
            clearImageFit(image, false);
            return;
        }

        clearCompactSize(image);
        image.removeAttribute("data-game-content-fit-state");
        markInteractive(image);

        if (image.dataset.gameContentFitExpanded === "true") {
            image.dataset.gameContentFitState = "expanded";
            image.setAttribute("aria-pressed", "true");
            markReady(image);
            return;
        }

        const fullImageHeight = image.getBoundingClientRect().height;
        const overflow = container.scrollHeight - container.clientHeight;
        if (fullImageHeight <= 0 || overflow <= overflowTolerance) {
            clearImageFit(image);
            markReady(image);
            return;
        }

        const minimumHeight = getMinimumImageHeight();
        const targetHeight = Math.max(
            minimumHeight,
            Math.floor(fullImageHeight - overflow - overflowTolerance));

        if (targetHeight >= fullImageHeight - overflowTolerance) {
            clearImageFit(image);
            markReady(image);
            return;
        }

        markInteractive(image);
        image.setAttribute("aria-pressed", "false");
        applyCompactHeight(container, image, targetHeight);
        markReady(image);
    };

    const observedContainers = new WeakSet();
    const resizeObserver = typeof ResizeObserver === "function"
        ? new ResizeObserver(() => scheduleFit())
        : null;

    const observeContainer = container => {
        if (!resizeObserver || observedContainers.has(container)) {
            return;
        }

        observedContainers.add(container);
        resizeObserver.observe(container);
    };

    const fitAll = () => {
        frameHandle = 0;

        document.querySelectorAll(containerSelector).forEach(container => {
            fitContainer(container);
            observeContainer(container);
        });
    };

    function scheduleFit() {
        if (frameHandle !== 0) {
            return;
        }

        frameHandle = window.requestAnimationFrame(() => {
            frameHandle = window.requestAnimationFrame(fitAll);
        });
    }

    const getImageContainer = image => {
        const container = image.closest(".game-content-blocks");
        return container instanceof HTMLElement &&
            container.matches(containerSelector)
            ? container
            : null;
    };

    const toggleImage = image => {
        if (image.dataset.gameContentFitEligible !== "true") {
            return;
        }

        if (image.dataset.gameContentFitState === "compact") {
            image.dataset.gameContentFitExpanded = "true";
            clearCompactSize(image);
            image.dataset.gameContentFitState = "expanded";
            image.setAttribute("aria-pressed", "true");
            markReady(image);
            return;
        }

        delete image.dataset.gameContentFitExpanded;
        image.setAttribute("aria-pressed", "false");

        const container = getImageContainer(image);
        if (container) {
            fitContainer(container);
            observeContainer(container);
        } else {
            scheduleFit();
        }
    };

    document.addEventListener("click", event => {
        const image = event.target instanceof Element
            ? event.target.closest(
                "img.game-content-image[data-game-content-fit-eligible='true']")
            : null;
        if (!(image instanceof HTMLImageElement)) {
            return;
        }

        toggleImage(image);
    });

    document.addEventListener("keydown", event => {
        if (event.key !== "Enter" && event.key !== " ") {
            return;
        }

        const image = event.target instanceof HTMLImageElement
            ? event.target
            : null;
        if (!image || image.dataset.gameContentFitEligible !== "true") {
            return;
        }

        event.preventDefault();
        toggleImage(image);
    });

    document.addEventListener("load", event => {
        if (!(event.target instanceof HTMLImageElement) ||
            !event.target.matches(".game-content-image")) {
            return;
        }

        const container = getImageContainer(event.target);
        if (!container) {
            return;
        }

        fitContainer(container);
        observeContainer(container);
    }, true);

    document.addEventListener("badwolf:host-gameplay-updated", fitAll);
    window.addEventListener("resize", scheduleFit);
    window.addEventListener("pageshow", scheduleFit);

    fitAll();
})();