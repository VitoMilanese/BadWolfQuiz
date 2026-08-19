# Answer History Editing

## Purpose

The host must be able to correct the recorded answer history while a game is running. This tool covers mistakes in judging, player attribution, and awarded points without requiring direct score adjustments that would leave runtime statistics inconsistent.

Answer history is authoritative gameplay data. Leaderboards, final standings, and weakest-player selection must be derived from the corrected records.

## Current implementation

The host can open a separate answer-history screen from the live game. The
implemented Engine commands support regular and wager-question attempts and
allow the host to:

- reassign an existing entry to another player;
- change its correct/incorrect result;
- replace its reward or penalty value;
- add a missing entry to any question in the current or an earlier round;
- keep an unopened question available when adding a manual entry, or explicitly resolve it as part of the same operation;
- delete an erroneous entry after explicit host confirmation.

Each change reverses the previous score contribution and applies the corrected
one atomically. Player score updates are broadcast to connected clients.
Standings and weakest-player selection read the corrected runtime attempts.
When a previous round is corrected, its score difference is also applied to the
current round's starting-score snapshot so it is not misreported as current
round score gain.

New history entries default to a correct answer. The add form preselects the
question's nominal point value, uses a 100-point spinner step, and accepts any
positive manually entered value. Zero and negative values are rejected. When an
unopened question is selected, the host is asked whether that question should
also be resolved; declining keeps the question available while still recording
the history entry.

Final-question history, persistent audit storage, filters, and score previews
remain future extensions.

## Host capabilities

The host can open the answer history and:

- change the player attributed to an answer;
- change the awarded or deducted score value;
- change whether the answer is marked correct or incorrect;
- add a missing answer record for a player and question;
- delete an erroneous answer record;
- review the resulting score changes before confirming them.

## Record model

Each answer-history entry must identify:

- the runtime game;
- the round and question;
- the player;
- whether the answer was correct;
- the signed score delta;
- the original judgment time;
- whether the entry was created or edited manually;
- audit information describing the latest host correction.

The signed score delta is stored explicitly because a corrected reward may differ from the question's original point value or wager.

## Editing rules

Editing a historical entry is an Engine command, not a direct database or UI mutation. The Engine validates that the referenced game, question, and player belong together.

Changing an entry must reverse the previous contribution and apply the replacement contribution atomically. Adding an entry applies its contribution exactly once. Deleting an entry reverses its score contribution before removing it. Repeating the same command must not duplicate score changes.

Manual corrections are allowed even when the player did not originally attempt the question. Adding an entry may target an unopened question in the current or an earlier round. The host explicitly chooses whether an unopened question is resolved; otherwise it remains available while the manual attempt is retained and remains editable in history. Existing resolved questions are never reopened by a history correction.

## Derived results

After every confirmed correction, the Engine must recalculate or consistently update:

- every affected player's score;
- total and per-round correct-answer counts;
- total and per-round attempt counts;
- inter-round leaderboard order;
- final standings and winner selection;
- weakest-player selection for the next round.

The ranking logic must consume the corrected answer history rather than stale cached counters. A manual score adjustment that is not represented by answer history remains a separate administrative operation and must have explicitly defined ranking semantics.

## User interface

The host interface should provide:

- a chronological list of answer records;
- an edit action for every record;
- an **Add answer record** action with **Correct answer** enabled by default;
- a single question selector that identifies round, category, and question value;
- a positive score-value input with a 100-point spinner step;
- automatic prefill of the selected question's nominal value;
- a confirmation dialog when a new entry targets an unopened question, allowing the host to resolve it or leave it available.

The tool must remain available during the game without exposing the correct answer or administrative controls to player clients. Pressing Escape returns to the live game through the same navigation target as **Back to game**; Escape is left to an open application dialog when one is active.

## Auditability

Corrections should preserve an audit trail rather than silently replacing historical facts. At minimum, the system should retain the original values, replacement values, correction timestamp, and host identity when host accounts are introduced.

This audit data is administrative and must not affect scoring by itself.
