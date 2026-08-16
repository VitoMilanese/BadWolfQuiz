(() => {
    if (window.BadWolfYouTubeAutoExpand?.scan) {
        window.BadWolfYouTubeAutoExpand.scan(document);
        return;
    }

    const youtubeFrameSelector =
        "iframe.youtube-auto-expand[data-youtube-launched]";
    const youtubeSourceFrameSelector =
        "iframe.youtube-auto-expand:not([data-youtube-launched])";
    const youtubePlaceholderSelector = "[data-youtube-placeholder]";
    const nativeMediaSelector = "audio, video";
    const players = new Map();
    const pendingFrames = new Set();
    const boundFrames = new WeakSet();
    const boundPlaceholders = new WeakSet();
    const scriptUrl = document.currentScript?.src ?? "";
    const placeholderImageUrl = scriptUrl
        ? new URL("../images/youtube-placeholder.svg", scriptUrl).toString()
        : "/images/youtube-placeholder.svg";
    const placeholderStylesheetUrl = scriptUrl
        ? new URL("../css/youtube-placeholder.css", scriptUrl).toString()
        : "/css/youtube-placeholder.css";
    let expandedIframe = null;
    let closeButton = null;
    let shouldResumeTimer = false;
    let timerPlaybackOwner = null;
    let apiCallbackInstalled = false;
    let suppressNextEscapeKeyUp = false;

    const ensurePlaceholderStylesheet = () => {
        const existing = Array.from(
            document.querySelectorAll('link[rel="stylesheet"]'))
            .some(link => {
                try {
                    return new URL(link.href, document.baseURI)
                        .pathname.endsWith("/css/youtube-placeholder.css");
                } catch {
                    return false;
                }
            });

        if (existing) {
            return;
        }

        const stylesheet = document.createElement("link");
        stylesheet.rel = "stylesheet";
        stylesheet.href = placeholderStylesheetUrl;
        stylesheet.dataset.youtubePlaceholderStyles = "true";
        document.head.appendChild(stylesheet);
    };

    ensurePlaceholderStylesheet();

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
        const resumeForm = document.querySelector(".game-timer-resume");
        if (!resumeForm || resumeForm.hidden) {
            return;
        }

        resumeForm.requestSubmit();
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

    const abandonTimedPlayback = iframe => {
        if (timerPlaybackOwner !== iframe) {
            return;
        }

        timerPlaybackOwner = null;
        shouldResumeTimer = false;
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
        closeButton.textContent = "\u00d7";
        closeButton.title = iframe.dataset.closeLabel ?? "Close video";
        closeButton.setAttribute("aria-label", closeButton.title);
        closeButton.addEventListener("click", () => restorePlaceholder(iframe));
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

    const isWithinRoot = (node, rootNode) =>
        rootNode instanceof Node &&
        (node === rootNode ||
            (rootNode instanceof Element && rootNode.contains(node)));

    const pauseYouTubeFrames = (exceptFrame, exceptRoot = null) => {
        for (const frame of document.querySelectorAll(youtubeFrameSelector)) {
            if (frame === exceptFrame || isWithinRoot(frame, exceptRoot)) {
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
            if (iframe.dataset.youtubeAutoplayLaunch === "true") {
                return;
            }

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

        delete iframe.dataset.youtubeAutoplayLaunch;
        endTimedPlayback(iframe);

        if (event.data === window.YT.PlayerState.ENDED &&
            expandedIframe === iframe) {
            restorePlaceholder(iframe);
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
                    onReady: event => {
                        if (iframe.dataset.youtubeAutoplay !== "true") {
                            return;
                        }

                        delete iframe.dataset.youtubeAutoplay;
                        try {
                            event.target.playVideo();
                        } catch {
                            // The autoplay query parameter remains as a fallback.
                        }
                    },
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

    const createPlaceholder = ({
        embedUrl,
        frameClass = "youtube-auto-expand",
        title = "YouTube",
        closeLabel = "",
        allow = "",
        allowFullscreen = true,
        autoplay = false
    } = {}) => {
        if (!embedUrl) {
            return null;
        }

        const placeholder = document.createElement("button");
        placeholder.type = "button";
        placeholder.className = "youtube-placeholder";
        placeholder.dataset.youtubePlaceholder = "";
        placeholder.dataset.youtubeEmbedUrl = embedUrl;
        placeholder.dataset.youtubeFrameClass = frameClass;
        placeholder.dataset.youtubeTitle = title;
        placeholder.dataset.youtubeAllowFullscreen = allowFullscreen
            ? "true"
            : "false";
        if (autoplay) {
            placeholder.dataset.youtubeAutoplay = "true";
        }
        if (closeLabel) {
            placeholder.dataset.youtubeCloseLabel = closeLabel;
        }
        if (allow) {
            placeholder.dataset.youtubeAllow = allow;
        }
        placeholder.setAttribute("aria-label", title);

        const image = document.createElement("img");
        image.className = "youtube-placeholder-image";
        image.src = placeholderImageUrl;
        image.alt = "";
        image.setAttribute("aria-hidden", "true");

        const play = document.createElement("span");
        play.className = "youtube-placeholder-play";
        play.setAttribute("aria-hidden", "true");

        placeholder.append(image, play);
        return placeholder;
    };

    function restorePlaceholder(iframe) {
        if (!iframe) {
            return;
        }

        const frameClass = Array.from(iframe.classList)
            .filter(className => className !== "youtube-auto-expanded")
            .join(" ");
        const placeholder = createPlaceholder({
            embedUrl: iframe.dataset.youtubeEmbedUrl ??
                iframe.getAttribute("src") ??
                iframe.src,
            frameClass,
            title: iframe.title || "YouTube",
            closeLabel: iframe.dataset.closeLabel ?? "",
            allow: iframe.getAttribute("allow") ?? "",
            allowFullscreen: iframe.allowFullscreen
        });

        if (expandedIframe === iframe) {
            clearExpandedPresentation();
        }

        pendingFrames.delete(iframe);
        players.delete(iframe);
        endTimedPlayback(iframe);

        if (!placeholder || !iframe.isConnected) {
            return;
        }

        iframe.replaceWith(placeholder);
        bindPlaceholder(placeholder);
    }

    const buildLaunchUrl = (value, managedFullscreen) => {
        try {
            const url = new URL(value, document.baseURI);
            url.searchParams.set("enablejsapi", "1");
            url.searchParams.set("autoplay", "1");
            if (managedFullscreen) {
                url.searchParams.set("fs", "0");
            }
            return url.toString();
        } catch {
            return value;
        }
    };

    const launchPlaceholder = (placeholder, autoplayLaunch = false) => {
        if (!placeholder.isConnected ||
            placeholder.dataset.youtubeLaunching === "true") {
            return;
        }

        const embedUrl = placeholder.dataset.youtubeEmbedUrl;
        if (!embedUrl) {
            return;
        }

        placeholder.dataset.youtubeLaunching = "true";

        const iframe = document.createElement("iframe");
        iframe.className = placeholder.dataset.youtubeFrameClass ?? "";
        const managedFullscreen = iframe.classList.contains("youtube-auto-expand");
        iframe.dataset.youtubeLaunched = "true";
        iframe.dataset.youtubeAutoplay = "true";
        if (autoplayLaunch) {
            iframe.dataset.youtubeAutoplayLaunch = "true";
        }
        iframe.dataset.youtubeEmbedUrl = embedUrl;
        iframe.src = buildLaunchUrl(embedUrl, managedFullscreen);
        iframe.title = placeholder.dataset.youtubeTitle ?? "YouTube";
        iframe.allow = placeholder.dataset.youtubeAllow ||
            "accelerometer; autoplay; clipboard-write; encrypted-media; " +
            "gyroscope; picture-in-picture; web-share";
        iframe.allowFullscreen = managedFullscreen
            ? false
            : placeholder.dataset.youtubeAllowFullscreen !== "false";

        const closeLabel = placeholder.dataset.youtubeCloseLabel;
        if (closeLabel) {
            iframe.dataset.closeLabel = closeLabel;
        }

        placeholder.replaceWith(iframe);

        if (managedFullscreen) {
            if (!autoplayLaunch) {
                pauseNativeMedia(null);
                pauseYouTubeFrames(iframe);
            }
            expandVideo(iframe);
        }

        if (iframe.matches(youtubeFrameSelector)) {
            bindFrame(iframe);
        }
    };

    const bindPlaceholder = placeholder => {
        if (boundPlaceholders.has(placeholder)) {
            return;
        }

        boundPlaceholders.add(placeholder);
        placeholder.addEventListener("click", () => {
            launchPlaceholder(placeholder);
        });

    };

    const replaceSourceFrameWithPlaceholder = iframe => {
        if (!iframe.isConnected || iframe.dataset.youtubeLaunched === "true") {
            return;
        }

        const placeholder = createPlaceholder({
            embedUrl: iframe.getAttribute("src") ?? iframe.src,
            frameClass: iframe.className,
            title: iframe.title || "YouTube",
            closeLabel: iframe.dataset.closeLabel ?? "",
            allow: iframe.getAttribute("allow") ?? "",
            allowFullscreen: iframe.hasAttribute("allowfullscreen"),
            autoplay: iframe.dataset.youtubeAutoplay === "true"
        });

        if (!placeholder) {
            return;
        }

        iframe.replaceWith(placeholder);
        bindPlaceholder(placeholder);
    };

    const forEachMatching = (rootNode, selector, callback) => {
        if (rootNode instanceof Element && rootNode.matches(selector)) {
            callback(rootNode);
        }

        rootNode.querySelectorAll?.(selector).forEach(callback);
    };

    const forEachFrame = (rootNode, callback) => {
        forEachMatching(rootNode, youtubeFrameSelector, callback);
    };

    const forEachSourceFrame = (rootNode, callback) => {
        forEachMatching(rootNode, youtubeSourceFrameSelector, callback);
    };

    const forEachPlaceholder = (rootNode, callback) => {
        forEachMatching(rootNode, youtubePlaceholderSelector, callback);
    };

    const bindMediaTree = rootNode => {
        forEachSourceFrame(rootNode, replaceSourceFrameWithPlaceholder);
        forEachPlaceholder(rootNode, bindPlaceholder);
        forEachFrame(rootNode, bindFrame);
    };

    const unbindMediaTree = rootNode => {
        if (rootNode instanceof Node && rootNode.isConnected) {
            return;
        }

        forEachFrame(rootNode, iframe => {
            pendingFrames.delete(iframe);
            players.delete(iframe);
            abandonTimedPlayback(iframe);

            if (expandedIframe === iframe) {
                clearExpandedPresentation();
            }
        });
    };

    const autoplayMediaTree = rootNode => {
        bindMediaTree(rootNode);
        forEachPlaceholder(rootNode, placeholder => {
            if (placeholder.closest(".question-clue-hidden") ||
                placeholder.dataset.youtubeAutoplay !== "true" ||
                placeholder.dataset.youtubeAutoplayAttempted === "true") {
                return;
            }

            placeholder.dataset.youtubeAutoplayAttempted = "true";
            launchPlaceholder(placeholder, true);
        });
    };

    const stopPlaybackOutside = rootNode => {
        pauseYouTubeFrames(null, rootNode);
        if (expandedIframe && !isWithinRoot(expandedIframe, rootNode)) {
            restorePlaceholder(expandedIframe);
        }
    };

    const stopAllPlayback = () => {
        stopPlaybackOutside(null);
    };

    const resetAutoplayTree = rootNode => {
        forEachPlaceholder(rootNode, placeholder => {
            delete placeholder.dataset.youtubeAutoplayAttempted;
            delete placeholder.dataset.youtubeLaunching;
        });
        forEachSourceFrame(rootNode, iframe => {
            delete iframe.dataset.youtubeAutoplayAttempted;
        });
    };

    window.BadWolfYouTubeAutoExpand = {
        scan: bindMediaTree,
        autoplay: autoplayMediaTree,
        stopAll: stopAllPlayback,
        stopAllExcept: stopPlaybackOutside,
        resetAutoplay: resetAutoplayTree,
        createPlaceholder
    };

    document.addEventListener("submit", event => {
        const form = event.target instanceof HTMLFormElement &&
            event.target.matches(".game-timer-resume")
            ? event.target
            : null;

        if (!form || !shouldResumeTimer) {
            return;
        }

        shouldResumeTimer = false;
    }, true);

    document.addEventListener("keydown", event => {
        if (event.key !== "Escape" || !expandedIframe) {
            return;
        }

        suppressNextEscapeKeyUp = true;
        event.preventDefault();
        event.stopImmediatePropagation();
        restorePlaceholder(expandedIframe);
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
        if (!(media instanceof HTMLMediaElement) ||
            media.dataset.autoplayPlayback === "true") {
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
