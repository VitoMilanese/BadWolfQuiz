# MasterHost Minigame Editor

The configured MasterHost manages the database-backed minigame catalog at `/Admin/MinigameEditor`. The page remains protected by the existing `MasterHost` authorization policy and keeps the existing game/question CRUD, answer import/export, resource synchronization, question enabled state, and runtime catalog contracts.

## Presentation

The editor uses the current BadWolfQuiz portal visual language:

- a full-width branded admin hero with game/question metrics;
- dedicated numbered **Games**, **Questions**, and **Answers** navigation;
- themed command panels, game cards, question rows, answer workspace, filters, empty states, synchronization panels, and destructive dialogs;
- responsive desktop, tablet, narrow-mobile, and small-phone layouts;
- visible keyboard focus and `prefers-reduced-motion` handling;
- normal vertical page scrolling on desktop and document scrolling on narrow mobile layouts.

## Server-side paging

Games, Questions, and Answers are paged at **25 items per page**.

Paging is applied in the data queries rather than by loading the complete catalog and hiding rows in the browser. In particular, the Games view renders at most 25 game-card `<img>` elements at once, so the browser cannot request card images for games that are not present on the current page.

The Answers game selector intentionally remains complete because it only contains lightweight game metadata and does not load game-card images.

Invalid or out-of-range page numbers are clamped to a valid page. Game/question mutations and answer imports preserve the current page when possible.

## Pager navigation

Each paged view exposes the same controls above and below its list/table.

- Previous and Next links remain available as normal server-rendered fallbacks.
- Direct numbered page buttons allow jumping to arbitrary nearby pages.
- Large page ranges use ellipses while keeping the final page directly reachable.
- Normal pager clicks are intercepted by the editor JavaScript and loaded through AJAX.
- The server still renders only the requested 25-item page; AJAX replaces only the paged list/table region and pager instead of reloading the complete document.
- The existing vertical scroll position is preserved during page changes.
- `pageNumber` is synchronized through the History API, and browser Back/Forward navigation reloads the appropriate paged region.

If AJAX paging cannot complete, the existing anchor URL remains a valid full-page fallback.

## Answer autosave with paging

Paged Answers use partial updates instead of replacing the complete selected game's answer matrix from the currently rendered 25 rows.

This means editing page 2 cannot delete or overwrite stored answers from page 1, page 3, or any other page. The assigned-answer count still reflects the complete selected game after each successful partial save.

Before pager navigation, section navigation, or selected-game changes, any pending answer autosave is flushed. The small autosave request uses `keepalive` so a recent edit is not silently dropped by navigation. Answer filters and autosave bindings are reinitialized after an AJAX page replacement.

Full TXT import/export keeps the existing complete-matrix contract and line order.

## Resource synchronization and existing behavior

The redesigned editor continues to support resource synchronization and cleanup, image uploads/replacements, game/question deletion confirmations, enabled/disabled questions, YES/NO/Unassigned answer filtering, TXT import/export, and the shared busy indicator where a full navigation or potentially slow write is still required.

Runtime minigame behavior is unaffected by the editor presentation and paging layer.

## Release

The redesigned and paged MasterHost Minigame Editor ships in Web `1.26.24` (`web-v1.26.24`) through issue #521 and PR #522.

See also [Minigames](minigames.md) for the runtime catalog and **Guess what I'm playing** gameplay documentation.
