# September 26: natural arrivals, no circling, café moments

Branch `claude/pensive-pasteur-qgz8fo`, on top of your `2a325f1` (car park
arrivals, NPC sitting). Nothing here moves furniture, re-bakes the NavMesh or
changes pay, patience or pacing.

## What was wrong (from the code and the baked layout, not guesses)

- **Circling.** When someone stood on or beside the spot an NPC was heading
  for, avoidance steered round them and the path steered straight back, so
  the NPC walked circles. The stuck watchdogs count *movement*, and a circle
  is lots of movement, so nothing ever stepped in. Patrons circled for about
  nine seconds, then walked out.
- **Look-around point.** Arriving customers first walk to a random point
  2–5 m inside. Sampled against the real layout: 17% picked a point inside
  the 1 m door opening and stood there 1–3 s with everyone behind them, and
  15% picked a point next to a chair, where a sitter's navigation body is
  parked, and circled it.
- **Chair hand-over.** A leaving NPC's brain lets go of the chair while the
  body still takes about two seconds to stand up and step back. For one frame
  another brain could claim it and walk up to someone still in it.
- **Give-up loop.** After failing every waiting spot, "wait where you stand"
  lasted one frame, then the customer tried again: walk, jam, shove, give
  up, and round again.
- **Cars are not involved.** They have no colliders, are excluded from the
  NavMesh bake, and never come within about 3 m of the walkable area.
- **Lounge.** The tub chair (0.735 m) and the lounge table (0.42 m) are both
  lower than the 0.75 m step height, so the bake treats them as floor. NPCs
  could path straight through the tub chair.

## Set up

1. Let Unity compile. Start from a clean Console and note any red error
   before testing gameplay.
2. Test arrivals and circling **before** placing moments. Those fixes need
   no setup.
3. Open `AcesCafeLayoutPlaytest`. Run **Fixit Fidget > NPC > Cafe moments 1 -
   Place sofa, bookshelf and lean spots**. Read the Console report: it lists
   every spot it placed and anything it skipped, with the reason.
4. In the Scene view, look at the gizmos under `20 - cafe moments`: blue sofa
   seats, orange bookshelf, green leans. Move anything that looks wrong.
   Delete any patron lean you'd rather keep clear.
5. Run **Fixit Fidget > Cafe furnishing > Map the circulation** to confirm
   you can still get round the room when it's full.
6. Save the scene only once you're happy with it. **Cafe moments 2 - Remove
   them** takes everything out again, including the two lounge obstacles.

## Tests

| # | Do this | Expect | If not, record |
|---|---|---|---|
| 1 | Watch 10 arrivals on a busy day | Nobody stops in the doorway. The look-around pause happens a few steps into the room, away from chairs and waiting spots | Where they stopped, and a screenshot |
| 2 | Watch customers pass seated patrons | No circling round a chair. Someone blocked near their spot pauses about a second, then carries on | The NPC name, and how long it lasted |
| 3 | Fill the tables and let a patron's seat be blocked | They wait, then take another seat. They don't circle, then walk out | Seat names |
| 4 | Let a patron leave while another heads for tables | Nobody walks up to a chair while its sitter is still standing up | Both NPCs, and the chair |
| 5 | Make a customer's waiting spot unreachable (or fill every spot) | They wait where they stand for about 8 s between attempts, with no repeated shoving | The Console "gave up" count |
| 6 | Customer at a loiter spot by the front windows | They step back and lean on the window. When served or leaving, they straighten up and walk off | Clipping through the wall, or a lean that reads as falling |
| 7 | Wait for a patron to browse (25% chance each) | They walk to the bookcase, reach, and a book appears in their hand. They carry it to a seat and it rests on their lap | Book position or rotation looking wrong |
| 8 | A reader leaves | The book is left on the table or sofa as they stand, and disappears about 45 s later | A book left floating |
| 9 | Sofa seats | Some patrons sit on the sofas using your sit animation, facing into the room, not on the book or throw | Seat height (sinking or floating), and which sofa |
| 10 | Fill every table seat | Extra patrons lean on a free wall instead of hovering at the door | Where they leaned |
| 11 | Walk through the window lounge | NPCs route round the tub chair, not through it | — |
| 12 | Close the day with people leaning or reading | Recap and next morning are normal, with no stuck poses | The sequence |

## Knobs (Inspector)

- **CustomerBrain / PatronBrain > Blocked near the goal:** radius 1.6 m,
  patience 1.25 s, close enough 0.75 m, polite pause 1.2 s, wait max 4 s.
  Also on CustomerBrain: give-up retry 8 s, and door clearance 2.5 m for the
  look-around point.
- **PatronBrain > Café moments:** browse 25%, sofa 30%, lean 12%, browse
  5–10 s, lean 25–50 s.
- **NpcPose** (added to NPCs at runtime; add it to the prefabs to tune it):
  lean angle 6°, step speed, lap tilt 35°, book rotation offset, book
  lingers 45 s.
- **Sofa seats:** each is a normal TableSeat. Move its SeatPose to fix the
  height. Untick **Ambient Only** to let waiting customers sit there too
  (that makes customer waiting easier).

## Limits, honestly

- None of this has run in Unity yet. It compiles with 0 errors against Unity
  2021.3 reference assemblies plus stand-ins for the TextMeshPro, UI,
  Input System and Cinemachine packages, with every script in the project
  included. Unity 6.5's own compile is the real check.
- The lean is the idle pose tipped back, not a lean animation, so judge
  whether it reads as leaning. A proper clip can replace it later without
  touching the bookkeeping.
- The book copies the lounge's "Book 1". If it looks turned the wrong way,
  set NpcPose's book rotation offset.
- Sofa seat height is assumed to be 0.45 m. The sit clip corrects feet by up
  to 12 cm; beyond that, move the SeatPose.
- Patron lean spots are chosen automatically and conservatively. Check where
  they land before saving.
