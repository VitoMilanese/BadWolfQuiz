(() => {
    const language = document.documentElement.lang || undefined;
    let formatter;
    try {
        formatter = new Intl.DateTimeFormat(language, {
            dateStyle: "medium",
            timeStyle: "short"
        });
    } catch {
        formatter = new Intl.DateTimeFormat(undefined, {
            dateStyle: "medium",
            timeStyle: "short"
        });
    }

    for (const element of document.querySelectorAll("time[data-achievement-history-time]")) {
        const value = element.getAttribute("datetime");
        if (!value) continue;

        const date = new Date(value);
        if (Number.isNaN(date.valueOf())) continue;
        element.textContent = formatter.format(date);
    }

    const root = document.querySelector("[data-achievement-history-root]");
    if (!root) return;

    const navigate = href => {
        if (!href) return;

        if (window.BadWolfBusy?.navigate) {
            window.BadWolfBusy.navigate(href);
            return;
        }

        window.location.assign(href);
    };

    for (const link of root.querySelectorAll("a[data-achievement-history-nav]")) {
        link.addEventListener("click", event => {
            if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
                return;
            }

            event.preventDefault();
            navigate(link.href);
        });
    }

    document.addEventListener("keydown", event => {
        if (event.key !== "Escape" || event.defaultPrevented ||
            event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
            return;
        }

        const backUrl = root.dataset.backUrl;
        if (!backUrl) return;

        event.preventDefault();
        navigate(backUrl);
    });
})();
