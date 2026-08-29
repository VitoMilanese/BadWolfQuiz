# Minigames

BadWolfQuiz includes a standalone **Minigames** section for lightweight games that can be played independently from the Jeopardy-style quiz runtime.

The header **Minigames** action opens the catalog at `/minigames`. Each minigame has its own page so additional games can be added without changing the catalog route.

## Guess what I'm playing

The first minigame is **Guess what I'm playing** (`Вгадай, у що я граю`) at:

`/minigames/guess-what-i-play`

It is a private two-player deduction game built around game-cover cards.

## Card resources

Game-card images are loaded from:

`Resources/Minigames/GameCards`

Supported image formats are `.png`, `.jpg`, `.jpeg`, `.webp`, and `.gif`.

The default table size comes from `Minigames:CardCount`. A new game accepts from 10 cards up to the number of available supported image files.

The optional Question cards pool is stored in:

`Resources/Minigames/GameCards/questions.txt`

One non-empty line is one YES/NO question. The bundled pool contains 938 unique questions.

## Rooms

- Player 1 creates a room and receives a six-character room code.
- Player 2 joins using that code or a shared room link.
- A room accepts at most two players.
- Multiple rooms can run independently at the same time.
- Room state is held in memory and scoped through SignalR groups.
- Rooms are removed after one hour without meaningful user activity.
- Clicking cards or controls and refreshing/resuming the page extends room lifetime.
- Passive SignalR synchronization and automatic turn timeout do not keep an abandoned room alive.
- Player 1's built-in or custom theme is synchronized to Player 2.

Browser membership is stored locally per room so refresh and SignalR reconnect can resume the same player when the room still exists.

## Room links and social preview

A copy-link control next to the room code copies an absolute URL containing `?room=CODE`.

The game page emits room-aware Open Graph and Twitter metadata. The request URL, including the room query, is used as the social URL so clients such as Telegram can display a rich preview card. Opening the shared link pre-fills the room code; a browser that already owns membership for that room resumes it automatically.

See [Social link previews](social-link-previews.md) for the shared metadata implementation.

## Starting a new game

**New game** opens a dialog where the player chooses the number of source cards and may enable **Question cards**.

For a newly generated table, each player excludes:

`floor(table card count / 10)`

cards before play begins. The same card cannot be excluded by both players. After exclusions, both players receive distinct private secret cards from the remaining table.

Examples:

- 10 source cards -> 1 exclusion per player -> 8 active cards.
- 20 source cards -> 2 exclusions per player -> 16 active cards.

## Card layout and local deduction

Active cards are displayed as 1:1 squares. Source artwork is contained instead of cropped.

The client calculates the largest square size that fits the available stage, distributes free horizontal and vertical space evenly, and centers an incomplete final row. Layout is recalculated for viewport changes and when the Question cards sidebar is visible.

Clicking a normal active card toggles a local dimmed/grayscale state. This deduction state belongs only to that browser and is not synchronized to the opponent.

## Turns

Player 1 starts the game.

- Each player's first turn lasts 3 minutes.
- Later turns last 90 seconds.
- The current player and countdown are displayed above the table.
- **Answer** and **End turn** are available at any moment of the active player's turn.
- A manual end immediately passes the turn.
- Timer expiry automatically passes the turn.

Turn deadlines and transitions are server-authoritative.

## Answer flow

During their turn, a player may press **Answer** and select one active card.

- Correct guess: history records the named game as correct and the player wins.
- Incorrect guess: history records the named game as incorrect and the turn immediately passes to the opponent.

## Question cards mode

When disabled, the game uses the normal free-form conversation flow.

When enabled, a vertical panel stays to the right of the table and contains shared history plus the active player's available questions.

Each player has an independent shuffled hidden question deck and sees only three current choices. Both players may therefore receive the same question independently. At most one question can be selected per turn. After selection, that slot is replenished from the player's hidden deck while questions remain.

The selected question is written to history and the opponent receives a modal requiring **YES** or **NO**. The answer is then appended to history. Pending question/recipient state survives refresh and reconnect.

After a player's question deck is exhausted, gameplay continues normally using the timer, **Answer**, and **End turn** controls.

History distinguishes Player 1 and Player 2 with different theme-aware colors. It records selected questions, YES/NO responses, game guesses, manual turn endings, and timed-out turn endings.

## In-place restart

The refresh-icon button beside **New game** restarts the current game without changing the already-active table.

It:

- keeps the exact active card set;
- skips the exclusion phase;
- clears shared history and Question-card state;
- clears any pending YES/NO response;
- reactivates all locally dimmed cards in both browsers;
- resets winner, turn counters, and timers;
- restarts with Player 1's first 3-minute turn;
- gives a new random secret card only to the player who pressed refresh;
- keeps the opponent's secret card unchanged.

The replacement secret differs from the requesting player's previous secret and from the opponent's current secret.

## Release

Introduced in Web `1.23.0` (`web-v1.23.0`) through issue #445 and PR #446.
