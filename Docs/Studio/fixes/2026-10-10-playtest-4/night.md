*The Night lead's report to the Producer, 10 October 2026, as returned (the playtest-4 fixes: every finding in both playtesters' first reports). Paths under `/home/claude/cafe/s7/` are the cloud workspace the leads wrote in; every file they name is in the repo at the same path after `Assets/`, `Tools/` or `Docs/`. What was integrated, and how it was checked on the Mac, is in the commits (parts 1-7) and `Docs/Studio/sessions/2026-10-11.md`.*

Lead 5 (Night): playtest 4 report

1. What I did, finding by finding

**N1. "Waiting until Grace sleeps is risk-free" (veteran: "the lab waited 52 s on her stoop ... then was in and out in 9 s; she woke 0 times").**
- She now stays up until 1:20 (was midnight) and turns her lamp off at 1:40 (was 12:20). I added a second cup of tea at 12:30–12:40 so the evening has a change every 40 s or sooner. The 2:40 glass of water and Thursdays are unchanged.
- I worked the numbers out at the night clock's real speed: 48 s a game hour, 0.8 s a minute (not the brief's 1.27 minutes a second). Her stoop is reached at about 11:15.
  - **Before:** waiting her out cost 52 s and bought 112 s of a sleeping house.
  - **Now:** waiting costs about 105 s until her front window goes dark, or 116 s until her lamp goes off. That buys 48 s before her bay lights up again at 2:40; she is downstairs about 10 s after that.
  - **Second chance:** about 35 s between her lamp going off again (about 3:15) and dawn, minus about 10 s to walk back to the bins.
- Meanwhile she sits in her armchair facing the TV three times (28, 34 and 30 s) with the cups behind her. That is the interesting window.
- One thing the code shows that the brief didn't: once she is in bed she can never catch Ace downstairs. Sound alone is capped at two-thirds of her mark, and she doesn't come down to look from bed. So the real safe window starts when she goes upstairs, not when her lamp goes off. That is why the bedtime had to move, not just the moment she falls asleep.
- **Every usual night, not Night 2 only.** His notebook page gives one bedtime. Only Night 2's errand is inside a house.
- Kept as they were: sound alone can't catch you (two-thirds cap), and "?" still comes before "Caught.".

**N2. "The morning's straight face is one lenient press."**
- The cups' meter is now: sweep 0.90 s (was 0.95), green 0.145 (was 0.19), near 0.016 (was 0.025), patience 5.5 s.
- Every player has Nerve by the cups' morning, so I tuned against Nerve. I simulated 40,000 mornings per setting at 60 fps with the green placed at random:

| Press | Old cups (with Nerve) | New cups (with Nerve) |
|---|---|---|
| Careless (any moment) | holds 37% | holds 26% |
| Hasty (as the needle first reaches the green) | holds 99.9% | holds 69% |
| Watched (timed after one sweep) | holds 99.8% | holds 96% (87% for a less steady hand) |

- A pass through the band now lasts 231 ms, against 447 ms for the gnome. The gnome's meter is unchanged.

**N3. "Noise has no read on screen."**
- New `NoiseRings`: every sound someone could hear is drawn as a warm-white ring on the floor at the radius the game already uses:
  - walking 4 m, sneaking 1 m (both halved with Soft feet);
  - the cups' rustle 3 m;
  - a creaky stair 5 m (2.5 m when sneaking);
  - her bedroom doors 5 m.
- Each ring spreads from 80% to full size in 0.12 s and fades within 0.5 s.
- Walking reads differently from sneaking: four times wider, brighter and thicker. At most one walking ring every 0.28 s.
- No new rule: it only draws what `NightNoise` already sends.

**N4. "Her house at night can't be read" (veteran at 12:19 AM; first hour: "the saffron house looks brown").**
- The cause: from the overhead camera her street wall is seen edge-on, and she turns see-through when Ace stands near. So her lit windows now light the pavement in front of her house, which the camera does see.
- A scene step adds three spot lights, wired into `GraceAtHome`:
  - a porch light under the bay over her door, on all night (it marks her house, her step and Barnaby);
  - her front window's light on the pavement while her standard lamp is on, with the TV's colour flickering in it;
  - her bedroom's light from the north bay while her bedside lamp is on.
- Her lit bay panes now glow 2.4× brighter than the rest of the street, so the camera's bloom picks them up.
- **Cover is unchanged.** None of the new lights counts as one of her lamps, so what she sees by (7 m lit, 3 m dark) is the same. The step's check samples about 116,000 points inside her walls and bays per light; none may be lit, and in my calculation none is (closest miss 4.5°).

**N5. "[E] Let yourself in" at her step with Barnaby in hand.**
- This is **not test-save only.** Break-ins 4 (30 Sept) turned her door on in his real game; the scene has `breakIns: 1`. The `WholeGamePlaytest` header comment that said otherwise is out of date, and it misled the first-hour tester.
- Fixed: her door isn't offered while anything is in Ace's hand (the bin bag, Barnaby, the cups). The errand's direction wins. Once his hand is empty, the door is offered again.

**N6 (write-up for Mansoor).** The smallest ways the street could carry risk using what exists. Nothing built.
1. **She sees out of her front window.** Her eyes reach her stoop and the pavement in front of her window through the glass, lit by the new porch and window lights. Taking Barnaby while her front room is lit becomes a risk. Roughly half a day plus a lab. Her rules are unchanged; it does make Night 1 less calm, and that's your tone.
2. **The three neighbours walking home get her eyes.** They pass at 11:36 PM, 12:33 AM and 1:40 AM; the lavender house's neighbour walks right past her stoop. About a day. But being seen by a neighbour needs a consequence, and "Caught." outside a house is a new outcome.
3. **Lights alone add nothing** until someone looks; they only pay off together with option 1.
- I'd recommend option 1.

**N7 (for the plan).** The smallest Night 3:
- What exists: his ask lines, his reminder, Ace's line, the hint and his page.
- What's needed:
  - a `NightThing` entry for the cones;
  - two cone objects at the existing road works;
  - a "put it here" zone at each of two West Street parking spots (the same pattern as the zone where Ace hands him things);
  - the return scene's lines (your sister);
  - a lesson that actually changes something ("routines" changes nothing yet);
  - a morning after, probably the officer's question.
- About 1–1.5 days, plus a Blender session if the road-works cones can't be reused.

**N8.**
- **Handled:** Break-ins 7 now times "Hm?" → "Caught." in a lit room (the veteran's run #2). Night 1 now checks the porch light over her step.
- **Not mine:**
  - "Learned: Nerve" and "Grace likes you." (Content).
  - The mess costing presses, not customers (Systems: seats).
  - The bail, cells and stars after being caught (specced, not built).
  - The house's colour at night under the night look (Environment/Art).
- **Flagged, not built:** "how lit Ace is" (the light-gem half of the veteran's note). Cheapest option: colour Ace's rings by whether he stands in her lamplight. That's presentation, but it changes how the rings look, so it's your call.

2. Decisions and reasoning

- **Bedtime 1:20 / lamp off 1:40 / water kept at 2:40.**
  - With 2:40 fixed, the wait and the safe window trade off one for one (about 164 s between them). A longer wait means a shorter window.
  - I picked about a 2-minute wait and a 48 s window. That costs real time, still leaves room for a calm 15 s errand, and ends on a visible warning (her bay lights up).
  - Alternatives:
    - 1:00 bed: wait about 100 s, window 62 s. Still roughly free once you've waited.
    - 1:30 bed: wait about 125 s, window 38 s. Too much standing around.
- **The second cup of tea** keeps waiting from being dead time. It is her existing tea step, scheduled twice; no new action.
- **Rings only inside her walls while she's home**, not "near a house" as the brief said. She ignores any noise made outside her walls, so a ring on her stoop would show a sound nobody hears. The rings appearing as Ace crosses her threshold is itself the signal that she can hear him now.
- **The porch light is on all night, every night**, including Thursdays when she's out ("left on for coming home"). It marks the house; her windows say whether she's up.
- **No modelled lamp fixture.** The porch light is a downlight under the bay. A real lantern would be a Blender session.
- **The cups' meter is now about as hard as the officer's harder question**, and harder than his Day 3 description. If you want the officer to stay the hardest, his numbers need tightening (not my file).

Decisions his to overrule:
- the 1:20 / 1:40 hours;
- the second tea at 12:30;
- every usual night rather than Night 2 only;
- the cups' 0.90 / 0.145 / 0.016;
- the ring style (colour, 0.5 s life, at most one walking ring every 0.28 s);
- rings inside her walls only;
- the porch light on all night;
- her lit bays 2.4× brighter (slightly marks her house; the brief asked for it to read);
- the lights' intensities (porch 2.2, front window 2.2, bedroom 3.0), tunable in the Inspector; a second run of the step leaves tuned lights alone.

3. Files (all under `/home/claude/cafe/s7/night/`)

Runtime (`Assets/Scripts/`):
- `GraceNight.cs`: the new usual night (9 steps), with her bedtime, lamp-off and water hours as constants, and helpers to look up an hour or describe her night in a line.
- `GraceAtHome.cs`: the three outside lights, driven by her lamps; the front one flickers with the TV. Adds `Listens(at)` for the rings, an `Outside()` report line, and a nightfall log built from her schedule.
- `NoiseRings.cs` (new): the pooled rings (Sprites/Default line renderers, nothing allocated per frame), with counters for the labs.
- `NightWalk.cs`: makes the rings at nightfall and removes them at the night's end; adds a `Rings` accessor.
- `NightHomes.cs`: her lit window panes use their own brighter material.
- `GraceHouse.cs`: her door isn't offered while Ace is carrying something (N5).
- `NightThings.cs`: the cups' meter numbers.

Labs (`Assets/Scripts/Diagnostics/`):
- `GraceAtHomeCheck.cs`: reads her hours from her schedule; checks the second tea and the outside lights; photographs her house (16a–c from the overhead, 17a–c in first person); checks and photographs the rings (18a sneaking, 18b walking; none on the street); checks N5 (photo 00); times "Hm?" → "Caught.".
- `NightTwoCheck.cs`: the wait's time limit comes from her schedule; reports the wait in seconds and game minutes; checks N5 with the cups in hand and the rings drawn during the errand.
- `NightOneCheck.cs`: the porch light is on over her step.

Editor (`Assets/Editor/`):
- `Playtest4NightSteps.cs` (new): "0 - Check first" and "1 - Her window and her porch light (scene)".
- `GraceRuleChecks.cs`: **taken (nobody owned it; it checks GraceNight).** Her new hours, plus the wait / window / dawn arithmetic written as checks.
- `NightRuleChecks.cs`: the cups' meter checks rewritten for the new numbers, with Nerve.

4. Unity steps, in order
1. Copy the files in. Run **Fixit Fidget › Checks › Pick up scripts changed from outside (reimport)**. It should compile clean.
2. Open the café scene, saved (not dirty). Run **Fixit Fidget › Playtest 4 › Night › 0 - Check first**. Expect: Grace at home found, the three lights "not wired", her new hours, the wait numbers (105 s / 116 s / 48 s), and the cups at 231 ms against the gnome's 447 ms.
3. Run **Fixit Fidget › Playtest 4 › Night › 1 - Her window and her porch light (scene)**. Expect "DONE: … change(s)", about 21 checks passed: wired, spot lights, off in the scene, no shadows, not her lamps, 0 points lit inside, landing 1.94 / 1.69 / 2.91 m out from her wall, the porch light under the bay. Then **File › Save**.
4. Run step 1 again. Expect "NOTHING TO CHANGE: already done".
5. Run **Fixit Fidget › Checks › Grace at home rules** and **Fixit Fidget › Checks › Night 1 rules**. Both should PASS.

5. Labs, in order (run after step 3)
1. **Night › Break-ins 7 - Grace at home: her night and her eyes and ears (lab, a check)**. About 4 min. All PASS; photos 00, 16a–c, 17a–c, 18a/b, plus the "Hm?"→"Caught." note.
2. **Night › Break-ins 7 - Grace at home: her Thursday (lab, a check)**. About 1.5 min. Includes the porch-light check.
3. **Night › Break-ins 8 - Grace's cover (lab, a check)**. About 1.5 min. Now uses the new "asleep" hour.
4. **Night › Night 2 - Play check (lab, drives itself)**. About 5 min, about 65 s longer than before. Expect: wait about 117 s (at least 90 s required); the green's width with Nerve 0.210; N5 and ring checks PASS.
5. **Night › Night 2 - Play check, stalling: one skip, his table** and **… the third skip**. Untouched: they never visit her house and use the officer's meter. Should pass as before.
6. **Night › Night 1 - Play check, keeping a straight face / cracking / the morning after Day 2**. About 2 min each. Only addition is the porch-light check.
7. **Night › Night 0 - Play check (lab, drives itself)**. Must stay 95/95; it asserts nothing about her door after Barnaby is taken.
8. **Night › Break-ins 3 - Walk Grace's house by itself (lab, a check)**. Nobody is home, so no rings; should pass as before.

Before step 3 runs, the Break-ins 7 night/Thursday checks and the Night 1 checks fail by design.

What he should see: a warm pool on her pavement that flickers while she watches TV, a softer wash from her bay when she goes to bed, then only the porch light. Inside, small rings when sneaking, big ones when walking that reach her chair. The cups' straight face means watching a sweep before pressing.

6. Needs another lead's file
- **`LodgerStory.cs` (Content), in `Pages()`:**
  `yield return Page("lodger.page.grace", Notebook.Kinds.Schedule, "Grace: the saffron house on the corner. Asleep by 1:45.");`
  (was "Asleep by 12:30."; 10 words, within the 12-word limit). Saves that already hold the page keep the old text; whether to refresh it is Content's call.
- **`WholeGamePlaytest.cs` (nobody's), header comment.** Replace "(NightWalk.breakIns, off in the real game until the break-ins are ready)" with "(NightWalk.breakIns, on in the real game since 30 Sept: this session doesn't need it)". Comment only.
- **Optional, `OfficerStory`:** tighten his numbers if the officer should stay the hardest meter.

7. Open questions, flags
- Going in while she's up still pays nothing extra (veteran: "same cups, same lesson, same morning"); the only incentive is the time waiting costs. A reward, such as the man's warmth +1 for cups brought back before her bedtime, would be a new rule. Your call, not built.
- `Tests/GraceRules` (not in the cloud copy) compiles GraceNight and GraceRuleChecks. If it has its own assertions about her hours, they need updating too. Run it on the Mac.
- Rings show how far a sound carries on Ace's floor. Hearing actually uses straight-line 3D distance, so a ring drawn under her bed slightly overstates how far it reaches upstairs.
- No new mechanics: all three new lights, the rings and the brighter panes present existing rules. The deal and Night 0's reveal are untouched.

8. Syntax check (`/home/claude/cafe/s7/tools/syntaxcheck`, all 13 files)
```
OK    Assets/Editor/GraceRuleChecks.cs
OK    Assets/Editor/NightRuleChecks.cs
OK    Assets/Editor/Playtest4NightSteps.cs
OK    Assets/Scripts/Diagnostics/GraceAtHomeCheck.cs
OK    Assets/Scripts/Diagnostics/NightOneCheck.cs
OK    Assets/Scripts/Diagnostics/NightTwoCheck.cs
OK    Assets/Scripts/GraceAtHome.cs
OK    Assets/Scripts/GraceHouse.cs
OK    Assets/Scripts/GraceNight.cs
OK    Assets/Scripts/NightHomes.cs
OK    Assets/Scripts/NightThings.cs
OK    Assets/Scripts/NightWalk.cs
OK    Assets/Scripts/NoiseRings.cs
exit: 0
```
It only checks syntax. I also checked every member I call against its declaration in the repo and every new local for name clashes; one clash in NightOneCheck (a local named `home` hiding the class's `home` field) was found and fixed.

Worth remembering: on 10 Oct, playtest 4 moved Grace's usual bedtime to 1:20 AM, lamp off at 1:40 AM (was midnight / 12:20), and Mansoor can overrule it.