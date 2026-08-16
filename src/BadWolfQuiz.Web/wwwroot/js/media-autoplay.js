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
