(() => {
    const root = document.querySelector('[data-word-rings-root]');
    if (!root || typeof window.BadWolfWordRingsPointerDrag === 'function') return;

    window.BadWolfWordRingsPointerDrag = ({
        stage,
        wordList,
        outsideZone,
        membershipAt,
        assignToStage,
        assignOutside,
        returnToBank,
        onChanged
    }) => {
        const wiredWords = new WeakSet();
        let active = null;

        const copyRingVariables = preview => {
            const styles = getComputedStyle(root);
            for (const name of ['--word-ring-a', '--word-ring-b', '--word-ring-c']) {
                const value = styles.getPropertyValue(name).trim();
                if (value) preview.style.setProperty(name, value);
            }
        };

        const createPreview = (source, event) => {
            const rect = source.getBoundingClientRect();
            const preview = source.cloneNode(true);
            preview.hidden = false;
            preview.tabIndex = -1;
            preview.draggable = false;
            preview.setAttribute('aria-hidden', 'true');
            preview.classList.remove('is-dragging', 'is-correct', 'is-wrong', 'is-placed');
            preview.classList.add('word-rings-pointer-preview');
            copyRingVariables(preview);
            document.body.append(preview);

            return {
                preview,
                offsetX: Math.max(0, Math.min(rect.width, event.clientX - rect.left)),
                offsetY: Math.max(0, Math.min(rect.height, event.clientY - rect.top))
            };
        };

        const positionPreview = (drag, clientX, clientY) => {
            drag.preview.style.left = `${clientX - drag.offsetX}px`;
            drag.preview.style.top = `${clientY - drag.offsetY}px`;
        };

        const clearDropTargets = () => {
            stage.classList.remove('is-drop-target');
            outsideZone.classList.remove('is-drop-target');
            wordList.classList.remove('is-drop-target');
        };

        const refreshPreview = (drag, clientX, clientY) => {
            const element = document.elementFromPoint(clientX, clientY);
            clearDropTargets();

            if (element && stage.contains(element)) {
                const membership = membershipAt(clientX, clientY).membership;
                drag.preview.classList.add('is-on-stage');
                drag.preview.classList.remove('is-outside');
                drag.preview.dataset.membership = membership;
                stage.classList.add('is-drop-target');
                return;
            }

            drag.preview.classList.remove('is-on-stage');
            drag.preview.dataset.membership = '';

            if (element && outsideZone.contains(element)) {
                drag.preview.classList.add('is-outside');
                outsideZone.classList.add('is-drop-target');
                return;
            }

            drag.preview.classList.remove('is-outside');
            if (element && wordList.contains(element)) {
                wordList.classList.add('is-drop-target');
            }
        };

        const clearActive = drag => {
            drag.preview.remove();
            drag.source.classList.remove('is-dragging');
            clearDropTargets();
        };

        const finish = (event, cancelled) => {
            const drag = active;
            if (!drag || drag.pointerId !== event.pointerId) return;

            const dropTarget = cancelled
                ? null
                : document.elementFromPoint(event.clientX, event.clientY);

            try {
                if (drag.source.hasPointerCapture?.(event.pointerId)) {
                    drag.source.releasePointerCapture(event.pointerId);
                }
            } catch {
                // The source may already have been detached by a completed drop.
            }

            active = null;
            clearActive(drag);

            if (cancelled || !dropTarget) return;

            if (stage.contains(dropTarget)) {
                assignToStage(drag.word, event.clientX, event.clientY);
                onChanged();
                return;
            }

            if (outsideZone.contains(dropTarget)) {
                assignOutside(drag.word);
                onChanged();
                return;
            }

            if (wordList.contains(dropTarget)) {
                returnToBank(drag.word);
                onChanged();
            }
        };

        const begin = (word, event) => {
            if (active) return;
            if (event.pointerType === 'mouse' && event.button !== 0) return;

            const value = word.dataset.word;
            if (!value) return;

            const previewState = createPreview(word, event);
            active = {
                word: value,
                source: word,
                pointerId: event.pointerId,
                ...previewState
            };

            word.classList.add('is-dragging');
            try {
                word.setPointerCapture?.(event.pointerId);
            } catch {
                // Pointer capture is an enhancement; document listeners still keep the drag alive.
            }

            positionPreview(active, event.clientX, event.clientY);
            refreshPreview(active, event.clientX, event.clientY);
            event.preventDefault();
        };

        const wireWord = word => {
            if (wiredWords.has(word)) return;
            wiredWords.add(word);
            word.draggable = false;
            word.addEventListener('pointerdown', event => begin(word, event));
        };

        document.addEventListener('pointermove', event => {
            if (!active || active.pointerId !== event.pointerId) return;
            positionPreview(active, event.clientX, event.clientY);
            refreshPreview(active, event.clientX, event.clientY);
            event.preventDefault();
        }, { passive: false });

        document.addEventListener('pointerup', event => finish(event, false));
        document.addEventListener('pointercancel', event => finish(event, true));

        return wireWord;
    };
})();
