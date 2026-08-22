(() => {
    const viewSelector = "[data-host-gameplay-view]";
    const nativeMediaSelector =
        "audio.game-content-audio, video.game-content-video";
    const nativeSelector =
        'audio.game-content-audio[data-autoplay-media="true"], ' +
        'video.game-content-video[data-autoplay-media="true"]';
    const youtubeSelector =
        '[data-youtube-placeholder][data-youtube-autoplay="true"]';
    const autoplaySelector = `${nativeSelector}, ${youtubeSelector}`;

    const isWithinRoot = (node, root) =>
        root instanceof Node &&
        (node === root ||
            (root instanceof Element && root.contains(node)));

    const stopNativePlayback = exceptRoot => {
        for (const media of document.querySelectorAll(nativeMediaSelector)) {
            if (isWithinRoot(media, exceptRoot)) {
                continue;
            }

            media.pause();
            media.autoplay = false;
            media.removeAttribute("autoplay");
            delete media.dataset.autoplayPlayback;
            try {
                media.currentTime = 0;
            } catch {
                // The media may not have loaded enough metadata to seek yet.
            }
        }
    };

    const stopActivePlayback = exceptRoot => {
        stopNativePlayback(exceptRoot);
        if (window.BadWolfYouTubeAutoExpand?.stopAllExcept) {
            window.BadWolfYouTubeAutoExpand.stopAllExcept(exceptRoot);
        } else {
            window.BadWolfYouTubeAutoExpand?.stopAll?.();
        }
    };

    const resetAutoplayAttempts = root => {
        if (!(root instanceof Element) && !(root instanceof Document)) {
            return;
        }

        if (root instanceof Element && root.matches(nativeSelector)) {
            delete root.dataset.autoplayAttempted;
        }
        root.querySelectorAll(nativeSelector).forEach(media => {
            delete media.dataset.autoplayAttempted;
        });
        window.BadWolfYouTubeAutoExpand?.resetAutoplay?.(root);
    };

    const tryPlayNative = media => {
        if (media.closest(".question-clue-hidden") ||
            media.dataset.autoplayAttempted === "true") {
            return;
        }

        media.dataset.autoplayAttempted = "true";
        media.dataset.autoplayPlayback = "true";
        media.autoplay = true;
        media.setAttribute("autoplay", "");
        media.addEventListener("pause", () => {
            delete media.dataset.autoplayPlayback;
        }, { once: true });

        try {
            const attempt = media.play();
            if (attempt && typeof attempt.catch === "function") {
                attempt.catch(error => {
                    delete media.dataset.autoplayPlayback;
                    console.debug("Media autoplay was blocked by the browser.", error);
                });
            }
        } catch (error) {
            delete media.dataset.autoplayPlayback;
            console.debug("Media autoplay was blocked by the browser.", error);
        }
    };

    const findFirstAutoplayTarget = root => {
        window.BadWolfYouTubeAutoExpand?.scan?.(root);

        const candidates = [];
        if (root instanceof Element && root.matches(autoplaySelector)) {
            candidates.push(root);
        }
        root.querySelectorAll?.(autoplaySelector).forEach(candidate => {
            candidates.push(candidate);
        });

        return candidates.find(candidate =>
            !candidate.closest(".question-clue-hidden"));
    };

    const activate = root => {
        if (!(root instanceof Element) && !(root instanceof Document)) {
            return;
        }

        const target = findFirstAutoplayTarget(root);
        if (!target) {
            return;
        }

        if (target.matches(nativeSelector)) {
            tryPlayNative(target);
            return;
        }

        window.BadWolfYouTubeAutoExpand?.autoplay?.(target);
    };

    const transition = root => {
        stopActivePlayback(root);
        resetAutoplayAttempts(root);
        activate(root);
    };

    const activateCurrentView = () => {
        const view = document.querySelector(viewSelector);
        if (view) {
            activate(view);
        }
    };

    window.BadWolfMediaAutoplay = {
        activate,
        transition,
        stop: () => stopActivePlayback(null)
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", activateCurrentView, { once: true });
    } else {
        activateCurrentView();
    }
})();

(() => {
    const gifPattern = /\.gif(?:$|[?#])/i;
    const maximumPosterDimension = 2048;
    const sampleSize = 24;
    const visibleAlphaThreshold = 8;
    const activeImages = new Set();
    const states = new WeakMap();
    let animationFrameRequested = false;

    const isGifImage = image => {
        if (!(image instanceof HTMLImageElement)) {
            return false;
        }

        if (image.dataset.gifLoopPoster === "false") {
            return false;
        }

        if (image.dataset.gifLoopPoster === "true") {
            return true;
        }

        const fileName = image.dataset.fileName || image.alt || "";
        const source = image.currentSrc || image.getAttribute("src") || "";
        return gifPattern.test(fileName.trim()) || gifPattern.test(source);
    };

    const clearPoster = image => {
        image.style.removeProperty("background-image");
        image.style.removeProperty("background-repeat");
        image.style.removeProperty("background-position");
        image.style.removeProperty("background-size");
        delete image.dataset.gifLoopPosterVisible;
    };

    const getBackgroundSize = objectFit => {
        switch (objectFit) {
            case "cover":
                return "cover";
            case "fill":
                return "100% 100%";
            case "none":
                return "auto";
            case "scale-down":
            case "contain":
            default:
                return "contain";
        }
    };

    const createState = image => {
        const sample = document.createElement("canvas");
        sample.width = sampleSize;
        sample.height = sampleSize;
        return {
            sourceKey: `${image.currentSrc || image.src}|${image.alt}`,
            sampleContext: sample.getContext("2d", { willReadFrequently: true }),
            poster: null,
            backgroundPosition: "50% 50%",
            backgroundSize: "contain",
            unsupported: false
        };
    };

    const getState = image => {
        const sourceKey = `${image.currentSrc || image.src}|${image.alt}`;
        let state = states.get(image);
        if (!state || state.sourceKey !== sourceKey) {
            clearPoster(image);
            state = createState(image);
            states.set(image, state);
        }
        return state;
    };

    const hasVisiblePixels = (image, state) => {
        const context = state.sampleContext;
        if (!context) {
            return true;
        }

        context.clearRect(0, 0, sampleSize, sampleSize);
        context.drawImage(image, 0, 0, sampleSize, sampleSize);
        const pixels = context.getImageData(0, 0, sampleSize, sampleSize).data;
        for (let index = 3; index < pixels.length; index += 4) {
            if (pixels[index] > visibleAlphaThreshold) {
                return true;
            }
        }
        return false;
    };

    const capturePoster = (image, state) => {
        const scale = Math.min(
            1,
            maximumPosterDimension / image.naturalWidth,
            maximumPosterDimension / image.naturalHeight);
        const width = Math.max(1, Math.round(image.naturalWidth * scale));
        const height = Math.max(1, Math.round(image.naturalHeight * scale));
        const canvas = document.createElement("canvas");
        canvas.width = width;
        canvas.height = height;
        const context = canvas.getContext("2d");
        if (!context) {
            return;
        }

        context.drawImage(image, 0, 0, width, height);
        state.poster = `url("${canvas.toDataURL("image/png")}")`;
        const computedStyle = window.getComputedStyle(image);
        state.backgroundPosition = computedStyle.objectPosition || "50% 50%";
        state.backgroundSize = getBackgroundSize(computedStyle.objectFit);
    };

    const showPoster = (image, state) => {
        if (!state.poster || image.dataset.gifLoopPosterVisible === "true") {
            return;
        }

        image.style.backgroundImage = state.poster;
        image.style.backgroundRepeat = "no-repeat";
        image.style.backgroundPosition = state.backgroundPosition;
        image.style.backgroundSize = state.backgroundSize;
        image.dataset.gifLoopPosterVisible = "true";
    };

    const updateImage = image => {
        if (!image.isConnected || !isGifImage(image)) {
            clearPoster(image);
            activeImages.delete(image);
            states.delete(image);
            return;
        }

        if (!image.complete || image.naturalWidth <= 0 || image.naturalHeight <= 0) {
            return;
        }

        const state = getState(image);
        if (state.unsupported) {
            return;
        }

        try {
            if (hasVisiblePixels(image, state)) {
                clearPoster(image);
                if (!state.poster) {
                    capturePoster(image, state);
                }
            } else {
                showPoster(image, state);
            }
        } catch (error) {
            state.unsupported = true;
            clearPoster(image);
            console.debug("GIF loop poster could not be created.", error);
        }
    };

    const tick = () => {
        animationFrameRequested = false;
        activeImages.forEach(updateImage);
        if (activeImages.size > 0) {
            requestTick();
        }
    };

    function requestTick() {
        if (animationFrameRequested) {
            return;
        }
        animationFrameRequested = true;
        window.requestAnimationFrame(tick);
    }

    const prepareImage = image => {
        if (!(image instanceof HTMLImageElement)) {
            return;
        }

        if (!isGifImage(image)) {
            clearPoster(image);
            activeImages.delete(image);
            states.delete(image);
            return;
        }

        activeImages.add(image);
        getState(image);
        requestTick();
    };

    const scan = root => {
        if (root instanceof HTMLImageElement) {
            prepareImage(root);
        }
        root.querySelectorAll?.("img").forEach(prepareImage);
    };

    scan(document);

    const observer = new MutationObserver(mutations => {
        for (const mutation of mutations) {
            if (mutation.type === "attributes") {
                prepareImage(mutation.target);
                continue;
            }

            for (const node of mutation.addedNodes) {
                if (node instanceof Element) {
                    scan(node);
                }
            }
        }
    });

    observer.observe(document.documentElement, {
        childList: true,
        subtree: true,
        attributes: true,
        attributeFilter: ["src", "alt", "data-file-name", "data-gif-loop-poster"]
    });
})();
