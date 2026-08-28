# External project links

BadWolfQuiz exposes optional project/community links through configuration instead of hardcoded URLs.

## Configuration

```json
{
  "Project": {
    "GitHubUrl": "https://github.com/VitoMilanese/BadWolfQuiz"
  },
  "Discord": {
    "InviteUrl": "https://discord.gg/nEREpDF44"
  }
}
```

Only absolute `http` or `https` URLs are used. Missing, empty, relative, malformed, or non-HTTP(S) values are treated as unavailable.

## UI behavior

- `Project:GitHubUrl` controls the GitHub icon on the About page and the GitHub/star call-to-action in the portal footer.
- If `Project:GitHubUrl` is unavailable, both GitHub links are omitted.
- `Discord:InviteUrl` controls the Discord icon on the About page.
- If `Discord:InviteUrl` is unavailable, the About page omits the Discord icon.

The About page and footer do not provide hardcoded fallback URLs for these links.
