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

A game must use its own snapshot throughout its lifetime. This makes its behavior deterministic and prevents later global changes from altering an already prepared or running session.

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

## Ownership and enforcement

The game-specific settings snapshot is part of the runtime session configuration. The Engine currently accepts an immutable `GameSessionSettings` snapshot containing both timer durations and both phase start modes.

The Game Engine interprets and enforces gameplay settings. The host and player UIs render the resulting state and submit permitted commands; they are not authoritative sources for settings or timing rules.

Settings that affect an active phase should not silently change that phase after it has started. Any future support for editing settings during a game must define when a new value takes effect.

## User interface expectations

The administration experience provides:

- a global settings menu for persistent host defaults;
- a game settings panel in the lobby before the game starts;
- inherited values preselected from the global settings;
- the ability to personalize settings for the new game;
- a clear way to distinguish inherited values from game-specific overrides.

A future settings implementation may add a reset action that restores a game setting to the current inherited default before the game starts.


## Implementation status

The Engine-level settings snapshot is implemented. It configures buzzer and answer durations, automatically opens the regular-question buzzer when requested, supports explicit start of a wager answer timer in manual mode, and controls whether negative-score players participate in the final question.

Global defaults are persisted per host in `App_Data/game-settings.json`, keyed by the authenticated host identifier. One host cannot read or overwrite another host's defaults. Files written by older versions are treated as a legacy initial default until each host saves their own settings. Creating a game immediately copies the current host's defaults into its runtime session. The host can edit the per-game values from the lobby until the game starts; updating them rebuilds the stopped timers. Once the game starts, the snapshot is locked.
