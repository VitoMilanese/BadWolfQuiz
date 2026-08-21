(() => {
    const answerKeyWindowName = "badwolf-answer-key";
    const answerKeyPathPrefix = "/admin/games/answerkey/";
    const popupFeatures = "popup=yes,width=960,height=720,resizable=yes,scrollbars=yes";

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

    const moveWindowToScreen = (popupWindow, screen) => {
        const bounds = getAvailableBounds(screen);
        if (!bounds ||
            typeof popupWindow?.moveTo !== "function" ||
            typeof popupWindow?.resizeTo !== "function") {
            return false;
        }

        popupWindow.moveTo(bounds.left, bounds.top);
        popupWindow.resizeTo(bounds.width, bounds.height);
        return true;
    };

    const placeOnOtherScreen = async popupWindow => {
        if (window.screen?.isExtended === false ||
            typeof window.getScreenDetails !== "function") {
            return;
        }

        try {
            const details = await window.getScreenDetails();
            const targetScreen = getOtherScreen(details);
            if (!targetScreen) {
                return;
            }

            if (moveWindowToScreen(popupWindow, targetScreen)) {
                popupWindow.focus();
            }
        } catch {
            // The AnswerKey window is already open; permission or placement failure uses that fallback.
        }
    };

    document.addEventListener("click", event => {
        if (event.defaultPrevented || !isPlainPrimaryActivation(event)) {
            return;
        }

        const target = event.target instanceof Element ? event.target : null;
        const anchor = target?.closest("a[href]");
        if (!isAnswerKeyLink(anchor)) {
            return;
        }

        const answerKeyWindow = window.open(
            anchor.href,
            answerKeyWindowName,
            popupFeatures);

        if (!answerKeyWindow) {
            return;
        }

        event.preventDefault();
        answerKeyWindow.focus();
        void placeOnOtherScreen(answerKeyWindow);
    });
})();
