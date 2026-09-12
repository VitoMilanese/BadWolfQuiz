(() => {
    "use strict";

    if (window.badWolfPeerRatedReturnNoReloadInitialized) {
        return;
    }
    window.badWolfPeerRatedReturnNoReloadInitialized = true;

    // The peer-rated runtime already handles ReturnToBoard with
    // BadWolfHostGameplay.refresh(). Prevent the older polish fallback from
    // installing its fetch wrapper, which forced a full window reload after
    // the same successful request.
    window.badWolfPeerRatedReturnFetchWrapped = true;
})();
