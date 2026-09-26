# Daytime foundation: one full-day playthrough to lock it

Run this once in Unity on branch `claude/pensive-pasteur-qgz8fo`. That is
`main` (your 25 Sept build, `9426285`) plus the 26 Sept NPC fixes. If every
row passes, the foundation is locked: `main` moves up to this branch, and
daytime work stays frozen apart from real bug fixes.

**Don't run** *Fixit Fidget > NPC > Cafe moments* for this test. Those
moments are a separate, later step. With none placed, NPCs run the same code
as your 25 Sept build.

## Before you press Play (2 minutes)

1. Close Unity. Run `git fetch origin` and `git checkout
   claude/pensive-pasteur-qgz8fo`, then open Unity. Note any red Console error
   before playing.
2. Open `AcesCafeLayoutPlaytest`. Select **GameManager**, and on
   **SaveManager** change *Interaction Playtest Save Name* to
   `playtest-foundation.json`. A new file means a fresh Day 1 (the Grace
   camera day: no guided opening, at most two customers at a time, Grace is
   the first featured arrival). `save.json` and
   `playtest-aces-cafe.json` are left alone.
3. Plug in a controller, but start on keyboard and mouse.
4. Don't save the scene afterwards. Or set the name back to
   `playtest-aces-cafe.json` first.

## The day, in order

| # | When | Check | Pass |
|---|---|---|---|
| 1 | Opening | Day 1 hints (top left) | They appear, are readable, and clear as you act |
| 2 | First 60 s | **Arrivals**: people come from the car park and neighbours' doors | Cars park and drivers get out. Walkers wait at kerbs and cross on green or at a safe gap. Nobody stops in the doorway |
| 3 | Throughout | **Cars** | No car drives through a person, a lamp post or another car. Signals change. Nobody waits at a kerb for more than about 15 s |
| 4 | Throughout | **Pedestrian collisions** | No two bodies overlap, outside or inside. Nobody walks circles round a spot or a seated person |
| 5 | Mid-day | **Seating**, with patrons and customers | Sit, sit-talk and stand-up look as good as on 25 Sept. Nobody walks up to a chair while someone is getting out of it |
| 6 | Early Day 1 | **Grace**: camera request, strap constraint, reunion mention | Her intake reads correctly, she sits, and her chatter and *Let me focus* both work |
| 7 | Grace's job | **Repairs**: clean, fix the shutter, hand back | Grade matches the work. Tickets update. Nothing is left on the shelf |
| 8 | Any time | **Drinks**: dispenser, two hands, cooling | Pours, freshness rings, serving and cold-drink blocking behave |
| 9 | Second half | **Controller**: play about 3 minutes on the pad only | Prompts switch to pad labels. Move, interact, the station view, switching hands and the circuit boost all work. Nothing needs the mouse |
| 10 | Closing | **Last orders** until the recap | New arrivals stop. Everyone inside is served or leaves. Leavers walk to their car or home. The recap appears once and the checkpoint saves |
| 11 | After recap | Continue to Day 2 | Morning opens empty with no leftover people, cups or books. Grace's return is remembered |

## If something fails

Write down the row number, the time on the day clock, the NPC's name from
the Console, and take a screenshot. Also note any Console warning starting
`gave up`, `couldn't reach` or `Closing grace`. The day log CSV and
`Logs/ArrivalsTrace` stay on your PC. Send the relevant lines.

## When it all passes

Tell me. I'll fast-forward `main` to this branch and tag it
`daytime-foundation-v1`. Changes after that are either bug fixes against this
checklist or the night work.
