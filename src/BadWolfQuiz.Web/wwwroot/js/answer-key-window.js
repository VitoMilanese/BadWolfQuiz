(() => {
    const answerKeyWindowName = "badwolf-answer-key";
    const answerKeyPathPrefix = "/admin/games/answerkey/";
    const fallbackPopupFeatures =
        "popup=yes,width=960,height=720,resizable=yes,scrollbars=yes";

    let cachedScreenDetails = null;
    let windowManagementPermissionState = "unknown";

    const isPlainPrimaryActivation = event =>
        event.button === 0 &&
        !event.altKey &&
        !event.ctrlKey &&
        !event.metaKey &&
        !event.shiftKey;

    const isAnswerKeyLink = anchor => {
        if (!(anchor instanceof HTMLAnchorElement)) {
            return false;
        }

        const url = new URL(anchor.href, window.location.href);
        return url.origin === window.location.origin &&
            url.pathname.toLowerCase().startsWith(answerKeyPathPrefix);
    };

    const isSameScreen = (screen, currentScreen) => {
        if (screen === currentScreen) {
            return true;
        }

        if (!screen || !currentScreen) {
            return false;
        }

        return screen.left === currentScreen.left &&
            screen.top === currentScreen.top &&
            screen.width === currentScreen.width &&
            screen.height === currentScreen.height;
    };

    const getOtherScreen = details => {
        const screens = Array.from(details?.screens ?? []);
        const currentScreen = details?.currentScreen;

        if (!currentScreen || screens.length < 2) {
            return null;
        }

        return screens.find(screen => !isSameScreen(screen, currentScreen)) ?? null;
    };

    const finiteOrFallback = (value, fallback) => {
        if (Number.isFinite(value)) {
            return value;
        }

        return Number.isFinite(fallback) ? fallback : null;
    };

    const getAvailableBounds = screen => {
        const left = finiteOrFallback(screen?.availLeft, screen?.left);
        const top = finiteOrFallback(screen?.availTop, screen?.top);
        const width = finiteOrFallback(screen?.availWidth, screen?.width);
        const height = finiteOrFallback(screen?.availHeight, screen?.height);

        if (left === null ||
            top === null ||
            width === null ||
            height === null ||
            width <= 0 ||
            height <= 0) {
            return null;
        }

        return { left, top, width, height };
    };

    const buildPopupFeatures = bounds => {
        if (!bounds) {
            return fallbackPopupFeatures;
        }

        return [
            "popup=yes",
            `left=${Math.round(bounds.left)}`,
            `top=${Math.round(bounds.top)}`,
            `width=${Math.round(bounds.width)}`,
            `height=${Math.round(bounds.height)}`,
            "resizable=yes",
            "scrollbars=yes"
        ].join(",");
    };

    const reinforceWindowBounds = (popupWindow, bounds) => {
        if (!bounds ||
            typeof popupWindow?.moveTo !== "function" ||
            typeof popupWindow?.resizeTo !== "function") {
            return;
        }

        try {
            popupWindow.moveTo(bounds.left, bounds.top);
            popupWindow.resizeTo(bounds.width, bounds.height);
        } catch {
            // Coordinate window.open() placement remains the primary mechanism.
        }
    };

    const openAnswerKeyWindow = (url, bounds) => {
        const popupWindow = window.open(
            url,
            answerKeyWindowName,
            buildPopupFeatures(bounds));

        if (!popupWindow) {
            return null;
        }

        reinforceWindowBounds(popupWindow, bounds);
        popupWindow.focus();
        return popupWindow;
    };

    const getScreenDetailsForPlacement = async () => {
        if (cachedScreenDetails) {
            return cachedScreenDetails;
        }

        try {
            cachedScreenDetails = await window.getScreenDetails();
            windowManagementPermissionState = "granted";
            return cachedScreenDetails;
        } catch {
            return null;
        }
    };

    const preloadWindowManagementState = async () => {
        if (typeof window.getScreenDetails !== "function") {
            windowManagementPermissionState = "unsupported";
            return;
        }

        if (typeof navigator.permissions?.query !== "function") {
            windowManagementPermissionState = "prompt";
            return;
        }

        try {
            const permission = await navigator.permissions.query({
                name: "window-management"
            });
            windowManagementPermissionState = permission.state;

            permission.addEventListener?.("change", () => {
                windowManagementPermissionState = permission.state;
                cachedScreenDetails = null;

                if (permission.state === "granted") {
                    void getScreenDetailsForPlacement();
                }
            });

            if (permission.state === "granted") {
                await getScreenDetailsForPlacement();
            }
        } catch {
            windowManagementPermissionState = "prompt";
        }
    };

    void preloadWindowManagementState();

    document.addEventListener("click", async event => {
        if (event.defaultPrevented || !isPlainPrimaryActivation(event)) {
            return;
        }

        const target = event.target instanceof Element ? event.target : null;
        const anchor = target?.closest("a[href]");
        if (!isAnswerKeyLink(anchor)) {
            return;
        }

        if (typeof window.getScreenDetails !== "function" ||
            windowManagementPermissionState === "denied") {
            return;
        }

        if (window.screen?.isExtended === false &&
            windowManagementPermissionState === "granted") {
            return;
        }

        event.preventDefault();

        const screenDetails = await getScreenDetailsForPlacement();
        const targetBounds = getAvailableBounds(getOtherScreen(screenDetails));
        const answerKeyWindow = openAnswerKeyWindow(anchor.href, targetBounds);

        if (!answerKeyWindow && targetBounds) {
            openAnswerKeyWindow(anchor.href, null);
        }
    });
})();
