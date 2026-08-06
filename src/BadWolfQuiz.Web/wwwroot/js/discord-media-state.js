((root, factory) => {
    const MediaState = factory();
    if (typeof module === "object" && module.exports) {
        module.exports = MediaState;
    } else {
        root.BadWolfDiscordMediaState = MediaState;
    }
})(typeof globalThis !== "undefined" ? globalThis : window, () =>
    class DiscordMediaState {
        constructor(onChange) {
            this.active = new Set();
            this.onChange = onChange;
        }

        start(key) {
            const wasActive = this.active.size > 0;
            this.active.add(key);
            if (!wasActive) {
                this.onChange(true);
            }
        }

        stop(key) {
            const wasActive = this.active.size > 0;
            this.active.delete(key);
            if (wasActive && this.active.size === 0) {
                this.onChange(false);
            }
        }

        get isActive() {
            return this.active.size > 0;
        }
    });
