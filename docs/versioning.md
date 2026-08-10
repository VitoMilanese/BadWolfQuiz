# Product versioning

BadWolfQuiz contains products that are released independently and therefore maintain separate semantic versions.

## BadWolfQuiz.Web

`BadWolfQuiz.Web` uses its own `MAJOR.MINOR.PATCH` version stored in `src/BadWolfQuiz.Web/BadWolfQuiz.Web.csproj`.

The deployed version is available from the About page, the host Settings UI, the logo tooltip, and `/api/version`. The version endpoint also exposes a short commit identifier when one is available from the build environment or assembly informational version.

Web releases should use tags in the form:

`web-vMAJOR.MINOR.PATCH`

Example: `web-v1.0.0`.

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
