# Guess what I'm playing: AI opponent

**Guess what I'm playing** can be played solo by enabling **Play against AI** in the New game dialog.

## Game setup

AI mode replaces Player 2 with a server-side AI opponent. Enabling it automatically enables **Question cards** and disables the Question cards checkbox while AI mode is selected. The regular two-player mode remains unchanged and can be selected again for a later game.

The AI receives its own secret game and its own independently shuffled Question Cards. It also chooses its required exclusions automatically, so the human player only chooses Player 1's exclusions.

## Catalog coverage

AI games are restricted to catalog entries with enough known answers:

`coverage = answered questions / enabled questions`

A game needs at least **80%** coverage to be selected as the AI's secret game or to appear on an AI-mode board. The New game dialog uses the eligible game count as the AI-mode maximum card count.

## Answering human questions

The AI never invents catalog answers. For its own secret game it maps stored question answers as follows:

- YES -> **Yes**;
- NO -> **No**;
- unassigned -> **Don't know**.

A **Don't know** answer is written to shared question history as a distinct third state.

## Candidate filtering

The AI starts from every active board card except its own secret game. After the human answers a question asked by the AI:

- **Yes** keeps candidates whose stored answer is YES;
- **No** keeps candidates whose stored answer is NO;
- **Don't know** eliminates nothing.

If contradictory answers reduce the candidate list to zero, the game ends immediately as a **Draw**. If exactly one candidate remains, the AI guesses it immediately.

## Question and guess strategy

While more than **15%** of the initial candidate list remains, the AI stays in the search phase. It uses Question Cards to gather information and does not start guessing.

At **15% or fewer** remaining candidates, it evaluates all three current Question Cards. When one or more questions can eliminate candidates, it asks the best one. When none can improve the candidate list, it guesses about **70%** of the time and otherwise intentionally consumes a Question Card to receive a different card later.

Questions with equal elimination value are chosen randomly. Candidate guesses with equal likelihood are also chosen randomly.

## Runtime ownership

AI state is server-authoritative and lives in a dedicated in-memory AI room runtime. SignalR still uses the existing minigame room snapshots for board and turn state, while a small server status call identifies AI mode and draws for presentation. Refresh and reconnect therefore do not expose the AI's secret or move decision logic into the browser.
