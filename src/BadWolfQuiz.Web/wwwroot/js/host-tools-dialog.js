(() => {
    const dialog = document.querySelector("[data-host-tools-dialog]");
    if (!(dialog instanceof HTMLDialogElement)) {
        return;
    }

    let opener = null;

    for (const button of document.querySelectorAll(
        "[data-open-host-tools-dialog]")) {
        button.addEventListener("click", () => {
            opener = button;
            if (!dialog.open) {
                dialog.showModal();
            }
        });
    }

    for (const button of dialog.querySelectorAll(
        "[data-close-host-tools-dialog]")) {
        button.addEventListener("click", () => dialog.close());
    }

    dialog.addEventListener(
        "click",
        event => {
            const action = event.target instanceof Element
                ? event.target.closest("[data-host-tools-dialog-dismiss]")
                : null;
            if (action && dialog.contains(action) && dialog.open) {
                dialog.close();
            }
        },
        true);

    dialog.addEventListener("click", event => {
        if (event.target === dialog) {
            dialog.close();
        }
    });

    dialog.addEventListener("close", () => {
        opener?.focus();
        opener = null;
    });
})();
