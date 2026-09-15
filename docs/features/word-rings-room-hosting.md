# Word Rings room hosting and controls

Word Rings multiplayer rooms support two creator roles.

- **Play with everyone** keeps the room creator as a normal participant. The server selects the three active ring rules randomly, the creator receives a personal hand, takes turns, and can win like any other player.
- **Host the game** makes the creator a non-playing controller. The host receives no words and is never assigned a turn. Before every round, the host must choose one rule for the blue, yellow, and red rings.

Multiplayer players never receive a reveal-rules control during an active round. The three rules are revealed in the victory/defeat dialog after the round ends.

## Host-selected rules

For a hosted room, the server returns up to six enabled, non-empty rule options for each ring. The host selects exactly one option per ring before the round can start. Because the host is not a playing participant, one connected player is enough to start a dedicated-host round once all three rules are selected.

Each ring has its own **Other options** action. That action can replace the current set with another set of up to six rules only once for that ring during the round setup. The selected rule IDs are kept server-side and validated against the options that were actually offered to the host.

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

The **Check** action is displayed directly below the word bank instead of in the top toolbar. This keeps the action next to the word that is currently being placed. After validation, every incorrect word is moved automatically into its correct Venn region; partial scoring changes only the points/feedback, not whether the correction happens.

The custom result dialog shows the three rules used by the completed round, color-matched to the blue, yellow, and red rings, in addition to the personal victory/defeat message.


## Manual adjudication for dedicated hosts

When the creator chooses the non-playing host role, the three selected rules are host-private during the active round. Player room-state responses redact all three rule texts while play is in progress. When the round finishes, the completed-round rules are included again so every player's victory/defeat dialog can show them. The host can reveal or hide the selected rules locally before and during the round with **Show rules**.

Player placements are not checked against the rule catalog in this mode. Pressing **Check** publishes one enlarged, pulsing pending token to every client and blocks that player's next submission. The host may keep the token in its submitted Venn region and press **Correct**, or drag it to another region and press **Move**. Correct awards 1 point and keeps the turn; outside-ring Correct can award that point only once per uninterrupted turn. Move awards 0.5 point when the final region shares at least one ring with the submitted region, otherwise 0 points, and always passes the turn. Resolving the token removes its pending pulse/scale treatment.
