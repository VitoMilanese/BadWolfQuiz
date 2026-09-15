(() => {
    const root = document.querySelector('[data-word-rings-root]');
    const dialog = root?.querySelector('[data-word-rings-result-dialog]');
    const rulesBlock = dialog?.querySelector('[data-result-rules]');
    if (!root || !(dialog instanceof HTMLDialogElement) || !(rulesBlock instanceof HTMLElement)) return;

    const targets = {
        A: rulesBlock.querySelector('[data-result-rule-a]'),
        B: rulesBlock.querySelector('[data-result-rule-b]'),
        C: rulesBlock.querySelector('[data-result-rule-c]')
    };

    const readRule = ring =>
        root.querySelector(`.word-rings-rule-${ring.toLowerCase()} span`)?.textContent?.trim() || '';

    const render = () => {
        const values = {
            A: readRule('A'),
            B: readRule('B'),
            C: readRule('C')
        };
        for (const ring of ['A', 'B', 'C']) {
            if (targets[ring]) targets[ring].textContent = values[ring];
        }
        rulesBlock.hidden = !values.A && !values.B && !values.C;
    };

    root.addEventListener('wordrings:game-ended', () => queueMicrotask(render));
    new MutationObserver(() => {
        if (dialog.open) queueMicrotask(render);
    }).observe(dialog, { attributes: true, attributeFilter: ['open'] });
})();
