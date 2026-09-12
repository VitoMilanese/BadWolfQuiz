# Gameplay achievement unlock notifications

Web `1.40.0` adds real-time achievement unlock presentation during active games.

## Authoritative unlock delivery

Achievement animations are presentation-only. An achievement is first unlocked and persisted by the server, then the existing game SignalR channel publishes an `AchievementUnlocked` notification containing the player identity, achievement code, title, description, artwork URL, and a stable event identifier.

The client never evaluates achievement conditions. Missing a transient animation therefore does not lose the achievement itself: persisted history remains authoritative and continues to appear in the normal achievement UI.

Built-in and host-defined achievements use the same notification payload. Live gameplay evaluation covers safe mid-game conditions immediately, while completion-only achievements continue to wait for authoritative finished-game persistence.

## Player presentation

The affected player receives a large achievement card with artwork, title, and description. Notifications are queued so several unlocks cannot overwrite one another. The current card remains visible long enough to read without blocking normal gameplay input and then dismisses automatically.

A session-scoped consumed-event set prevents reconnects, repeated SignalR state delivery, and normal host/player shell updates from replaying the same animation indefinitely.

## Host presentation

The host sees the achievement artwork animated over the gameplay card of the player who unlocked it. The effect is rendered as a separate overlay positioned from the current player-card bounds rather than replacing the card DOM, so avatar/image/webcam media, contributor frames, buzzer/result states, and player-card sizing are preserved.

Simultaneous unlocks for different players use independent queues. Multiple unlocks for the same player are serialized.

## Reduced motion

The notification styles respect `prefers-reduced-motion`. The unlock information remains visible and readable while movement-heavy effects are reduced.

## Debug testing

When `DebugMode` is enabled in `appsettings.json`, the host gameplay header exposes an additional trophy action. Pressing it unlocks a random still-locked built-in achievement for the first player in the current player list through the normal server-side achievement service and dispatches the same notification used by real gameplay unlocks.

The debug endpoint is available only in debug mode, requires the authenticated owner of the active game, uses anti-forgery validation, and returns the notification to the host as a local fallback so the animation can still be exercised if the dedicated notification SignalR listener is not connected yet.

## Gameplay transition compatibility

The same release keeps two host gameplay transitions AJAX-only: **Nobody answered** for host-selected multiple choice and **Return to board** after peer-rated all-player results. Both update the existing host shell instead of forcing a full-page refresh, which keeps real-time notification listeners and the rest of the gameplay client state intact.

## Regression coverage

Regression tests cover persisted-before-notified delivery, SignalR dispatch, player and host queues, de-duplication, host-card targeting, reduced-motion assets, debug-mode wiring, and the two AJAX-only host transitions.
