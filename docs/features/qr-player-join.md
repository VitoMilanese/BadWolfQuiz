# QR Player Join

The host lobby keeps the six-character join code hidden behind a reveal control
by default. The host can reveal the existing QR code and join code when needed.
Dedicated actions allow the host to copy either the join code or the direct
player join link.

The QR payload is the direct player URL `/Join/{code}`. After scanning, the join
page keeps the game code in a hidden field and asks only for the player name.
Manual code entry remains available from the regular join page.

During a running game, the host can open the join information from the **Tools**
menu as a floating panel instead of a modal dialog. The panel can be moved and
resized so the host can place it in an unused part of the game screen.

The panel position, size, and visibility are persisted in the browser and
restored after a page reload. Its title, QR code, and six-character join code
scale with the available panel size so the content remains visible when the
panel is resized.

The floating join panel is temporarily hidden while a question or answer is
being displayed so it does not obstruct gameplay. When the game returns to a
state where the panel can be shown, an open panel becomes visible again.

## Public address

`Game:PublicBaseUrl` controls the origin encoded in the QR code. When it is empty,
the application uses the scheme, host, port, and path base of the current host
request. Set it when the host opens the site through `localhost`, behind a reverse
proxy, or at an address that player phones cannot reach directly.

Example:

```json
"Game": {
  "PublicBaseUrl": "https://quiz.example.com"
}
```

The QR PNG endpoint is part of the authenticated host page and verifies that the
requested active game belongs to the current host.

## Joining after the game starts

The host can open or close late joining during a running game with the lock
button beside the player list. Existing players may still reconnect and require
host approval while the lock is closed. When the lock is open, new running-game
players appear as pending until the host approves their connection.

## Removed and blocked players

The player-removal dialog gives the host two choices. **Remove player** revokes
every active access token, disconnects the player, and removes their active card
without blocking the name from joining again. **Block player** performs the same
removal but keeps the player blocked from joining the same game with the same
case-insensitive name until the host explicitly unblocks them from **Blocked
players** in the in-game **Tools** menu.

A removed-only player does not appear in **Blocked players** and may immediately
submit a fresh join request. After a blocked player is unblocked, the next
successful join follows the same restoration flow. In both cases, rejoining
restores the original player record, including its identifier, score, avatar,
and uploaded-image selection. The browser clears revoked player access when it
receives the removal event so the next join submits fresh credentials instead of
following a stale player-page URL.

## Player-device navigation and screen wake behavior

The compact **Menu** expander on the player buzzer page contains the same
navigation actions available to that user in the regular site header, together
with the language selector. The full header remains hidden so the buzzer and
player controls retain the available phone viewport.

While the buzzer page is visible, the client requests the browser's Screen Wake
Lock and periodically reacquires it if the browser releases it. A media-based
fallback starts after player interaction for Safari and browsers where the
standard API is unavailable.

Unblocking does not add a disconnected card immediately. The next successful
join restores the original player record, including its identifier, score,
avatar, and uploaded-image selection. The returning player remains pending until
the host approves the rejoin.

Rejoin approval is propagated in real time. The host player list updates without
a full page reload, and the approved player is immediately notified that access
has been restored.

The browser clears revoked player access when it receives the removal event so
the next join submits fresh credentials instead of following a stale player-page
URL.
