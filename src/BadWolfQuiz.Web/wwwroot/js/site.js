for (const message of document.querySelectorAll("[data-auto-dismiss]")) {
    window.setTimeout(() => {
        message.classList.add("message-hidden");
        message.addEventListener("transitionend", () => message.remove(), { once: true });
    }, 4000);
}

document.querySelectorAll("details.action-menu").forEach(menu => {
    menu.addEventListener("toggle", () => {
        if (!menu.open) {
            return;
        }

        document.querySelectorAll("details.action-menu[open]").forEach(other => {
            if (other !== menu) {
                other.removeAttribute("open");
            }
        });
    });
});

document.addEventListener("click", event => {
    const selectedItem = event.target.closest?.(".action-menu-item");
    selectedItem?.closest("details.action-menu")?.removeAttribute("open");

    document.querySelectorAll("details.action-menu[open]").forEach(menu => {
        if (!menu.contains(event.target)) {
            menu.removeAttribute("open");
        }
    });
});

document.addEventListener("keydown", event => {
    if (event.key === "Escape") {
        document.querySelectorAll("details.action-menu[open]").forEach(menu => {
            menu.removeAttribute("open");
        });
    }
});
