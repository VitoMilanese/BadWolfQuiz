# Restart game

The host **Tools** menu includes **Restart game** as its final action during regular gameplay.

Restarting is destructive and always requires an explicit browser confirmation. Cancelling the confirmation submits nothing and leaves the running game unchanged.

## Runtime behavior

A confirmed restart keeps the existing game registration, public join code, host ownership, player access tokens, presence tracking, and player membership. The quiz runtime state is rebuilt as a fresh run of the same quiz:

- the game stays in the running state;
- the active round returns to round 1;
- every regular question returns to its initial available state;
- question selections, wagers, answer attempts, buzzer state, reveal progress, round-advance flags, final-question progress, and timers are cleared;
- every current and removed player's stored score is reset to `0`;
- the current active player is preserved when that player is still present, otherwise the first remaining player becomes active;
- the current round-start score baseline is reset to `0` for every current player.

The host is redirected to the first running-round intro after the reset. Connected clients receive refreshed game status, player/score state, timer state, and buzzer state over the existing SignalR game group.

## Persistence

Restart replaces only the runtime `GameSession` inside the existing `GameSessionRegistration`. The registration itself is retained, so the public code and connection/access bookkeeping remain stable. The registration persistence revision is advanced after the reset so the clean runtime state is persisted normally.

## Release

BadWolfQuiz Web: **1.13.0**

Tag after merge: `web-v1.13.0`.
