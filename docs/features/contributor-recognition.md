# Contributor recognition and avatar frames

BadWolfQuiz can recognize contributors by display name and expose optional avatar-frame customization for recognized hosts and players. Authenticated host accounts listed in `PremiumHosts:HostIds` can also use avatar frames both in their host Settings and while participating as players.

## Configuration

Contributor names come from the existing `Footer:Contributors` array in `appsettings.json` or the equivalent configuration source. Matching trims the candidate name and compares it case-insensitively.

The recognition cookie is not an authorization mechanism. Contributor eligibility is always recalculated from the configured contributor list and the current host or player name.

Premium frame eligibility comes from the existing `PremiumHosts:HostIds` array. For host Settings, the current authenticated host identifier is compared directly with that list. While participating as a player, premium access is likewise resolved from the authenticated host account in the player's browser (`CurrentHost.Id`) because `GamePlayerId` is a game-scoped identifier. A player access token by itself never grants premium frame access.

`DebugMode` is a top-level `appsettings.json` switch and defaults to `false`. When enabled, the running host game page exposes temporary host-card/frame tuning controls in the header. These controls are browser-only helpers and do not change persisted host settings or frame files.

## Thank-you dialog

On the first Razor page opened by an authenticated recognized host in a browser that has not seen the contributor acknowledgement before, the application shows a localized thank-you dialog. This also covers hosts whose authentication session already existed before the feature was deployed; they do not need to sign out and sign in again.

A long-lived, HTTP-only cookie scoped to the authenticated host identifier records that the acknowledgement was shown, so acknowledging one contributor account does not suppress the dialog for another contributor using the same browser. Removing or changing the cookie cannot grant contributor-only controls because the cookie is never consulted for eligibility.

## Host avatar frames

Recognized contributor hosts and authenticated hosts whose ID is listed in `PremiumHosts:HostIds` get an Avatar frame section on the global Settings page. They can enable or disable the frame and choose from the image frames stored in `Resources/Frames`. The frame picker uses the same thumbnail-grid interaction pattern as the avatar picker.

The same frame controls are also available in the current game's settings: in the lobby settings panel before the game starts and in **Tools → Game settings** while the game is running. Those controls bind to the current session's `SettingsInput`, so a host can override the frame for that game without first changing the global Settings page. The host game page renders the frame from the current game's settings, while other pages continue to use the saved per-host defaults.

Host frame preference from the global Settings page is persisted with the existing per-host game settings. The server strips frame fields from global Settings posts only when the host is neither a recognized contributor nor a currently configured premium host. Changing the display name cannot grant contributor access during the same Settings POST, while premium access remains tied to the authenticated host identifier rather than the display name.

Running-game **Tools → Game settings** submissions are handled asynchronously. After a successful server save, the current host avatar is updated directly inside the existing live host card when Avatar is the selected visual source, so changing the avatar does not require `F5`. The host-card DOM node itself is preserved, which keeps its drag/resize state and allows the active frame overlay to remain attached to the same card. The gameplay region is then refreshed from the saved session state.

When a host frame setting changes, active game pages receive the updated frame state without requiring a game restart. The selected transparent PNG is rendered over the host avatar/image/webcam visual rather than drawing a CSS border. Host cards inserted while a newly created game is initializing are observed and receive the frame immediately, without requiring a page refresh. Premium host frames use the same live rendering path as contributor host frames.

When `DebugMode` is enabled, the running-game header adds seven helper buttons after the normal header actions. The reset/refresh button removes the saved host-card size and restores the CSS default 100% card dimensions without changing its saved vertical position. The previous/next frame buttons cycle through the dynamically discovered frame catalog, wrapping at either end, so frames can be previewed without returning to Settings. The `+5`, `-5`, `+1`, and `-1` buttons temporarily adjust the selected debug frame's native-size avatar inset in 5px or 1px steps. A live `Xpx` value to the right shows the current native inset being tuned. Frame selection and inset adjustments are temporary for the current page; inset overrides are kept separately per frame, remain clamped at zero, and still scale proportionally when the host card is resized. Reloading the page clears the debug selection and adjustments.

## Player avatar frames

Recognized contributor players and authenticated premium host accounts get equivalent controls inside the existing player media settings disclosure. Premium access is available when the current authenticated host account identifier is present in `PremiumHosts:HostIds`; the player's game-scoped `GamePlayerId` is not compared with that list.

The preference is stored in browser local storage by normalized player name and is synchronized to the current game using the player's existing access token. The server revalidates the player access token, player identifier, and frame eligibility before accepting a frame update. Contributor players are revalidated against the configured contributor name list. Premium players are revalidated against the authenticated host account and `PremiumHosts:HostIds`. The recognition cookie is not involved in either path.

For premium players, the runtime player state records the premium host account identifier that authorized the frame. Host game pages include the frame only while that identifier still qualifies as premium, preserving the same live configuration behavior as contributor-name eligibility.

The selected frame image is rendered as a square overlay over the player's current avatar, uploaded image, webcam preview, or webcam URL preview where that visual is shown. The overlay follows live card resizing. Built-in avatars use the pixel inset configured in the selected frame file name as a native-image reference value. The browser scales that inset proportionally with the rendered frame size, so the avatar-to-frame spacing remains consistent when cards are resized. Host game pages refresh contributor-frame state when the normal player roster changes.

## Frame assets

Frame PNG files are stored in `src/BadWolfQuiz.Web/Resources/Frames` and copied to both build and publish output. The catalog is discovered from the folder at runtime instead of using a fixed frame count, so additional `.png` files become selectable automatically.

Use the file-name format `<frame-id>-<avatar-inset-px>.png`. The inset is measured at the PNG frame's intrinsic size, not as a fixed CSS value. For example, if `1-10.png` is a 512×512 frame, it uses a 10px inset when rendered at 512px, 5px at 256px, and 15px at 768px. `2-15.png` follows the same proportional rule from its own intrinsic size. The suffix is configuration rather than part of the stored frame ID, so changing `1-10.png` to `1-12.png` updates the inset without breaking saved selections of frame `1`. Legacy files such as `1.png` remain supported and use a default `10px` inset. If both a legacy file and an explicit-inset file exist for the same frame ID, the explicit-inset file is preferred.

Numeric frame IDs are ordered numerically first, followed by other IDs. Frames are served through the stable `/frames/{id}.png` Razor endpoint so the source assets remain outside `wwwroot` even when the physical file name contains the inset suffix.

## Localization

Contributor-specific UI strings live in `ContributorResource` resources for EN, UK, IT, and RU. As required by the project localization convention, every feature value in `ContributorResource.ru.resx` is exactly `Україна`.

## Release

Contributor recognition and avatar-frame customization are targeted for **BadWolfQuiz Web 1.15.0** with tag `web-v1.15.0`. The implementation is tracked by issue #110 and PR #229. Because 1.15.0 has not been released yet, fixes found while validating this feature remain part of 1.15.0 rather than creating a 1.15.1 patch release.
