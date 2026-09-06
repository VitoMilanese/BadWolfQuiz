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

## AI selection

AI does not use the search dialog. In free-selection mode it evaluates all questions remaining in its own server-side pool.

For each question, known candidate answers are split into YES and NO groups. The selection score is:

`min(YES candidates, NO candidates)`

This maximizes the guaranteed elimination in the worse of the two possible answers. Unassigned answers are excluded from the score because they are not eliminated by either YES or NO. Questions tied for the best score are chosen randomly.

When only one candidate remains, AI guesses immediately. If the question pool is exhausted while multiple candidates remain, AI guesses among its remaining candidates instead of stalling.

## Compatibility

Existing `StartNewGame`, `StartNewGameWithHints`, and `StartNewSoloGame` contracts keep the three-card behavior. The free-selection mode uses explicit extended Hub methods, so existing multiplayer and clients remain backward compatible.