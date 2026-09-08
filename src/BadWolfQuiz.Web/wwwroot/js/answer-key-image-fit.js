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

    const getSingleImage = () => {
        const blocks = Array.from(container.children)
            .filter(element => element instanceof HTMLElement && element.classList.contains("game-content-block"));
        if (blocks.length !== 1) {
            return null;
        }

        const images = blocks[0].querySelectorAll(":scope > img.game-content-image");
        return images.length === 1 && images[0] instanceof HTMLImageElement
            ? images[0]
            : null;
    };

    const clearFit = image => {
        image.style.removeProperty("width");
        image.style.removeProperty("height");
        image.style.removeProperty("max-width");
        image.style.removeProperty("max-height");
        image.removeAttribute("data-answer-key-image-fitted");
    };

    const resolvePixelLength = value => {
        const parsed = Number.parseFloat(value);
        return Number.isFinite(parsed) ? parsed : Number.POSITIVE_INFINITY;
    };

    const fitSingleImage = () => {
        frameHandle = 0;

        const image = getSingleImage();
        container.querySelectorAll("img.game-content-image[data-answer-key-image-fitted]")
            .forEach(candidate => {
                if (candidate !== image) {
                    clearFit(candidate);
                }
            });

        if (!image ||
            content.hidden ||
            !image.complete ||
            image.naturalWidth <= 0 ||
            image.naturalHeight <= 0 ||
            container.clientWidth <= 0 ||
            container.clientHeight <= 0) {
            return;
        }

        clearFit(image);

        const block = image.closest(".game-content-block");
        if (!(block instanceof HTMLElement)) {
            return;
        }

        const blockStyle = window.getComputedStyle(block);
        const gap = resolvePixelLength(blockStyle.rowGap || blockStyle.gap);
        const siblings = Array.from(block.children).filter(child => child !== image);
        const siblingsHeight = siblings.reduce((sum, sibling) => {
            return sum + (sibling instanceof HTMLElement ? sibling.getBoundingClientRect().height : 0);
        }, 0);
        const gapHeight = Number.isFinite(gap)
            ? Math.max(0, block.children.length - 1) * gap
            : 0;

        const imageStyle = window.getComputedStyle(image);
        const cssMaxWidth = resolvePixelLength(imageStyle.maxWidth);
        const cssMaxHeight = resolvePixelLength(imageStyle.maxHeight);
        const availableWidth = Math.max(
            1,
            Math.min(container.clientWidth, block.clientWidth || container.clientWidth, cssMaxWidth));
        const availableHeight = Math.max(
            1,
            Math.min(container.clientHeight - siblingsHeight - gapHeight, cssMaxHeight));

        const scale = Math.min(
            availableWidth / image.naturalWidth,
            availableHeight / image.naturalHeight);
        if (!Number.isFinite(scale) || scale <= 0) {
            return;
        }

        const targetWidth = Math.max(1, Math.floor(image.naturalWidth * scale));
        const targetHeight = Math.max(1, Math.floor(image.naturalHeight * scale));

        image.style.setProperty("width", `${targetWidth}px`, "important");
        image.style.setProperty("height", `${targetHeight}px`, "important");
        image.style.setProperty("max-width", "100%", "important");
        image.style.setProperty("max-height", `${Math.floor(availableHeight)}px`, "important");
        image.dataset.answerKeyImageFitted = "true";
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
