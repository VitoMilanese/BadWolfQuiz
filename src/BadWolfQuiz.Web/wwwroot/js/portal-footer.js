class PortalFooterElement extends HTMLElement {
    #timerId = null;
    #transitionTimeoutId = null;
    #abortController = null;
    #contributors = [];
    #currentIndex = 0;
    #reducedMotion = false;

    connectedCallback() {
        this.#abortController?.abort();
        this.#abortController = new AbortController();
        const signal = this.#abortController.signal;
        this.#contributors = [...this.querySelectorAll("[data-footer-contributor]")];
        this.#currentIndex = Math.max(0, this.#contributors.findIndex(item => item.dataset.current === "true"));
        this.#reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

        const dialog = this.querySelector("dialog");
        const openButton = this.querySelector("[data-open-donation-dialog]");
        const closeButton = this.querySelector("[data-close-donation-dialog]");
        openButton?.addEventListener("click", () => dialog?.showModal(), { signal });
        closeButton?.addEventListener("click", () => dialog?.close(), { signal });
        dialog?.addEventListener("click", event => {
            if (event.target === dialog) {
                dialog.close();
            }
        }, { signal });
        window.addEventListener("pagehide", () => this.#stopRotation(), { signal });

        if (this.#contributors.length > 1) {
            this.#timerId = window.setInterval(() => this.#showNextContributor(), 2000);
        }
    }

    disconnectedCallback() {
        this.#stopRotation();
        this.#abortController?.abort();
        this.#abortController = null;
    }

    #showNextContributor() {
        const current = this.#contributors[this.#currentIndex];
        const nextIndex = (this.#currentIndex + 1) % this.#contributors.length;
        const showNext = () => {
            delete current.dataset.current;
            current.setAttribute("aria-hidden", "true");
            this.#currentIndex = nextIndex;
            const next = this.#contributors[this.#currentIndex];
            next.dataset.current = "true";
            next.setAttribute("aria-hidden", "false");
            this.classList.remove("is-changing-contributor");
            this.#transitionTimeoutId = null;
        };

        if (this.#reducedMotion) {
            showNext();
            return;
        }

        this.classList.add("is-changing-contributor");
        this.#transitionTimeoutId = window.setTimeout(showNext, 160);
    }

    #stopRotation() {
        if (this.#timerId !== null) {
            window.clearInterval(this.#timerId);
            this.#timerId = null;
        }
        if (this.#transitionTimeoutId !== null) {
            window.clearTimeout(this.#transitionTimeoutId);
            this.#transitionTimeoutId = null;
        }
        this.classList.remove("is-changing-contributor");
    }
}

if (!customElements.get("portal-footer")) {
    customElements.define("portal-footer", PortalFooterElement);
}
