(() => {
    if (window.BadWolfYouTubeAutoExpand?.scan) {
        window.BadWolfYouTubeAutoExpand.scan(document);
        return;
    }

    const youtubeFrameSelector = "iframe.youtube-auto-expand";
    const nativeMediaSelector = "audio, video";
    const players = new Map();
    const pendingFrames = new Set();
    const boundFrames = new WeakSet();
    let expandedIframe = null;
    let closeButton = null;
    let shouldResumeTimer = false;
    let timerPlaybackOwner = null;
    let apiCallbackInstalled = false;
    let suppressNextEscapeKeyUp = false;

    const pauseRunningTimer = () => {
        const timerPanel = document.getElementById("game-timer");
        const pauseForm = document.querySelector(".game-timer-pause");

        shouldResumeTimer = Boolean(
            timerPanel &&
            !timerPanel.hidden &&
            pauseForm &&
            !pauseForm.hidden);

        if (shouldResumeTimer) {
            pauseForm.requestSubmit();
        }
    };

    const resumePausedTimer = () => {
        if (!shouldResumeTimer) {
            return;
        }

        shouldResumeTimer = false;
        document.querySelector(".game-timer-resume")?.requestSubmit();
    };

    const beginTimedPlayback = iframe => {
        const alreadyTrackingPlayback = timerPlaybackOwner !== null;
        timerPlaybackOwner = iframe;

        if (!alreadyTrackingPlayback) {
            pauseRunningTimer();
        }
    };

    const endTimedPlayback = iframe => {
        if (timerPlaybackOwner !== iframe) {
            return;
        }

        timerPlaybackOwner = null;
        resumePausedTimer();
    };

    const clearExpandedPresentation = () => {
        expandedIframe?.classList.remove("youtube-auto-expanded");
        expandedIframe = null;
        closeButton?.remove();
        closeButton = null;
        document.body.classList.remove("youtube-auto-expanded-open");
    };

    const expandVideo = iframe => {
        if (expandedIframe === iframe) {
            return;
        }

        clearExpandedPresentation();
        expandedIframe = iframe;
        iframe.classList.add("youtube-auto-expanded");
        document.body.classList.add("youtube-auto-expanded-open");

        closeButton = document.createElement("button");
        closeButton.type = "button";
        closeButton.className = "youtube-auto-expand-close";
        closeButton.textContent = "×";
        closeButton.title = iframe.dataset.closeLabel ?? "Close video";
        closeButton.setAttribute("aria-label", closeButton.title);
        closeButton.addEventListener("click", clearExpandedPresentation);
        document.body.appendChild(closeButton);
        closeButton.focus({ preventScroll: true });
    };

    const pauseNativeMedia = exceptMedia => {
        for (const media of document.querySelectorAll(nativeMediaSelector)) {
            if (media !== exceptMedia && !media.paused) {
                media.pause();
            }
        }
    };

    const pauseYouTubeFrames = exceptFrame => {
        for (const frame of document.querySelectorAll(youtubeFrameSelector)) {
            if (frame === exceptFrame) {
                continue;
            }

            const player = players.get(frame);
            if (typeof player?.pauseVideo === "function") {
                try {
                    player.pauseVideo();
                    continue;
                } catch {
                    players.delete(frame);
                }
            }

            frame.contentWindow?.postMessage(
                JSON.stringify({
                    event: "command",
                    func: "pauseVideo",
                    args: []
                }),
                "*");
        }
    };

    const handleStateChange = (iframe, event) => {
        if (event.data === window.YT.PlayerState.PLAYING) {
            beginTimedPlayback(iframe);
            pauseNativeMedia(null);
            pauseYouTubeFrames(iframe);
            expandVideo(iframe);
            return;
        }

        const playbackStopped =
            event.data === window.YT.PlayerState.PAUSED ||
            event.data === window.YT.PlayerState.ENDED ||
            event.data === window.YT.PlayerState.CUED ||
            event.data === window.YT.PlayerState.UNSTARTED;

        if (!playbackStopped) {
            return;
        }

        endTimedPlayback(iframe);

        if (event.data === window.YT.PlayerState.ENDED &&
            expandedIframe === iframe) {
            clearExpandedPresentation();
        }
    };

    const ensureJsApiEnabled = iframe => {
        try {
            const url = new URL(iframe.src, document.baseURI);
            if (url.searchParams.get("enablejsapi") === "1") {
                return;
            }

            url.searchParams.set("enablejsapi", "1");
            iframe.src = url.toString();
        } catch {
            // The YouTube API will ignore malformed URLs.
        }
    };

    const initializeFrame = iframe => {
        if (!document.contains(iframe) || players.has(iframe)) {
            return;
        }

        ensureJsApiEnabled(iframe);

        if (!window.YT?.Player) {
            pendingFrames.add(iframe);
            ensureYouTubeApi();
            return;
        }

        pendingFrames.delete(iframe);

        try {
            const player = new window.YT.Player(iframe, {
                events: {
                    onStateChange: event => handleStateChange(iframe, event)
                }
            });
            players.set(iframe, player);
        } catch {
            pendingFrames.add(iframe);
        }
    };

    const initializePendingFrames = () => {
        for (const iframe of Array.from(pendingFrames)) {
            initializeFrame(iframe);
        }

        document.querySelectorAll(youtubeFrameSelector).forEach(initializeFrame);
    };

    function ensureYouTubeApi() {
        if (window.YT?.Player) {
            initializePendingFrames();
            return;
        }

        if (!apiCallbackInstalled) {
            apiCallbackInstalled = true;
            const previousCallback = window.onYouTubeIframeAPIReady;
            window.onYouTubeIframeAPIReady = () => {
                previousCallback?.();
                initializePendingFrames();
            };
        }

        if (!document.querySelector("script[data-youtube-iframe-api]")) {
            const script = document.createElement("script");
            script.src = "https://www.youtube.com/iframe_api";
            script.dataset.youtubeIframeApi = "true";
            document.head.appendChild(script);
        }
    }

    const bindFrame = iframe => {
        if (boundFrames.has(iframe)) {
            initializeFrame(iframe);
            return;
        }

        boundFrames.add(iframe);
        iframe.addEventListener("load", () => initializeFrame(iframe));
        initializeFrame(iframe);
    };

    const forEachFrame = (rootNode, callback) => {
        if (rootNode instanceof Element && rootNode.matches(youtubeFrameSelector)) {
            callback(rootNode);
        }

        rootNode.querySelectorAll?.(youtubeFrameSelector).forEach(callback);
    };

    const bindMediaTree = rootNode => {
        forEachFrame(rootNode, bindFrame);
    };

    const unbindMediaTree = rootNode => {
        forEachFrame(rootNode, iframe => {
            pendingFrames.delete(iframe);
            players.delete(iframe);
            endTimedPlayback(iframe);

            if (expandedIframe === iframe) {
                clearExpandedPresentation();
            }
        });
    };

    window.BadWolfYouTubeAutoExpand = {
        scan: bindMediaTree
    };

    document.addEventListener("keydown", event => {
        if (event.key !== "Escape" || !expandedIframe) {
            return;
        }

        suppressNextEscapeKeyUp = true;
        event.preventDefault();
        event.stopImmediatePropagation();
        clearExpandedPresentation();
    }, true);

    window.addEventListener("keyup", event => {
        if (event.key !== "Escape" || !suppressNextEscapeKeyUp) {
            return;
        }

        suppressNextEscapeKeyUp = false;
        event.preventDefault();
        event.stopImmediatePropagation();
    }, true);

    document.addEventListener("play", event => {
        const media = event.target;
        if (!(media instanceof HTMLMediaElement)) {
            return;
        }

        pauseNativeMedia(media);
        pauseYouTubeFrames(null);
    }, true);

    bindMediaTree(document);

    const observer = new MutationObserver(mutations => {
        for (const mutation of mutations) {
            mutation.removedNodes.forEach(unbindMediaTree);
            mutation.addedNodes.forEach(bindMediaTree);
        }
    });
    observer.observe(document.body, { childList: true, subtree: true });
})();
