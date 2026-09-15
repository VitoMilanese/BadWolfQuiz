(() => {
    const body = document.body;
    const field = document.querySelector('[data-site-starfield]');
    if (!(body instanceof HTMLElement) || !(field instanceof HTMLElement)) {
        return;
    }

    const personalPreference = body.dataset.animatedStars !== 'false';
    const appearanceEndpoint = '/api/background-stars';
    const minStars = 16;
    const maxStars = 52;
    const unknownRetryLimit = 10;
    const registeredRooms = new Set();
    const unknownRetries = new Map();
    let resizeTimer = null;
    let activeContextKey = '';
    let synchronizationGeneration = 0;
    let starfieldHost = null;

    const resolveStarfieldHost = () => {
        const pageShell = document.querySelector('main.page-shell');
        const pageRoot = pageShell?.firstElementChild;
        if (pageRoot instanceof HTMLElement) {
            return pageRoot;
        }
        if (pageShell instanceof HTMLElement) {
            return pageShell;
        }

        const standaloneRoot = Array.from(body.children).find(element =>
            element !== field &&
            element instanceof HTMLElement &&
            !['SCRIPT', 'STYLE', 'LINK'].includes(element.tagName));
        return standaloneRoot instanceof HTMLElement ? standaloneRoot : body;
    };

    const ensureStarfieldHost = () => {
        const nextHost = resolveStarfieldHost();
        if (!(nextHost instanceof HTMLElement)) {
            return;
        }

        if (starfieldHost !== nextHost) {
            starfieldHost?.classList.remove('site-starfield-host');
            nextHost.classList.add('site-starfield-host');
            starfieldHost = nextHost;
        }

        if (field.parentElement !== nextHost) {
            nextHost.prepend(field);
        }
    };

    const isEnabled = () => body.dataset.animatedStars !== 'false';

    const renderStars = () => {
        ensureStarfieldHost();
        field.replaceChildren();
        const enabled = isEnabled();
        field.hidden = !enabled;
        if (!enabled) {
            return;
        }

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
            const star = document.createElement('span');
            const bright = Math.random() < 0.13;
            const size = bright
                ? 2.2 + Math.random() * 1.25
                : 1 + Math.random() * 1.65;
            const duration = 3.1 + Math.random() * 4.7;
            const minOpacity = 0.08 + Math.random() * 0.20;
            const maxOpacity = 0.52 + Math.random() * 0.43;

            star.className = bright
                ? 'site-starfield-star is-bright'
                : 'site-starfield-star';
            star.style.left = `${(x / width) * 100}%`;
            star.style.top = `${(y / height) * 100}%`;
            star.style.setProperty('--star-size', `${size.toFixed(2)}px`);
            star.style.setProperty('--star-duration', `${duration.toFixed(2)}s`);
            star.style.setProperty('--star-delay', `${(-Math.random() * duration).toFixed(2)}s`);
            star.style.setProperty('--star-min-opacity', minOpacity.toFixed(2));
            star.style.setProperty('--star-max-opacity', maxOpacity.toFixed(2));
            fragment.appendChild(star);
        }

        field.appendChild(fragment);
    };

    const setEnabled = enabled => {
        const normalized = enabled !== false;
        if ((body.dataset.animatedStars !== 'false') === normalized && field.childElementCount > 0) {
            return;
        }

        body.dataset.animatedStars = normalized ? 'true' : 'false';
        renderStars();
    };

    window.BadWolfStarfield = Object.freeze({
        setEnabled,
        isEnabled,
        refresh: renderStars
    });

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

        if (path === '/join' || path === '/player/lobby') {
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
            return result;
        };
    }

    window.addEventListener('popstate', () => synchronizeAppearance(true));
    window.addEventListener('storage', event => {
        if (event.key?.startsWith('badwolf-minigame-player:') ||
            event.key?.startsWith('badwolf.wordrings.room.')) {
            synchronizeAppearance(true);
        }
    });

    window.addEventListener('resize', () => {
        if (resizeTimer !== null) {
            window.clearTimeout(resizeTimer);
        }
        resizeTimer = window.setTimeout(() => {
            resizeTimer = null;
            renderStars();
        }, 180);
    });

    const pageShell = document.querySelector('main.page-shell');
    if (pageShell instanceof HTMLElement) {
        new MutationObserver(() => {
            const previousHost = starfieldHost;
            ensureStarfieldHost();
            if (previousHost !== starfieldHost) {
                renderStars();
            }
        }).observe(pageShell, { childList: true });
    }

    renderStars();
    synchronizeAppearance(true);
    window.setInterval(() => synchronizeAppearance(true), 12000);
})();
