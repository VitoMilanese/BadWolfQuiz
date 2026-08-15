# Player Buzzer UX

## Purpose

The player buzzer page is the primary interaction surface for players during regular buzzer questions. On phones and tablets it must favor a large, reliable touch target without allowing browser gestures or page movement to interfere with the buzz action.

## Mobile layout

On narrow viewports, the player page is constrained to the visible dynamic viewport and the buzzer panel uses the remaining available height after the player identity, menu controls, and presence notice.

The buzzer is rendered as a large rounded rectangle rather than a circular control. It stretches to the available panel width and height while avoiding horizontal or vertical page scrollbars in the normal collapsed-menu state.

The layout uses valid `:has()` selectors without nesting `:has()` inside another `:has()`, so the viewport rules remain valid in Safari and other supporting browsers.

## Touch behavior

The page blocks horizontal panning and horizontal overscroll. The buzzer itself uses `touch-action: none`, which prevents a finger movement that starts on the buzzer from being interpreted as a browser pan or scroll gesture.

A primary pointer press triggers the existing buzzer action on `pointerdown` instead of waiting for the later click release. This keeps the interaction responsive while preserving keyboard-triggered clicks.

Outside the buzzer, mobile touch behavior uses `touch-action: manipulation` / `pan-y` as appropriate so double-tap zoom is suppressed for the gameplay surface while intentional vertical scrolling remains available where required.

## Expanded player settings

The page shell stays constrained to the viewport, but the player lobby becomes its own vertical scroll container. This allows the expanded **Menu** and **Image, avatar and webcam** sections to scroll normally without restoring body-level page movement.

The buzzer keeps `touch-action: none`, so scrolling the expanded settings does not weaken the touch protection on the buzzer itself.

## Text selection

Text selection is disabled across the player buzzer page, including headings, player information, status text, menu labels, and the buzzer label. This prevents accidental text selection during rapid touch interaction.

## State and realtime behavior

These UX changes do not alter game rules, buzzer eligibility, winner selection, SignalR state synchronization, reconnect behavior, or server-side buzzer validation. Existing open, disabled, claimed, and waiting states continue to be driven by the authoritative game state.

## Regression coverage

`PlayerBuzzerTouchRegressionTests` covers the mobile interaction contract, including:

- horizontal pan and overscroll prevention;
- non-nested `:has()` selectors for Safari-compatible viewport rules;
- use of the available mobile panel space;
- vertical scrolling for expanded settings;
- primary `pointerdown` buzzer activation;
- non-selectable player-page text;
- the rounded rectangular buzzer shape.
