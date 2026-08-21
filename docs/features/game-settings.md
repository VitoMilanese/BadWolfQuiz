# Game Settings

## Purpose

Game settings control host-configurable gameplay behavior without moving game rules into the UI. The application provides global defaults for the host and creates an independent settings snapshot for every new game.

This model allows a host to keep preferred defaults while personalizing a particular lobby or game without affecting other sessions.

## Settings levels

### Global settings

The host has a global settings menu containing persistent defaults for newly created games.

Changing a global gameplay setting affects games created after the change. It does not retroactively modify an existing lobby or running game. The site theme is a presentation preference rather than a gameplay rule: player-facing pages use the host's current theme, and connected players receive live theme updates when the host changes it. This synchronization does not mutate the game-specific gameplay settings snapshot.

### Game settings

When a lobby is created, the application copies the current global settings into a game-specific settings snapshot.

The lobby creation flow exposes a game settings menu where the host can review and personalize those values for that game. A personalized value belongs only to that game and does not change the global default.

A game uses its own settings snapshot throughout its lifetime. This prevents
later global changes from altering that session, while still allowing the host
to edit the game-specific snapshot in the lobby or during regular play.

## Timer and buzzer start modes

Wager-question answer timing is controlled by `WagerQuestionAnswerTimerStartMode`, which supports `Automatic` and `Manual`.

Regular buzzer questions also retain the game-level `RegularQuestionBuzzerStartMode` Automatic/Manual setting, but authored question/round buzzer modes take precedence. The game-level setting is used as a compatibility fallback only when a question resolves through **Use round default** and no concrete authored mode is available.

The user-facing authored buzzer modes are documented in [Buzzer activation modes](buzzer-activation-modes.md). They apply consistently to Standard, Four Clues, and Host-selected multiple-choice questions. Wager and all-player questions remain outside the normal buzzer flow.

For the game-level fallback, `Automatic` opens the buzzer and starts the shared buzzer-window timer when the question is revealed. `Manual` keeps the buzzer inactive and presents the host **Activate buzzer** control. For a wager question, `Automatic` starts the answering player's timer when the question is revealed, while `Manual` presents a **Start timer** host control.

Final-question eligibility has an additional boolean setting:

- `AllowNegativeScoreFinalPlayers`.

It defaults to `true`. When disabled, players whose score is below zero are excluded from the final question. Zero-score players remain eligible.

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

The Engine-level settings snapshot is implemented. It configures buzzer and answer durations, provides the game-level Automatic/Manual buzzer fallback, supports explicit start of a wager answer timer in manual mode, controls whether negative-score players participate in the final question, and enforces configurable answer reward decay for regular buzzer questions. Authored buzzer modes are carried in the quiz snapshot and resolved before the game-level fallback is consulted.

Global defaults are persisted per host in `App_Data/game-settings.json`, keyed by
the authenticated host identifier. One host cannot read or overwrite another
host's defaults. Files written by older versions are treated as a legacy initial
default until each host saves their own settings. Creating a game immediately
copies the current host's defaults into its runtime session. The host can edit
the per-game values from the lobby or during regular play; updating them rebuilds
the timers. The snapshot becomes locked outside those two session states.
