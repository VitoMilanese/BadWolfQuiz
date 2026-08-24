(() => {
    const script = document.currentScript;
    const simulateAntiBot =
        script?.dataset.simulateYoutubeAntiBot === "true";
    const launchedFrameSelector = "iframe[data-youtube-launched]";
    const managedFrameClass = "youtube-auto-expand";
    const playbackHealthTimeoutMs = 12000;
    const simulatedBlockDelayMs = 900;
    const watchedFrames = new Map();
    const boundFrames = new WeakSet();
    const inlinePlayers = new Map();
    const pendingInlineFrames = new Set();
    let apiCallbackInstalled = false;
    let expandedFallback = null;
    let expandedFallbackFrame = null;
    let expandedFallbackFrameVisibility = "";

    const fallbackCopy = {
        en: {
            title: "YouTube blocked playback",
            message: "The embedded player could not start because YouTube requested verification.",
            retry: "Try again"
        },
        uk: {
            title: "YouTube заблокував відтворення",
            message: "Вбудований плеєр не запустився через перевірку YouTube.",
            retry: "Спробувати ще раз"
        },
        ru: {
            title: "YouTube заблокировал воспроизведение",
            message: "Встроенный плеер не запустился из-за проверки YouTube.",
            retry: "Повторить"
        },
        it: {
            title: "YouTube ha bloccato la riproduzione",
            message: "Il player incorporato non è stato avviato perché YouTube richiede una verifica.",
            retry: "Riprova"
        }
    };

    const getFallbackCopy = () => {
        const language = (document.documentElement.lang || "en")
            .toLowerCase()
            .split("-")[0];
        return fallbackCopy[language] ?? fallbackCopy.en;
    };

    const clearPlaybackWatchdog = iframe => {
        const timeoutId = watchedFrames.get(iframe);
        if (timeoutId !== undefined) {
            window.clearTimeout(timeoutId);
            watchedFrames.delete(iframe);
        }
    };

    const clearExpandedFallback = () => {
        const fallback = expandedFallback;
        const iframe = expandedFallbackFrame;
        const previousVisibility = expandedFallbackFrameVisibility;

        expandedFallback = null;
        expandedFallbackFrame = null;
        expandedFallbackFrameVisibility = "";

        fallback?.remove();

        if (iframe?.isConnected) {
            iframe.style.visibility = previousVisibility;
            delete iframe.dataset.youtubeAntiBotBlocked;
        }
    };

    const createBlockedSurface = reason => {
        const copy = getFallbackCopy();
        const surface = document.createElement("div");
        surface.className = "youtube-antibot-fallback";
        surface.dataset.youtubeAntiBotFallback = "true";
        surface.dataset.youtubeAntiBotReason = reason;
        surface.setAttribute("role", "status");
        surface.setAttribute("aria-live", "polite");

        const icon = document.createElement("div");
        icon.className = "youtube-antibot-fallback-icon";
        icon.textContent = "!";
        icon.setAttribute("aria-hidden", "true");

        const title = document.createElement("strong");
        title.className = "youtube-antibot-fallback-title";
        title.textContent = copy.title;

        const message = document.createElement("p");
        message.className = "youtube-antibot-fallback-message";
        message.textContent = copy.message;

        const retryButton = document.createElement("button");
        retryButton.type = "button";
        retryButton.className =
            "button button-primary youtube-antibot-fallback-retry";
        retryButton.textContent = copy.retry;

        surface.append(icon, title, message, retryButton);
        return { surface, retryButton };
    };

    const retryManagedPlayback = iframe => {
        if (!(iframe instanceof HTMLIFrameElement) || !iframe.isConnected) {
            clearExpandedFallback();
            return;
        }

        const marker = document.createComment("youtube-antibot-retry");
        iframe.after(marker);
        clearExpandedFallback();

        if (typeof window.BadWolfYouTubeAutoExpand?.stopAll !== "function") {
            marker.remove();
            return;
        }

        window.BadWolfYouTubeAutoExpand.stopAll();

        const restoredPlaceholder = marker.previousSibling;
        marker.remove();

        if (restoredPlaceholder instanceof HTMLElement &&
            restoredPlaceholder.matches("[data-youtube-placeholder]")) {
            restoredPlaceholder.click();
        }
    };

    const presentManagedFallback = (iframe, surface, retryButton) => {
        if (!iframe.isConnected ||
            !document.body.classList.contains("youtube-auto-expanded-open")) {
            return false;
        }

        clearExpandedFallback();

        expandedFallback = surface;
        expandedFallbackFrame = iframe;
        expandedFallbackFrameVisibility = iframe.style.visibility;

        iframe.dataset.youtubeAntiBotBlocked = "true";
        iframe.style.visibility = "hidden";

        surface.classList.add("youtube-antibot-fallback-expanded");
        retryButton.addEventListener(
            "click",
            () => retryManagedPlayback(iframe),
            { once: true });

        document.body.appendChild(surface);
        retryButton.focus({ preventScroll: true });
        return true;
    };

    const retryInlinePlayback = (iframe, surface, source) => {
        if (!(iframe instanceof HTMLIFrameElement) || !surface.isConnected) {
            return;
        }

        boundFrames.delete(iframe);
        clearPlaybackWatchdog(iframe);
        pendingInlineFrames.delete(iframe);
        inlinePlayers.delete(iframe);

        surface.replaceWith(iframe);
        iframe.src = source;
        startPlaybackWatchdog(iframe);
    };

    const presentInlineFallback = (iframe, surface, retryButton) => {
        const source = iframe.getAttribute("src") ?? iframe.src;
        retryButton.addEventListener(
            "click",
            () => retryInlinePlayback(iframe, surface, source),
            { once: true });
        iframe.replaceWith(surface);
    };

    const pauseBlockedPlayback = iframe => {
        const player = inlinePlayers.get(iframe);
        if (typeof player?.pauseVideo === "function") {
            try {
                player.pauseVideo();
                return;
            } catch {
                inlinePlayers.delete(iframe);
            }
        }

        iframe.contentWindow?.postMessage(
            JSON.stringify({
                event: "command",
                func: "pauseVideo",
                args: []
            }),
            "*");
    };

    const markPlaybackBlocked = (iframe, reason) => {
        if (!(iframe instanceof HTMLIFrameElement) || !iframe.isConnected) {
            clearPlaybackWatchdog(iframe);
            pendingInlineFrames.delete(iframe);
            inlinePlayers.delete(iframe);
            return;
        }

        clearPlaybackWatchdog(iframe);
        pauseBlockedPlayback(iframe);
        pendingInlineFrames.delete(iframe);
        inlinePlayers.delete(iframe);

        const { surface, retryButton } = createBlockedSurface(reason);

        iframe.dispatchEvent(new CustomEvent(
            "badwolf:youtube-blocked",
            {
                bubbles: true,
                detail: { reason }
            }));

        if (iframe.classList.contains(managedFrameClass) &&
            presentManagedFallback(iframe, surface, retryButton)) {
            return;
        }

        presentInlineFallback(iframe, surface, retryButton);
    };

    const markPlaybackHealthy = iframe => {
        if (!(iframe instanceof HTMLIFrameElement) || simulateAntiBot) {
            return;
        }

        clearPlaybackWatchdog(iframe);
    };

    const initializeInlineFrame = iframe => {
        if (!iframe.isConnected ||
            iframe.classList.contains(managedFrameClass) ||
            inlinePlayers.has(iframe)) {
            return;
        }

        if (!window.YT?.Player) {
            pendingInlineFrames.add(iframe);
            ensureYouTubeApi();
            return;
        }

        pendingInlineFrames.delete(iframe);

        try {
            const player = new window.YT.Player(iframe, {
                events: {
                    onStateChange: event => {
                        if (event.data !== window.YT.PlayerState.UNSTARTED) {
                            markPlaybackHealthy(iframe);
                        }
                    },
                    onError: () => markPlaybackBlocked(iframe, "player-error")
                }
            });
            inlinePlayers.set(iframe, player);
        } catch {
            pendingInlineFrames.add(iframe);
        }
    };

    const initializePendingInlineFrames = () => {
        for (const iframe of Array.from(pendingInlineFrames)) {
            initializeInlineFrame(iframe);
        }
    };

    function ensureYouTubeApi() {
        if (window.YT?.Player) {
            initializePendingInlineFrames();
            return;
        }

        if (!apiCallbackInstalled) {
            apiCallbackInstalled = true;
            const previousCallback = window.onYouTubeIframeAPIReady;
            window.onYouTubeIframeAPIReady = () => {
                previousCallback?.();
                initializePendingInlineFrames();
            };
        }

        if (!document.querySelector("script[data-youtube-iframe-api]")) {
            const apiScript = document.createElement("script");
            apiScript.src = "https://www.youtube.com/iframe_api";
            apiScript.dataset.youtubeIframeApi = "true";
            document.head.appendChild(apiScript);
        }
    }

    const startPlaybackWatchdog = iframe => {
        if (!(iframe instanceof HTMLIFrameElement) ||
            !iframe.matches(launchedFrameSelector) ||
            boundFrames.has(iframe)) {
            return;
        }

        boundFrames.add(iframe);

        const timeoutId = window.setTimeout(
            () => markPlaybackBlocked(
                iframe,
                simulateAntiBot ? "simulated" : "startup-timeout"),
            simulateAntiBot ? simulatedBlockDelayMs : playbackHealthTimeoutMs);
        watchedFrames.set(iframe, timeoutId);

        if (!simulateAntiBot && !iframe.classList.contains(managedFrameClass)) {
            initializeInlineFrame(iframe);
        }
    };

    const findWatchedFrameBySource = source => {
        for (const iframe of watchedFrames.keys()) {
            if (iframe.contentWindow === source) {
                return iframe;
            }
        }

        return null;
    };

    const readPlayerState = payload => {
        if (!payload || typeof payload !== "object") {
            return null;
        }

        if (payload.event === "onStateChange" &&
            Number.isFinite(Number(payload.info))) {
            return Number(payload.info);
        }

        if (payload.event === "infoDelivery" &&
            payload.info &&
            Number.isFinite(Number(payload.info.playerState))) {
            return Number(payload.info.playerState);
        }

        return null;
    };

    window.addEventListener("message", event => {
        const iframe = findWatchedFrameBySource(event.source);
        if (!iframe) {
            return;
        }

        let payload = event.data;
        if (typeof payload === "string") {
            try {
                payload = JSON.parse(payload);
            } catch {
                return;
            }
        }

        const playerState = readPlayerState(payload);
        if (playerState === null || playerState === -1) {
            return;
        }

        markPlaybackHealthy(iframe);
    });

    document.addEventListener("badwolf:youtube-error", event => {
        const iframe = event.target;
        if (!(iframe instanceof HTMLIFrameElement) ||
            !iframe.matches(launchedFrameSelector)) {
            return;
        }

        markPlaybackBlocked(iframe, "player-error");
    }, true);

    const scan = rootNode => {
        if (rootNode instanceof HTMLIFrameElement &&
            rootNode.matches(launchedFrameSelector)) {
            startPlaybackWatchdog(rootNode);
        }

        rootNode.querySelectorAll?.(launchedFrameSelector)
            .forEach(startPlaybackWatchdog);
    };

    const cleanupFrame = iframe => {
        clearPlaybackWatchdog(iframe);
        pendingInlineFrames.delete(iframe);
        inlinePlayers.delete(iframe);

        if (expandedFallbackFrame === iframe) {
            clearExpandedFallback();
        }
    };

    const cleanup = rootNode => {
        if (rootNode instanceof HTMLIFrameElement) {
            cleanupFrame(rootNode);
        }

        rootNode.querySelectorAll?.(launchedFrameSelector)
            .forEach(cleanupFrame);
    };

    window.BadWolfYouTubeAntiBotFallback = {
        scan,
        simulate: simulateAntiBot,
        markPlaybackBlocked
    };

    scan(document);

    const observer = new MutationObserver(mutations => {
        for (const mutation of mutations) {
            mutation.removedNodes.forEach(cleanup);
            mutation.addedNodes.forEach(scan);
        }
    });
    observer.observe(document.body, { childList: true, subtree: true });

    const expandedPresentationObserver = new MutationObserver(() => {
        if (expandedFallback &&
            !document.body.classList.contains("youtube-auto-expanded-open")) {
            clearExpandedFallback();
        }
    });
    expandedPresentationObserver.observe(document.body, {
        attributes: true,
        attributeFilter: ["class"]
    });
})();
