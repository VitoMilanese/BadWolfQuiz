# Player and Host Cards

## Purpose

The fixed card row at the bottom of the host screen keeps participants visible throughout gameplay without taking space away from the question presentation.

Player cards communicate score and temporary question state. A separate optional host card provides a visual presence for the host without treating the host as a participant.

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

Account registration is not required for the first version of this feature. Avatar ownership and persistence must follow the existing nickname-based player identity model until reserved accounts are introduced.

## Live video sources

A player may optionally replace the static avatar with a live visual source:

- a webcam feed;

The exact transport, permissions, fallback behavior, and performance limits will be designed before implementation. A failed or unavailable stream must fall back to the player's avatar or the default card appearance.

## Host card

The host does not participate in gameplay and has no score.

The optional host card displays the host name and visual source:

- a static image;
- a webcam feed;

A setting controls whether the host card is visible. When disabled, it must not reserve empty space in the card row.

Global settings provide defaults that are copied into a new game. The lobby may override them for that game. A game cannot start with a visible host card until the host name and either an uploaded image or webcam source are selected.

## Future design decisions

Implementation still requires decisions about:

- avatar file validation and storage;
- moderation and removal of uploaded images;
- media device selection and browser permissions;
- stream quality and bandwidth limits;
- responsive layout when many player cards and the host card are visible.
