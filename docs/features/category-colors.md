# Colored game board categories

## Purpose

Quiz categories can carry their own visual color preference so the host game board can distinguish category columns without changing question values or gameplay rules.

## Category color modes

Each category supports one of three modes in the category description/editor UI:

- **Automatic** assigns a stable color from the built-in category palette based on the category's board position.
- **Theme** keeps the existing theme-driven board appearance and does not apply category-specific colors.
- **Custom** lets the author select an explicit RGB/hex color with the shared color-picker UI.

Custom colors derive the category header, question-cell, resolved-cell, border, and accent colors from the selected base color. Foreground colors are selected from light or dark text according to contrast so category labels and question values remain readable.

Category color mode and custom color are stored on the reusable quiz definition. Existing quizzes default to Automatic. The values are preserved when quizzes are cloned and when `.bwquiz` packages are exported and imported.

## Gameplay setting

Hosts can enable or disable **Colored categories** globally in host settings. The selected default is copied into a game when its runtime settings are created. During a running game, the same setting is available in the game-settings dialog and affects only that game.

Changing the checkbox while the running-game settings dialog is open previews the result immediately on the board. Saving commits the selected state. Canceling with the Cancel button, close button, backdrop, or Escape restores both the board and checkbox to the persisted game setting.

The preview/reset behavior uses delegated gameplay event handling so it also works immediately after the lobby transitions into the running game without requiring a browser refresh.

## Runtime updates

The persistent host board keeps category color metadata synchronized when gameplay markup is refreshed in place. Round changes replace the board contents normally, while same-round updates preserve the existing board DOM and refresh the category color mode, custom color, and enabled state from the latest server-rendered markup.

Resolved questions use a muted derivative of their category color. When colored categories are disabled, all category-specific CSS variables are cleared and the normal active site theme controls the board.

## Compatibility

The database migration adds nullable-compatible/defaulted category color fields for existing installations, and the runtime game-setting value defaults to enabled when older stored settings do not contain the property. This keeps existing quizzes, active-game data, and stored settings loadable after upgrade.
