# Game Settings

## Purpose

Game settings control host-configurable gameplay behavior without moving game rules into the UI. The application provides global defaults for the host and creates an independent settings snapshot for every new game.

This model allows a host to keep preferred defaults while personalizing a particular lobby or game without affecting other sessions.

## Settings levels

### Global settings

The host has a global settings menu containing persistent defaults for newly created games.

Changing a global setting affects games created after the change. It does not retroactively modify an existing lobby or running game.

### Game settings

When a lobby is created, the application copies the current global settings into a game-specific settings snapshot.

The lobby creation flow exposes a game settings menu where the host can review and personalize those values for that game. A personalized value belongs only to that game and does not change the global default.

A game uses its own settings snapshot throughout its lifetime. This prevents
later global changes from altering that session, while still allowing the host
to edit the game-specific snapshot in the lobby or during regular play.

## Timer and buzzer start modes

Regular-question buzzer activation and wager-question answer timing are controlled by separate settings:

- `RegularQuestionBuzzerStartMode`;
- `WagerQuestionAnswerTimerStartMode`.

Each setting supports:

- `Automatic`;
- `Manual`.

They are intentionally independent even though both describe how a phase starts.

Final-question eligibility has an additional boolean setting:

- `AllowNegativeScoreFinalPlayers`.

It defaults to `true`. When disabled, players whose score is below zero are excluded from the final question. Zero-score players remain eligible.

| Regular question | Wager question | Result |
| --- | --- | --- |
| `Automatic` | `Automatic` | The buzzer and wager answer timer start automatically in their respective flows. |
| `Automatic` | `Manual` | The regular-question buzzer starts on reveal; the host starts the wager answer timer. |
| `Manual` | `Automatic` | The host activates the regular-question buzzer; the wager answer timer starts on reveal. |
| `Manual` | `Manual` | The host explicitly starts both phases. |

For a regular question, `Automatic` activates the buzzer and starts the shared buzzer window timer when the question is revealed. `Manual` presents an **Activate buzzer** host control; activating it starts the same timer.

For a wager question, `Automatic` starts the answering player's timer when the question is revealed. `Manual` presents a **Start timer** host control.

## Answer reward decay

Regular-question answer rewards have an optional decay rule configured by:

- `AnswerRewardDecayEnabled`;
- `AnswerRewardDecayStartAfterSeconds` (5-45 seconds, default 10);
- `AnswerRewardDecayMinimumPercent` (10-90%, default 25).

When enabled, a correct-answer reward remains at its current full value for the
configured delay and then decreases linearly until the configured minimum is
reached at 1 displayed second remaining. Incorrect-answer penalties are not
reduced. The rule applies only to the individual answer timer of a regular
buzzer question; wager and final questions are excluded.

For four-clue questions, the current clue-dependent correct-answer value is the
base value before decay is applied. Returning to the buzzer phase restores that
base value, and a later buzzer winner starts a new decay window.

## Ownership and enforcement

The game-specific settings snapshot is part of the runtime session configuration. The Engine currently accepts an immutable `GameSessionSettings` snapshot containing timer durations, phase start modes, final-player eligibility, and answer-reward-decay configuration.

The Game Engine interprets and enforces gameplay settings. The host and player UIs render the resulting state and submit permitted commands; they are not authoritative sources for settings or timing rules.

Settings may be edited while the session is in the lobby or regular-play state.
Saving replaces the game-specific snapshot and rebuilds both timers from the new
durations. Settings are locked once the final-question workflow begins or the
session is completed.

## User interface expectations

The administration experience provides:

- a global settings menu for persistent host defaults;
- a game settings panel in the lobby and during regular play;
- inherited values preselected from the global settings;
- the ability to personalize settings for the new game;
- a clear way to distinguish inherited values from game-specific overrides.

A future settings implementation may add a reset action that restores a game
setting to the inherited global default.


## Implementation status

The Engine-level settings snapshot is implemented. It configures buzzer and answer durations, automatically opens the regular-question buzzer when requested, supports explicit start of a wager answer timer in manual mode, controls whether negative-score players participate in the final question, and enforces configurable answer reward decay for regular buzzer questions.

Global defaults are persisted per host in `App_Data/game-settings.json`, keyed by
the authenticated host identifier. One host cannot read or overwrite another
host's defaults. Files written by older versions are treated as a legacy initial
default until each host saves their own settings. Creating a game immediately
copies the current host's defaults into its runtime session. The host can edit
the per-game values from the lobby or during regular play; updating them rebuilds
the timers. The snapshot becomes locked outside those two session states.
