# Minigames

BadWolfQuiz includes a standalone **Minigames** section for lightweight games that can be played independently from the Jeopardy-style quiz runtime.

The header **Minigames** action opens the catalog at `/minigames`. Each minigame has its own page so additional games can be added without changing the catalog route.

## Guess what I'm playing

The first minigame is **Guess what I'm playing** (`Вгадай, у що я граю`) at:

`/minigames/guess-what-i-play`

It is a private two-player deduction game built around game-cover cards.

## Database-backed catalog

The runtime catalog is stored in the main BadWolfQuiz SQLite database. The catalog contains:

- games, including the display name and card-image bytes/content type;
- the ordered shared YES/NO question pool;
- an optional YES/NO answer for every game/question pair.

Runtime room snapshots keep the same lightweight card contract used by the original implementation. A card's opaque key is now the database game identifier while its display name remains the game title. Exclusions, secret-card selection, guesses, reconnects, and in-memory room state therefore do not depend on physical resource paths.

Card-image endpoints read the image bytes from the database and stream them with the stored content type. The game no longer resolves live card images from `Resources/Minigames/GameCards`.

The default table size still comes from `Minigames:CardCount`. A new game accepts from 10 cards up to the number of games currently stored in the database catalog.

## Legacy catalog bootstrap

`Resources/Minigames/GameCards` remains a one-time bootstrap source for existing installations.

On the first database-catalog access, when both the game and question tables are empty:

1. `questions.txt` is imported in line order after trimming empty/duplicate questions;
2. each supported top-level `.png`, `.jpg`, `.jpeg`, `.webp`, or `.gif` file becomes one game whose name is the image file name without its extension;
3. if a matching `<GameName>.txt` exists and contains exactly one `0` or `1` line per question, its answers are imported for that game (`1` = YES, `0` = NO).

Once the database contains catalog data, later runtime/editor reads use the database and do not re-import changed legacy files automatically.

## MasterHost minigame editor

The configured MasterHost receives a **Minigame editor** action in the header menu. The editor is available at `/Admin/MinigameEditor` and is protected by the existing `MasterHost` authorization policy.

The editor is split into three views:

- **Games** — list card previews and names, create games, rename games, replace card images, delete games, and open a game's answer editor;
- **Questions** — view the deterministic question order, append questions, edit question text, and delete questions;
- **Answers** — select one game and assign YES, NO, or unassigned for every question.

Deleting a game also removes its stored question answers. Deleting a question removes that question's answers from every game and closes the question-order gap so subsequent TXT imports continue to map line-for-line to the visible question order.

### Bulk answer import

The Answers view accepts a per-game TXT file. The import is valid only when:

- the file contains exactly one line for every current question;
- every line contains exactly `1` or `0` after trimming;
- line N maps to question N in the current editor order.

A valid import transactionally replaces the selected game's complete answer set. An invalid file changes nothing.

### Busy indicator

Editor navigation and potentially slow writes use the shared `BadWolfBusy` overlay. This includes section/game navigation, image uploads/replacements, large answer saves, TXT imports, and destructive editor submissions.

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

The history view also supports three client-side filters:

- `1 → 2` combines completed Player 1 questions with Player 2 answers;
- `2 → 1` combines completed Player 2 questions with Player 1 answers;
- full history preserves all chronological question, answer, guess, and turn events.

Directional rows keep the asking player's and answering player's colors independently and do not make extra SignalR calls.

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

Introduced in Web `1.23.0` (`web-v1.23.0`) through issue #445 and PR #446. Directional history filters were added in Web `1.24.0`.
