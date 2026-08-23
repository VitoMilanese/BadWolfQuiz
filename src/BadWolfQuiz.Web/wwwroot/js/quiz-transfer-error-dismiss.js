(() => {
    const dismissDelayMilliseconds = 4000;
    const removalFallbackMilliseconds = 400;

    document.querySelectorAll('.message.message-error[role="alert"]')
        .forEach(message => {
            message.setAttribute("data-auto-dismiss", "");

            window.setTimeout(() => {
                message.classList.add("message-hidden");

                let removed = false;
                const removeMessage = () => {
                    if (removed) {
                        return;
                    }

                    removed = true;
                    message.remove();
                };

                message.addEventListener("transitionend", removeMessage, { once: true });
                window.setTimeout(removeMessage, removalFallbackMilliseconds);
            }, dismissDelayMilliseconds);
        });
})();
