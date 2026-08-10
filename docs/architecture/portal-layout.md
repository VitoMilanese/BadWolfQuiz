# Portal Layout and Persistent Footer

## Purpose

Pages that use the standard portal layout keep the footer visible while the main
page content scrolls independently. This prevents long editor, administration,
and content pages from pushing the footer below the viewport.

## Layout behavior

The portal body is constrained to the viewport height. The standard `.page-shell`
becomes the flexible scrollable region between the top navigation and the footer,
while the footer remains outside that scrolling region.

The layout uses `100dvh` for modern responsive viewport sizing with `100vh` as a
fallback. The page shell is allowed to shrink with `min-height: 0` and owns the
vertical overflow, so long page content scrolls without moving the footer out of
view.

Short pages continue to fill the available space without introducing unnecessary
body scrolling.

## Scope

This behavior applies only to pages that use the standard portal layout and
footer. Gameplay and embedded layouts that do not render the standard portal
footer keep their existing scrolling behavior.
