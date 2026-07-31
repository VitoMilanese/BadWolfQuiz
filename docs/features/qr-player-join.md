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

The global and per-game setting `AllowNewPlayersAfterStart` controls whether a
name that was not present in the initial lobby may join a running game. Existing
players may still reconnect and require host approval. When late joining is
enabled, new running-game players also appear as pending until the host approves
their connection.
