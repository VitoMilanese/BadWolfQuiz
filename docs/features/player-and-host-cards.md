# Player and Host Cards

## Purpose

The fixed card row at the bottom of the host screen keeps participants visible throughout gameplay without taking space away from the question presentation.

Player cards communicate score and temporary question state. A separate optional host card provides a visual presence for the host without treating the host as a participant.

The host may resize the fixed player-card row vertically. The selected height is
stored in the browser and restored before the gameplay layout is first painted
after a page reload, preventing a visible jump from the default height to the
saved height.

## Current question states

While a question is active:

- the answering player's card uses a solid yellow background;
- players who have already answered incorrectly use a muted gray background;
- other player cards retain their normal appearance.

These temporary states are cleared when the question ends.

## Compact controls

Administrative actions use a compact vertical icon strip anchored to the right edge of each player card. Removing the player is always the top action.

Every icon control must remain accessible through a localized label or tooltip and keyboard focus.

## Player avatars

A player card may display an avatar below the player's name.

A player may:

- select an avatar from the available built-in collection;
- upload an image file;
- leave the avatar unset.

Player registration is not required. The selected avatar or uploaded image is stored in the player's browser by normalized player name and restored when that name joins a later game from the same device.

## Live video sources

A player may optionally replace the static avatar with a live visual source:

- a webcam feed;
- an HTTPS webcam page URL, including a VDO.Ninja viewer link;

The browser publishes a local webcam stream to the host while the player is connected and active. Alternatively, the player can send a validated HTTPS viewer URL that the host embeds directly in the player card. A failed, disabled, or unavailable local stream falls back to the player's uploaded image, selected avatar, or default card appearance.

## Host card

The host does not participate in gameplay and has no score.

The optional host card displays the host name and visual source:

- a static image;
- a built-in avatar;
- a webcam feed;
- an HTTPS webcam page URL, including a VDO.Ninja viewer link;

A setting controls whether the host card is visible. When disabled, it must not reserve empty space in the card row.

Global settings provide defaults that are copied into a new game. The lobby may override them for that game. A visible host card requires a host name or a selected image, avatar, webcam, or webcam URL source. Webcam URLs are embedded only when they use HTTPS.

On wide boards the host card may move into a dedicated side position so it does
not reduce the playable board area. A host-adjusted card width and height are
stored in that browser and restored after page reloads, viewport changes, and
movement between the card row and side position. When horizontal space becomes
tight, the host card returns to the bottom row first; the compact player score
list on the right remains visible until the viewport becomes narrower still.

The board uses all available width for its configured category count. Category
headers are synchronized to the tallest rendered heading, while question rows
share the remaining height. This keeps differently sized boards aligned without
introducing horizontal board scrolling.

## Future design decisions

Future improvements may include:

- moderation and removal of uploaded images;
- media device selection and browser permissions;
- stream quality and bandwidth limits;
- additional responsive-layout refinements for very large player lists.
