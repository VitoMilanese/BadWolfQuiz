# Contributor recognition and avatar frames

BadWolfQuiz can recognize contributors by display name and expose optional avatar-frame customization for recognized hosts and players.

## Configuration

Contributor names come from the existing `Footer:Contributors` array in `appsettings.json` or the equivalent configuration source. Matching trims the candidate name and compares it case-insensitively.

The recognition cookie is not an authorization mechanism. Contributor eligibility is always recalculated from the configured contributor list and the current host or player name.

## Thank-you dialog

On the first Razor page opened by an authenticated recognized host in a browser that has not seen the contributor acknowledgement before, the application shows a localized thank-you dialog. This also covers hosts whose authentication session already existed before the feature was deployed; they do not need to sign out and sign in again.

A long-lived, HTTP-only cookie scoped to the authenticated host identifier records that the acknowledgement was shown, so acknowledging one contributor account does not suppress the dialog for another contributor using the same browser. Removing or changing the cookie cannot grant contributor-only controls because the cookie is never consulted for eligibility.

## Host avatar frames

Recognized hosts get an Avatar frame section on the global Settings page. They can enable or disable the frame and choose from the image frames stored in `Resources/Frames`. The frame picker uses the same thumbnail-grid interaction pattern as the avatar picker.

Host frame preference is persisted with the existing per-host game settings. The server strips contributor-only frame fields from Settings posts made by hosts whose current saved display name is not recognized as a contributor.

When a host frame setting changes, active game pages receive the updated frame state without requiring a game restart. The selected transparent PNG is rendered over the host avatar/image/webcam visual rather than drawing a CSS border. Host cards inserted while a newly created game is initializing are observed and receive the frame immediately, without requiring a page refresh.

## Player avatar frames

Recognized players get equivalent controls inside the existing player media settings disclosure. The preference is stored in browser local storage by normalized player name and is synchronized to the current game using the player's existing access token.

The server revalidates the player access token, player identifier, and configured contributor name before accepting a frame update. The recognition cookie is not involved.

The selected frame image is rendered as a square overlay over the player's current avatar, uploaded image, webcam preview, or webcam URL preview where that visual is shown. The overlay follows live card resizing. For built-in avatars, the client measures the transparent center opening of the selected frame PNG and automatically insets the avatar so its artwork stays inside that frame's safe area. Host game pages refresh contributor-frame state when the normal player roster changes.

## Frame assets

Frame PNG files are stored in `src/BadWolfQuiz.Web/Resources/Frames` and copied to both build and publish output. The catalog is discovered from the folder at runtime instead of using a fixed frame count, so additional `.png` files become selectable automatically. Numeric file names are ordered numerically first, followed by other file names. Frames are served through the `/frames/{id}.png` Razor endpoint so the source assets remain outside `wwwroot`.

## Localization

Contributor-specific UI strings live in `ContributorResource` resources for EN, UK, IT, and RU. As required by the project localization convention, every feature value in `ContributorResource.ru.resx` is exactly `Україна`.
