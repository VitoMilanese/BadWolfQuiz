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
    const posterSources = new WeakMap();

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
        if (image.dataset.gifLoopPosterReady !== "true") {
            return;
        }

        image.style.removeProperty("background-image");
        image.style.removeProperty("background-repeat");
        image.style.removeProperty("background-position");
        image.style.removeProperty("background-size");
        delete image.dataset.gifLoopPosterReady;
        posterSources.delete(image);
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

    const capturePoster = image => {
        if (!isGifImage(image) ||
            !image.complete ||
            image.naturalWidth <= 0 ||
            image.naturalHeight <= 0) {
            if (!isGifImage(image)) {
                clearPoster(image);
            }
            return;
        }

        const sourceKey = `${image.currentSrc || image.src}|${image.alt}`;
        if (posterSources.get(image) === sourceKey) {
            return;
        }

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

        try {
            context.drawImage(image, 0, 0, width, height);
            const poster = canvas.toDataURL("image/png");
            const computedStyle = window.getComputedStyle(image);
            image.style.backgroundImage = `url("${poster}")`;
            image.style.backgroundRepeat = "no-repeat";
            image.style.backgroundPosition =
                computedStyle.objectPosition || "50% 50%";
            image.style.backgroundSize = getBackgroundSize(
                computedStyle.objectFit);
            image.dataset.gifLoopPosterReady = "true";
            posterSources.set(image, sourceKey);
        } catch (error) {
            // Cross-origin images cannot be copied to canvas. In that case,
            // keep the original browser rendering without the poster fallback.
            console.debug("GIF loop poster could not be created.", error);
        }
    };

    const prepareImage = image => {
        if (!(image instanceof HTMLImageElement)) {
            return;
        }

        if (!isGifImage(image)) {
            clearPoster(image);
            return;
        }

        if (image.complete && image.naturalWidth > 0) {
            capturePoster(image);
            return;
        }

        image.addEventListener("load", () => capturePoster(image), { once: true });
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
