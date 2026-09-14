(() => {
    'use strict';

    const rootSelector = '[data-word-rings-editor-shell]';
    const minimumWordLength = 3;

    const membershipInput = () =>
        document.querySelector('[data-word-rings-membership-word]');

    const enforceMinimumWordLength = () => {
        const input = membershipInput();
        if (!(input instanceof HTMLInputElement)) return;

        input.minLength = minimumWordLength;
        const save = document.querySelector('[data-word-rings-membership-save]');
        if (save instanceof HTMLButtonElement && input.value.trim().length < minimumWordLength) {
            save.disabled = true;
        }
    };

    const enhanceWordPager = root => {
        if (!(root instanceof HTMLElement)) return;
        if (root.querySelector('[data-word-rings-word-pager-position="top"]')) return;

        const bottom = root.querySelector('.word-rings-editor-word-pager');
        const list = root.querySelector('.word-rings-editor-word-list');
        if (!(bottom instanceof HTMLElement) || !(list instanceof HTMLElement)) return;

        bottom.dataset.wordRingsWordPagerPosition = 'bottom';
        const top = bottom.cloneNode(true);
        if (!(top instanceof HTMLElement)) return;

        top.dataset.wordRingsWordPagerPosition = 'top';
        top.classList.add('word-rings-editor-word-pager-top');
        list.before(top);
    };

    const enhance = () => {
        enforceMinimumWordLength();
        enhanceWordPager(document.querySelector(rootSelector));
    };

    const initialRoot = document.querySelector(rootSelector);
    const host = initialRoot?.parentElement;
    if (host) {
        new MutationObserver(enhance).observe(host, { childList: true });
    }

    document.addEventListener('input', event => {
        if (event.target instanceof HTMLInputElement &&
            event.target.matches('[data-word-rings-membership-word]')) {
            enforceMinimumWordLength();
        }
    });

    document.addEventListener('change', event => {
        if (event.target instanceof HTMLElement &&
            (event.target.matches('[data-word-rings-membership-rule]') ||
             event.target.matches('[data-word-rings-membership-ring]'))) {
            queueMicrotask(enforceMinimumWordLength);
        }
    });

    enhance();
})();
