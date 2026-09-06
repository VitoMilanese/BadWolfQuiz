(() => {
    "use strict";

    const form = document.querySelector("form.host-settings-form");
    if (!(form instanceof HTMLFormElement)) {
        return;
    }

    document.addEventListener("keydown", event => {
        const isSaveKey =
            event.code === "KeyS" ||
            (event.key || "").toLowerCase() === "s";
        const hasSaveModifier = event.ctrlKey || event.metaKey;

        if (!isSaveKey || !hasSaveModifier || event.altKey) {
            return;
        }

        event.preventDefault();
        event.stopImmediatePropagation();

        if (window.BadWolfHostSettingsFloatingSave?.hasUnsavedChanges?.()) {
            form.requestSubmit();
        }
    }, { capture: true });
})();
