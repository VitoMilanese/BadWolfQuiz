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

The host may override the active player by selecting a participant manually or ask the engine to select a random current participant. Both operations are forbidden while a wager question is in progress.

A question records the player who selected it. Once a wager question is selected, the active player is locked until that question is resolved. This guarantees that the answering player and score-dependent wager state cannot be changed after wager entry begins.

## Wager questions

A wager is required for a question marked as a wager question explicitly in Question Editor or selected by one of the round's random-wager configurations.

Explicit wager settings and random wager settings are additive. Enabling random wagers does not turn an explicitly marked wager question back into a regular question. **Exclude from random wager selection** controls only random eligibility and does not disable an explicit wager.

The round can independently request normal random wagers and anonymous shared random wagers. The two random selections use non-overlapping positions within the eligible question pool, so one question cannot be selected simultaneously as both random wager modes.

For regular single-answer-player wager questions, Question Editor provides two wager modes:

- **Normal wager**
- **Anonymous shared wager**

The player who selected the wager question is the only player who answers it. The host does not choose a separate answering player.

### Normal wager

Before revealing a normal wager question, the player states a wager verbally and the host enters it with an on-screen numeric keypad. The wager-entry summary shows the selected player's current score directly below the player name, alongside the existing allowed-wager range, so the host can see the balance used to make the wager decision. While wager entry is active, the question board is hidden and the keypad is centered so the host view focuses on the wager. A `MAX` key enters the allowed maximum immediately. If digit entry would exceed the maximum, the UI replaces the value with the maximum. The keypad is a presentation aid; the game engine remains responsible for validating the submitted amount.

The minimum question wager depends on the question value:

- questions worth less than 10 points use a 1-point minimum;
- questions worth 10 points or more keep the normal 5-point minimum.

When the applicable minimum is 1, a player whose current score is 0 or more can still submit a 1-point wager. The host's **Set minimum wager** action uses the same calculated minimum as normal wager validation.

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

A valid normal wager moves the runtime question from `AwaitingWager` to `Active`. The wager stores the selecting player, amount, and submission timestamp.

### Anonymous shared wager

For an anonymous shared wager, the selected answering player does not choose or enter an amount. Every other player captured when wagering begins privately chooses `0%`, `25%`, `50%`, `75%`, or `100%` of an equal conceptual share of the question value.

The host sees only submission progress while collection is active. Individual percentages, individual amounts, and partial totals remain private. Missing/AFK submissions can be forced to 100%.

After collection completes, the combined contribution becomes the answering player's wager. The normal player buzzer stays unavailable for the entire anonymous shared wager question because the answering player is predetermined and funding players do not receive attempts.

Settlement is zero-sum: a correct answering player gains the combined wager while funders lose their own contributions; an incorrect answering player loses the combined wager while funders gain their own contributions.

See [Anonymous shared wager](anonymous-shared-wager.md) for calculation, privacy, lifecycle, recovery, and settlement details.

### All-player wager entry

All-player wager entry on a player's device follows the same upper-bound behavior as normal wager entry: if appending a digit would make the wager exceed the allowed maximum, the displayed value is immediately replaced with that maximum instead of retaining an oversized value.

## Scoring

A correct regular answer adds the question's point value. An incorrect regular answer subtracts the question's point value.

For a normal wager question, a correct answer adds the wager and an incorrect answer subtracts the wager. Scores may be negative.

Anonymous shared wager scoring uses the balanced multi-player settlement described above and in the dedicated feature document. Funding-player score changes are never applied independently of the matching answering-player delta.

Score changes are performed through game-engine judging commands. Every judgment records an immutable answer attempt, and repeated judging of the same player or a resolved question is rejected so a score delta cannot be applied twice.

The host also requires an explicit administrative score-correction tool. Corrections must not mutate UI state directly and should eventually be recorded as auditable score transactions.

## Future history-based selection

The intended host controls also include selecting the active player with the highest aggregate score since a chosen date. The default start date is January 1 of the current year.

Host-scoped lifetime player statistics and aggregate final scores are already available through persistent game history. The remaining work for this strategy is the active-player selection workflow itself:

- a date-range query beginning from the chosen start date;
- an Engine/application command that selects the highest-scoring eligible player from that range;
- a host control that submits the selection request and handles ties or unavailable history consistently.

### Persistent player identity

Players do not create an account before joining a game. A persistent player identity is scoped to the host and identified by the host account together with a normalized nickname. Therefore, players with the same displayed name in games owned by different hosts are distinct players.

Joining remains a one-action flow after the nickname is entered. A game participation record references the persistent player profile and stores the game, date, and score result. This data supports history and aggregate-score queries.

Normalization removes insignificant differences such as surrounding whitespace and letter casing while preserving the entered name for display. For example, `Player-X`, `player-x`, and ` Player-X ` identify the same player for one host.

Nicknames are intentionally not reserved through player accounts. Games are coordinated through Discord, and the host confirms the person behind a nickname through the existing lobby approval flow. This keeps joining fast while leaving identity control with the host.

Until aggregate history queries exist, the available active-player strategies are first joined, manual selection, and random selection.
