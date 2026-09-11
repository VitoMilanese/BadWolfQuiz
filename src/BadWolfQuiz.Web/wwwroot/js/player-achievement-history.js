(() => {
    const root = document.querySelector("[data-achievement-history-root]");
    if (!root) return;

    const language = document.documentElement.lang || undefined;
    let formatter;
    try {
        formatter = new Intl.DateTimeFormat(language, {
            dateStyle: "medium",
            timeStyle: "short"
        });
    } catch {
        formatter = new Intl.DateTimeFormat(undefined, {
            dateStyle: "medium",
            timeStyle: "short"
        });
    }

    const formatTimes = scope => {
        for (const element of scope.querySelectorAll("time[data-achievement-history-time]")) {
            const value = element.getAttribute("datetime");
            if (!value) continue;

            const date = new Date(value);
            if (Number.isNaN(date.valueOf())) continue;
            element.textContent = formatter.format(date);
        }
    };

    formatTimes(document);

    const navigate = href => {
        if (!href) return;

        if (window.BadWolfBusy?.navigate) {
            window.BadWolfBusy.navigate(href);
            return;
        }

        window.location.assign(href);
    };

    const isPlainLeftClick = event =>
        event.button === 0 &&
        !event.metaKey &&
        !event.ctrlKey &&
        !event.shiftKey &&
        !event.altKey;

    let requestController = null;
    let requestVersion = 0;
    let busyDelayHandle = 0;
    let busyOverlayOwned = false;

    const startBusyDelay = () => {
        if (busyDelayHandle || busyOverlayOwned) return;

        busyDelayHandle = window.setTimeout(() => {
            busyDelayHandle = 0;
            busyOverlayOwned = window.BadWolfBusy?.show?.() === true;
        }, 120);
    };

    const finishBusy = () => {
        if (busyDelayHandle) {
            window.clearTimeout(busyDelayHandle);
            busyDelayHandle = 0;
        }

        if (busyOverlayOwned) {
            window.BadWolfBusy?.hide?.();
            busyOverlayOwned = false;
        }
    };

    const updateView = async (href, updateHistory, forceRefresh = false) => {
        const currentView = root.querySelector("[data-achievement-history-view]");
        if (!currentView || !href) {
            navigate(href);
            return;
        }

        let targetUrl;
        try {
            targetUrl = new URL(href, window.location.href);
        } catch {
            navigate(href);
            return;
        }

        if (targetUrl.origin !== window.location.origin) {
            navigate(targetUrl.href);
            return;
        }

        if (!forceRefresh && updateHistory && targetUrl.href === window.location.href) {
            return;
        }

        const scrollLeft = window.scrollX;
        const scrollTop = window.scrollY;
        requestController?.abort();

        const controller = new AbortController();
        requestController = controller;
        const version = ++requestVersion;

        currentView.setAttribute("aria-busy", "true");
        startBusyDelay();

        try {
            const response = await fetch(targetUrl.href, {
                method: "GET",
                credentials: "same-origin",
                headers: {
                    "Accept": "text/html",
                    "X-Requested-With": "XMLHttpRequest"
                },
                signal: controller.signal
            });

            if (!response.ok) {
                throw new Error(`Achievement history request failed with ${response.status}.`);
            }

            const html = await response.text();
            if (version !== requestVersion) return;

            const parsed = new DOMParser().parseFromString(html, "text/html");
            const incomingRoot = parsed.querySelector("[data-achievement-history-root]");
            const incomingView = incomingRoot?.querySelector("[data-achievement-history-view]");
            if (!incomingRoot || !incomingView) {
                throw new Error("Achievement history response did not contain the expected view.");
            }

            const replacement = document.importNode(incomingView, true);
            currentView.replaceWith(replacement);

            const currentCount = root.querySelector("[data-achievement-history-session-count]");
            const incomingCount = incomingRoot.querySelector("[data-achievement-history-session-count]");
            if (currentCount && incomingCount) {
                currentCount.textContent = incomingCount.textContent;
            }

            formatTimes(replacement);

            if (updateHistory) {
                window.history.pushState(
                    { playerAchievementHistory: true },
                    "",
                    targetUrl.href);
            }

            window.requestAnimationFrame(() => {
                window.scrollTo(scrollLeft, scrollTop);
            });
        } catch (error) {
            if (error?.name === "AbortError") {
                return;
            }

            console.error("Achievement history view update failed:", error);
            navigate(targetUrl.href);
        } finally {
            if (version === requestVersion) {
                root.querySelector("[data-achievement-history-view]")
                    ?.removeAttribute("aria-busy");
                finishBusy();
            }

            if (requestController === controller) {
                requestController = null;
            }
        }
    };

    const confirmIdentity = async form => {
        if (!(form instanceof HTMLFormElement) || form.dataset.submitting === "true") {
            return;
        }

        const confirmationMessage = form.dataset.confirmMessage;
        if (confirmationMessage && !window.confirm(confirmationMessage)) {
            return;
        }

        const submitButton = form.querySelector("button[type='submit']");
        form.dataset.submitting = "true";
        if (submitButton instanceof HTMLButtonElement) {
            submitButton.disabled = true;
        }
        startBusyDelay();

        try {
            const response = await fetch(form.action, {
                method: "POST",
                credentials: "same-origin",
                headers: {
                    "Accept": "application/json",
                    "X-Requested-With": "XMLHttpRequest"
                },
                body: new FormData(form)
            });

            let payload = null;
            try {
                payload = await response.json();
            } catch {
                payload = null;
            }

            if (!response.ok) {
                throw new Error(
                    payload?.message ||
                    form.dataset.errorMessage ||
                    `Achievement history confirmation failed with ${response.status}.`);
            }

            finishBusy();
            const nextUrl = payload?.nextUrl || window.location.href;
            const absoluteNextUrl = new URL(nextUrl, window.location.href).href;
            const changesAddress = absoluteNextUrl !== window.location.href;
            await updateView(absoluteNextUrl, changesAddress, true);
        } catch (error) {
            console.error("Achievement history confirmation failed:", error);
            finishBusy();
            window.alert(
                error?.message ||
                form.dataset.errorMessage ||
                "Achievement history confirmation failed.");
        } finally {
            if (form.isConnected) {
                delete form.dataset.submitting;
                if (submitButton instanceof HTMLButtonElement) {
                    submitButton.disabled = false;
                }
            }
            finishBusy();
        }
    };

    root.addEventListener("click", event => {
        const link = event.target.closest("a[data-achievement-history-nav]");
        if (!(link instanceof HTMLAnchorElement) || !isPlainLeftClick(event)) {
            return;
        }

        event.preventDefault();
        if (link.hasAttribute("data-achievement-history-view-nav")) {
            void updateView(link.href, true);
            return;
        }

        navigate(link.href);
    });

    root.addEventListener("submit", event => {
        const form = event.target.closest("form[data-achievement-history-confirm-form]");
        if (!(form instanceof HTMLFormElement)) {
            return;
        }

        event.preventDefault();
        void confirmIdentity(form);
    });

    window.addEventListener("popstate", () => {
        void updateView(window.location.href, false);
    });

    document.addEventListener("keydown", event => {
        if (event.key !== "Escape" || event.defaultPrevented ||
            event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
            return;
        }

        const backUrl = root.dataset.backUrl;
        if (!backUrl) return;

        event.preventDefault();
        navigate(backUrl);
    });
})();
