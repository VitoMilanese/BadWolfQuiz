# Gameplay content layout

Bad Wolf Quiz uses the available host gameplay viewport aggressively for active questions, answers, resolved-question previews, the Final Question, and the canonical Final Answer so content can remain readable without unnecessary scrolling.

## Vertical space

Active question/answer presentations and resolved-question previews reclaim the normal page-shell top and bottom padding while the game is running. This lets question content start directly below the game header and leaves more vertical room above the fixed player scoreboard.

Normal stacked content blocks use a smaller vertical gap than editor cards. This reduces avoidable scrolling for combinations such as text plus an image with a top or bottom caption.

## Player-card score and status overlays

The fixed gameplay player cards keep essential score and presence information visible without consuming additional card layout space.

When the separate right-side board scoreboard is not visible, each player card shows the current score as a bottom-anchored overlay on top of the player's avatar, uploaded image, webcam, or webcam URL media area. The score sits above the player name and does not add a normal-flow row or resize the media, name, or card controls. Contributor avatar frames remain below the score overlay so they cannot cover the score.

On wide board views where the right-side player scoreboard is actually visible, the duplicate score on the fixed bottom card remains hidden. A right-side scoreboard that is present in the DOM but hidden during question or answer presentation does not suppress the player-card score.

Inactive, disconnected, and rejoin-pending states are represented by compact top-right overlay icons instead of visible status text. The icons do not affect card layout. The localized status text remains available through the element's title and accessible label.

## Single-image viewport fitting

A host presentation is eligible for automatic image fitting when its content contains exactly one image block. This applies to normal questions and answers, resolved-question previews, the Final Question, and the canonical Final Answer shown before final results.

If the full-size image would make the content area scroll, the host UI reduces only that image enough to fit the presentation when possible. The calculated height never goes below a viewport-aware minimum of 120-180 px, so the image remains useful even when the surrounding text or captions need substantial space.

The fit is recalculated after:

- image loading;
- soft host gameplay navigation;
- viewport resizing;
- browser history restoration;
- content-area resizing, including player-scoreboard resizing.

If the minimum image size is still not enough to fit all content, normal scrolling remains available.

## Expanding and refitting an image

An automatically fitted image is interactive:

1. Click the compact image, or focus it and press Enter/Space, to restore its normal size. Scrolling is allowed in this expanded state.
2. Activate it again to recalculate and return to the compact fitted size.

The same interaction is available for eligible Final Question and Final Answer images. Only images that were eligible for automatic fitting receive this interaction.

## First-paint behavior

Eligible images remain visually hidden until the viewport-fit controller has measured the current gameplay layout and confirmed the compact size. The first reveal is delayed through a follow-up layout-settle pass so an intermediate full-size image is not painted.

A hidden image still participates in browser layout, so its provisional full-size geometry can otherwise create a temporary scrollbar. While an eligible image is still waiting for its final fit, the content container suppresses that provisional vertical scrollbar. Normal overflow behavior is restored immediately after the image is marked ready.

This applies to soft transitions such as **No correct answer -> answer** as well as transitions into the Final Question and canonical Final Answer presentations.

## Exclusions

Automatic single-image fitting intentionally does not change these layouts:

- Four Clues presentations;
- all-player answer grids;
- presentations containing more than one image.

Those layouts keep their existing media sizing and scrolling behavior.

## Implementation

The behavior is implemented by:

- `wwwroot/css/game-content-viewport-fit.css` for reclaimed viewport space, compact block spacing, first-paint image hiding, provisional-scrollbar suppression, and compact/expanded presentation styling;
- `wwwroot/js/game-content-viewport-fit.js` for eligibility detection across normal and final gameplay presentations, overflow measurement, minimum-height enforcement, click/keyboard toggling, and resize/navigation recalculation;
- `wwwroot/css/game-player-card-overlays.css` for non-flow player score and presence overlays;
- `wwwroot/js/player-admission-menu.js` for preserving localized presence labels as accessible titles/labels when the visual status is icon-only;
- `GameplayContentViewportFitRegressionTests` for regression coverage of asset loading, spacing, eligibility, sizing, first-paint readiness, layout settling, scrollbar suppression, and toggling;
- `FinalQuestionViewportFitRegressionTests` for Final Question and Final Answer integration with the shared viewport-fit controller;
- `PlayerCardOverlayRegressionTests` for score visibility, contributor-frame layering, score spacing, status-icon alignment, accessibility, and unchanged media render paths.
