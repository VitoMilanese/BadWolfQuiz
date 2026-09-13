(() => {
    const root = document.querySelector('[data-word-rings-root]');
    if (!root) return;

    let dragPreview = null;

    const clearDragVisuals = () => {
        dragPreview?.remove();
        dragPreview = null;
        root.querySelectorAll('.word-rings-word.is-dragging').forEach(token => {
            token.classList.remove('is-dragging');
        });
    };

    const cssVariable = (name, fallback) => {
        const value = getComputedStyle(root).getPropertyValue(name).trim();
        return value || fallback;
    };

    const roundedRect = (context, x, y, width, height, radius) => {
        const r = Math.min(radius, width / 2, height / 2);
        context.beginPath();
        context.moveTo(x + r, y);
        context.lineTo(x + width - r, y);
        context.quadraticCurveTo(x + width, y, x + width, y + r);
        context.lineTo(x + width, y + height - r);
        context.quadraticCurveTo(x + width, y + height, x + width - r, y + height);
        context.lineTo(x + r, y + height);
        context.quadraticCurveTo(x, y + height, x, y + height - r);
        context.lineTo(x, y + r);
        context.quadraticCurveTo(x, y, x + r, y);
        context.closePath();
    };

    const membershipStroke = (context, membership, width, height) => {
        const colors = {
            A: cssVariable('--word-ring-a', '#5f86ee'),
            B: cssVariable('--word-ring-b', '#e0b43c'),
            C: cssVariable('--word-ring-c', '#e85d5d')
        };
        const keys = [...(membership || '')].filter(key => colors[key]);
        if (keys.length === 0) return cssVariable('--line', '#68809a');
        if (keys.length === 1 || typeof context.createConicGradient !== 'function') {
            return colors[keys[0]];
        }

        const gradient = context.createConicGradient(-Math.PI / 2, width / 2, height / 2);
        const segment = 1 / keys.length;
        keys.forEach((key, index) => {
            const start = index * segment;
            const end = (index + 1) * segment;
            gradient.addColorStop(start, colors[key]);
            gradient.addColorStop(Math.max(start, end - 0.0001), colors[key]);
        });
        return gradient;
    };

    const createDragPreview = (source, event) => {
        if (!event.dataTransfer) return;

        dragPreview?.remove();

        const rect = source.getBoundingClientRect();
        const width = Math.max(44, Math.ceil(rect.width));
        const height = Math.max(34, Math.ceil(rect.height));
        const ratio = Math.max(1, Math.min(2, window.devicePixelRatio || 1));
        const canvas = document.createElement('canvas');
        canvas.width = Math.ceil(width * ratio);
        canvas.height = Math.ceil(height * ratio);
        canvas.style.width = `${width}px`;
        canvas.style.height = `${height}px`;
        canvas.style.position = 'fixed';
        canvas.style.left = '0';
        canvas.style.top = '0';
        canvas.style.zIndex = '-1';
        canvas.style.pointerEvents = 'none';

        const context = canvas.getContext('2d');
        if (!context) return;
        context.scale(ratio, ratio);

        const sourceStyles = getComputedStyle(source);
        const borderWidth = 3;
        const inset = borderWidth / 2;
        const membership = source.classList.contains('is-on-stage')
            ? source.dataset.membership ?? ''
            : '';

        roundedRect(context, inset, inset, width - borderWidth, height - borderWidth, height / 2);
        context.fillStyle = cssVariable('--panel-2', '#102f50');
        context.fill();
        context.lineWidth = borderWidth;
        context.strokeStyle = membershipStroke(context, membership, width, height);
        context.stroke();

        const fontSize = Number.parseFloat(sourceStyles.fontSize) || 16;
        const fontFamily = sourceStyles.fontFamily || 'sans-serif';
        const fontWeight = sourceStyles.fontWeight || '800';
        context.font = `${fontWeight} ${fontSize}px ${fontFamily}`;
        context.fillStyle = sourceStyles.color || cssVariable('--text', '#ffffff');
        context.textAlign = 'center';
        context.textBaseline = 'middle';
        context.fillText((source.textContent || '').trim(), width / 2, height / 2 + 0.5);

        document.body.append(canvas);
        dragPreview = canvas;
        event.dataTransfer.setDragImage(canvas, width / 2, height / 2);
    };

    root.addEventListener('dragstart', event => {
        const target = event.target instanceof Element
            ? event.target.closest('.word-rings-word')
            : null;
        if (!target || !root.contains(target)) return;
        createDragPreview(target, event);
    }, true);

    root.addEventListener('dragend', clearDragVisuals, true);
    root.addEventListener('drop', () => requestAnimationFrame(clearDragVisuals), true);
})();
