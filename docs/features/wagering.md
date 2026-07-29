# Wagering and Active Player Rules

## Purpose

This document defines the gameplay rules for question wagers and the player who currently has the right to choose the next question. It records both the behavior implemented in the current runtime and the intended behavior that depends on future game history persistence.

## Active player

During regular play, exactly one participant is the active player. The active player is the participant who tells the host which board question to open.

The first player who joins the lobby becomes the initial active player. Joining players do not replace an existing active player.

After a regular question is judged:

- if the active player answers correctly, that player remains active;
- if another player answers correctly, that player becomes active;
- if nobody answers correctly, the existing active player remains active;
- incorrect answers alone never transfer the right to choose.

The host may override the active player at any time by selecting a participant manually. The host may also ask the engine to select a random current participant.

A question records the player who selected it. Changing the active player after the question was selected does not change the selecting player for that question.

## Wager questions

A wager is required only for a question marked as a wager question, either explicitly in the editor or selected by the round's random-wager configuration.

The player who selected the wager question is the only player who answers it. The host must not choose an answering player separately.

Before revealing the question, the player states a wager verbally and the host enters it with an on-screen numeric keypad. The keypad is a presentation aid; the game engine remains responsible for validating the submitted amount.

The minimum question wager is 5 points.

The maximum question wager is:

```text
max(selecting player's current score, highest question value on the current board)
```

Examples for a board whose highest question value is 1,000:

| Player score | Maximum wager |
| ---: | ---: |
| 400 | 1,000 |
| 1,000 | 1,000 |
| 2,700 | 2,700 |
| -300 | 1,000 |

A valid wager moves the runtime question from `AwaitingWager` to `Active`. The wager stores the selecting player, amount, and submission timestamp.

## Scoring

A correct regular answer adds the question's point value. An incorrect regular answer subtracts the question's point value.

For a wager question, a correct answer adds the wager and an incorrect answer subtracts the wager. Scores may be negative.

Score changes must be performed through game-engine commands. A future judging workflow will apply these rules and prevent the same answer from being scored twice.

The host also requires an explicit administrative score-correction tool. Corrections must not mutate UI state directly and should eventually be recorded as auditable score transactions.

## Future history-based selection

The intended host controls also include selecting the active player with the highest aggregate score since a chosen date. The default start date is January 1 of the current year.

This requires functionality that is not yet implemented:

- persistent completed-game results;
- a stable way to identify the same participant across games;
- aggregation of final net scores, including negative results;
- a date-range query and host control.

Until those capabilities exist, the available active-player strategies are first joined, manual selection, and random selection.
