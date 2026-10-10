# Playtest: the genre veteran, 10 October 2026

*Build `playtest-2` at `b8cbf6e` (after the café redesign). Played from the producer's evidence pack; I never touched Unity. Evidence tags as in the protocol §1. An autopilot day is not a human day; where I use one, I say how it bends the number.*

## Verdict

**Feels like a solid game to a genre veteran: Not yet**, because the day's pressure isn't fair (Impatient and Rushed customers are lost the moment you take their job, whatever you do) and the night's risk is optional. The bench is nearly there. Every fix below is a number, a data field or content. None is a new system.

- **Bench (ReStory's bar): Solid with fixes.** Screws back out of their own holes and fall, empty holes read as holes, the broken screen lands in the tray beside the new one, and the phone wakes. Two problems: the cleaning payoff happens under the cloth, and a repair ends twice.
- **Day (hectic, not cozy-only): Not yet.** It is hectic: an instant server still lost 5 of 9 customers on Day 5. But 17 of 18 standing waiters stormed out across the lab and his own days, the authored rushes reach the counter one phase late, and every day opens with about 40 s of nothing.
- **Night (nerve, not horror): Not yet.** The man, the reveal and his corner are the best hook in the game. But on the route a careful player takes, nobody can see Ace (Night 1) or Grace is asleep (Night 2). The only nerve test is a one-press timing bar the next morning.

## The run

**What I played** (all on the final scene, 10 Oct):
- **Day 5 lab, autopilot, Game view** (231 s: trace, frames, video, CSV). I looked at frames at the busiest moments and around one storm-out.
- **Day 3 lab, autopilot, stress tests** (204 s). I used the trace for the burst, 6 patrons, the door block, the full house and the aisle block. The whole-café video has block glitches and only covers the east half.
- **Day 3 lab at 15:35** (CSV only, no recording), as a third autopilot sample.
- **Day 1 with nobody touching anything** (180 s trace), only for the opening's timing.
- **His own Days 1–15** (`DayLogs/AcesCafeLayout`, older builds). Days 1–3 were written 7 Oct. Days 4–15 have no review column, so they predate 26 Sept. He played Days 1–4 and 10. On Days 5–9 and 11–15 he served nobody, so I use those only for queue patience.
- **The bench and the day's loop:** Bench 3 (128/128, all 18 photos), Stations as reach (first person and overhead), Juice, the recap phone.
- **The nights:** Night 0 (with Night 1's errand), Night 1 (straight face and cracking), Night 2 (the cups, one skip, the third skip). Reports and photos.
- **The code and data behind the numbers:** patience (`CustomerBrain`, the archetypes on the scene's spawner), days (`Assets/Data/Days`), patrons (`PatronSpawner`), Grace at home (`GraceNight`, `NightNoise`), the meter (`NightThings`), the economy (`Upgrade_*`, `Drink_*`, device prefabs).
- Python over every trace (customers' timelines, queue, idle, stalls, walks). The scripts are in `playtest-pack/work-veteran/`.

**How the instruments bend the numbers:**
- **What the autopilot does.** It hears each customer 2.5–6 s after they reach the counter and ends a repair 30–80 s after accepting it (a drink 15–35 s). It never moves Ace (0.00 m all day), never dials a hold call and never does the Human fault.
- **What that means for the lab numbers.**
  - The queue is a floor: a human at the bench lets it build.
  - Idle is a ceiling.
  - Waits are the autopilot's dice.
  - A standing repair customer can never be served, because the autopilot's 30 s minimum is longer than their patience. That is why finding 1 leans on his own logs and on the code.
- **One lab storm-out is an artifact.** Day 5's Tomas (a Human fault) stood 50.7 s at the till and stormed out. It's the autopilot, not a game bug.
- **The Day 1 CSV beside the 16:10 recording is the wrong file.** It is byte-identical to the 13:55 lab file and lists 2 customers. The trace shows 4 (Grace, Mansoor, the Officer, Walter). The recording stopped at 180 s before the day closed, so it wrote no log of its own. I use the trace.
- **Not in the pack:** Grace's cover and being caught (Break-ins 7/8), and a human-driven day with a trace. See *Runs I'd need*.

## The five that matter most

### 1. Impatient and Rushed customers are lost the moment you accept them (weakens the Day pillar most)

**What happens.** Impatient and Rushed walk-ins choose to stand while they wait (`preferredWaitKind: 0`), and standing drains patience at 1.15×. With their 0.7× and 0.6× multipliers, that leaves 25–27 s for Impatient and 21–22 s for Rushed on Days 3–5 [read: the scene's archetypes, `servicePatience` 45, `WaitingSpot` 1.15, Day_03–05 multipliers]. Every repair takes longer than that.
- **Lab:** 9 standing waiters. 8 stormed out 23.3–32.2 s after "yes", and the 9th (a drink) was served at 0% patience. Every seated waiter was served (14 of 14) [measured: Day05/Day03 CSVs, `service_s`, `wait_kind`].
- **His own days:** 9 standing waiters, 9 storm-outs, 23.3–33.1 s after "yes" (Days 3, 4, 7, 8). On the days he actually played, 5 of 5 stormed out, including 3 hold-call jobs [measured: his Day03/04/07/08 CSVs].
- **His repair loop:** 41–84 s from "yes" to the customer walking out, median 63 s. The Human fault is the exception at 5–6 s [measured: Day02–04 and Day10 `service_s`]. Even a perfect robot (Bench 3) needs 16 s of hands, plus the talk and the walk.
- **Day 5 lab:** 5 of 9 walk-ins were standing types. With an instant server, the day ended at −3 reputation, all of it from 5 "Never again" reviews [measured: Day05_summary; seen: `day5-lab-recap-phone-8pm.jpg`].

**Where.** Day 5. Mansoor (Rushed, seized pocket watch) is accepted at 14:44 and stands at the counter's west end. At 15:55 his ticket turns red ("Losing patience") and a red burst shows over his head [seen: frame at trace 106 s]. He's gone at 16:10. Nina, Omar and Deshawn follow the same pattern.

**Why it matters.**
- **The rule is hidden, the punishment is certain.** *Papers, Please* is tense because "the player is given specific rules on what documentation is required", and a citation arrives "shortly after the entrant leaves" ([Wikipedia](https://en.wikipedia.org/wiki/Papers,_Please)). Here the rule (a customer who stands can't outlast a repair) is written nowhere. The −2 review arrives 20–30 s after a "yes" that could never be won.
- **A veteran stops deciding.** Within two days he will decline every "How long?", "Can you do it now?" and "I'm late" customer on sight. Two of the five archetypes, about 40% of the full cast, turn from pressure into a decline rule.
- **PlateUp! is harsher and still fair.** "If just one guest waits too long, it is game over" ([TheGamer](https://www.thegamer.com/plate-up-how-to-reach-day-15-final/)). It feels fair because every patience bar is one you could have saved.
- **The code already names this.** `PatronSpawner`'s own comment calls permanent 1.15× standing "punishment rather than pressure", and that is exactly what these two types now get, by their own choice.

**Fix (data, one line).** Let Impatient and Rushed sit like everyone else (`preferredWaitKind` → Seat) and keep their short fuse in the queue (21–25 s there on Day 5). Seated, they'd last 40–47 s on Day 5: a race a player can win by starting their job first.

### 2. The night's risk is optional, and the nerve test is one lenient press (weakens the Night pillar)

**What happens.**
- **Night 1:** nobody on the street can see Ace. Grace's eyes and ears work "only for Ace inside her house", and the patrol isn't built [read: `GraceAtHome` header; Night 0 spec §6]. The robot walked to Grace's step in 12 s, took Barnaby and walked back in 11 s. That's 66 s from the back door to "Call it a night", with zero risk [measured: night-zero-check report, 28.0–68.6 s].
- **Night 2:** the lab waited 52 s on her stoop until her lamp went off (12:20 AM), then sneaked in, took the cups and was out in 9 s. She woke 0 times and her "?" never showed [measured: night-two-check-cups report, 27.4–88.2 s].
- **Going in while she's up is the risky option, and it pays nothing extra.** The cups are 2.7 m from her armchair and their rustle carries 3 m. Taking that risk gets you the same cups, the same lesson and the same morning [read: break-ins spec §6, `GraceNight`].
- **The morning's nerve test is one press.**
  - The needle crosses the bar in 1.1 s (0.95 s for the cups).
  - The hold band is 28% of the bar, 38% with Nerve.
  - You get 5.5–6 s to wait for a good pass [read: `NightThings`].
  - The lab held it after 0.7 s. A crack costs Grace's suspicion +1 [measured: Night 1 straight-face and cracking reports].
- **Danger is invisible until she's already noticing.** Steps carry 4 m walking and 1 m sneaking, but nothing on screen says how loud or how lit Ace is. Her "?" only appears once she has heard or seen him [read: `NightNoise`, `NoticeMark`].

**Why it matters.**
- **Readability.** Thief's designers built the light gem because players "didn't necessarily know what the guard was thinking, or how well you were hiding" ([Game Developer](https://www.gamedeveloper.com/design/building-the-original-i-thief-s-i-revolutionary-stealth-system)). Mark of the Ninja draws each sound as "a spreading blue ring that says exactly how far that noise will reach", because otherwise these are "questions players can only learn the answers to through failure and risk-taking" ([Game Developer](https://gamedeveloper.com/design/reinventing-stealth-in-2d-with-i-mark-of-the-ninja-i-)). Here neither noise nor sight is drawn.
- **Risk you can skip for free isn't nerve.** On the careful route, readability doesn't even matter, because she's asleep. It's not horror (good), but it isn't nerve either; it's a detour [inferred].

**Fix (tuning or content; his call, because the calm night is his tone).** Make waiting cost night time with what already exists: have her fall asleep later, so the safe window sits close to her 2:40 water trip and the 4 AM dawn. And narrow the morning band or speed the sweep for the cups.

### 3. The authored rushes reach the counter one phase late, and every day opens with about 40 s of nothing (Day pillar)

**What happens.**
- **The lag.** Walk-ins spawn 18–21 m down the street (or by the car park), walk 12–39 s to the door, then 10–18 s to the till. From spawn to counter takes 25–52 s, median 38 s [measured: Day05 CSV `arrived_s` plus the trace's queue times].
- **The shape inverts.** Day 5 is authored as Opening / Morning rush / Lull / Lunch rush / Last orders. At the counter it arrived as **0 / 2 / 3 / 1 / 3** customers: the lull was the busiest stretch and the lunch rush the quietest. Day 3 shows the same: its "real midday rush" brought 2 to the counter, and the Recover phase brought 3 [measured: traces].
- **The dead opening.** The first customer reached the till at 11:30 AM on Day 5 (41 s in, 23% of the day), 11:21 AM on Day 3, and 11:25 AM on the idle Day 1 [measured].
- **It does rise.** Through Day 5 the counter got busier: 2, then 3, then 4 arrivals per minute. That's rising pressure, but as a drip, not two peaks [measured].

**Why it matters.** Pressure reads as a rush you can see coming and a breath after it. The occupancy doc says it in one line: "chaos is only chaos measured against calm". At 180 s, a 38 s lag is 21% of the day, enough to invert the shape you authored; at the 420 s ship length it would be 9%. And 40 s with nothing to do at the open is the first thing a veteran notices.

**Fix (data).** Start each phase about 35 s earlier, or spawn walk-ins nearer the door, so the counter sees the shape you authored. Get the first customer to the till within about 10 s of opening.

### 4. The bench hides its cleaning payoff and ends twice (Bench pillar)

**What happens.**
- **The cloth covers about two-thirds of the screen** while you wipe and when "Fixed!" fires. The smudge thinning and the gleam both happen under it; the tooltip's "78 %" is your only read of progress [seen: Bench 3 photos 13a, 13 and 14].
- **The repair has two endings.**
  - "Fixed!" fires in pale green on the freshly lit white screen (low contrast) the moment the new screen seats. The cover is still on the mat and both screws are still out [seen: 08].
  - You then refit the cover and two screws. That took the robot 5.8 s and will take him longer.
  - "Back together · Perfect" then fires in orange, over a "Screw: Hold to unscrew" prompt [seen: 10; measured: report lines 71 and 87].
  - The device can't be handed back until it's whole (`JobBase.CanHandBack`).

**Why it matters.**
- **ReStory sells the whole cycle, with a reward on every fit.** Its store page: "take each gadget apart, clean it, replace faulty parts, and assemble it back together" ([Steam](https://store.steampowered.com/app/3812600/ReStory_Chill_Electronics_Repairs/), 95% of 5,565 reviews positive). Automaton: "slotting in pieces always rewards you with the appropriate (and satisfying) audio cues" ([Automaton](https://automaton-media.com/en/?p=55490)).
- **Here two rewards are spent badly.** The clean glass is the cleaning job's reward, and it's covered. The last screw is the repair's reward, and it comes after the climax.
- **Repetition is ReStory's own weak point.** A reviewer: "It didn't take too long for me to be bored of brushing, and to become sad when screwing" ([Loot Level Chill](https://lootlevelchill.com/?p=16223)). So every beat here needs its payoff.

**Fix (one line each).** Lift the cloth off the glass when a smudge clears. And pick one ending: for example, the device wakes when the part seats and "Fixed!" fires when it's back together. The spec chose "Fixed!" at the last part, so this one is his call.

### 5. The first upgrade is about 1.3 of his days away and buys about a second (economy feel; weakens "a loop you get better at")

**What happens.**
- **Price against takings.** The cheapest upgrades are Faster Machine $300, Wide Brush $300 and Magnetic Driver $350 [read: decoded `costPerLevel`]. His played days earned $182–374, median $226 [measured: Days 2–4 and 10]. So the first purchase is 0.8–1.6 days away, median 1.3. Progression's target is half a day (the `UpgradeEconomyPass` header), a target set against $476–963 days on 28 Aug.
- **What each one buys.**
  - Magnetic Driver: Bench 3's cracked screen spends about 2 s turning its four screws, so level 1 (−30%) saves about 0.6 s a repair.
  - Wide Brush: saves about 0.8 s of a 2.8 s scrub.
  - Faster Machine: saves 0.75–1.75 s a drink.
  - [measured: Bench 3 timestamps; read: `valuePerLevel`; inferred]. None of them touches what actually loses customers (finding 1).
- **Money is lumpy.** One Perfect cracked screen for a Sentimental customer pays $137.50 plus up to $115 in tip, more than his median day. A Human fault pays $31 [read: payouts, ×1.25 for Perfect, tip ≤ 60% × speed × mood].

**Why it matters.** The economy pass's own loop is "What screwed me today? → buy the fix → tomorrow I'm ready". Today what screws you is standing customers, and nothing in the shop answers that; what's on sale shaves a second. PlateUp! changes tomorrow's kitchen every day ("When each day starts up, you get a number of random items", [TV Tropes](https://tvtropes.org/pmwiki/pmwiki.php/VideoGame/PlateUp)).

**Fix.** Re-measure a day's takings on human days (ideally 420 s long), then price level 1 at half of it ($110–190 on today's numbers). Then check whether any level-1 item changes a single outcome.

## Notes in his format

### Day
- **Day 5, 9:00–11:30 AM — F — [measured]** Nobody reaches the counter for the first 41 s. The first walk-in spawns 20 m down the street at 9:31 and gets to the till at 11:30 (Day 3: 11:21; the idle Day 1: 11:25). *(traces 145339, 150206, 161003)*
- **Day 5, 13:11–15:23 (the Lull) — F — [measured]** This was the busiest stretch: 3 at the counter (14:24, 14:40, 15:15). The lunch rush (15:23–18:01) brought 1 (16:26); last orders brought 3 (18:46, 19:08, 19:40). *(trace 145339)*
- **Day 5, 14:44–16:10 — F — [measured]** Mansoor (Rushed) storms out 23.3 s after "yes". Omar and Deshawn (Rushed) also at 23.3 s; Nina (Impatient) at 26.4 s. *(Day05_customers.csv)*
- **Day 5, 15:55 — L — [seen]** The warning reads well from above: the ticket goes red ("Losing patience") and a red burst shows over his head. He's gone 4 s later, and no move would have saved the repair. *(frame at trace 106 s)*
- **Day 5, all day — F — [read]** These are autopilot artifacts, not game bugs. Tomas's Human fault ("Check the mute switch") was never done, so he stood 50.7 s at the till. Hold calls were never dialled: "[E] Call support" sat on screen from 0:35 to 2:05 of the video.
- **Day 5, 8 PM — F — [seen]** The phone: "$65 · no stars yet · 9 reviews today · −3 reputation". Even an instant server's day loses reputation, all of it from standing customers. *(day5-lab-recap-phone-8pm.jpg)*
- **Day 5, 8 PM — L — [seen]** Each review line on the phone carries 1–5 stars (Priya ★★★★★, Mansoor ★). The reputation spec decided reviews would be verdicts, not stars, so "stars" means only the café (§10.2). For playtester-first-hour and UI.
- **Day 3 stress, 10:47–19:20 — F — [measured]** The room held through all five stress tests: a 2-customer burst at the door, 6 patrons at once, the door blocked 15 s, every seat filled (24 patrons inside at the peak), the aisle blocked 25 s. The queue never went above 2, and no customer was stuck longer than 0.2 s. One patron was stuck 35 s by the front door (15:14–17:21) while 3 customers got past. *(trace 150206, stuck flags)*
- **Days 3 and 5 — F — [measured/read]** No customer ever stood for want of a seat: 24 seats, at most 14 patrons, 3 seats always kept free (`PatronSpawner`). The "café competes for your space" squeeze never fires. Only the two standing types stand, and they choose to.
- **Any day — F — [measured]** Stations as reach works: 9 presses per drink and 9 per repair, with 0 station presses in first person (it was 4 and 3). Four of each nine are the conversation ("talk, hear them out, hear them out, take the request"). On the tenth coffee of the day, those four are busywork. Letting lines advance on their own reading time wouldn't break the reveal lock. *(stations-first-person report)*
- **Any day — F — [measured/inferred]** Walking is cheap at Ace's 5 m/s: till to bench 4.0 m (0.8 s), till to dispenser 3.9 m (0.8 s), bench to dispenser 7.4 m (1.5 s). The repair bar added 0.7–1.1 m per leg, about 0.2 s, not "about a second". The counter's front is solid from the west wall to x 4.7, so the only way to the floor is round its east end. If E can't reach across the counter, the bench to a repair-bar stool is about 20 m (4 s), and to the west standing spot about 23 m (4.6 s). The stool at x −3.2 took 4 of the 7 seated waits in the traces. *(stand points: stations report; reach 2.2 m: `PlayerInteractor`)*
- **Bench 3 (lab, Day 1 clock), 9:18 AM — S — [seen/inferred]** The hint reads "Cracked screen: Remove the back cover first". The back comes off so the screen can lift out of the front, and nothing inside links the two (a flex connector shows, but nothing gets unplugged). A ReStory player will read it as a gate, not a cause. *(01, 06, 07)*
- **Bench 3, 10:02 AM — F — [seen]** Screws land on the mat about 2 cm from their holes, and the holes turn into dark rings. You always know which screws are out. Keep. *(04)*
- **Bench 3, 10:07 AM — L — [seen]** The pry tool stands upright on the middle of the back like the screwdriver, instead of levering at the seam. *(05)*
- **Bench 3, 10:34 AM — L — [seen]** "Fixed!" is pale green on the lit white screen: low contrast at the one moment that should pop. *(08)*
- **Bench 3, 10:54 AM — F — [seen]** "Back together · Perfect" sits over "Screw: Hold to unscrew". The finished state still offers to take it apart. *(10)*
- **Bench 3, 11:21–11:31 AM — F — [seen]** The cloth covers about two-thirds of the screen. The smudge, the gleam and half of "Fixed!" are under it. *(13a, 13, 14)*
- **Bench (pocket watch) — L — [seen]** The big blue "#2" job tag floats over the watch's dial in the close-up, right on the thing you're repairing. *(stations repair-5-working-on-it.jpg)*

### Night
- **Night 0 (Monday), 11:00 PM — S — [seen]** The reveal lands: the lamp, the push-in, the far lid, the man rising, 5.4 s from the bag going in to "You didn't see me." Keep it exactly. *(night-zero 05, 06)*
- **Night 0/1, 11:04–11:56 PM — F — [measured]** Barnaby is just a walk: 12 s there, 11 s back, and nobody who could see Ace. 66 s from the back door to "Call it a night". *(night-zero report)*
- **Night 2 (Tuesday), 11:14 PM–12:25 AM — F — [measured]** The lab stood on her stoop for 52 s until her lamp went off (12:20 AM), then was in and out in 9 s. She woke 0 times. *(night-two-check-cups report)*
- **Night 2, 12:19 AM — L — [seen]** From the night overhead I can't pick out her window, or tell whether it's lit or dark. The lab read her state from code. Needs his hands. *(04b, 04c)*
- **Night 2 — F — [read]** Noise has no on-screen read. Walking carries 4 m and sneaking 1 m (2 m and 0.5 m after Soft feet); the cups' rustle carries 3 m. You only find out when her "?" appears. *(NightNoise.cs)*
- **Night 2 — F — [read]** Close to her in a lit room, her mark fills in 1 s if Ace walks and 2 s if he sneaks. From "Hm?" (a third) to "Caught." is 0.67 s walking, 1.33 s sneaking. One fair rule: sound alone can never catch you (it's capped at two-thirds). *(GraceNight.cs)*
- **Day 2 and Day 3 mornings — F — [read/measured]** The straight face is one press: a 0.95–1.1 s sweep, a hold band of 28–38% of the bar, and 5.5–6 s to wait for a good pass. The lab held it after 0.7 s. *(NightThings.cs; Night 1 straight-face report)*
- **Night 2, 12:52 AM — S — [seen]** The return pays off: four cups on his crate, "You walk like a cop. I can fix that.", then "Learned: Soft feet." His corner shows the story so far. *(night-two cups 08)*
- **Night 3 — S — [read]** After the cups, his next favour is the cones, which aren't in the game yet, so Night 3 is "Nothing tonight". The night's pull stops after Night 2. *(cups report, 105.6 s)*
- **Day 3, his mess morning — F — [measured]** On the third skip, his cups block 8 of 24 seats across 3 tables, each cleared with 1 press, and his 1-star review counts (−2). Since seats are never short, the mess costs 3 presses, not customers. *(night-two-check-note)*

## The numbers

### A. Lab days (the autopilot: an instant, tireless server)
| | Day 5 (Game view) | Day 3 + stress | Day 3, 15:35 (no video) |
|---|---|---|---|
| Arrived / accepted | 9 / 9 | 10 / 7 (2 declined, 1 shelf full) | 8 / 8 |
| Served (the visit ended happy) | 4 | 4 | 7 |
| Stormed out after "yes" | 5 (4 standing, plus Tomas: artifact) | 3 (all standing) | 1 (standing) |
| Stormed out in the queue | 0 | 0 | 0 |
| Patience at exit, served | 0.0, 0.4, 0.6, 0.9 | 0.4, 0.6, 0.7, 0.9 | 0.6–0.9 |
| Longest wait after "yes" | 59.0 s (Priya, seated, served) | 69.3 s (Saamin, seated, served) | 49.1 s (Beatriz, seated, served) |
| Money (patrons only: the autopilot pays no jobs) | $65 | $145 | $65 |
| Reputation | −3 | +2 | +12 |
| In the queue: mean / max / share of time empty | 0.36 / 2 / 64% | 0.21 / 2 / 81% | — |
| Active customers: mean / max | 3.3 / 6 | 3.6 / 5 | — |
| First customer at the till | 11:30 AM (41 s) | 11:21 AM (39 s) | — |
| Idle (nobody at the till, no job open): share / longest | 30% / 41 s (the opening) | 23% / 39 s (the opening) | — |

How the autopilot bends these: the queue is a floor; idle is a ceiling; "served" flatters, because the autopilot never fails a seated job; and standing storm-outs are forced by its 30 s minimum repair.

### B. Standing against seated waits, all the evidence
| | Standing (Impatient, Rushed) | Seated (everyone else) |
|---|---|---|
| Lab (3 days) | 9: 8 stormed out (23.3–32.2 s after "yes"), 1 served at 0% | 14: all 14 served |
| His days | 9: all 9 stormed out (23.3–33.1 s); 5 of 5 on the days he played | 16 on played days: 11 served, 2 repaired but left waiting for their drink, 3 stormed out after 86–136 s |
| Patience after "yes", from the code (Day 3 / 4 / 5) | Impatient 27.4 / 26.0 / 24.7 s; Rushed — / 22.3 / 21.1 s | Day 5: Cheerful 81 s, Sentimental 74 s, Chatty 88 s |
| With a coffee (if they asked for one) | Impatient, Day 5: 24.7 → ~38 s | Cheerful, Day 5: 81 → ~112 s (+31 s, not double: the 0.5× drain lasts 20 s, then 0.8×) |
| Share of walk-ins | Day 5 lab 5/9; his Day 4 4/10; 2 of the 5 archetypes in the full cast | |

### C. Repair and drink times
| | Seconds | Source |
|---|---|---|
| Cracked screen, perfect robot: first tool → "Back together · Perfect" | 16.4 (screws about 2 s; "Fixed!" 10.6 s in) | [measured] Bench 3, 16.2–32.6 s |
| The same repair on the day's clock, including the check's zoom and spin tests | 28 | Bench 3, line 89 |
| Gunked phone (2 grime spots, 1 smudge), robot | 7.0 | Bench 3, 35.3–42.3 s |
| A coffee, "talk" → served, robot, customer at the counter | 9.0 | stations, 19.3–28.3 s |
| A repair's overhead without the bench (talk, carry, open, hand back), robot | about 6 | stations report |
| His repairs, "yes" → walked out (Days 2–4, 10) | 40.8, 59.1, 63.4, 74.5, 83.8 (median 63); Human fault 5.1, 6.4 | [measured] his CSVs |
| His longest waits | Grace's camera 136 s (Day 1, stormed out); Deshawn 108 s; Alishba 105 s | his CSVs |
| GDD target per repair | 45–90 | GDD v3, Part 3 |

### D. The Day 5 rhythm (lab)
| Authored phase (spawn every) | Clock | Spawned | At the door | At the counter |
|---|---|---|---|---|
| Opening (16 s) | 9:00–10:39 | 2 | 0 | 0 |
| Morning rush (7 s) | 10:39–13:11 | 3 | 2 | 2 |
| Lull (26 s) | 13:11–15:23 | 1 | 3 | 3 |
| Lunch rush (6 s) | 15:23–18:01 | 3 | 2 | 1 |
| Last orders (15 s) | 18:01–20:00 | 1 | 2 | 3 |

- **The lag:** spawn → door 12–39 s (median 22); door → till 10–18 s (median 13); spawn → till 25–52 s (median 38).
- **Arrivals at the counter per minute:** 2, then 3, then 4.
- **The cap:** 6 active customers, reached 2.7% of the time.

### E. His own days (older builds)
| Day | Arrived | Served | Stormed out: queue / after "yes" | Money (tips) | Reputation |
|---|---|---|---|---|---|
| 1 | 2 | 0 | 1 / 1 (Grace's camera, after 136 s) | $60 ($0) | −3 |
| 2 | 3 | 3 (2 Perfect repairs) | 0 / 0 | $249 ($55) | +6 |
| 3 | 7 | 5 (2 Perfect repairs) | 0 / 2 (both standing) | $203 ($41) | +6 |
| 4 | 10 | 4, plus 1 left unhappy (2 Perfect, 1 Good) | 0 / 6 | $374 ($63) | — |
| 10 | 5 | 1, plus 1 left unhappy (2 Perfect) | 1 / 3 | $182 ($41) | — |

- **The ramp rises:** 2, 3, 7, 10 arrivals across Days 1–4 [measured].
- **Days 5–9 and 11–15:** he served nobody. Unheard customers stormed out of the queue 34–69 s after reaching the door.

### F. Walks (Ace moves at 5 m/s)
| Leg | Metres | Seconds |
|---|---|---|
| Till ↔ bench | 4.0 | 0.8 |
| Till ↔ dispenser | 3.9 | 0.8 |
| Bench ↔ dispenser | 7.4 | 1.5 |
| Bench → a repair-bar stool, round the east end | ≈ 20 | ≈ 4.0 |
| Bench → the west standing spot (−6.2, 10.9), round the end | ≈ 23 | ≈ 4.6 |
| Dispenser → a stool, round the end | ≈ 13 | ≈ 2.6 |

The stand points are measured (stations report); the round-the-end paths are [inferred] from coordinates. Hand-back reach is 2.2 m [read]. A stool's waiting spot is about 2.4–2.6 m from the nearest place behind the bench.

### G. The nights
| | |
|---|---|
| Night 0 and Night 1's errand (robot): back door → "Call it a night" | 66 s; reveal 5.4 s; deal 12.2 s; 23 s of walking for the errand; 0 observers |
| Night 2 (robot): stoop → she's asleep → in → out | waited 52 s (11:14 PM–12:20 AM); 9 s inside; woken 0 times |
| Grace's sight | 110° cone (60° while she watches TV), 7 m lit / 3 m dark. Full mark in 1 s close and lit, 3 s at the edge of her view. Sneaking or the TV halves the rate; the torch doubles it |
| Grace's hearing | walking 4 m, sneaking 1 m, rustle 3 m, lock pick 4 m, creaky stair 5 m. Soft feet halves your steps. Sound alone caps her mark at two-thirds |
| The morning meter | sweep 1.1 s (gnome) / 0.95 s (cups); hold band 28% / 24% of the bar (Nerve widens the green 1.45×); 6 / 5.5 s to press; one press |
| Caught (spec; not run today) | $80 bail, late opening, −1 star for 5 days, −20 reputation, Grace −2 |
| Skipping his favour | 1 skip: he holds a table until 1:30 PM. 2 or more: his cups on 8 of 24 seats. The 3rd: also a 1-star review (−2) and the Officer's harder question |

### H. The economy
| | |
|---|---|
| Upgrades (level 1 / 2 / 3, $) | Faster Machine 300 / 800 / 1,800 · Wide Brush 300 / 750 · Magnetic Driver 350 / 900 / 2,000 · Bulk Restock 400 / 1,000 · Shelf Space 600 / 1,500 · Bigger Bench 900 / 2,400 |
| Repairs (base pay) | Phone: cracked screen $110 · gunked $45 · frozen boot $45 · hold call $60 · silent calls (Human) $25. Watch: debris $75 · seized $160. ×1.25 Perfect, ×1 Good, ×0.6 Passable; tip ≤ 60% × speed × mood |
| Drinks | Coffee $3 · Tea $3 · Espresso $4 · Americano $4 · Hot Chocolate $5 · Latte $6 · patrons pay $5 each |
| Money out | Restock (20 cups + 20 beans) $30; bail $80 |
| His day against the first upgrade | median $226 → $300 is 1.3 days (range 0.8–1.6); target 0.5 |

## What feels right
- **The screw.** It backs out along its own axis, part-way stays part-way, it hops free and lands 2 cm from its hole, and the hole stays dark until the screw is back. ReStory's "is it in or out?" problem is solved. *(Bench 3, 03–04; report lines 30–36)* [seen/measured]
- **The before and after you can see.** The broken screen lies cracked in the tray beside the fresh one, then the phone wakes. *(07, 08)* [seen]
- **The man.** Night 0's reveal and his corner (the gnome facing the street, four cups on a crate) are the strongest "one more night" in the game. *(night-zero 05–06; cups 08)* [seen]
- **Stations as reach.** 0 station presses in first person. The juice reads from above at the default zoom ("+$6 +$3 tip", the "Fixed!" sparks). *(stations report; juice 30, 60)* [measured/seen]
- **Fair rules at night.** Sound alone can never get you caught, and her "?" warns at a third before "Caught." *(GraceNight.cs)* [read]

## Needs his hands (questions for tonight)
1. Time one cracked-screen repair, from the shelf to handing it back. The robot needs 16 s of hands, and a seated customer lasts 74–88 s on Day 5. Where did your seconds go?
2. On Day 3 or later, take one Impatient or Rushed repair (they say "How long?", "Can you do it now?" or "I'm late") and go straight to it. Did you save them? Did it ever feel winnable?
3. Hand a repair back to someone standing at a repair-bar stool. Did E reach them across the bench, or did you walk round the counter's east end?
4. In the bench close-up, how did you know someone new had reached the till: a sound, a bark, the tickets, or nothing?
5. Cleaning a phone: could you see the smudge go and the gleam, or was the cloth in the way?
6. "Fixed!" or "Back together · Perfect": which one felt like the end of the repair? Did refitting the cover and screws feel like a reward or a chore?
7. Did the day have a rush and a lull you could feel? How long after "Open" did your first customer reach you?
8. Night 2: did you go in while Grace was up, or wait for her window to go dark? Could you tell her window from the street? How long did you wait, and was it tense or dead time?
9. If she caught you: did you see the "?" before "Caught."? Could you have got away?
10. The straight face: did it feel like holding a straight face, or like a timing button? Did you ever crack without meaning to?
11. At Grace's: could you hear your own steps, and tell walking from sneaking by ear alone?
12. At the Day 3 or Day 4 recap: what did you want to buy, and could you afford it? After buying it, did the next day feel any different?

## Runs I'd need
1. **A human day with the recorder.** He plays his own Day 3–5 while the producer runs `Café life › Record 8 minutes (Game view)`. From `trace.csv` I'd measure Ace's walks (A rows: metres per order, trips round the counter), the queue while he's at the bench, standing against seated outcomes, and idle time. This replaces the autopilot's guesses with his hands.
2. **Grace's cover and being caught.** `Night › Break-ins 7 - …` (Grace at home, at night) and `Night › Break-ins 8 - Grace's cover (lab, a check)`. I'd look at the "?" filling, her turning and coming to look, "Caught.", and the next morning (bail, the late opening, the scandal's row on the phone). I'd measure "Hm?" → "Caught." in a lit room and behind cover.
3. **Day 5 lab at human repair speed.** The same lab session with `CafeLabDirector.repairVisit` set to 45–90 s in the Inspector before starting, recorded: what the queue and the seats do when repairs take human time.
4. **Day 5 at 420 s** (the ship length), autopilot, recorded: counter arrivals per phase, to see whether the two-rush shape survives the ~38 s spawn-to-till lag.
5. **The idle Day 1 again, left to close,** so its CSV matches its trace.

## Flags
- **The bench, spec against build.** The bench spec §6 D (his call, 7 Oct) says a device handed back with its cover off costs nothing on the grade. The build won't hand back an opened device at all (`JobBase.CanHandBack`; Bench 3 lines 37 and 60). As a veteran I prefer the build (ReStory's "everything you take apart must be put back together"), but it's his call.
- **The occupancy doc** says a coffee takes a seated customer from 75 s to about 150 s. In code the 0.5× drain lasts 20 s, then 0.8× (`drinkingSeconds` 20, `satisfiedDrain` 0.8): the coffee is worth about +30 s.
- **The redesign doc** says the repair bar adds "about a second per repair trip". At Ace's 5 m/s, the measured legs add about 0.2 s.
- **The economy pass** sets level 1 at half a day against $476–963 days (measured 28 Aug); his logged days now earn $182–374.
- **The phone** shows stars on review lines, as the playtest-3 plan (§5.3) built it. The reputation spec's decision of "verdicts, not stars" (§10.2) was never formally reversed.
- **The README's Day 1 line** ("Grace, then Walter", $65) matches neither the CSV beside the recording (Grace and the Officer, $70) nor the trace (four customers).
- **No new mechanic is proposed.** Every fix above is a number, a data field or content.

## Sources
- ReStory: [Steam store page](https://store.steampowered.com/app/3812600/ReStory_Chill_Electronics_Repairs/), [Automaton](https://automaton-media.com/en/?p=55490), [Loot Level Chill review](https://lootlevelchill.com/?p=16223), [Notebookcheck](https://www.notebookcheck.net/Why-this-repair-simulator-with-95-positive-reviews-is-blowing-up-right-now.1367444.0.html)
- PlateUp!: [TheGamer](https://www.thegamer.com/plate-up-how-to-reach-day-15-final/), [TV Tropes](https://tvtropes.org/pmwiki/pmwiki.php/VideoGame/PlateUp)
- Papers, Please: [Wikipedia](https://en.wikipedia.org/wiki/Papers,_Please)
- Mark of the Ninja (Nels Anderson): [Game Developer](https://gamedeveloper.com/design/reinventing-stealth-in-2d-with-i-mark-of-the-ninja-i-)
- Thief's light gem: [Game Developer](https://www.gamedeveloper.com/design/building-the-original-i-thief-s-i-revolutionary-stealth-system)
