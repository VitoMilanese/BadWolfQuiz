# Quiz Editor Workspace Redesign

## Scope

BadWolfQuiz Web `1.26.44` refreshes the complete quiz-authoring workspace: the main board editor, regular Question Editor, Final Question Editor, round/category Description Editor, and their preview surfaces.

The redesign is presentation-focused. Existing quiz persistence, drag/drop, asynchronous save/delete behavior, media handling, file-picker Escape protection, final-question rules, and Play/navigation semantics remain unchanged unless explicitly noted below.

## Workspace presentation

The editor surfaces share a dedicated workspace presentation layer with consistent panels, controls, spacing, responsive behavior, and sticky action bars. The action bars sit close to the footer with a partially transparent background so controls remain available without creating a large empty region below the editor.

Board question cells keep their compact information density. Question media thumbnails render without an additional image frame. The hover delete action uses the normal icon treatment on a red destructive button and remains separate from move/copy controls.

Regular and final question editors rely on their active tabs for section context, so redundant Question / Correct answer / Description headings are hidden where the tab already communicates the same state.

## Context menus and Escape behavior

Round editing and Add block menus are toggles: pressing the same trigger again closes the open menu. When one of these menus is open, Escape closes only that menu instead of navigating away from the editor. Existing protection for Escape while a native file picker is open remains intact.

## Preview presentation

Round/category description previews and regular/final question/answer previews use gameplay-style full presentation surfaces rather than legacy framed editor cards. Preview media preserves its aspect ratio, scales within the available viewport, and applies the visible frame to the media itself instead of to a full-width container.

Round/category description previews center the title and content as one presentation group, matching the corresponding gameplay intro more closely. Regular and final question previews use the same presentation background language while preserving dedicated Four Clues and other specialized layouts.

## Scrolling and interaction

Vertical mouse-wheel scrolling continues through the board even when the pointer is over the horizontally scrollable question grid. Editor text is non-selectable by default to avoid accidental highlighting during drag/click interaction, while text inputs, textareas, and editable controls remain selectable.

Reset keeps a stable reserved place in editor action bars so reloading persisted state does not make neighboring buttons jump while the reset control is reconstructed.

## Description editor loading

Opening round/category description editors no longer hydrates stored media BLOBs as part of the initial GET. The page loads entity and content-block metadata through projections, while stored image/audio bytes continue to be served by the existing dedicated file handlers. This keeps description-editor startup responsive even when descriptions contain large stored media.

## Regression coverage

Focused regression tests cover workspace asset registration, browser scroll/layout fixes, preview presentation, context-menu Escape/toggle behavior, destructive controls, stable reset layout, and description-editor metadata-only loading. The category-heading regression test is formatting- and line-ending-tolerant so equivalent code formatting does not create false failures.

## Release

The redesigned Quiz Editor workspace ships in BadWolfQuiz Web `1.26.44` (`web-v1.26.44`) through issue #577.
