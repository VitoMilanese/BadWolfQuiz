# Final Question

## Purpose

The final question is a private, staged conclusion to a game. Each player confirms a wager and an answer from their own device before the host judges the submissions.

## Runtime flow

1. The host starts the final phase after the last board round is complete.
2. Every participating player submits and confirms a private wager.
3. The Engine locks wagering only after every wager is present.
4. The question is released and players submit private answers.
5. The Engine locks answering only after every answer is present.
6. The host judges each answer.
7. The wager is added for a correct answer and subtracted for an incorrect answer.
8. After every submission is judged, the game is completed and final standings are available.

## Initial eligibility and wager rule

By default, every player participates in the final phase, including players with a negative score. The host can disable negative-score participation in global settings or for a specific lobby. The minimum wager is 5 points. The default maximum is 1,000 points. A player whose current score exceeds 1,000 may wager up to that score:

`minimum = 5`

`maximum = max(1000, current score)`.

Eligibility and wager limits remain domain rules and may be refined when the product behavior is finalized.

## Privacy

Other players' wagers and answers must not be exposed by player projections. The runtime aggregate stores the authoritative values, while the Web layer will provide player-specific and host-specific projections.

## Commands

- `StartFinalQuestion()`
- `SubmitFinalWager()`
- `LockFinalWagers()`
- `SubmitFinalAnswer()`
- `LockFinalAnswers()`
- `JudgeFinalAnswer()`

Each command validates the current phase and rejects duplicate or out-of-order submissions.
