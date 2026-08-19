# SEO indexing operations

BadWolfQuiz exposes a deliberately small search-facing surface for `badwolf.buzz`.

## Search languages

Only these cultures are valid SEO/search languages:

- `uk`
- `en`
- `it`

The novelty `ru` UI culture is application-only. It must never appear in public SEO routes, sitemap entries, canonical alternates, `hreflang`, `x-default`, structured-data language declarations, or other search-facing metadata.

## Indexable pages

The initial whitelist is:

- Home
- FAQ
- About
- Public Quizzes

Each page has an equivalent localized route under `/uk`, `/en`, and `/it`.

All other Razor pages are treated as application pages and receive `noindex, nofollow` through the shared SEO head policy. This includes non-prefixed duplicates of the whitelist pages, gameplay/private/admin flows, Join URLs, forms, and the novelty `ru` UI surface.

## Discovery documents

`/robots.txt` allows general public crawling, blocks private/runtime application paths, and references `https://badwolf.buzz/sitemap.xml`.

`/sitemap.xml` is generated from the same SEO route catalog as the localized routes. It contains exactly the canonical whitelist URLs for `uk`, `en`, and `it`, using absolute HTTPS URLs.

## Metadata and structured data

Indexable localized pages render SEO metadata in the initial server response:

- localized `<title>` and description;
- self-referencing canonical URL;
- `hreflang` links for `uk`, `en`, and `it`;
- `x-default` pointing to the Ukrainian equivalent;
- Open Graph metadata;
- JSON-LD `Organization` and `WebSite` data.

A page is indexable only when the route culture and active UI culture match a supported SEO culture.

## Regression expectations

When changing public routes or localization, keep the following invariants covered by tests:

- every indexable page has metadata for every SEO culture;
- sitemap contents come only from the SEO route catalog;
- `ru` never becomes a search-facing culture;
- non-SEO pages remain `noindex, nofollow`;
- canonical and alternate URLs remain absolute `https://badwolf.buzz/...` URLs;
- `NoveltyLocalizationTests` continue to pass.

## Post-deployment verification

After deploying SEO changes:

1. Verify `https://badwolf.buzz/robots.txt` returns `200` and references the sitemap.
2. Verify `https://badwolf.buzz/sitemap.xml` returns `200`, parses as XML, and contains only canonical `uk`/`en`/`it` whitelist URLs.
3. Inspect one localized page per culture in the raw server response and verify title, description, canonical, `hreflang`, Open Graph, JSON-LD, and the `<html lang>` value.
4. Confirm non-SEO routes emit `noindex, nofollow` and do not appear in the sitemap.
5. Confirm there are no `/ru` SEO URLs, `hreflang="ru"`, or `ru` structured-data language declarations.

Google Search Console setup remains a deployment/operations task because it requires access to the production Google account and DNS. After deployment, add `badwolf.buzz` as a Domain Property, verify it through DNS, submit `/sitemap.xml`, inspect the canonical localized homepages, request indexing, and monitor indexing reports.
