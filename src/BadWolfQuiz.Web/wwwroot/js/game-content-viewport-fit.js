(() => {
    if (window.badWolfGameContentViewportFitInitialized) {
        return;
    }

    window.badWolfGameContentViewportFitInitialized = true;

    const containerSelector = [
        ".host-game-board .current-question-summary:not(.wager-mode) .game-content-blocks:not(.four-clue-grid):not(.all-player-answer-grid)",
        ".host-game-board .question-review-preview .game-content-blocks:not(.four-clue-grid):not(.all-player-answer-grid)",
        ".host-game-board .final-question-panel .game-content-presentation .game-content-blocks:not(.four-clue-grid):not(.all-player-answer-grid)"
    ].join(",");
    const imageSelector = ":scope > .game-content-block > img.game-content-image";
    const gameplayImageSelector = [
        "img.game-content-image",
        "img.final-question-transition-image",
        ".all-player-choice-option img",
        ".all-player-host-choice-option img"
    ].join(",");
    const gameplayImageRadius = "clamp(12px, 1.3vw, 20px)";
    const overflowTolerance = 2;
    let frameHandle = 0;

    const applyGameplayImageCorners = image => {
        if (!(image instanceof HTMLImageElement)) {
            return;
        }

        image.style.setProperty(
            "border-radius",
            gameplayImageRadius,
            "important");

        if (!image.complete ||
            image.naturalWidth <= 0 ||
            image.naturalHeight <= 0) {
            image.style.removeProperty("clip-path");
            return;
        }

        const bounds = image.getBoundingClientRect();
        if (bounds.width <= 0 || bounds.height <= 0) {
            return;
        }

        const naturalRatio = image.naturalWidth / image.naturalHeight;
        const boxRatio = bounds.width / bounds.height;
        let horizontalInset = 0;
        let verticalInset = 0;

        if (boxRatio > naturalRatio) {
            const paintedWidth = bounds.height * naturalRatio;
            horizontalInset = Math.max(
                0,
                (bounds.width - paintedWidth) / 2);
        } else if (boxRatio < naturalRatio) {
            const paintedHeight = bounds.width / naturalRatio;
            verticalInset = Math.max(
                0,
                (bounds.height - paintedHeight) / 2);
        }

        image.style.setProperty(
            "clip-path",
            `inset(${verticalInset}px ${horizontalInset}px ${verticalInset}px ${horizontalInset}px round ${gameplayImageRadius})`,
            "important");
    };

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
        delete image.dataset.gameContentFitSettling;
        image.dataset.gameContentFitReady = "true";
        image.removeAttribute("data-game-content-fit-pending");
        image.style.removeProperty("visibility");
    };

    const settleInitialFit = image => {
        if (image.dataset.gameContentFitReady === "true") {
            markReady(image);
            return;
        }

        if (image.dataset.gameContentFitSettling === "true") {
            markReady(image);
            return;
        }

        image.dataset.gameContentFitSettling = "true";
        scheduleFit();
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
        images.forEach(applyGameplayImageCorners);

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
            settleInitialFit(image);
            return;
        }

        const fullImageHeight = image.getBoundingClientRect().height;
        const overflow = container.scrollHeight - container.clientHeight;
        if (fullImageHeight <= 0 || overflow <= overflowTolerance) {
            clearImageFit(image);
            settleInitialFit(image);
            return;
        }

        const imageBlock = image.closest(".game-content-block");
        const imageBlockHeight = imageBlock instanceof HTMLElement
            ? imageBlock.getBoundingClientRect().height
            : fullImageHeight;
        const overflowWithoutImage =
            container.scrollHeight - imageBlockHeight - container.clientHeight;

        // If the text/other blocks already need scrolling, shrinking the image
        // cannot remove the scrollbar. Keep the image at its normal presentation
        // size and let the existing scroll container expose the full answer.
        if (overflowWithoutImage > overflowTolerance) {
            clearImageFit(image);
            settleInitialFit(image);
            return;
        }

        const minimumHeight = getMinimumImageHeight();
        const targetHeight = Math.max(
            minimumHeight,
            Math.floor(fullImageHeight - overflow - overflowTolerance));

        if (targetHeight >= fullImageHeight - overflowTolerance) {
            clearImageFit(image);
            settleInitialFit(image);
            return;
        }

        markInteractive(image);
        image.setAttribute("aria-pressed", "false");
        applyCompactHeight(container, image, targetHeight);
        settleInitialFit(image);
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

        document.querySelectorAll(gameplayImageSelector)
            .forEach(applyGameplayImageCorners);

        document.querySelectorAll(containerSelector).forEach(container => {
            fitContainer(container);
            observeContainer(container);
        });

        document.querySelectorAll(gameplayImageSelector)
            .forEach(applyGameplayImageCorners);
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
            scheduleFit();
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
        if (!(event.target instanceof HTMLImageElement)) {
            return;
        }

        if (event.target.matches(gameplayImageSelector)) {
            applyGameplayImageCorners(event.target);
        }

        if (!event.target.matches(".game-content-image")) {
            return;
        }

        const container = getImageContainer(event.target);
        if (!container) {
            return;
        }

        fitContainer(container);
        observeContainer(container);
        applyGameplayImageCorners(event.target);
    }, true);

    document.addEventListener("badwolf:host-gameplay-updated", fitAll);
    window.addEventListener("resize", scheduleFit);
    window.addEventListener("pageshow", scheduleFit);

    fitAll();
})();