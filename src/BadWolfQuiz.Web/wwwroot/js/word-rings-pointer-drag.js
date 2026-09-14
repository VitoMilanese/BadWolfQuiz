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
        canBegin = () => true,
        canDropStage = () => true,
        canDropOutside = () => true,
        canReturnToBank = () => true,
        bringToFront = () => {},
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
            const sourceOffsetX = Math.max(0, Math.min(rect.width, event.clientX - rect.left));
            const sourceOffsetY = Math.max(0, Math.min(rect.height, event.clientY - rect.top));
            const clickRatioX = rect.width > 0 ? sourceOffsetX / rect.width : 0.5;
            const clickRatioY = rect.height > 0 ? sourceOffsetY / rect.height : 0.5;

            const preview = source.cloneNode(true);
            preview.hidden = false;
            preview.tabIndex = -1;
            preview.draggable = false;
            preview.setAttribute('aria-hidden', 'true');
            preview.classList.remove('is-dragging', 'is-correct', 'is-wrong', 'is-placed');
            preview.classList.add('word-rings-pointer-preview');
            copyRingVariables(preview);
            document.body.append(preview);

            const previewRect = preview.getBoundingClientRect();
            return {
                preview,
                offsetX: previewRect.width * clickRatioX,
                offsetY: previewRect.height * clickRatioY
            };
        };

        const positionPreview = (drag, clientX, clientY) => {
            drag.preview.style.left = `${clientX - drag.offsetX}px`;
            drag.preview.style.top = `${clientY - drag.offsetY}px`;
        };

        const clearDropTargets = () => {
            stage.classList.remove('is-drop-target');
            outsideZone?.classList.remove('is-drop-target');
            wordList.classList.remove('is-drop-target');
        };

        const refreshPreview = (drag, clientX, clientY) => {
            const element = document.elementFromPoint(clientX, clientY);
            clearDropTargets();
            drag.preview.classList.remove('is-drop-blocked');

            if (element && stage.contains(element)) {
                const membership = membershipAt(clientX, clientY).membership;
                const allowed = canDropStage(drag.word, membership, drag.source);
                drag.preview.classList.add('is-on-stage');
                drag.preview.classList.remove('is-outside');
                drag.preview.dataset.membership = membership;
                drag.preview.classList.toggle('is-drop-blocked', !allowed);
                if (allowed) stage.classList.add('is-drop-target');
                return;
            }

            drag.preview.classList.remove('is-on-stage');
            drag.preview.dataset.membership = '';

            if (element && outsideZone?.contains(element)) {
                const allowed = canDropOutside(drag.word, drag.source);
                drag.preview.classList.add('is-outside');
                drag.preview.classList.toggle('is-drop-blocked', !allowed);
                if (allowed) outsideZone.classList.add('is-drop-target');
                return;
            }

            drag.preview.classList.remove('is-outside');
            if (element && wordList.contains(element)) {
                const allowed = canReturnToBank(drag.word, drag.source);
                drag.preview.classList.toggle('is-drop-blocked', !allowed);
                if (allowed) wordList.classList.add('is-drop-target');
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

            if (cancelled || !drag.moved || !dropTarget) return;

            if (stage.contains(dropTarget)) {
                const membership = membershipAt(event.clientX, event.clientY).membership;
                if (!canDropStage(drag.word, membership, drag.source)) return;
                assignToStage(drag.word, event.clientX, event.clientY, drag.source);
                onChanged();
                return;
            }

            if (outsideZone?.contains(dropTarget)) {
                if (!canDropOutside(drag.word, drag.source)) return;
                assignOutside(drag.word, drag.source);
                onChanged();
                return;
            }

            if (wordList.contains(dropTarget) && canReturnToBank(drag.word, drag.source)) {
                returnToBank(drag.word, drag.source);
                onChanged();
            }
        };

        const begin = (word, event) => {
            if (active) return;
            if (event.pointerType === 'mouse' && event.button !== 0) return;

            const value = word.dataset.word;
            if (!value) return;

            bringToFront(word);
            if (!canBegin(value, word)) return;

            const previewState = createPreview(word, event);
            active = {
                word: value,
                source: word,
                pointerId: event.pointerId,
                startX: event.clientX,
                startY: event.clientY,
                moved: false,
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
            if (!active.moved && Math.hypot(event.clientX - active.startX, event.clientY - active.startY) > 3) {
                active.moved = true;
            }
            positionPreview(active, event.clientX, event.clientY);
            refreshPreview(active, event.clientX, event.clientY);
            event.preventDefault();
        }, { passive: false });

        document.addEventListener('pointerup', event => finish(event, false));
        document.addEventListener('pointercancel', event => finish(event, true));

        return wireWord;
    };
})();
