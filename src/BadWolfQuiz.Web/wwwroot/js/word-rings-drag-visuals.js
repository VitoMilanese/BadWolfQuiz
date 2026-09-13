(() => {
    const root = document.querySelector('[data-word-rings-root]');
    if (!root) return;

    let dragGhost = null;

    const clearDragVisuals = () => {
        dragGhost?.remove();
        dragGhost = null;
        root.querySelectorAll('.word-rings-word.is-dragging').forEach(token => {
            token.classList.remove('is-dragging');
        });
    };

    const createDragGhost = (source, event) => {
        if (!event.dataTransfer) return;

        dragGhost?.remove();

        const sourceRect = source.getBoundingClientRect();
        const ghost = document.createElement('span');
        ghost.className = 'word-rings-word word-rings-drag-ghost';
        ghost.textContent = source.textContent;
        ghost.style.width = `${sourceRect.width}px`;
        ghost.style.height = `${sourceRect.height}px`;

        if (source.classList.contains('is-on-stage')) {
            ghost.classList.add('is-on-stage');
            ghost.dataset.membership = source.dataset.membership ?? '';
        }

        root.append(ghost);
        dragGhost = ghost;

        event.dataTransfer.setDragImage(
            ghost,
            Math.max(1, sourceRect.width / 2),
            Math.max(1, sourceRect.height / 2));
    };

    root.addEventListener('dragstart', event => {
        const target = event.target instanceof Element
            ? event.target.closest('.word-rings-word')
            : null;
        if (!target || !root.contains(target)) return;

        createDragGhost(target, event);
    }, true);

    root.addEventListener('dragend', clearDragVisuals, true);

    root.addEventListener('drop', () => {
        requestAnimationFrame(clearDragVisuals);
    }, true);
})();
