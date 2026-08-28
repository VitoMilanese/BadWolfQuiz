# Shareable quiz description links

BadWolfQuiz can create a stable public-by-link description URL for a quiz from the host's `/Quizzes` page. The link is intended for announcing an upcoming game in Telegram or another service that renders Open Graph previews.

## Host workflow

Use **Actions → Description link** for a quiz. BadWolfQuiz copies an absolute description URL to the clipboard without navigating away from the quiz list and shows temporary copy feedback.

The action is available for both public and non-public quizzes. Creating or opening the description link does not change the quiz publication flag and does not add a non-public quiz to the Public Quizzes catalog.

## Public-by-link surface

The description URL uses an opaque token bound to the quiz identity. Anyone who has the URL can open the read-only page without signing in, so the link should be treated as an unlisted announcement link rather than an authentication credential.

The page exposes only announcement information:

- quiz title;
- quiz description;
- rating and rating count when available.

It does not expose questions, answers, editor controls, host-only actions, credentials, or private quiz configuration. Invalid tokens and unavailable quizzes return the normal not-found result.

## Social previews

The description page emits server-rendered Open Graph and Twitter metadata and references a quiz-specific 1200×630 Bad Wolf Quiz preview image. The preview uses the current quiz title and description and can include rating information when available.

Social services such as Telegram fetch the URL from their own servers. A URL using `localhost`, a private LAN address, or another externally unreachable origin cannot produce a Telegram preview even when it works in the host's browser. Preview testing therefore requires a publicly reachable HTTPS URL, such as the production BadWolfQuiz site or a temporary public HTTPS tunnel.

When BadWolfQuiz runs behind a reverse proxy, copied absolute URLs reuse the application's forwarded host/protocol handling so the public HTTPS origin is used instead of the internal Kestrel address.

## Search visibility

Quiz description pages are deliberately public-by-link but not search-discoverable:

- they emit `noindex, nofollow`;
- they also emit `X-Robots-Tag: noindex, nofollow`;
- they are not added to the sitemap or SEO indexable-route catalog;
- creating a description link does not make the quiz public in the Public Quizzes catalog.

The link is for sharing an announcement, not for starting a game.
