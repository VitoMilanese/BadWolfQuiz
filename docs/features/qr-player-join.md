# QR Player Join

The host game screen shows a QR code together with the existing six-character
join code. The QR payload is the direct player URL `/Join/{code}`. After scanning,
the join page keeps the game code in a hidden field and asks only for the player
name. Manual code entry remains available from the regular join page.

The in-game join overlay constrains its title, QR image, and six-character code
to the current viewport. On short or narrow displays the dialog itself scrolls
instead of allowing the code or QR image to overflow its frame.

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

Removing a player revokes every active access token and removes the player from
the active card list. The player remains blocked from joining the same game with
the same case-insensitive name. The host can open **Blocked players** from the
in-game **Tools** menu and explicitly unblock that player.

Unblocking does not add a disconnected card immediately. The next successful
join restores the original player record, including its identifier, score,
avatar, and uploaded-image selection. The browser clears revoked player access
when it receives the removal event so the next join submits fresh credentials
instead of following a stale player-page URL.

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
