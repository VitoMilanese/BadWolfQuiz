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
})();
