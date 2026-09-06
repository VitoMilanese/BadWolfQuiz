# Free question selection

**Guess what I'm playing** supports two Question-card selection modes for both normal two-player games and solo games against AI.

## New game option

Enable **Question cards**, then optionally enable **Free question selection**.

- Off: each player keeps the existing independent shuffled deck with three visible Question cards.
- On: the three visible cards are replaced by one **Choose question** button.
- Solo AI still forces **Question cards** on, but **Free question selection** remains optional.
- **Allow hints** remains independent.

## Human question search

The **Choose question** button is available only during the local player's active turn before a question has been selected.

It opens a modal search over that player's remaining enabled questions:

- the filter requires at least 3 characters;
- matching is a case-insensitive substring search;
- results are paged at 10 questions per page;
- clicking a result immediately selects and asks that question;
- the selected question is removed only from the asking player's pool;
- the opponent has an independent pool and may still ask the same question on a later turn;
- restart restores the full question pool while preserving the selected question mode.

The server validates room membership, turn ownership, selection mode, and that the selected question is still available. The browser never submits an arbitrary question outside the server-side pool.

## Hint search in free-selection mode

When **Allow hints** and **Free question selection** are both enabled, the game-card hint dialog is split into two tabs.

- The Question cards tab keeps the existing current-question and previously-asked hint lists.
- The Search questions tab uses the same minimum 3-character filter and 10-row paging model as the question picker.
- Hint search is read-only and does not consume a question.
- Results are limited to that player's own remaining enabled question pool, so already-used questions disappear only for the player who used them.
- The opponent keeps an independent pool and may still see the same question in their own hint search.
- YES and NO mappings are shown normally; an unassigned game/question mapping remains visible as **Information unavailable**.
- When Question cards are disabled, the existing free-form game-card hint search keeps its previous assigned-answer-only behavior.

The hint endpoint continues to validate room membership, active-table card membership, hint availability, and question-selection mode. The complete answer matrix is never sent to the client.

## AI selection

AI does not use the search dialog. In free-selection mode it evaluates all questions remaining in its own server-side pool.

For each question, known candidate answers are split into YES and NO groups. The selection score is:

`min(YES candidates, NO candidates)`

This maximizes the guaranteed elimination in the worse of the two possible answers. Unassigned answers are excluded from the score because they are not eliminated by either YES or NO. Questions tied for the best score are chosen randomly.

When only one candidate remains, AI guesses immediately. If the question pool is exhausted while multiple candidates remain, AI guesses among its remaining candidates instead of stalling.

## Compatibility

Existing `StartNewGame`, `StartNewGameWithHints`, and `StartNewSoloGame` contracts keep the three-card behavior. The free-selection mode uses explicit extended Hub methods, so existing multiplayer and clients remain backward compatible.
