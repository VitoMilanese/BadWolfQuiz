# Animated Backgrounds

## Purpose

BadWolfQuiz can render a lightweight animated background across visual site pages without interfering with gameplay controls or page layout.

Dark themes use a sparse randomized field of pulsing stars. Light themes use colored balls and outlined rings instead, so the effect remains visible without looking like white specks on a bright background.

## Host preference

`AnimatedStarsEnabled` is stored with the signed-in host's persistent site/game settings and defaults to `true`, including settings files created before the option existed.

The option is available in `/Admin/Settings`. Changing the checkbox updates the current page immediately; reloading the page is not required.

The preference is presentation-only. It does not change quiz rules, scoring, timers, or the immutable gameplay-settings snapshot.

## Player synchronization

Players inherit the room owner's current animated-background preference:

- quiz join and player pages follow the quiz host;
- **Guess what I'm playing** follows the room creator;
- **Word Rings** follows the room creator.

Registered hosts remain the authoritative source for the preference, so connected players periodically re-read it and can receive a later Settings change without recreating the room. Guest-created minigame rooms keep the default enabled effect.

## Rendering behavior

Particles are generated once per page load. Their positions, sizes, shapes, timing, animation phases, and colors remain stable while ordinary UI controls are opened and closed.

The same particle container is re-hosted between visual roots when host gameplay performs soft navigation. This is required for live question, answer, round-summary, transition, and Final Question surfaces because those views can replace their DOM without a full page reload.

The particle layer is always non-interactive and remains below meaningful page content. Opaque full-page surfaces explicitly host the layer above their own background so the effect remains visible on gameplay, Answer Key, and editor pages.

`prefers-reduced-motion` disables pulsing/floating animation while leaving the particles visible.

## Implementation

The global assets are injected by `BackgroundStarsTagHelper`:

- `wwwroot/css/background-stars.css`;
- `wwwroot/js/background-stars.js`;
- `wwwroot/js/background-stars-gameplay-sync.js`.

The host/player synchronization endpoint is `/api/background-stars`.

No database migration is required because the preference is persisted in the existing host settings JSON model.

## Release

This feature ships in BadWolfQuiz Web `1.43.0` via issue #674 and pull request #675.
