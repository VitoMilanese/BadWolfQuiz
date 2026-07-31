# QR Player Join

The host game screen shows a QR code together with the existing six-character
join code. The QR payload is the direct player URL `/Join/{code}`. After scanning,
the join page keeps the game code in a hidden field and asks only for the player
name. Manual code entry remains available from the regular join page.

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
