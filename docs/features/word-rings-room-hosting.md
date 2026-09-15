# Word Rings room hosting and controls

Word Rings multiplayer rooms support two creator roles.

- **Play with everyone** keeps the room creator as a normal participant. The server selects the three active ring rules randomly, the creator receives a personal hand, takes turns, and can win like any other player.
- **Host the game** makes the creator a non-playing controller. The host receives no words and is never assigned a turn. Before every round, the host must choose one rule for the blue, yellow, and red rings.

Multiplayer players never receive a reveal-rules control during an active round. The three rules are revealed in the victory/defeat dialog after the round ends.

## Host-selected rules

For a hosted room, the server returns up to nine enabled, non-empty rule options for each ring. The host selects exactly one option per ring before the round can start.

Each ring has its own **Other options** action. That action can replace the current set with another set of up to nine rules only once for that ring during the round setup. The selected rule IDs are kept server-side and validated against the options that were actually offered to the host.

When the round starts, the puzzle and its expected Venn memberships are built from exactly those three selected rules. After the hosted round finishes, fresh choices are prepared for the next round.

## Hand size

A multiplayer player's visible hand is capped by the target score:

- target 5–9: the hand limit equals the target score;
- target 10–15: the hand limit is 10;
- because the room target itself cannot be lower than 5, the hand limit is never below 5.

Players can still have a longer server-side queue; only the current hand is visible and playable at one time.

## Room-owner controls

The room creator has server-authoritative controls in the player list and toolbar:

- assign the current turn to another eligible participant who still has words;
- remove another player from the room;
- lock or unlock the waiting room for new joins.

A locked room rejects new join attempts. The creator cannot remove themselves. If the current player is removed, the server selects the next eligible participant. These controls are available to both creator roles.

## Check button and results

The **Check** action is displayed directly below the word bank instead of in the top toolbar. This keeps the action next to the word that is currently being placed.

The custom result dialog shows the three rules used by the completed round, color-matched to the blue, yellow, and red rings, in addition to the personal victory/defeat message.
