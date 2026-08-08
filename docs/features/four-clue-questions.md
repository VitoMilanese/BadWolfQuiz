# Four-Clue Questions

## Purpose

Four-clue questions reveal their content progressively instead of treating the
question timer as a single interval that immediately ends the question. Clue
progress is authoritative game state: both automatic timer expiration and a
manual host reveal use the same domain transition and timing rules.

## Runtime state

A four-clue question uses the immutable clue blocks from the quiz definition and
tracks only reveal progress in the running game.

- `RevealedClueCount` determines how many clues are currently public.
- `CanRevealClue` indicates whether another clue remains available.
- revealing a clue changes runtime state; it is not only a client-side visibility
  operation.
- the Game Engine remains authoritative for clue progression and timer behavior.

Clients render the current reveal state and may request permitted commands, but
they do not decide which clue is authoritative or whether the question has timed
out.

## Initial reveal

When a four-clue question becomes active, only the clues currently marked as
revealed are visible. Unrevealed clues may already exist in the rendered page so
that they can be exposed without a full page reload, but they must remain hidden
until the server reports their reveal.

## Timer expiration flow

Question timer expiration has two possible outcomes.

If another clue is available:

1. the Engine reveals exactly one next clue;
2. the question remains active;
3. a new full question-timer interval starts;
4. the expiration result is `QuestionTimerOutcome.ClueRevealed`;
5. clients are notified of the newly revealed clue and restarted timer.

If no further clue is available, expiration follows the normal unresolved-question
timeout flow. The final clue therefore receives the same full timer interval as
each earlier clue.

Conceptually:

```text
timer expires
    |
    +-- another clue exists --> reveal one clue --> restart full timer
    |                              |
    |                              +--> question remains active
    |
    +-- no clue remains -------> normal unresolved-question timeout
```

A single expiration must never reveal multiple clues.

## Manual clue reveal

The host may reveal the next clue before the current timer expires.

Manual reveal uses the same `RevealNextClue()` domain operation as automatic
timer-driven reveal. A successful manual reveal therefore:

- reveals exactly one next clue;
- restarts the full question timer;
- publishes the updated clue state;
- publishes the restarted timer state.

This equivalence is intentional. Timer restart belongs to the domain operation
rather than to a particular UI handler, so every successful reveal has identical
timing semantics.

The **Reveal clue** action is available only while another clue can be revealed.
After the final clue is visible, the action is removed or disabled.

## Real-time synchronization

Clue revelation is propagated to connected clients without requiring a page
reload. The host view updates the existing clue content in place when the server
reports the reveal. It also updates the displayed correct-answer value to
match the newly revealed clue count without requiring a page reload.

The notification is a transport mechanism, not the source of truth. A refreshed
or reconnected client reconstructs clue visibility from the current authoritative
runtime state.

The timer state broadcast after a reveal must correspond to the newly started
full interval so that host and player countdowns remain synchronized with the
server.

The displayed correct-answer value is also updated in place when a clue reveal
changes the four-clue base reward. The value change uses the same short emphasis
animation used by answer reward decay.

## Answer reward decay

Four-clue questions participate in answer reward decay because they are regular
buzzer questions. Decay is applied only during an individual player answer
timer, after the clue-dependent value has been determined. Two visible clues use
100% of the question value as the base, three use 50%, and four use 25%.

An incorrect answer always deducts 100% of the original question value. Returning
to the buzzer phase removes any player-specific decay and restores the current
clue-dependent base reward. The **Reveal clue** action is hidden while a player
is answering and becomes available again only in a buzzer-phase state where a
clue can still be revealed.

## Timer display

Client countdowns display remaining whole seconds using ceiling semantics. If a
fraction of a second remains, the displayed value must not prematurely drop to
the next lower second.

The host and player interfaces use the same display rule. The server-calculated
timer state remains authoritative; client countdown animation is only a
presentation of that state.

## Host tools during question flow

The host **Tools** menu remains available while a question or answer is open.
Actions that are not valid in the current gameplay state are hidden rather than
removing the entire tools menu.

This keeps applicable game and round controls reachable without exposing commands
that the current state does not permit.

## Media playback coordination

Game pages coordinate native audio, native video, and embedded YouTube playback
so that multiple media sources do not play over one another.

When native audio or video starts:

- other native media is paused;
- active YouTube playback is paused.

When a YouTube video starts:

- native media is paused;
- other YouTube embeds are paused;
- the YouTube video that initiated playback remains active.

## YouTube integration ownership

Media coordination must not create an additional `YT.Player` instance for an
iframe already managed by the existing YouTube behavior.

The coordinator observes YouTube iframe state messages to determine which embed
started playback and sends iframe commands when another YouTube video must be
paused. The existing YouTube integration remains responsible for player behavior
such as automatic expand while playing and collapse when playback ends.

This ownership boundary prevents multiple JavaScript components from competing
for the same YouTube player state.

## Testing expectations

At minimum, tests and regression checks should cover:

- a four-clue question initially exposes only its revealed clues;
- timer expiration reveals exactly one next clue when one is available;
- automatic reveal restarts the full question timer;
- manual reveal restarts the same full timer;
- clue visibility updates without a page reload;
- the displayed correct-answer value updates without a page reload when the
  third or fourth clue is revealed;
- answer reward decay uses the current clue-dependent value as its base and resets when returning to the buzzer phase;
- incorrect answers continue to deduct the full original question value while decay is enabled;
- the reveal action is hidden while a player owns the answer timer;
- the reveal action is unavailable after the final clue;
- expiration after the final clue follows the normal unresolved-question timeout;
- host and player timer displays use consistent ceiling semantics;
- the Tools menu remains available during question and answer flow while invalid
  actions remain hidden;
- starting native media pauses other native media and YouTube playback;
- starting YouTube playback pauses native media and other YouTube embeds;
- YouTube auto-expand and auto-collapse continue to work with media coordination;
- media coordination does not create a competing YouTube player instance.
