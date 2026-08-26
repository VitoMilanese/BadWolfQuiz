(() => {
    "use strict";

    if (window.badWolfMultipleChoiceAnswerOptionsGuardInstalled) {
        return;
    }
    window.badWolfMultipleChoiceAnswerOptionsGuardInstalled = true;

    const style = document.createElement("style");
    style.id = "multiple-choice-answer-options-guard-styles";
    style.textContent = `
.multiple-choice-answer-options-help[hidden] {
    display: none !important;
}
`;
    document.head.appendChild(style);

    const NativeMutationObserver = window.MutationObserver;
    const nativeAddEventListener = EventTarget.prototype.addEventListener;
    let overridesActive = false;

    const isAnswerBlockList = target =>
        target instanceof Element &&
        target.matches("#answer-blocks [data-content-block-list]");

    const mutationTargetElement = mutation =>
        mutation.target instanceof Element
            ? mutation.target
            : mutation.target.parentElement;

    const containsContentBlockCard = node =>
        node instanceof Element &&
        (node.matches(".content-block-card") ||
            node.querySelector(".content-block-card"));

    const isStructuralAnswerMutation = mutation => {
        if (mutation.type !== "childList") {
            return false;
        }

        const target = mutationTargetElement(mutation);
        if (target?.closest(".multiple-choice-answer-option-correct-badge")) {
            return false;
        }

        const changedNodes = [
            ...mutation.addedNodes,
            ...mutation.removedNodes
        ];
        return changedNodes.some(containsContentBlockCard);
    };

    class GuardedMutationObserver {
        constructor(callback) {
            this.structuralAnswerOnly = false;
            this.callback = callback;
            this.inner = new NativeMutationObserver(mutations => {
                const filtered = this.structuralAnswerOnly
                    ? mutations.filter(isStructuralAnswerMutation)
                    : mutations;
                if (filtered.length > 0) {
                    this.callback(filtered, this);
                }
            });
        }

        observe(target, options) {
            this.structuralAnswerOnly =
                isAnswerBlockList(target) &&
                options?.childList === true &&
                options?.subtree === true;
            this.inner.observe(target, options);
        }

        disconnect() {
            this.inner.disconnect();
        }

        takeRecords() {
            const records = this.inner.takeRecords();
            return this.structuralAnswerOnly
                ? records.filter(isStructuralAnswerMutation)
                : records;
        }
    }

    const guardedAddEventListener = function(type, listener, options) {
        const isRedundantAnswerInputSync =
            type === "input" &&
            isAnswerBlockList(this) &&
            typeof listener === "function" &&
            listener.name === "scheduleSync";
        if (isRedundantAnswerInputSync) {
            return;
        }

        return nativeAddEventListener.call(this, type, listener, options);
    };

    const installOverrides = () => {
        if (overridesActive) {
            return;
        }
        overridesActive = true;
        if (typeof NativeMutationObserver === "function") {
            window.MutationObserver = GuardedMutationObserver;
        }
        EventTarget.prototype.addEventListener = guardedAddEventListener;
    };

    const restoreOverrides = () => {
        if (!overridesActive) {
            return;
        }
        overridesActive = false;
        if (typeof NativeMutationObserver === "function") {
            window.MutationObserver = NativeMutationObserver;
        }
        EventTarget.prototype.addEventListener = nativeAddEventListener;
    };

    window.badWolfMultipleChoiceAnswerOptionsRestoreMutationObserver = () => {
        restoreOverrides();
        delete window.badWolfMultipleChoiceAnswerOptionsRestoreMutationObserver;
    };

    if (document.readyState === "loading") {
        // Capture-phase DOMContentLoaded runs before the controller's normal
        // DOMContentLoaded listener. This keeps the monkey-patch scoped to the
        // controller initialization instead of the whole page parse lifecycle.
        nativeAddEventListener.call(
            document,
            "DOMContentLoaded",
            installOverrides,
            { capture: true, once: true });
    } else {
        installOverrides();
    }
})();
