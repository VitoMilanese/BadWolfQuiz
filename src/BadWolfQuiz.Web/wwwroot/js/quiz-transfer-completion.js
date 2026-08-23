(() => {
    const quizzesRoute = "/admin/quizzes";
    const transferCompletionCookie = "badwolfquiz-transfer-complete";
    const importFrameName = "badwolfquiz-import-target";
    const operationTimeoutMilliseconds = 30 * 60 * 1000;
    const completionSoundDurationMilliseconds = 420;
    const messageAutoDismissMilliseconds = 4000;

    const normalisePath = value =>
        (value || "/").replace(/\/$/, "").toLowerCase() || "/";

    const isQuizzesIndex = path =>
        path === quizzesRoute || path === `${quizzesRoute}/index`;

    if (!isQuizzesIndex(normalisePath(window.location.pathname))) {
        return;
    }

    const scheduleErrorAutoDismiss = () => {
        document.querySelectorAll('.message.message-error[role="alert"]')
            .forEach(message => {
                window.setTimeout(() => {
                    message.classList.add("message-hidden");
                    message.addEventListener(
                        "transitionend",
                        () => message.remove(),
                        { once: true });
                }, messageAutoDismissMilliseconds);
            });
    };

    scheduleErrorAutoDismiss();

    let audioContext = null;
    let pollHandle = 0;
    let timeoutHandle = 0;
    let trackedImportForm = null;
    let trackedImportOriginalTarget = null;
    let trackedImportTokenInput = null;

    const readCookie = name => {
        const prefix = `${encodeURIComponent(name)}=`;
        const item = document.cookie
            .split("; ")
            .find(value => value.startsWith(prefix));
        return item ? decodeURIComponent(item.slice(prefix.length)) : null;
    };

    const clearTransferCookie = () => {
        document.cookie = `${encodeURIComponent(transferCompletionCookie)}=; Max-Age=0; Path=/; SameSite=Lax`;
    };

    const parseTransferCompletion = value => {
        if (!value) {
            return null;
        }

        const parts = value.split(":");
        if (parts.length !== 3 ||
            (parts[0] !== "export" && parts[0] !== "import") ||
            (parts[2] !== "success" && parts[2] !== "failure")) {
            return null;
        }

        return {
            operation: parts[0],
            token: parts[1],
            succeeded: parts[2] === "success"
        };
    };

    const createOperationToken = () => {
        if (typeof window.crypto?.randomUUID === "function") {
            return window.crypto.randomUUID();
        }

        return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, character => {
            const random = Math.floor(Math.random() * 16);
            const value = character === "x" ? random : (random & 0x3) | 0x8;
            return value.toString(16);
        });
    };

    const armCompletionSound = () => {
        const AudioContextType = window.AudioContext || window.webkitAudioContext;
        if (!AudioContextType) {
            return;
        }

        audioContext ??= new AudioContextType();
        if (audioContext.state === "suspended") {
            void audioContext.resume().catch(() => { });
        }
    };

    const scheduleCompletionTone = () => {
        if (!audioContext || audioContext.state !== "running") {
            return;
        }

        const tones = [
            { frequency: 523.25, offset: 0, duration: 0.12 },
            { frequency: 659.25, offset: 0.11, duration: 0.13 },
            { frequency: 783.99, offset: 0.23, duration: 0.18 }
        ];

        const baseTime = audioContext.currentTime + 0.01;
        tones.forEach(tone => {
            const oscillator = audioContext.createOscillator();
            const gain = audioContext.createGain();
            const start = baseTime + tone.offset;
            const end = start + tone.duration;

            oscillator.type = "sine";
            oscillator.frequency.setValueAtTime(tone.frequency, start);
            gain.gain.setValueAtTime(0.0001, start);
            gain.gain.exponentialRampToValueAtTime(0.075, start + 0.018);
            gain.gain.exponentialRampToValueAtTime(0.0001, end);

            oscillator.connect(gain);
            gain.connect(audioContext.destination);
            oscillator.start(start);
            oscillator.stop(end + 0.01);
        });
    };

    const playCompletionSound = () => {
        if (!audioContext || audioContext.state === "closed") {
            return 0;
        }

        if (audioContext.state === "suspended") {
            void audioContext.resume()
                .then(scheduleCompletionTone)
                .catch(() => { });
        } else {
            scheduleCompletionTone();
        }

        return completionSoundDurationMilliseconds;
    };

    const restoreImportForm = () => {
        if (!trackedImportForm) {
            return;
        }

        if (trackedImportOriginalTarget === null) {
            trackedImportForm.removeAttribute("target");
        } else {
            trackedImportForm.setAttribute("target", trackedImportOriginalTarget);
        }

        trackedImportTokenInput?.remove();
        trackedImportForm = null;
        trackedImportOriginalTarget = null;
        trackedImportTokenInput = null;
    };

    const clearTracking = () => {
        if (pollHandle !== 0) {
            window.clearInterval(pollHandle);
            pollHandle = 0;
        }
        if (timeoutHandle !== 0) {
            window.clearTimeout(timeoutHandle);
            timeoutHandle = 0;
        }
        restoreImportForm();
    };

    const finishImport = succeeded => {
        clearTransferCookie();
        clearTracking();
        window.BadWolfBusy?.hide();

        const delay = succeeded ? playCompletionSound() : 0;
        window.setTimeout(() => window.location.reload(), delay);
    };

    const finishExport = succeeded => {
        clearTransferCookie();
        clearTracking();
        if (succeeded) {
            playCompletionSound();
        }
    };

    const startTracking = (operation, expectedToken = null) => {
        clearTracking();

        const checkCompletion = () => {
            const completion = parseTransferCompletion(
                readCookie(transferCompletionCookie));
            if (!completion || completion.operation !== operation) {
                return;
            }
            if (expectedToken && completion.token !== expectedToken) {
                return;
            }

            if (operation === "import") {
                finishImport(completion.succeeded);
            } else {
                finishExport(completion.succeeded);
            }
        };

        pollHandle = window.setInterval(checkCompletion, 100);
        timeoutHandle = window.setTimeout(() => {
            clearTransferCookie();
            clearTracking();
            if (operation === "import") {
                window.BadWolfBusy?.hide();
            }
        }, operationTimeoutMilliseconds);
    };

    const isQuizExportLink = link => {
        if (!(link instanceof HTMLAnchorElement)) {
            return false;
        }

        let target;
        try {
            target = new URL(link.href, window.location.href);
        } catch {
            return false;
        }

        return target.origin === window.location.origin &&
            isQuizzesIndex(normalisePath(target.pathname)) &&
            target.searchParams.get("handler")?.toLowerCase() === "export";
    };

    const formHandler = form => {
        try {
            return new URL(form.action || window.location.href, window.location.href)
                .searchParams
                .get("handler")
                ?.toLowerCase() ?? "";
        } catch {
            return "";
        }
    };

    const ensureImportFrame = () => {
        let frame = document.querySelector(`iframe[name="${importFrameName}"]`);
        if (frame instanceof HTMLIFrameElement) {
            return frame;
        }

        frame = document.createElement("iframe");
        frame.name = importFrameName;
        frame.hidden = true;
        frame.setAttribute("aria-hidden", "true");
        frame.title = "Quiz import completion target";
        document.body.appendChild(frame);
        return frame;
    };

    const prepareImport = form => {
        const frame = ensureImportFrame();
        const token = createOperationToken();

        clearTransferCookie();
        armCompletionSound();
        startTracking("import", token);

        const tokenInput = document.createElement("input");
        tokenInput.type = "hidden";
        tokenInput.name = "importToken";
        tokenInput.value = token;
        form.appendChild(tokenInput);

        trackedImportForm = form;
        trackedImportOriginalTarget = form.getAttribute("target");
        trackedImportTokenInput = tokenInput;
        form.target = frame.name;
    };

    document.addEventListener("click", event => {
        if (event.defaultPrevented ||
            event.button !== 0 ||
            event.metaKey ||
            event.ctrlKey ||
            event.shiftKey ||
            event.altKey) {
            return;
        }

        const link = event.target instanceof Element
            ? event.target.closest("a[href]")
            : null;
        if (!isQuizExportLink(link) ||
            link.dataset.busyLocked === "true" ||
            window.BadWolfBusy?.isBusy) {
            return;
        }

        clearTransferCookie();
        armCompletionSound();
        startTracking("export");
    }, true);

    document.addEventListener("submit", event => {
        const form = event.target instanceof HTMLFormElement ? event.target : null;
        if (!form || formHandler(form) !== "import") {
            return;
        }
        if (form.dataset.busyLocked === "true" || window.BadWolfBusy?.isBusy) {
            return;
        }

        prepareImport(form);
    }, true);

    window.addEventListener("pageshow", clearTracking);
})();