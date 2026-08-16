# Restart game

The host **Tools** menu includes **Restart game** as its final action during regular gameplay and while the final question is in progress.

Restarting is destructive and always requires an explicit in-app confirmation dialog. Cancelling the dialog submits nothing and leaves the current game unchanged.

## Runtime behavior

A confirmed restart keeps the existing game registration, public join code, host ownership, player access tokens, presence tracking, and player membership. The quiz runtime state is rebuilt as a fresh run of the same quiz:

- the game stays in the running state;
- the active round returns to round 1;
- every regular question returns to its initial available state;
- question selections, wagers, answer attempts, buzzer state, reveal progress, round-advance flags, final-question progress, and timers are cleared;
- every current and removed player's stored score is reset to `0`;
- the current active player is preserved when that player is still present, otherwise the first remaining player becomes active;
- the current round-start score baseline is reset to `0` for every current player.

The restart action is available from the regular-game Tools menu and from a Tools menu shown during final wagering, final answering, and final judging. The host is redirected to the first running-round intro after the reset. Connected clients receive refreshed game status, player/score state, timer state, and buzzer state over the existing SignalR game group.

When restart occurs while the final question is active, connected player pages also receive the existing final-progress transition signal. Their normal player-transition flow preserves the access token, reloads the page, and renders the fresh running state. This removes stale final-wager keypads, answer fields, final-question content, and other final-only controls instead of leaving them visible after the host has restarted the game.

## Persistence

Restart replaces only the runtime `GameSession` inside the existing `GameSessionRegistration`. The registration itself is retained, so the public code and connection/access bookkeeping remain stable. The registration persistence revision is advanced after the reset so the clean runtime state is persisted normally.

## Release

BadWolfQuiz Web: **1.13.0**

Tag after merge: `web-v1.13.0`.
