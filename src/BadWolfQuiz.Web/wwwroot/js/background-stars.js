(() => {
    const body = document.body;
    const field = document.querySelector('[data-site-starfield]');
    if (!(body instanceof HTMLElement) || !(field instanceof HTMLElement)) {
        return;
    }

    let personalPreference = body.dataset.animatedStars !== 'false';
    const appearanceEndpoint = '/api/background-stars';
    const minStars = 16;
    const maxStars = 52;
    const unknownRetryLimit = 10;
    const registeredRooms = new Set();
    const unknownRetries = new Map();
    let activeContextKey = '';
    let synchronizationGeneration = 0;
    let starfieldHost = null;
    let particlesBuilt = false;
    let hostRefreshFrame = 0;

    const isEnabled = () => body.dataset.animatedStars !== 'false';

    const parseColor = value => {
        const normalized = String(value || '').trim();
        const shortHex = /^#([0-9a-f]{3})$/i.exec(normalized);
        if (shortHex) {
            return shortHex[1].split('').map(part => Number.parseInt(part + part, 16));
        }

        const hex = /^#([0-9a-f]{6})$/i.exec(normalized);
        if (hex) {
            return [
                Number.parseInt(hex[1].slice(0, 2), 16),
                Number.parseInt(hex[1].slice(2, 4), 16),
                Number.parseInt(hex[1].slice(4, 6), 16)
            ];
        }

        const rgb = /^rgba?\(\s*([\d.]+)[,\s]+([\d.]+)[,\s]+([\d.]+)/i.exec(normalized);
        return rgb
            ? [Number(rgb[1]), Number(rgb[2]), Number(rgb[3])]
            : null;
    };

    const isLightTheme = () => {
        const rootStyle = getComputedStyle(document.documentElement);
        const bodyStyle = getComputedStyle(body);
        const rgb = parseColor(rootStyle.getPropertyValue('--bg')) ??
            parseColor(bodyStyle.getPropertyValue('--bg'));
        if (!rgb) {
            return false;
        }

        const linear = rgb.map(channel => {
            const value = Math.max(0, Math.min(255, channel)) / 255;
            return value <= 0.04045
                ? value / 12.92
                : Math.pow((value + 0.055) / 1.055, 2.4);
        });
        const luminance =
            (0.2126 * linear[0]) +
            (0.7152 * linear[1]) +
            (0.0722 * linear[2]);
        return luminance >= 0.48;
    };

    const updateStarfieldTone = () => {
        field.dataset.starfieldTone = isLightTheme() ? 'light' : 'dark';
    };

    const isVisibleElement = element =>
        element instanceof HTMLElement &&
        !element.hidden &&
        getComputedStyle(element).display !== 'none';

    const fillsViewport = element => {
        const rect = element.getBoundingClientRect();
        return rect.width >= window.innerWidth * 0.88 &&
            rect.height >= window.innerHeight * 0.62;
    };

    const hasPaintedBackground = element => {
        if (!(element instanceof HTMLElement)) {
            return false;
        }

        const style = getComputedStyle(element);
        const backgroundImage = String(style.backgroundImage || '').trim();
        if (backgroundImage && backgroundImage !== 'none') {
            return true;
        }

        const backgroundColor = String(style.backgroundColor || '').trim();
        if (!backgroundColor || backgroundColor === 'transparent') {
            return false;
        }

        const alphaMatch = /rgba?\([^)]*(?:,|\/)\s*([\d.]+)\s*\)$/i.exec(backgroundColor);
        const alpha = alphaMatch ? Number(alphaMatch[1]) : 1;
        return Number.isFinite(alpha) && alpha > 0.04;
    };

    const isPageChrome = element =>
        element instanceof HTMLElement &&
        element.matches('.page-heading, header, footer, nav, dialog, .topbar');

    const resolveStarfieldHost = () => {
        const playerGameRoot = document.querySelector(
            '[data-game-code][data-player-id][data-final-status]');
        if (isVisibleElement(playerGameRoot)) {
            return playerGameRoot;
        }

        const hostGameplayView = document.querySelector('[data-host-gameplay-view]');
        if (hostGameplayView instanceof HTMLElement) {
            const activeGameplayRoot = Array.from(hostGameplayView.children)
                .find(element =>
                    isVisibleElement(element) &&
                    !['LINK', 'SCRIPT', 'STYLE'].includes(element.tagName));
            if (activeGameplayRoot instanceof HTMLElement) {
                return activeGameplayRoot;
            }
        }

        const answerKeyRoot = document.querySelector('.answer-key-page');
        if (isVisibleElement(answerKeyRoot)) {
            return answerKeyRoot;
        }

        const visibleHostBoard = document.querySelector(
            '[data-host-gameplay-board]:not([hidden])');
        const hostGameBoard = document.querySelector('.host-game-board');
        if (isVisibleElement(visibleHostBoard) && isVisibleElement(hostGameBoard)) {
            return hostGameBoard;
        }

        const pageShell = document.querySelector('main.page-shell');
        if (pageShell instanceof HTMLElement) {
            const visualChildren = Array.from(pageShell.children).filter(element =>
                element !== field &&
                isVisibleElement(element) &&
                !['LINK', 'SCRIPT', 'STYLE'].includes(element.tagName));

            const gameIntroRoot = visualChildren.find(element =>
                element.classList.contains('game-intro-page') && fillsViewport(element));
            if (gameIntroRoot instanceof HTMLElement) {
                return gameIntroRoot;
            }

            const paintedViewportRoot = visualChildren.find(element =>
                !isPageChrome(element) &&
                fillsViewport(element) &&
                hasPaintedBackground(element));
            if (paintedViewportRoot instanceof HTMLElement) {
                return paintedViewportRoot;
            }

            return pageShell;
        }

        return body;
    };

    const ensureStarfieldHost = () => {
        const nextHost = resolveStarfieldHost();
        if (!(nextHost instanceof HTMLElement)) {
            return false;
        }

        const changed = starfieldHost !== nextHost || field.parentElement !== nextHost;
        if (!changed) {
            return false;
        }

        if (starfieldHost && starfieldHost !== body) {
            starfieldHost.classList.remove('site-starfield-host');
        }
        if (nextHost !== body) {
            nextHost.classList.add('site-starfield-host');
        }
        starfieldHost = nextHost;

        if (field.parentElement !== nextHost) {
            nextHost.append(field);
        }
        return true;
    };

    const buildParticles = () => {
        if (particlesBuilt) {
            return;
        }
        particlesBuilt = true;
        field.replaceChildren();

        const width = Math.max(window.innerWidth, 320);
        const height = Math.max(window.innerHeight, 480);
        const area = width * height;
        const requestedCount = Math.max(
            minStars,
            Math.min(maxStars, Math.round(area / 52000)));
        const minimumDistance = Math.max(
            44,
            Math.min(96, Math.sqrt(area / requestedCount) * 0.42));
        const points = [];
        const fragment = document.createDocumentFragment();
        const maximumAttempts = requestedCount * 48;

        for (let attempt = 0; attempt < maximumAttempts && points.length < requestedCount; attempt++) {
            const x = 8 + Math.random() * Math.max(1, width - 16);
            const y = 8 + Math.random() * Math.max(1, height - 16);
            if (points.some(point => Math.hypot(point.x - x, point.y - y) < minimumDistance)) {
                continue;
            }

            points.push({ x, y });
            const particle = document.createElement('span');
            const bright = Math.random() < 0.14;
            const ring = Math.random() < 0.46;
            const colorIndex = Math.floor(Math.random() * 5);
            const starSize = bright
                ? 2.2 + Math.random() * 1.25
                : 1 + Math.random() * 1.65;
            const bubbleSize = bright
                ? 9 + Math.random() * 5
                : 5 + Math.random() * 6;
            const duration = 3.8 + Math.random() * 5.4;
            const starMinOpacity = 0.08 + Math.random() * 0.20;
            const starMaxOpacity = 0.52 + Math.random() * 0.43;
            const bubbleMinOpacity = 0.24 + Math.random() * 0.18;
            const bubbleMaxOpacity = 0.52 + Math.random() * 0.30;
            const driftX = -5 + Math.random() * 10;
            const driftY = -4 + Math.random() * 8;

            particle.className = [
                'site-starfield-star',
                bright ? 'is-bright' : '',
                ring ? 'is-ring' : 'is-orb',
                `is-color-${colorIndex + 1}`
            ].filter(Boolean).join(' ');
            particle.style.left = `${(x / width) * 100}%`;
            particle.style.top = `${(y / height) * 100}%`;
            particle.style.setProperty('--star-size', `${starSize.toFixed(2)}px`);
            particle.style.setProperty('--bubble-size', `${bubbleSize.toFixed(2)}px`);
            particle.style.setProperty('--particle-duration', `${duration.toFixed(2)}s`);
            particle.style.setProperty('--particle-delay', `${(-Math.random() * duration).toFixed(2)}s`);
            particle.style.setProperty('--star-min-opacity', starMinOpacity.toFixed(2));
            particle.style.setProperty('--star-max-opacity', starMaxOpacity.toFixed(2));
            particle.style.setProperty('--bubble-min-opacity', bubbleMinOpacity.toFixed(2));
            particle.style.setProperty('--bubble-max-opacity', bubbleMaxOpacity.toFixed(2));
            particle.style.setProperty('--bubble-drift-x', `${driftX.toFixed(2)}px`);
            particle.style.setProperty('--bubble-drift-y', `${driftY.toFixed(2)}px`);
            fragment.appendChild(particle);
        }

        field.appendChild(fragment);
    };

    const refreshPresentation = () => {
        ensureStarfieldHost();
        updateStarfieldTone();
        buildParticles();
        field.hidden = !isEnabled();
    };

    const setEnabled = enabled => {
        const normalized = enabled !== false;
        body.dataset.animatedStars = normalized ? 'true' : 'false';
        buildParticles();
        field.hidden = !normalized;
    };

    const scheduleHostRefresh = () => {
        if (hostRefreshFrame !== 0) {
            return;
        }
        hostRefreshFrame = window.requestAnimationFrame(() => {
            hostRefreshFrame = 0;
            ensureStarfieldHost();
        });
    };

    window.BadWolfStarfield = Object.freeze({
        setEnabled,
        isEnabled,
        refresh: refreshPresentation
    });

    const settingsToggle = document.getElementById('Input_AnimatedStarsEnabled');
    if (settingsToggle instanceof HTMLInputElement) {
        settingsToggle.addEventListener('change', () => {
            personalPreference = settingsToggle.checked;
            activeContextKey = '';
            synchronizationGeneration += 1;
            setEnabled(personalPreference);
        });
    }

    const normalizeCode = value => String(value || '').trim().toUpperCase();

    const readGuessWhatToken = code => {
        try {
            return localStorage.getItem(`badwolf-minigame-player:${code}`) || '';
        } catch {
            return '';
        }
    };

    const readWordRingsToken = code => {
        try {
            const value = JSON.parse(localStorage.getItem(`badwolf.wordrings.room.${code}`) || 'null');
            return typeof value?.token === 'string' ? value.token : '';
        } catch {
            return '';
        }
    };

    const getAppearanceContext = () => {
        const path = window.location.pathname.replace(/\/+$/, '').toLowerCase();
        const pathSegments = path.split('/').filter(Boolean);
        const query = new URLSearchParams(window.location.search);

        if (path === '/minigames/guess-what-i-play') {
            const code = normalizeCode(query.get('room'));
            return code
                ? { kind: 'guess', code, playerToken: readGuessWhatToken(code) }
                : null;
        }

        if (path === '/minigames/word-rings') {
            const code = normalizeCode(query.get('room'));
            return code
                ? { kind: 'word-rings', code, playerToken: readWordRingsToken(code) }
                : null;
        }

        if (path === '/join') {
            const code = normalizeCode(query.get('code'));
            return code ? { kind: 'quiz', code, playerToken: '' } : null;
        }

        if (pathSegments.length >= 3 &&
            pathSegments[0] === 'player' &&
            pathSegments[1] === 'lobby') {
            const code = normalizeCode(pathSegments[2]);
            return code ? { kind: 'quiz', code, playerToken: '' } : null;
        }

        if (path === '/player/lobby') {
            const code = normalizeCode(query.get('code'));
            return code ? { kind: 'quiz', code, playerToken: '' } : null;
        }

        return null;
    };

    const registerMinigameAppearance = async context => {
        if (context.kind === 'quiz' || !context.playerToken) {
            return false;
        }

        const registrationKey = `${context.kind}:${context.code}:${context.playerToken}`;
        if (registeredRooms.has(registrationKey)) {
            return false;
        }
        registeredRooms.add(registrationKey);

        const form = new URLSearchParams();
        form.set('kind', context.kind);
        form.set('code', context.code);
        form.set('playerToken', context.playerToken);
        form.set('animatedStarsEnabled', String(personalPreference));

        try {
            const response = await fetch(`${appearanceEndpoint}?handler=Register`, {
                method: 'POST',
                body: form,
                headers: {
                    'Accept': 'application/json',
                    'Content-Type': 'application/x-www-form-urlencoded;charset=UTF-8'
                }
            });
            if (!response.ok) {
                return false;
            }

            const payload = await response.json();
            if (payload?.registered === true && typeof payload.animatedStarsEnabled === 'boolean') {
                setEnabled(payload.animatedStarsEnabled);
                return true;
            }
        } catch (error) {
            console.debug('Could not register the minigame starfield preference.', error);
        }

        return false;
    };

    const loadAppearance = async (context, generation) => {
        await registerMinigameAppearance(context);

        const url = new URL(appearanceEndpoint, window.location.origin);
        url.searchParams.set('kind', context.kind);
        url.searchParams.set('code', context.code);

        try {
            const response = await fetch(url, {
                headers: { 'Accept': 'application/json' },
                cache: 'no-store'
            });
            if (!response.ok || generation !== synchronizationGeneration) {
                return;
            }

            const payload = await response.json();
            if (payload?.known === true && typeof payload.animatedStarsEnabled === 'boolean') {
                unknownRetries.delete(`${context.kind}:${context.code}`);
                setEnabled(payload.animatedStarsEnabled);
                return;
            }

            if (context.kind !== 'quiz') {
                const retryKey = `${context.kind}:${context.code}`;
                const retryCount = unknownRetries.get(retryKey) || 0;
                if (retryCount < unknownRetryLimit) {
                    unknownRetries.set(retryKey, retryCount + 1);
                    window.setTimeout(() => synchronizeAppearance(true), 650);
                }
            }
        } catch (error) {
            console.debug('Could not synchronize the starfield preference.', error);
        }
    };

    const synchronizeAppearance = force => {
        const context = getAppearanceContext();
        if (!context) {
            if (activeContextKey) {
                activeContextKey = '';
                synchronizationGeneration += 1;
                setEnabled(personalPreference);
            }
            return;
        }

        const key = `${context.kind}:${context.code}:${context.playerToken}`;
        if (!force && key === activeContextKey) {
            return;
        }

        activeContextKey = key;
        const generation = ++synchronizationGeneration;
        void loadAppearance(context, generation);
    };

    for (const methodName of ['pushState', 'replaceState']) {
        const original = history[methodName];
        if (typeof original !== 'function') {
            continue;
        }
        history[methodName] = function (...args) {
            const result = original.apply(this, args);
            window.setTimeout(() => synchronizeAppearance(true), 0);
            window.setTimeout(scheduleHostRefresh, 0);
            return result;
        };
    }

    window.addEventListener('popstate', () => {
        synchronizeAppearance(true);
        scheduleHostRefresh();
    });
    window.addEventListener('storage', event => {
        if (event.key?.startsWith('badwolf-minigame-player:') ||
            event.key?.startsWith('badwolf.wordrings.room.')) {
            synchronizeAppearance(true);
        }
    });

    new MutationObserver(() => {
        updateStarfieldTone();
    }).observe(document.documentElement, {
        attributes: true,
        attributeFilter: ['data-theme']
    });

    const pageShell = document.querySelector('main.page-shell');
    if (pageShell instanceof HTMLElement) {
        new MutationObserver(scheduleHostRefresh).observe(pageShell, { childList: true });
    }

    const hostGameplayView = document.querySelector('[data-host-gameplay-view]');
    if (hostGameplayView instanceof HTMLElement) {
        new MutationObserver(scheduleHostRefresh).observe(hostGameplayView, { childList: true });
    }

    refreshPresentation();
    synchronizeAppearance(true);
    window.setInterval(() => synchronizeAppearance(true), 12000);
})();