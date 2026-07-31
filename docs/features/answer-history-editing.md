# Answer History Editing

## Purpose

The host must be able to correct the recorded answer history while a game is running. This tool covers mistakes in judging, player attribution, and awarded points without requiring direct score adjustments that would leave runtime statistics inconsistent.

Answer history is authoritative gameplay data. Leaderboards, final standings, and weakest-player selection must be derived from the corrected records.

## Host capabilities

The host can open the answer history and:

- change the player attributed to an answer;
- change the awarded or deducted score value;
- change whether the answer is marked correct or incorrect;
- add a missing answer record for a player and question;
- review the resulting score changes before confirming them.

A future version may also support deleting an erroneous record. Until deletion semantics are specified, an invalid record can be corrected by changing its attribution, result, or score value.

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

Changing an entry must reverse the previous contribution and apply the replacement contribution atomically. Adding an entry applies its contribution exactly once. Repeating the same command must not duplicate score changes.

Manual corrections are allowed even when the player did not originally attempt the question. They do not reopen a resolved question or change the live question flow unless a separate command explicitly requests that behavior.

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

- filters by round, question, and player;
- a chronological list of answer records;
- an edit action for every record;
- an **Add answer record** action;
- a preview of score changes before confirmation;
- clear marking of manually created or edited records;
- a confirmation step for changes that alter standings.

The tool must remain available during the game without exposing the correct answer or administrative controls to player clients.

## Auditability

Corrections should preserve an audit trail rather than silently replacing historical facts. At minimum, the system should retain the original values, replacement values, correction timestamp, and host identity when host accounts are introduced.

This audit data is administrative and must not affect scoring by itself.
