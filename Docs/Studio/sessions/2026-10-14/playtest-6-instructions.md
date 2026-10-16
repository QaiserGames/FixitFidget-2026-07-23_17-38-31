# Playtest 6: how to run it (for Mansoor; 14 Oct 2026, updated 15 Oct)

*Three players, each recorded, each on a fresh game, nobody helping: you, your girlfriend, and one person who has never seen the game. The studio's rule since today: the scope stays frozen until someone who isn't you finishes Day 3 and wants Day 4.*

*15 Oct: the build ran on your Mac and passed its labs (`Docs/Studio/sessions/2026-10-15.md`). Claude then played Day 1 by hand and fixed three things a player would hit: E now hands a finished device back over the bench from wherever you worked on it (it used to say "Set down" unless Ace stood near the middle of the bench, and Grace walked out "Never again" while he held her fixed camera); nobody walks out on a device while it's in your hands (their bar still drains, and a slow repair still costs the tip and the "Loved it", but it stops at a tenth); and the drinks card no longer says "E brings it into view" half a metre before it does. **Before playing: push today's commit (GitHub Desktop), and make sure Unity has compiled (no red in the Console).**

## Before anyone plays (15 minutes, once)

1. **Listen to the sounds first.** Play `all-cues-at-suggested-volume.wav` (85 s; every new sound in a row; Claude sent it in the chat on 14 Oct). Note any you'd swap ("number 12 is too loud", "the screw tink is wrong"). Swaps are cheap.
2. **Put a mouse and a pad on the table.** No trackpad: the cloth's rub is a held drag, the hardest thing to do on a trackpad, and none of the labs models one.
3. **Unity:** open the café scene (`AcesCafeLayoutPlaytest`), then **Fixit Fidget › Playtest › Fresh game 1 - Start a new game (keeps your save as a copy)**. Your Day 5 save is kept; **Fresh game 2** brings it back afterwards.
4. **Settings to check before the stranger plays** (they survive "Fresh game"): in the phone's settings, the intro on **Full**, **Aim help** as it ships (on), **Vibration** on, the quality preset as you play it. Set them back if you changed them.

## Recording each player (the agents timestamp every struggle from it)

1. QuickTime Player › File › **New Screen Recording**; in **Options**, choose your **microphone** so their voice is in it.
2. Record the whole screen from the main menu to the end of Day 3 (or until they stop wanting to play).
3. Save it in `~/Documents/FixitFidget/Recordings/` as `playtest6-<who>-<date>.mov` (that folder is never committed).

## While they play

- **Nobody helps.** Don't explain, don't point. If they ask "what am I supposed to do?", say "what do you think?" and write down the moment.
- The person watching writes three things with the time: **where they got stuck**, **what they said out loud**, and **when they smiled**.
- Let them stop whenever they want. Where they stop is the most useful thing we learn.

## After each player (5 minutes)

Fill in the playtest journal together: https://claude.ai/artifact/Mk8oWoBSKLtJvoGdobbPwK. For the stranger, switch on "A guest played" and write their one-sentence answer to "what is this game?" in their own words.

## What we're testing (so you know what to notice, not to steer them)

- **Day 1's first repair** (Grace's camera, a lens and film-path clean): under 90 seconds for a first-timer, no stall over 10 seconds.
- **The hand-back:** after "Fixed!" and "[E] Take it to Grace", does E say "Hand it back" from where they stand at the bench, with Grace on her stool? Write down any "Set down" they didn't want.
- **Anyone's patience bar going red while their device is in your hands** (on the bench or carried): they won't walk out now, but write down when it happens; it means the repair is too slow for its customer.
- **The cloth and the brush:** grab, drag, see the dirt go; did it "feel the impact"?
- **Screws and holes** (on the walk-in phone and Day 3's watch): do the holes look right next to the screws? Does every screw land in a cup?
- **Day 2's swap:** one press flies the new shutter over, a hold presses it in.
- **The checklist card:** did they read it, or ignore it? Did they find Tab / View ("what's left")?
- **Customers:** does anyone storm out on Days 1–2? (The target is nobody.)
- **The café:** does it look full and alive from above? Do people sit on the repair-bar stools?
- **The intro:** different each morning? Grace's house only on Day 2? Day 1's bench shot now looks from Ace's side (it used to look through the counter's frosted glass).
- **The drinks machine:** at the back bar, does the card's "E brings it into view" match what E does?
- **The sounds:** anything that sounds wrong, too loud, or missing (the street shots in the intro are still quiet: they wait for your recordings).
- **The night:** does the zoom still jump when you walk from the bins toward Grace's house?

## Afterwards

Tell the next session where the recordings are. It will timestamp every stall against the targets and bring you the list.
