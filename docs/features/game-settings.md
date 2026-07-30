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

| Regular question | Wager question | Result |
| --- | --- | --- |
| `Automatic` | `Automatic` | The buzzer and wager answer timer start automatically in their respective flows. |
| `Automatic` | `Manual` | The regular-question buzzer starts on reveal; the host starts the wager answer timer. |
| `Manual` | `Automatic` | The host activates the regular-question buzzer; the wager answer timer starts on reveal. |
| `Manual` | `Manual` | The host explicitly starts both phases. |

For a regular question, `Automatic` activates the buzzer and starts the shared buzzer window timer when the question is revealed. `Manual` presents an **Activate buzzer** host control; activating it starts the same timer.

For a wager question, `Automatic` starts the answering player's timer when the question is revealed. `Manual` presents a **Start timer** host control.

## Ownership and enforcement

The game-specific settings snapshot is part of the runtime session configuration.

The Game Engine interprets and enforces gameplay settings. The host and player UIs render the resulting state and submit permitted commands; they are not authoritative sources for settings or timing rules.

Settings that affect an active phase should not silently change that phase after it has started. Any future support for editing settings during a game must define when a new value takes effect.

## User interface expectations

The administration experience provides:

- a global settings menu for persistent host defaults;
- a game settings menu during lobby creation;
- inherited values preselected from the global settings;
- the ability to personalize settings for the new game;
- a clear way to distinguish inherited values from game-specific overrides.

A future settings implementation may add a reset action that restores a game setting to the current inherited default before the game starts.
