(() => {
    const iframes = Array.from(document.querySelectorAll(
        "iframe.youtube-auto-expand"));

    if (iframes.length === 0) {
        return;
    }

    let expandedIframe = null;
    let closeButton = null;
    let shouldResumeTimer = false;

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

    const collapseVideo = () => {
        const wasExpanded = expandedIframe !== null;
        expandedIframe?.classList.remove("youtube-auto-expanded");
        expandedIframe = null;
        closeButton?.remove();
        closeButton = null;
        document.body.classList.remove("youtube-auto-expanded-open");

        if (wasExpanded) {
            resumePausedTimer();
        }
    };

    const expandVideo = iframe => {
        if (expandedIframe === iframe) {
            return;
        }

        collapseVideo();
        expandedIframe = iframe;
        iframe.classList.add("youtube-auto-expanded");
        document.body.classList.add("youtube-auto-expanded-open");
        pauseRunningTimer();

        closeButton = document.createElement("button");
        closeButton.type = "button";
        closeButton.className = "youtube-auto-expand-close";
        closeButton.textContent = "×";
        closeButton.title = iframe.dataset.closeLabel ?? "Close video";
        closeButton.setAttribute("aria-label", closeButton.title);
        closeButton.addEventListener("click", collapseVideo);
        document.body.appendChild(closeButton);
    };

    document.addEventListener("keydown", event => {
        if (event.key === "Escape" && expandedIframe) {
            collapseVideo();
        }
    });

    for (const iframe of iframes) {
        try {
            const url = new URL(iframe.src);
            url.searchParams.set("enablejsapi", "1");
            iframe.src = url.toString();
        } catch {
            // The YouTube API will ignore malformed URLs.
        }
    }

    const initializePlayers = () => {
        for (const iframe of iframes) {
            if (iframe.dataset.youtubePlayerInitialized === "true") {
                continue;
            }

            iframe.dataset.youtubePlayerInitialized = "true";
            new window.YT.Player(iframe, {
                events: {
                    onStateChange: event => {
                        if (event.data === window.YT.PlayerState.PLAYING) {
                            expandVideo(iframe);
                        } else if (
                            event.data === window.YT.PlayerState.ENDED &&
                            expandedIframe === iframe) {
                            collapseVideo();
                        }
                    }
                }
            });
        }
    };

    if (window.YT?.Player) {
        initializePlayers();
        return;
    }

    const previousCallback = window.onYouTubeIframeAPIReady;
    window.onYouTubeIframeAPIReady = () => {
        previousCallback?.();
        initializePlayers();
    };

    if (!document.querySelector("script[data-youtube-iframe-api]")) {
        const script = document.createElement("script");
        script.src = "https://www.youtube.com/iframe_api";
        script.dataset.youtubeIframeApi = "true";
        document.head.appendChild(script);
    }
})();
