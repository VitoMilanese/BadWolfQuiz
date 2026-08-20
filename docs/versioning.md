# Product versioning

BadWolfQuiz contains products that are released independently and therefore maintain separate semantic versions.

## BadWolfQuiz.Web

`BadWolfQuiz.Web` uses its own `MAJOR.MINOR.PATCH` version stored in `src/BadWolfQuiz.Web/BadWolfQuiz.Web.csproj`.

The deployed version is available from the About page, the host Settings UI, the logo tooltip, and `/api/version`. The version endpoint also exposes a short commit identifier when one is available from the build environment or assembly informational version.

Web releases should use tags in the form:

`web-vMAJOR.MINOR.PATCH`

Example: `web-v1.0.0`.

The mandatory all-player question feature is the headline backwards-compatible feature for Web `1.16.0`, so it uses a MINOR bump from `1.15.10`.

Question copying and same-quiz cloning from the Quiz Editor board are the headline backwards-compatible feature for Web `1.19.0`, so this release uses a MINOR bump from `1.18.0`.

Host-selected multiple-choice questions are the headline backwards-compatible feature for Web `1.20.0`, so this release uses a MINOR bump from `1.19.0`. The final `1.20.0` implementation includes the host AJAX lifecycle, correct-answer-only presentation, stable randomized option order with **Nobody answered** last, dynamic reward display updates, and automatic closure when no eligible buzzer player remains.

Follow-up fixes made before the first `1.20.0` release remain part of `1.20.0`; a PATCH bump is reserved for compatible fixes released after that version has shipped.

Web `1.20.1` is a PATCH release that fixes Quiz Editor question drag-and-drop so successful swaps and moves update the board in place without a full page reload, while failed moves surface an error without forcing a reload.

Web `1.20.2` is a PATCH release that prevents duplicate round creation from repeated **Add Round** submissions by locking the dialog actions and showing the shared busy indicator while the request is in progress.

Web `1.20.3` is a PATCH release that fixes cross-round question moves in the Quiz Editor by closing and locking the dialog immediately, showing the shared busy indicator, and updating the source question cell in place without a full page reload.

Web `1.20.4` is a PATCH release that fixes **Copy Question** target-category capacity detection by treating truly blank placeholder questions as free slots and reusing those placeholders when copying. It also adds a localized **Exchange category** tooltip and local loading indicators for the Copy Question, cross-round question exchange, and category exchange dialogs while destination lists are prepared.

## BadWolfQuizLogDownloaderWpf

`BadWolfQuizLogDownloaderWpf` maintains a separate version in its own project file. Its version is displayed in the application title bar and changes independently of the web application.

Downloader releases should use tags in the form:

`log-downloader-vMAJOR.MINOR.PATCH`

Example: `log-downloader-v1.0.0`.

## Semantic versioning

- PATCH: compatible fixes.
- MINOR: backwards-compatible features.
- MAJOR: major or breaking release milestones.

A release of one product does not require a version change in the other product.
