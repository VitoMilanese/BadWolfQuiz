(() => {
    const content = document.querySelector("[data-answer-key-content]");
    if (!(content instanceof HTMLElement)) {
        return;
    }

    const container = content.querySelector(":scope > .game-content-blocks");
    if (!(container instanceof HTMLElement)) {
        return;
    }

    let frameHandle = 0;

    const getSingleImageContext = () => {
        const blocks = Array.from(container.children)
            .filter(element => element instanceof HTMLElement && element.classList.contains("game-content-block"));
        const imageEntries = blocks.flatMap(block => {
            const images = Array.from(block.querySelectorAll(":scope > img.game-content-image"))
                .filter(image => image instanceof HTMLImageElement);
            return images.map(image => ({ block, image }));
        });

        return imageEntries.length === 1
            ? { blocks, block: imageEntries[0].block, image: imageEntries[0].image }
            : null;
    };

    const clearFit = image => {
        image.style.removeProperty("width");
        image.style.removeProperty("height");
        image.style.removeProperty("max-width");
        image.style.removeProperty("max-height");
        image.removeAttribute("data-answer-key-image-fitted");
    };

    const resolvePixelLength = (value, fallback) => {
        const match = /^(-?\d+(?:\.\d+)?)px$/i.exec(value?.trim() ?? "");
        if (!match) {
            return fallback;
        }

        const parsed = Number.parseFloat(match[1]);
        return Number.isFinite(parsed) ? parsed : fallback;
    };

    const getElementHeight = element =>
        element instanceof HTMLElement ? element.getBoundingClientRect().height : 0;

    const fitSingleImage = () => {
        frameHandle = 0;

        const context = getSingleImageContext();
        const image = context?.image ?? null;
        container.querySelectorAll("img.game-content-image[data-answer-key-image-fitted]")
            .forEach(candidate => {
                if (candidate !== image) {
                    clearFit(candidate);
                }
            });

        if (!context ||
            content.hidden ||
            !context.image.complete ||
            context.image.naturalWidth <= 0 ||
            context.image.naturalHeight <= 0 ||
            container.clientWidth <= 0 ||
            container.clientHeight <= 0) {
            return;
        }

        const { blocks, block, image: currentImage } = context;
        clearFit(currentImage);

        const containerStyle = window.getComputedStyle(container);
        const containerGap = resolvePixelLength(
            containerStyle.rowGap || containerStyle.gap,
            0);
        const otherBlocksHeight = blocks
            .filter(candidate => candidate !== block)
            .reduce((sum, candidate) => sum + getElementHeight(candidate), 0);
        const containerGapHeight = Math.max(0, blocks.length - 1) * containerGap;

        const blockStyle = window.getComputedStyle(block);
        const blockGap = resolvePixelLength(
            blockStyle.rowGap || blockStyle.gap,
            0);
        const imageSiblings = Array.from(block.children).filter(child => child !== currentImage);
        const imageSiblingsHeight = imageSiblings.reduce(
            (sum, sibling) => sum + getElementHeight(sibling),
            0);
        const blockGapHeight = Math.max(0, block.children.length - 1) * blockGap;

        const imageStyle = window.getComputedStyle(currentImage);
        const responsiveHeightFraction = window.matchMedia("(max-width: 720px)").matches
            ? 0.62
            : 0.68;
        const cssMaxWidth = resolvePixelLength(imageStyle.maxWidth, 1180);
        const cssMaxHeight = resolvePixelLength(
            imageStyle.maxHeight,
            Math.max(1, window.innerHeight * responsiveHeightFraction));
        const availableWidth = Math.max(
            1,
            Math.min(container.clientWidth, block.clientWidth || container.clientWidth, cssMaxWidth));
        const availableHeight = Math.max(
            1,
            Math.min(
                container.clientHeight -
                    otherBlocksHeight -
                    containerGapHeight -
                    imageSiblingsHeight -
                    blockGapHeight,
                cssMaxHeight));

        const scale = Math.min(
            availableWidth / currentImage.naturalWidth,
            availableHeight / currentImage.naturalHeight);
        if (!Number.isFinite(scale) || scale <= 0) {
            return;
        }

        const targetWidth = Math.max(1, Math.floor(currentImage.naturalWidth * scale));
        const targetHeight = Math.max(1, Math.floor(currentImage.naturalHeight * scale));

        currentImage.style.setProperty("width", `${targetWidth}px`, "important");
        currentImage.style.setProperty("height", `${targetHeight}px`, "important");
        currentImage.style.setProperty("max-width", "100%", "important");
        currentImage.style.setProperty("max-height", `${Math.floor(availableHeight)}px`, "important");
        currentImage.dataset.answerKeyImageFitted = "true";
    };

    const scheduleFit = () => {
        if (frameHandle !== 0) {
            return;
        }

        frameHandle = window.requestAnimationFrame(() => {
            frameHandle = window.requestAnimationFrame(fitSingleImage);
        });
    };

    container.addEventListener("load", event => {
        if (event.target instanceof HTMLImageElement &&
            event.target.classList.contains("game-content-image")) {
            scheduleFit();
        }
    }, true);

    const visibilityObserver = typeof MutationObserver === "function"
        ? new MutationObserver(scheduleFit)
        : null;
    visibilityObserver?.observe(content, {
        attributes: true,
        attributeFilter: ["hidden"]
    });

    const resizeObserver = typeof ResizeObserver === "function"
        ? new ResizeObserver(scheduleFit)
        : null;
    resizeObserver?.observe(container);

    window.addEventListener("resize", scheduleFit, { passive: true });
    scheduleFit();
})();
