# Customer memory and identity check

Branch: `codex/grace-showcase-memory`, based on recap-save checkpoint `402fa808`.

This is the next bounded M1 slice: factual return dialogue, one regular identity
per day, and expression hooks. It does not complete the full character showcase.
No new portrait artwork is needed to test it.

## Quick checks

1. Let Unity finish importing. Stop Play Mode and run **Fixit Fidget > Checks >
   Customer memory and identity**. Look for one PASS message and no errors.
   These checks use synthetic history and inactive temporary objects. They do not
   load, reset, or write your save or modify the scene/assets.
2. For a live two-visit test, use a separate test copy/save when convenient.
   Keep your current progress; this branch does not reset it. The existing
   schedule features Grace late in Day 1 and again early in Day 2.
3. Hear Grace's request, step away without choosing, and reopen it. The same
   request should still be readable. Accept and complete her repair, then finish
   the day. On Day 2, her intake should acknowledge the previous repair and
   describe today's actual device and fault.
4. Observe names: a generic walk-in should never be named Grace, and Grace should
   not appear twice on the same day. Existing customer counts and schedule timings
   remain authored in the DayDefinition assets.

The Editor check exercises the unhappy outcomes so you do not need to replay
every branch immediately. When testing those paths in Play Mode later, compare:

| Previous visit | Return acknowledgement |
| --- | --- |
| Perfect or Good repair; all service completed | Thanks for the previous repair |
| Passable repair | Acknowledges rough edges |
| Rejected repair returned | Acknowledges unfinished work |
| Some service delivered, remainder missed | Acknowledges partial service |
| Timed out without service | Says the previous visit ran out of time |
| Deliberately declined | Recalls that the job was not accepted |
| Turned away for stock or shelf capacity | Recalls the capacity problem |
| Drink-only service | Recognises the counter without inventing a repaired device |

A high accumulated relationship must not make a recent bad repair sound good.
Only an actual repair handback supplies a repair grade to memory. Existing trust,
patience, tip, and payout tuning is retained; balancing those is a separate decision.

## Portraits can wait

Grace's profile has Neutral, Happy, Annoyed, Sad, and Surprised sprite slots.
The UI requests neutral, pleased, impatient, worried, or surprised expressions.
An unassigned expression falls back to neutral; with no artwork or default
portrait, the existing card shows an initial and a short mood label.

This is a temporary readable fallback, not a final art direction. Distinctive
Hades-like customer portraits remain an idea to explore. No final portraits are
generated, purchased, or required by this change.

The visible expression changes apply to the existing conversation panel. Floor
handoffs still use the existing bubbles; a new floor dialogue panel is not part
of this slice. The real portraits and their visual composition need a later art
review at the game's camera/UI size.

## Completion status

- Implemented: save-compatible memory snapshots, factual callback selection,
  authored Grace callbacks, regular/name reservations, repeat intake, and
  portrait expression selection/fallback.
- Automated Unity Editor checks are provided; Unity compilation and Play Mode
  verification must be run in the full project. They were not run in the coding
  workspace because Unity and a C# runtime are unavailable there.
- M1 remains open: signature storyteller interaction and polite focus boundary,
  camera/photo consequence, final portraits, and fresh-player recognition test.

See `recap-input-checklist.md` for the cursor bug bundled into this review branch.
