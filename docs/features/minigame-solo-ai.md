# Guess what I'm playing: solo AI mode

The **Guess what I'm playing** minigame supports an optional **Solo game against AI** mode in the **New game** dialog.

## Start rules

- Solo mode is available only to Player 1 when the room does not contain a human Player 2.
- Enabling solo mode automatically enables and locks **Question cards** for that game.
- **Free question selection** remains optional. When disabled, the normal three-card Question deck is used. When enabled, the human searches the complete remaining enabled-question pool and the AI chooses directly from its own complete remaining pool.
- The AI occupies the Player 2 slot, so another browser cannot join the room while the solo game is active.
- Starting a later non-solo game removes the AI opponent and makes the Player 2 slot available again.
- An in-place restart keeps the AI opponent and restarts the current Question selection mode with Player 1 taking the first turn.

## Eligible game cards

Solo games use only catalog games whose assigned YES/NO answers cover at least **80%** of the complete catalog question count.

The 80% check is server-side and is applied before the random table is generated. Games below the threshold cannot be drawn into a solo table. The New game dialog also lowers the maximum card count to the number of currently eligible games while the solo checkbox is enabled.

## Three-card AI turns

In the default Question-card mode, the AI uses the same server-side Player 2 question deck as a human player:

- it receives an independent shuffled deck;
- it sees three current Question cards;
- on each AI turn it selects one of those three cards;
- the human player answers through the existing YES/NO response dialog;
- the selected slot is replenished by the existing Question-card deck logic.

## Free-question AI turns

When **Free question selection** is enabled, the browser does not receive a three-card hand. The human sees one **Choose question** button that opens a server-backed search dialog. Search requires at least 3 characters and returns 10 questions per page. Selecting a result consumes that question only from the requesting player's independent pool.

The AI has no search dialog. On its turn it evaluates every question still available in its own pool against the current candidate games. For each question, the score is the smaller of its known YES and NO candidate counts, which represents the guaranteed number of candidates eliminated by the worse of the two possible answers. The AI chooses randomly among questions tied for the highest score. Unassigned answers are not counted as eliminations because they survive either response.

After each human response, the AI filters its remaining candidate games using the stored catalog answer for that question. A missing answer does not eliminate a candidate. When only one candidate remains, the AI guesses it immediately. If no questions remain while several candidates remain, it guesses among its remaining candidates instead of stalling indefinitely.

## AI answers

When Player 1 asks a question, the AI answers against its own secret game using the catalog matrix:

- stored `YES` -> AI answers YES;
- stored `NO` -> AI answers NO;
- missing/unassigned mapping -> the UI displays **I don't know** (`Не знаю` in Ukrainian).

The AI's secret game and answer lookup stay server-side. The browser receives only the ordinary room snapshot plus the minimal solo-display status needed to label Player 2 as AI and render unknown-answer history entries.

## Existing options

**Allow hints** remains independent and may be enabled or disabled in solo mode. The normal two-player mode and the existing `StartNewGame` / `StartNewGameWithHints` Hub contracts remain available. Extended option contracts are used only when free question selection is requested.