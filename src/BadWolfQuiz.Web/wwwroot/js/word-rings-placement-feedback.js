(() => {
    window.BadWolfWordRingsPlacementFeedback = ({ root } = {}) => {
        if (!(root instanceof HTMLElement)) return { play: () => {} };

        const classes = ['is-feedback-correct', 'is-feedback-partial', 'is-feedback-wrong'];
        const play = (token, kind) => {
            if (!(token instanceof HTMLElement)) return;
            const normalized = kind === 'correct' || kind === 'partial' ? kind : 'wrong';
            token.classList.remove(...classes);
            void token.offsetWidth;
            token.classList.add(`is-feedback-${normalized}`);
            window.setTimeout(() => token.classList.remove(`is-feedback-${normalized}`), 760);

            if (window.matchMedia?.('(prefers-reduced-motion: reduce)').matches) return;
            const rect = token.getBoundingClientRect();
            if (rect.width <= 0 || rect.height <= 0) return;

            const effect = document.createElement('span');
            effect.className = `word-rings-placement-fx is-${normalized}`;
            effect.style.left = `${rect.left + rect.width / 2}px`;
            effect.style.top = `${rect.top + rect.height / 2}px`;
            effect.setAttribute('aria-hidden', 'true');

            const count = normalized === 'correct' ? 14 : 10;
            for (let index = 0; index < count; index += 1) {
                const particle = document.createElement('span');
                particle.className = 'word-rings-placement-fx-particle';
                const angle = (Math.PI * 2 * index / count) + (normalized === 'wrong' ? 0.14 : 0);
                const distance = (normalized === 'correct' ? 44 : normalized === 'partial' ? 34 : 28) + ((index % 3) * 5);
                particle.style.setProperty('--fx-x', `${Math.cos(angle) * distance}px`);
                particle.style.setProperty('--fx-y', `${Math.sin(angle) * distance}px`);
                particle.style.setProperty('--fx-delay', `${(index % 4) * 18}ms`);
                effect.append(particle);
            }

            document.body.append(effect);
            window.setTimeout(() => effect.remove(), 900);
        };

        return { play };
    };
})();
