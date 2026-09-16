# Word Rings gameplay options

Word Rings now separates the score target from the number of words visible in a player's hand.

- **Play to** is configurable from 5 to 20 points and defaults to 5.
- **Words in hand** is configurable from 5 to 10 and defaults to 5. It can never exceed the score target.
- A player's visible hand is capped again by the number of points still needed to win, rounded up. For example, 0.5 points remaining allows 1 visible word, 1.5 allows 2, and 2.5 allows 3.
- Words beyond the current visible hand remain queued and are not valid targets for word-based action cards until they become visible.
- A player wins immediately when either their score reaches the target or their remaining word pool reaches zero.

The multiplayer creation dialog exposes both **Play to** and **Words in hand**. Solo mode exposes the same values in the gear/settings dialog and stores the choice locally for subsequent solo games.

At the beginning of every multiplayer round, the server randomly chooses the first playing participant. The player list shows a roulette-style highlight that accelerates and then slows down until it lands on the server-selected player, with a short tick sound for each move. A dedicated host who only judges words is excluded from the roulette.
