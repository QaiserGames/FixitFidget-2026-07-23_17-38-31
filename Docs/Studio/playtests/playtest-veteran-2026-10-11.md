# Playtest: the genre veteran, run 2 — 11 October 2026

*Build `playtest-2` at `ceaf81d` (the six playtest-4 fix leads, integrated in seven commits). Played from the producer's evidence pack (`playtest-pack-2`); I never touched Unity. Evidence tags as in the protocol §1. An autopilot day is not a human day; where I use one, I say how it bends the number. My analysis scripts are in `playtest-pack-2/work-veteran/`.*

## Verdict

**Feels like a solid game to a genre veteran: Not yet**, because the night's nerve is still opt-in. On the route a careful player takes, nobody can see Ace on Night 1, Grace is asleep on Night 2 (now after 116 s on her stoop instead of 52 s), and Night 3 is "Nothing tonight". The day and the bench each moved up.

- **Bench (ReStory's bar): Solid with fixes.** One ending, the cloth lifts off the clean glass, the pry levers at the seam. Left to fix:
  - "Fixed!" fires over the phone's back, 4 s after the screen lit up with no words.
  - The hint names a cable no verb touches.
  - No instrument has played Grace's camera, the hardest job.
- **Day (hectic, not cozy-only): Solid with fixes** (was Not yet). Everyone sits and can be saved in principle. The first customer reaches the till about 14 s in. Rush, lull, rush now reaches the counter (cleanly at the ship's 420 s). Left to fix: at human speed the last third buries one pair of hands (5 waiting at the peak, all 4 losses after 8 PM). Reassure is cheap only at the one stool customers actually use, and a coffee helps only those who ordered one.
- **Night (nerve, not horror): Not yet.** It reads now: the noise rings, her window from the street and her "?" make "caught" your own mistake. But the careful route still carries zero risk. The fix made it longer, not riskier.

## Since run 1

Every one of my run-1 top five and every note I tagged F (run 1 had no B notes). Where one run-1 item had two parts that moved differently, it has two rows.

| Run 1 said | Now | Evidence |
|---|---|---|
| **#1** Impatient and Rushed *choose to stand* (1.15×) and last 21–27 s after "yes" | **fixed** | 8 of 8 waits seated on the human-speed Day 5 and 9 of 9 on Day 3 (Priya's Human fault stays at the counter) [measured: CSVs `wait_kind`]. Rushed now lasts 42.2–43.3 s after "yes" [measured: Day05 `service_s`]; the code says 40.5 s seated [read]. |
| **#1** …and every repair takes longer than their patience | **still true at human pace for bench repairs** | Human-speed Day 5: Rushed bench repairs 2 of 2 lost (Ali, Beatriz, 42.6 s). Rushed support calls 2 of 2 saved, at 0.1 patience (they park) [measured]. Lab repairs take 45–90 s against 40.5 s. Reassure from the bench reaches only the x −3.2 stool (top five #2). His own hands would settle it; see *Runs I'd need* 1. |
| **#2** The night's risk is optional | **worse** (for the careful player) | Night 2: waited 116 s on her stoop (was 52), 8.2 s inside, woken 0 [measured: night-two-check-cups]. Same zero risk, twice the dead time. Night 1 unchanged (below). |
| **#2** …danger is invisible until she's already noticing | **fixed** | Rings: 1 m sneaking, 4 m walking, 3 m the cups' rustle, drawn on her floor while she's home; 18b shows the walking ring reaching her chair as she says "Hello?" [seen]. |
| **#2** …the morning's nerve test is one lenient press | **still true** (for a careful player) | Cups: green 0.145 (0.210 with Nerve); the lab held it 0.5 s after the meter appeared [measured]. The night lead's simulation: a press after watching one sweep holds 96% (was 99.8%); a hasty one 69% (was 99.9%) [read: `fixes/night.md` N2]. Barnaby's meter unchanged. |
| **#3** The rushes reach the counter a phase late; about 40 s of nothing at the open | **fixed** | First at the till 12.9–15.6 s in six runs (Labs C/D/E 14.0/14.2/13.4; human-speed Nina 14.1; Day 3 Yuki 12.9; Day 1 Grace 15.6) [measured]. At the ship's 420 s: 1/7/2/6/5 per phase (Lab D). At 180 s it's still noisy: Lab C 1/2/2/3/1 (morning rush = lull). |
| **#4** The cloth covers the cleaning payoff | **fixed** | The pad hides 18% of the glass (was about ⅔). It lifts 38 mm when the smudge clears, with 100% of the spot in view 0.30 s later; the gleam and "Fixed!" land on lit glass [measured: Bench 3; seen: 13b, 14]. |
| **#4** …and a repair ends twice | **fixed** (one new wrinkle) | One "Fixed! · Perfect" after the last screw, endings counted: 1 [measured]. The wrinkle: it now lands on the phone's back (top five #4). |
| **#5** The first upgrade is about 1.3 of his days away and buys about a second | **still true** | Prices and payouts unchanged (`Upgrade_*`: $300/$300/$350 first levels). Deferred by design until his own days are measured [read: README "Not built"]. |
| F: Day 5, nobody at the counter for 41 s | **fixed** | As #3. |
| F: Day 5, the lull was the busiest stretch | **fixed** | The human-speed recording: 1/3/1/3/1 at the till, plus 2 after close [measured: trace]. |
| F: Rushed storm out 23.3 s after "yes" | **fixed** (the 23 s); bench repairs still lost | As #1. |
| F: autopilot artifacts (Human fault never done, calls never dialled) | **fixed** (instrument) | Day 3: Priya's Human fault served in 6.6 s. Human speed: 4 support calls dialled, answered and handed back [measured: CSVs]. |
| F: −3 reputation even with an instant server | **fixed** | Fast autopilot Day 3: 10 "Loved it", +20. Human speed: 0 (2 Loved, 2 Liked, 2 Let down, 2 Never again) [measured: summaries]. |
| F: the room held under stress | **still true** (good) | Longest stuck customer 0.7 s; one patron stuck 7.4 s in the doorway when 24 patrons were sent in at once [measured: stress.py]. |
| F: nobody stands for want of a seat; the squeeze never fires | **still true** | 24 seats, patrons peak at 10 on Day 5; Sit 4: "nobody waited standing while a seat was free" [measured]. His call (Systems flagged it). |
| F: 9 presses per drink/repair, 4 of them talk | **fixed** (my number was partly the instrument) | 8 a drink and 7 a repair from above, 7 and 7 in first person; 2 of each are talk. The replies come up by themselves in 0.4–0.9 s. My "4" was the lab pressing every 0.45 s [measured: Stations A/B]. |
| F: a hand-back to a repair-bar stool was about 20 m round the counter | **fixed at the stool; still true on the floor** | "Ace walked 0.00 m" at the stool [measured: Stations A/B, with "the rest of the room held"]. 6 of 8 waits on the human-speed day were on the floor, 13.6–20.9 m from the bench [inferred from coordinates]. |
| F: screws land 2 cm from their holes; keep | **still true** (good) | 2.4 cm [measured: Bench 3]. |
| F: "Back together · Perfect" over "Hold to unscrew" | **fixed** | A finished screw reads "Screw" with no verb and won't turn (0% out) [seen: 10b; measured]. |
| F: the cloth covers about ⅔ of the screen | **fixed** | As #4. |
| F: Barnaby is a walk, nobody can see Ace | **still true** | About 6 s to her step, 6.8 s back, 0 observers. Back door to "Call it a night": 59.6 s (was 66) [measured: Night 1, Night 0 reports]. |
| F: Night 2, 52 s on the stoop, in and out in 9 s, woke 0 | **worse** | 116 s, 8.2 s, woke 0 [measured]. |
| F: noise has no read on screen | **fixed** | As #2. |
| F: "Hm?" → "Caught." 0.67 s walking / 1.33 s sneaking (from code) | **still true** walking (0.91 s); **can't tell** sneaking | Break-ins 7 timed 0.91 s walking behind her in her lamplight [measured]. A sneaking catch at range wasn't timed (*Runs I'd need* 6). |
| F: the straight face is one press | **still true** | As #2. |
| F: his mess costs presses, not customers | **still true** [read] | Seats still never short; the stalling labs weren't re-run (`fixes/night.md` §5 says untouched). |

## The run

**What I played** (all from tonight's pack; times are the runs' own):
- **Day 5 at human speed, Game view, 236 s to the day's close.** The trace (65,501 rows), the CSV, 4 full-size screenshots, the contact sheets as a flipbook, and 6 extra frames I pulled at the impatience card, dusk and the storm-outs.
- **Day 3, fast autopilot, the five stress tests** (219 s). Trace and CSV; the whole-café video has block glitches and shows only the east half.
- **A new game's Day 1 with no input** (180 s). Trace and CSV: the opening and Day 1 patience only.
- **The day's shape at the till:** Labs C (180 s), D (420 s), E (human speed).
- **The bench:** Bench 3 (161 checks, all 23 photos), Juice (33), the dispenser frame (13), Stations A/B (88, 86).
- **The night:** Night 0 and Night 0 after Grace walked out (96 each), Night 1's straight face (77), Night 2's cups (107), and Grace at home: her night (127, all 26 photos), her Thursday (24), her cover (31).
- **The recap phones** (Day 1, Day 5), Sit 4's photos, the café-life checks, and the dusty rose door run.
- **His own Days 1–15** (same files as run 1, checked by md5).
- **The data and code:** `Upgrade_*`, `Drink_*`, payouts, `Assets/Data/Days`, and the scene's spawner and archetypes. Code: `CafeLab`, `CustomerBrain`, `CustomerSpawner`, `WaitingArea`, `PlayerInteractor` (through `fixes/systems.md`).
- **The comparison games**, sourced on the web (*Sources*).

**How the instruments bend the numbers:**
- **The human-speed autopilot hears every customer 3–6 s after they reach the till, even while its hands are busy.** So the queue is a floor (empty 77.5% of the day), and the waiting moves to "accepted, seated, waiting for the hands". A player in the bench close-up hears people later.
- **It works first come, first served.** It never reassures, never walks, and queues a drink owed after a support call only once the phone is back, behind every repair. A player would pour it during the 20–30 s hold.
- **Its pace.** Drinks take 20–40 s (the Stations robot needs 8.9 s, talk to served). Repairs take 45–90 s: the GDD's band, and close to his own "yes → walked out" median of 63 s.
- **Bench repairs it ends pay $0 in the lab** (`FinishAndLeave`). The day's $446 is 4 support calls ($300), $76 in tips and $70 from patrons. It can't be set beside his takings.
- **The fast autopilot (Day 3)** works every job in parallel and served everyone. Good for the room, useless for the pressure.
- **Stations' "Ace walked 0.00 m"** came with its customer put on a stool and "the rest of the room held". In natural days, 4 of 17 waits were at the repair bar.
- **The Stations labs (22:32–22:33) ran before the UI commit.** Their first-person photo still shows a blue "#2"; the Day 1 guide at 22:48 reports "0 job tags". I don't judge the job tag from Stations.
- **Not in the pack:** a human-played day, a bold Night 2 run, Grace's camera played by its verbs (Bench 3 does it "by the check's hand"), and a 420 s day at human speed.

## The five that matter most (now)

### 1. The night's nerve is still opt-in, and the fix lengthened the safe route instead of making it risky (weakens the Night pillar most)

**What happens.**
- **Night 2, the careful route** [measured: night-two-check-cups, 26.0–150.6 s]:
  - On her stoop at 11:15 PM. Her front window goes dark after 106 s; her lamp goes off at 1:40 AM, 116 s in.
  - Then 8.2 s from her door to back out with the cups: 14 sneaking rings, 1 rustle, woken 0.
  - From the street, her tea trips don't show: the pavement light follows her standard lamp, which stays on [read: `fixes/night.md` N4; Grace at home 11.8 s]. So unless you watch her through the window in first person, it's about 100 s of nothing, then dark.
- **Night 1** [measured: Night 1 straight-face report, 8.3→14.2→21.0 s]: about 6 s to her step, Barnaby taken, 6.8 s back. Nobody on the street can see Ace. Seeing out of her front window is the lead's option N6.1, not built.
- **The bold route now reads and plays well, and pays nothing extra.**
  - Rings show what she hears, the rustle turns her head, and the bar hides you while crouched [measured: Grace at home 105.1–135.0 s].
  - Same cups, lesson and morning either way [read: `fixes/night.md` §7].
- **The morning.** The cups' face was held 0.5 s after the meter appeared [measured]; a watched press holds 96% [read: lead's sim]. A crack costs Grace's suspicion +1.
- **Night 3** is still "Nothing tonight" [read: cups report 168.0 s].

**Why it matters.**
- Stealth games pay for risk. *Mark of the Ninja* gives "points for executing a variety of tactics", and "this score is reduced when an alert is sounded" ([Wikipedia](https://en.wikipedia.org/wiki/Mark_of_the_Ninja)).
- Here the safe route costs only the player's patience, so boredom is the only lever. A veteran will go in early because standing still is dull, not because the game asked for nerve.
- A cozier player will stand on a stoop for two minutes on every Night 2. Pillar 3 is "nerve"; on the route most people take, there is none.

**Fix (his call; tone).** The lead's N6.1 is the smallest change that puts risk on the route everyone takes: her existing sight, through her front glass, onto her stoop. It's about half a day and uses no new rule [read: `fixes/night.md` N6]. A reward for going in while she's up would be a new rule; it's flagged, not proposed.

### 2. At human speed the last third buries one pair of hands, and the levers that buy time reach only the one stool customers use (Day pillar)

**What happens** [measured: human-speed trace and CSV].
- **The pile-up.** Accepted customers waiting on Ace: 3 or more for 31.5% of the open day, a peak of 5 at 196 s (16 s after 8 PM). Service ran 67 s past closing. All 4 losses came after 8 PM.
- **Demand against supply.** At the lab's pace, the accepted work was about 1.7× what one pair of hands could do between the first "yes" (48 s) and the last departure (239 s) [inferred: about 330 s of hands against 190 s].
- **Rushed customers** last 40.5 s seated after "yes" on Day 5 [read], 42.2–43.3 s measured.
  - A bench repair can't fit at 45–90 s (Ali at a window stool, Beatriz at a bistro: both lost at 42.6 s).
  - Support calls fit because they park (Malcom and Saamin served at 0.1).
- **Reassure** adds +12.2, +7.3 and +4.4 s for a seated Rushed customer on Day 5 [read: `CustomerBrain.Reassure`]. Over the counter it reaches 3.5 m, so from the bench it reaches only the repair-bar stools [read: `fixes/systems.md` S2].
- **Where people actually wait.**
  - Only the x −3.2 stool ever took a waiting customer: 4 of 4 repair-bar waits in tonight's recordings, 0 at −4.2 and −5.2 [measured: seats.py].
  - 6 of 8 human-speed waits were on the floor, 13.6–20.9 m from the bench round the counter's east end. That's 5.4–8.4 s per round trip at 5 m/s [inferred from coordinates].
  - So walking out to reassure a floor-seated Rushed customer nets about +4 to +7 s on the first trip, about 0 on the second, and less than nothing on the third, with the repair stopped while you walk [inferred].
- **A coffee** can only go to someone who ordered one [read: `CustomerBrain` line 1860], and only 15% of Rushed customers order a drink [read: scene archetypes].

**Why it matters.**
- *PlateUp!* is harsher — "If just one guest waits too long, it is game over" ([TheGamer](https://www.thegamer.com/plate-up-how-to-reach-day-15-final/)) — but every bar there is one you could have saved.
- Here a Rushed bench repair accepted while another job is running can only be saved by not taking it. Declining is free (no review), so the decision exists now, which is good.
- But it's made blind (the patience bar is hidden until they wait, a lock) and learned by losing. *Papers, Please* gives "specific rules" up front ([Wikipedia](https://en.wikipedia.org/wiki/Papers,_Please)); here the archetype's line ("I'm late") is the only rule.
- The systems report's "up to about 64 s" with Reassure is true at the stool, which took 1 in 4 waits.

**Fix (data).**
- Find why the −4.2 and −5.2 stools never win a claim (`WaitingArea` clearance or walking distance). Three stools that work would put the cheap levers where the promise was ("the owner sits across the glass").
- Or take the lead's own fallback: Rushed ×0.6 → ×0.75 (50.6 s seated on Day 5).

### 3. A Perfect repair, paid and tipped, then a "Let down" walk-out over the drink (Day pillar: fairness)

**What happens** [measured].
- **Human-speed day:** Mansoor ($75 + $31 tip, Perfect support call) walked out 109.6 s after "yes", waiting for a hot chocolate. Omar ($75 + $33) left at 103.0 s, waiting for a latte. Both reviewed "Let down".
- **His own days:** Day 4, Alishba (Perfect cleaning, $94 + $39) walked out at 104.6 s with a coffee owed. Day 10, Deshawn (Perfect support call, $75 + $21) at 107.9 s with a latte owed.
- **Partly the autopilot,** which queues an owed drink only after the phone comes back [read: `CafeLab.HandBackTheCall`]. But 2 of the 9 repairs he accepted on his played days ended the same way.

**Why it matters.** The player did the hard part perfectly and loses the review over the easy part. "Don't take on more than you can finish" is the right lesson (reputation spec §3), but it lands on a 9 s coffee. A veteran will call it a gotcha unless the owed drink is impossible to miss once the device is back.

**Fix (presentation, if hands confirm it).** The ticket already reads "+ Hot Chocolate" after the device goes back [seen: frame at video 160 s]. If he misses it while playing, make that line louder; no rule changes.

### 4. "Fixed!" now fires once, over the phone's back (Bench pillar)

**What happens.**
- **The cracked screen** [seen: Bench 3 photos 08, 10; measured: report 25.3 s and 29.5 s].
  - The fresh screen seats, and the phone wakes face-up with no words, the back cover on the mat.
  - You flip it, refit the cover and two screws (the robot took 4.2 s).
  - "Fixed! · Perfect" fires on its dark plate (9.2:1, it reads) over the phone's back.
  - For the cleaning job it lands on the lit, clean glass (14), which is right.
- **The hint** "Its cable plugs in under the back cover. Take the cover off first" names a cable that no verb touches. Photo 06 shows the internals with a small connector; there's no unplug step [seen; read: `fixes/bench.md` B6].
- **Grace's camera** is Day 1's job and the hardest in the demo. In Bench 3 its cleaning and shutter swap are done "by the check's hand" [read: report 43.5 s, 44.8 s]. At "Fixed!" its shutter bay reads empty with one blade standing, probably mid-fire [seen: 16].

**Why it matters.**
- *ReStory*'s satisfaction is a reward on every fit: "slotting in pieces always rewards you with the appropriate (and satisfying) audio cues" ([Automaton](https://automaton-media.com/en/?p=55490)). Its loop ends with "Reassemble", then "Complete and send" ([Pro Game Guides](https://progameguides.com/restory-chill-electronics-repairs/restory-chill-electronics-repairs-beginners-guide/)).
- The run-1 problem (two endings) is solved. But the biggest visual reward (the screen lighting) is still spent before the job is done, and the words land on a blank back.

**Fix (presentation, his call).** Let the one "Fixed!" show the working face: wake it again at the last screw, or turn it face-up for the words. Separately, either say what the cover does in words that need no cable, or let the cable stay a story the art shows.

### 5. The reasons to press on thin out by Day 3 (the "one more day" pull; economy feel)

**What happens.**
- **Night 3 is "Nothing tonight"** [read].
- **The first upgrade is still $300:** 1.3 of his days (median $226), and it buys about 0.6–1.75 s [read: assets; measured: run 1, unchanged].
- **After a full Day 5 the phone reads** "8 reviews today · 0 reputation · 5 to your first star" [seen: recap].
- **Shelf Space ($600)** is the one upgrade that answers a loss measured tonight: Deshawn turned away at 5:01 PM, three trays full [measured: CSV].
- **Support calls pay $75 for about 9 s of the lab's hands** (talk 4 + dial 1.5 + answer 1.5 + hand back 2) plus a 20–30 s park [read: `CafeLab`, `HoldCallJob`]. A Perfect cracked screen pays $137.50 for about 16 s of robot hands plus the talk, carry and walk. Per second of hands, the counter jobs out-earn the bench, which is the hero verb [inferred].

**Why it matters.** *PlateUp!*'s pull is that tomorrow is different: "When each day starts up, you get a number of random items" ([TV Tropes](https://tvtropes.org/pmwiki/pmwiki.php/VideoGame/PlateUp)). Here tomorrow's kitchen is the same until about Day 3, and the night's story stops at Night 2.

**Fix.** Measure his takings on human days, then price level 1 at about half a day (as in run 1). Night 3's cones are the lead's 1–1.5 days [read: `fixes/night.md` N7]; that's his call, and it's content, not a mechanic.

## Notes in his format

### Day
- **Day 5, 9:52 AM — F — [measured]** The first customer reaches the till 14.1 s into the day (was 41 s). Labs C/D/E: 14.0/14.2/13.4 s; Day 1's Grace 15.6 s. Keep. *(trace 234000; day-shape reports)*
- **Day 5, 11:37 AM–7:00 PM — F — [measured]** The authored shape arrived at the till: Opening 1, Morning rush 3, Lull 1, Lunch rush 3, Last orders 1, plus 2 after close. *(timeline.py, trace 234000)*
- **Day 5, the rushes — F — [measured]** At 420 s the counter sees each rush at about half its authored rate: 4.3 and 3.6 a minute, against 8.6 and 10 authored. The lull runs at 1.4 a minute. The counter's three places hold new walkers 10–13% of the day. It still reads as rush, lull, rush. *(Lab D report)*
- **Day 5, all day — F — [measured]** Everyone waits seated: 8 of 8 at human speed, 9 of 9 on Day 3 (Priya's Human fault stays at the counter). Patrons peak at 10; the seat squeeze never fires. *(CSVs; seats.py)*
- **Day 5, 1:15 PM–8 PM and after close — F — [measured]** Rushed after "yes": 42.2, 42.6, 42.6 and 43.3 s, seated (was 21–23 s standing). Both support calls survived at 0.1. Both bench repairs were lost: Ali (taken at 7:19 PM) and Beatriz (taken after close). *(Day05 CSV)*
- **Day 5, 3:16 PM — F — [seen]** "GETTING IMPATIENT: Malcom is getting impatient. Walk over and press E to reassure." fired while the prompt read "[E] Answer support" for Malcom's own call, with 14 s left on the ring. The card points 10 m away; the fix was the phone. For UI. *(video frame 88 s)*
- **Day 5, 5:01 PM — F — [measured]** Deshawn (Rushed, cracked screen) was turned away: three intake trays full (two phones on hold, Alishba's). *(CSV)*
- **Day 5, 8:00 PM, "finishing up" — F — [measured]** Saamin and Beatriz reached the till 6.5 s and 11.7 s after close. Service ran 67 s past 8 PM, and all 4 storm-outs came after it. 5 accepted customers were waiting on Ace at the peak, 16 s after close. Is overtime meant to be where the day breaks? His call. *(trace; CSV)*
- **Day 5, after close — F — [measured]** Mansoor and Omar paid for Perfect support calls ($75 + $31, $75 + $33), then walked out "Let down" over a hot chocolate and a latte, 109.6 and 103.0 s after "yes". *(CSV; top five #3)*
- **Day 5 and Day 3, the waits — F — [measured]** Only the x −3.2 repair-bar stool took waiting customers (4 of 4 repair-bar waits; 0 at −4.2 or −5.2). 13 of 17 waits were on the floor, 7.7–11.2 m from the till. *(seats.py)*
- **Any day, the hand-back — F — [measured / inferred]** At the stool, over the counter: 0.00 m walked. To the floor seats used tonight: 13.6–20.9 m from the bench round the counter's east end, 2.7–4.2 s each way. *(Stations A/B; coordinates)*
- **Any day, the presses — F — [measured]** A drink takes 8 presses and a repair 7 from above; 7 and 7 in first person. Two of each are talk; the replies come up by themselves in 0.4–0.9 s. *(Stations reports)*
- **Day 3, the stress tests — F — [measured]** The room held again. Longest stuck customer 0.7 s; one patron stuck 7.4 s in the doorway when 24 patrons were sent in at once. *(stress.py on trace 234940)*
- **Day 1, 2:34 PM — F — [measured]** The Officer reached the till 91 s in (51% of the day), not "about 63 s". The spawner checks for a due named visit only when its walk-in gap runs out (20–26 s on Day 1), and his walk was 44.6 s. *(trace 234541; `CustomerSpawner`)*
- **Day 1, 9:57 AM–3:35 PM, no input — F — [measured]** Grace waited 92.0 s at the till unheard before walking out (Day 1 ×2.0; was 64 s). Fine for a learner. *(trace 234541)*
- **Overhead, handing back at the stool — L — [seen / inferred]** The Stations overhead photo of "Hand it back" shows two stools and the word "Saamin" over empty floor. It was probably taken mid-blend from the close-up. *(stations-overhead repair-over-the-counter.jpg)* [needs hands]
- **Overhead, the bar stools — L — [seen]** A customer waiting at a bar stool stands beside it (no tall-stool sit, a known open item), so from above they look like they're queueing, at a seat's drain. *(Juice 61)*
- **West Street, the dusty rose door — B — [measured]** The door rush failed 4 of 12: someone stood 84.8 s at (−16.8, 2.1), three people bunched within 0.7 m 13 times, and 2 walks were teleported. It passed 12/12 on 10 Oct; it's being re-run. Open. *(door-rush-dusty-rose-door report)*
- **Bench 3 (lab, Day 1 clock), 9:44 AM — S — [seen / read]** "Cracked screen: Its cable plugs in under the back cover." The cause is now said, but no verb touches the cable. A *ReStory* player will look for it. *(01b, 06)*
- **Bench 3, 10:01–10:04 AM — L — [seen]** The pry sits in the seam and levers (handle from 32° down 14°). From the close-up's steep angle, the 7° pop reads mostly as the screw holes turning into half-moons. Sound will have to carry it [needs hands]. *(04b, 05)*
- **Bench 3, 10:30 AM — F — [seen]** The screen lights face-up, with no words, while the cover is on the mat. *(08)*
- **Bench 3, 10:49 AM — F — [seen / measured]** "Fixed! · Perfect" fires once, 4.2 s later, on a plate that reads (9.2:1), over the phone's back. The finished screw offers nothing. Keep the one ending; the face-down moment is the problem. *(10, 10b)*
- **Bench 3, 11:16–11:24 AM — F — [seen / measured]** The pad hides 18% of the glass and lifts when the smudge clears; the gleam and "Fixed!" land on clean glass. While wiping, 88% of that smudge is still under the pad, and the read is "78 %". That's also how *ReStory* reads cleaning: "Watch the percentage rather than relying only on the audio" ([GamerBlurb](https://gamerblurb.com/articles/restory-chill-electronics-repairs-how-to-clean)). Keep. *(13a, 13b, 14)*
- **Bench 3, 11:37–11:44 AM — F — [read / seen]** Grace's camera is cleaned and its shutter swapped "by the check's hand", so no instrument has played Day 1's job. At "Fixed!" its shutter bay looks empty, with one blade standing. [needs hands] *(15, 16)*
- **Bench 3 — F — [measured]** Robot hands: the cracked screen took 15.8 s from the first tool to "Fixed!" (16.4 s in run 1). The gunked phone took 6.1 s, with one grime spot cleaned by the check's hand. *(report)*

### Night
- **Night 0 (Monday), 11:00 PM — S — [measured]** The reveal still lands: 5.4 s from the bag to "You didn't see me." On a day Grace walked out, he says "Grace walked out. Café owners who talk don't last." Keep both. *(night-zero reports)*
- **Night 0/1, 11:04 PM–midnight — F — [measured]** Barnaby: about 6 s to her step and 6.8 s back, with nobody who could see Ace. Back door to "Call it a night": 59.6 s. *(Night 1 straight face 8.3→14.2→21.0 s; Night 0 59.6 s)*
- **Night 2 (Tuesday), 11:15 PM–1:40 AM — F — [measured]** The careful route: 116 s on her stoop, then 8.2 s from her door to out with the cups. Woken 0, and no ring of Ace's ever reached her. The wait doubled; the risk didn't appear. *(night-two-check-cups)*
- **Night, her house, while she's up — F — [measured / seen]** The bold route reads. Sneaking rings are 1 m, walking 4 m, the rustle 3 m (3.1 m to her ear in her chair). "Hm?" and her head turns. It's fair: the ring shows why she heard. Keep. *(Grace at home 105.1–111.1 s; 18a, 18b)*
- **Night, her house, 11:11 PM — F — [measured]** "Hm?" to "Caught." took 0.91 s with Ace walking behind her shoulder in her lamplight, caught 1.0 s after setting off. That's short, but it's the worst mistake in the house. *Mark of the Ninja* also spots you at close range "irrespective of lighting" ([Wikipedia](https://en.wikipedia.org/wiki/Mark_of_the_Ninja)). For scale, *Metal Gear Solid V: Ground Zeroes* gives "around five seconds" of slow motion after you're spotted ([Giant Bomb](https://giantbomb.com/wiki/Games/Metal_Gear_Solid_V_Ground_Zeroes)). A sneaking catch at range is the case to time (*Runs I'd need* 6). *(Grace at home 171.7 s)*
- **Night, cover — F — [measured]** Crouched behind her breakfast bar, Ace is looked at and not seen (her mark ≤ 0.33). After two noises she walks round the bar's end and sees him (the line clears the bar by 0.8–7.3 cm): "move, don't just hide". [needs hands] *(night and cover reports)*
- **Night, from above — L — [seen / measured]** Her three states (TV, bed, asleep) differ from the night overhead by a warm pool at her stoop and two edge-on bay panes about 10 px wide. The biggest change in the whole frame is a neighbouring block's windows going dark. In first person from the street it's unmistakable: Grace in her armchair at 11:06 PM, dark at 1:43 AM. *(pixel diff of 16a–c; 17a, 17c)*
- **Day 3 morning, the cups — F — [measured / read]** The lab held her straight face 0.5 s after the meter appeared. Barnaby's is unchanged (held after 0.7 s). *(cups report 192.9–193.4 s; Night 1 report)*
- **Night 3 — S — [read]** Still "Nothing tonight": the man's pull stops after the cups. *(cups report 168.0 s)*
- **Day 3, his mess morning — F — [read]** Seats are never short, so his cups on 8 seats still cost presses, not customers (not re-run).

## The numbers

### A. The human-speed day against his own days
| | Day 5, human speed (lab) | Day 3, fast autopilot (lab) | His Day 2 | His Day 3 | His Day 4 | His Day 10 |
|---|---|---|---|---|---|---|
| Arrived | 11 | 10 (+2 still walking at close) | 3 | 7 | 10 | 5 |
| Accepted | 8 | 10 | 3 | 7 | 8 | 4 |
| Left satisfied | 4 | 10 | 3 | 5 | 4 (+1 unhappy) | 1 (+1 unhappy) |
| Walked out after "yes" | 4 (2 Rushed bench, 2 owed drinks) | 0 | 0 | 2 (both standing) | 6 | 3 |
| Walked out unheard / declined / shelf full | 0 / 2 / 1 | 0 / 0 / 0 | 0 / 0 / 0 | 0 / 0 / 0 | 0 / 0 / 0 | 1 / 0 / 0 |
| Waits seated / standing | 8 / 0 | 9 / 0 | 3 / 0 | 4 / 2 | 5 / 3 | 3 / 0 |
| Median `service_s` | 43.0 | 32.4 | 53.2 | 33.1 | 66.2 | 89.6 |
| Patience at exit, served | 0.6, 0.1, 0.5, 0.1 | 0.5–1.0 | 0.6–0.9 | 0.3–1.0 | 0.2–0.4 | 0.9 |
| Losses after "yes", seconds | 42.6, 42.6, 103.0, 109.6 | — | — | 30.0, 33.1 | 28.3–32.1 standing; 104.6 seated | 86.3–107.9 seated |
| Money | $446 = $300 calls + $76 tips + $70 patrons (bench repairs pay $0 in the lab) | $183 | $249 | $203 | $374 | $182 |
| Reputation | 0 | +20 | +6 | +6 | (no column) | (no column) |

His days are older builds; Days 5–9 and 11–15 served nobody (queue storm-outs only), as in run 1.

### B. The human-speed day, person by person (day seconds; 8 PM = 180 s)
| Who | At the till | "Yes" | Waited at | Left | Outcome |
|---|---|---|---|---|---|
| Nina (Impatient, drink) | 14.1 (9:52 AM) | — | — | 21.8 | Declined (the lab's 8% dice) |
| Yuki (Chatty, drink) | 42.9 | 48.0 | stool −3.2 | 89.2 | Served, 0.6 |
| Valerie (Chatty, watch) | 55.3 | — | — | 62.8 | Declined |
| Malcom (Rushed, call) | 65.5 | 69.6 | bistro (1.66, 7.8) | 112.9 | Served, 0.1 |
| Mansoor (Chatty, call + hot chocolate) | 92.0 | 96.9 | stool −3.2 | 206.5 | Walked out, owed drink |
| Omar (Cheerful, call + latte) | 118.8 | 124.0 | bistro (1.66, 7.8) | 226.9 | Walked out, owed drink |
| Alishba (Sentimental, frozen boot) | 122.8 | 127.0 | communal | 179.9 | Served, 0.5 |
| Deshawn (Rushed, cracked screen) | 131.1 (5:01 PM) | — | — | 137.2 | Trays full |
| Ali (Rushed, frozen boot) | 163.1 | 168.8 | window stool | 211.5 | Walked out, 42.6 s |
| Saamin (Rushed, call) | 186.5 (after close) | 191.8 | communal | 234.1 | Served, 0.1 |
| Beatriz (Rushed, watch debris) | 191.7 (after close) | 196.4 | bistro (4.34, 7.8) | 239.1 | Walked out, 42.6 s |

Active customers: mean 4.4, max 6 (at the cap 18.7% of the time); run 1's Day 5 averaged 3.3. Accepted and waiting on Ace: 0 for 21.8%, 1 for 23.9%, 2 for 22.9%, 3 for 24.9%, 4 for 6.6% of the open day; the peak of 5 came after close. Idle (nobody at the till, no job waiting): 15.3%, longest 23.1 s (19.8–42.9 s), none after. Street walkers, spawn to till: 31.6–51.0 s, median 37.6.

### C. Patience after "yes", from the code (45 s × person × day; seat ×0.6, standing ×1.15)
| Day 5 (×0.9) | Seated | +3 Reassures (each needs Ace in reach) | Standing (only if every seat is full) | Queue |
|---|---|---|---|---|
| Rushed (×0.6) | 40.5 (measured 42.2–43.3) | 64.3 | 21.1 | 21.6 |
| Impatient (×0.7) | 47.2 | 75.0 | 24.7 | 25.2 |
| Sentimental (×1.1) | 74.3 | 117.9 | 38.7 | 39.6 |
| Cheerful (×1.2) | 81.0 | 128.6 | 42.3 | 43.2 |
| Chatty (×1.3) | 87.8 | 139.3 | 45.8 | 46.8 |

A coffee halves the drain for 20 s, then 0.8× (about +10 s); only for someone who ordered one. Day 3 (×1.0) and Day 4 (×0.95) are 11% and 6% longer.

### D. The day's shape at the till (Day 5's phases)
| Run | Opening | Morning rush | Lull | Lunch rush | Last orders | First at the till |
|---|---|---|---|---|---|---|
| Run 1 (10 Oct), 180 s | 0 | 2 | 3 | 1 | 3 | 41 s |
| Lab C, 180 s | 1 | 2 | 2 | 3 | 1 | 14.0 s |
| Lab E, 180 s, human speed | 1 | 3 | 1 | 3 | 0 | 13.4 s |
| Human-speed recording, 180 s | 1 | 3 | 1 | 3 | 1 (+2 after close) | 14.1 s |
| **Lab D, the ship's 420 s** | **1** | **7 (4.3/min)** | **2 (1.4/min)** | **6 (3.6/min)** | **5 (4.0/min)** | **14.2 s** |

At 420 s the shape holds. Last orders is as busy as the lunch rush, because walkers spill over. Spawn to till ran 25–61 s across the labs (medians 36–43 s), with the lead learned from 36 s.

### E. Repair, drink and walk times
| | Seconds | Source |
|---|---|---|
| Cracked screen, robot: first tool → "Fixed!" | 15.8 (the wake at 11.6 in, words 4.2 s later) | [measured] Bench 3, 13.7–29.5 s |
| Gunked phone, robot (one spot by the check's hand) | 6.1 | Bench 3, 33.9–40.0 s |
| A coffee, robot: "talk" → served | 8.9 | Stations overhead, 11.6–20.5 s |
| A repair's overhead without the bench (talk, carry, hand back), robot | about 9 (17.7 s with an 8.6 s lab repair) | Stations overhead, 34.5–52.2 s |
| Human-speed lab: drinks / repairs (its dice) | 20–40 / 45–90 (Alishba's repair: 48.0) | `CafeLab`; CSV |
| His repairs, "yes" → walked out (old builds) | 40.8, 59.1, 63.4, 74.5, 83.8 (median 63) | his CSVs |
| Hand-back over the counter to the stool | 0.00 m walked | Stations A/B |
| Bench → floor seats used tonight, round the east end | 13.6–20.9 m, 2.7–4.2 s each way | [inferred] from coordinates, 5 m/s |
| Till → the same seats | about 4 m less | [inferred] |

### F. The nights
| | |
|---|---|
| Night 0 + Night 1's errand (robot): back door → "Call it a night" | 59.6 s (was 66); reveal 5.4 s; 0 observers |
| Night 1: counter → her step → back | 5.9 s + 6.8 s; 0 observers |
| Night 2, careful: on her stoop → asleep → in → out | 116 s waiting (window dark at 106 s), 8.2 s inside, woken 0, 15 rings (14 sneaking, 1 rustle) |
| Her usual night | 11:00 PM sit · 11:35 tea · 11:45 sit · 12:30 tea · 12:40 sit · 1:20 bed · 1:40 asleep · 2:40 water · 3:05 asleep; the night runs at 48 s a game hour |
| The sleeping window | 48 s from 1:40 AM to her 2:40 water; about 35 s more after 3:15 [read: `fixes/night.md`] |
| Rings | sneaking 1 m · walking 4 m (at most one per 0.28 s) · rustle 3 m · creaky stair 5 m (2.5 m sneaking) · her doors 5 m; only inside her walls while she's home |
| Her sight | 7 m lit / 3 m dark; 110° (60° watching TV) |
| "Hm?" → "Caught." | 0.91 s (walking, close, lamplight); sneaking at range not timed |
| Straight face | Barnaby green 0.22 (0.319 with Nerve), held after 0.7 s · the cups: sweep 0.90 s, green 0.145 (0.210 with Nerve), held after 0.5 s · the Officer's Day 3 question: green 0.247 with Nerve |
| The lead's simulation, the cups with Nerve | careless 26% · hasty 69% · watched 96% hold |

### G. The economy (unchanged since run 1)
| | |
|---|---|
| Upgrades, level 1 / 2 / 3 ($) | Faster Machine 300 / 800 / 1,800 · Wide Brush 300 / 750 · Magnetic Driver 350 / 900 / 2,000 · Bulk Restock 400 / 1,000 · Shelf Space 600 / 1,500 · Bigger Bench 900 / 2,400 |
| Base pay ($) | Phone: cracked 110, gunked 45, frozen boot 45, Human 25 · Watch: debris 75, seized 160 · support call 60 · ×1.25 Perfect |
| Drinks ($) | Coffee 3 · Tea 3 · Espresso 4 · Americano 4 · Hot Chocolate 5 · Latte 6 · patrons 5 |
| His days | $182–374, median $226; from work alone $143–304, median $163 |
| First upgrade against his day | $300 = 1.3 days (target 0.5) |

## What feels right
- **The rings.** *Mark of the Ninja*'s expanding circle, drawn only where she can hear. The 4 m walking ring reaching her chair as she says "Hello?" is the whole rule in one picture. *(18a, 18b)* [seen/measured]
- **The day opens and breathes on time:** 13–16 s to the first customer, and 1/7/2/6/5 at the ship's 420 s. [measured]
- **The repair bar at its stool:** the hand-back and the coffee over the counter, 0.00 m walked. In first person Ace faces the room across the glass. *(Stations; Bench 3 photo 00)* [measured/seen]
- **One ending, on a plate that reads.** The screws still back out of their own holes and land 2.4 cm away; a finished phone offers nothing to undo. *(10, 10b)* [seen/measured]
- **Her window from the street:** Grace in her armchair through the glass at 11:06 PM, dark at 1:43 AM. And the man's reveal, 5.4 s, untouched. *(17a, 17c; Night 0)* [seen/measured]

## Needs his hands (questions for when he plays)
1. On Day 3–5, a Rushed customer says "I'm late" with two tickets already up. Did you take it or decline? If you took it, did you save them? Did you walk out to reassure them, and did it help?
2. When you handed a repair back, was the customer at a repair-bar stool (no walk) or out on the floor? How often did you go round the counter's east end?
3. Did anyone leave "Let down" after you'd handed their repair back, still waiting on a drink? Did the ticket's "+ Latte" register?
4. On a support call, did you pour their drink while they were on hold, or after?
5. Time one cracked screen. The robot needs 15.8 s from the first tool to "Fixed!". How long was yours, and how long from "yes" to the hand-back?
6. At "Fixed! · Perfect", which way up was the phone? Did the moment feel like the end, or had the screen lighting up already been the end?
7. When the back came off, did you look for the cable the hint mentions?
8. Grace's camera on Day 1: how long did it take, and did any step stump you? No lab plays its verbs.
9. Night 2: did you wait on her stoop (about 2 minutes) or go in while she was up? If you waited, was it tense or dead? If you went in, did the rings tell you what she could hear?
10. If she caught you, did you see "Hm?" before "Caught."? (0.91 s when walking near her in her lamplight.) Could you have got away?
11. From the night's overhead camera, could you tell whether she was up, in bed or asleep, without switching to first person?
12. The cups' straight face: did you watch one sweep before pressing? Did it feel like holding a face, or like a timing button?
13. At the Day 3 or 4 recap: what did you want to buy, could you afford it, and did the next day feel different? When Night 3 said "Nothing tonight", did you want to go out anyway?

## Runs I'd need
1. **His own Day 3–5 with the recorder**: `Fixit Fidget › Café life › Record 8 minutes (Game view)` right after the day starts. It's still the one run nothing replaces. From `trace.csv` A rows and the CSV I'd measure: walks per order (stool against floor), reassure trips, declines, Rushed outcomes, owed drinks, and Ace's idle time.
2. **`Café life › Lab › Play a lab session - Day 5, autopilot at human speed`, twice more.** One run is dice. I'd look at how the losses spread, and whether they keep landing after 8 PM.
3. **A 420 s day at human speed** (Lab D's length with Lab E's autopilot, if the lab can combine them). Does the last third still bury one pair of hands when arrivals spread over 7 minutes and the cap stays at 6?
4. **`NPC › Sit 4` with the x −3.2 stool already taken**, or `Checks › Waiting space reservations`: why don't the −4.2 and −5.2 stools ever win a claim?
5. **Bench 3 with Grace's camera played by its verbs** (brush in the film path, cloth on the lens, tweezers on the shutter) instead of "the check's hand": a time and a photo per step.
6. **Break-ins 7 with a sneaking catch timed at range** (in her cone, unlit, 3–7 m): "Hm?" to "Caught." when Ace is doing the careful thing wrong.
7. **`Checks › Street doors - rush hour at the dusty rose door`** in a fresh session (already in progress).

## Flags
- **Stations ran before the UI commit.** Their photos (22:32–22:33) show the old "#2", so the README's "every lab on the final scene" isn't quite true for them. Their press and walk numbers stand.
- **`fixes/systems.md` S4:** "the Officer at about 63 s" on Day 1. Measured: at the till at 91 s.
- **`fixes/systems.md` S1:** Reassure "up to about 64 s" holds at the repair-bar stools. For the 3 in 4 waits on the floor, the walk eats most of it [inferred].
- **The bench spec:** §2.7 still says a device handed back with its cover off "costs nothing". The build refuses the hand-back, and `fixes/bench.md` keeps that rule and asks for the spec to be updated (§2.5, §6 D, §7). Not done in the Project doc yet.
- **Day 5's last orders** let two walkers already on the street reach the till after 8 PM, and service ran 67 s past closing. Intended? His call.
- **No new mechanic is proposed.** Every fix above is a number, a data field, presentation or content; the two that would be new rules (a reward for going in early, an unplug step) are flagged as his.

## Sources
- *ReStory*:
  - cleaning by percentage: [GamerBlurb](https://gamerblurb.com/articles/restory-chill-electronics-repairs-how-to-clean)
  - "Reassemble" then "Complete and send": [Pro Game Guides](https://progameguides.com/restory-chill-electronics-repairs/restory-chill-electronics-repairs-beginners-guide/)
  - satisfying audio on every fit: [Automaton](https://automaton-media.com/en/?p=55490)
  - "Picking apart the layers of each device is consistently satisfying": [Prima Games](https://primagames.com/reviews/restory-review)
  - screws "too forgiving": [COGconnected](https://cogconnected.com/review/restory-review/)
- *Mark of the Ninja*: score for tactics, docked on alarm; the expanding circle; spotted up close "irrespective of lighting" ([Wikipedia](https://en.wikipedia.org/wiki/Mark_of_the_Ninja)). The noise ring's design: [Game Developer](https://gamedeveloper.com/design/reinventing-stealth-in-2d-with-i-mark-of-the-ninja-i-).
- *Metal Gear Solid V: Ground Zeroes*: Reflex Mode, "slow motion for around five seconds" after being spotted ([Giant Bomb](https://giantbomb.com/wiki/Games/Metal_Gear_Solid_V_Ground_Zeroes)). Used only as a yardstick for the 0.91 s window, not proposed.
- *PlateUp!*: [TheGamer](https://www.thegamer.com/plate-up-how-to-reach-day-15-final/), [TV Tropes](https://tvtropes.org/pmwiki/pmwiki.php/VideoGame/PlateUp).
- *Papers, Please*: [Wikipedia](https://en.wikipedia.org/wiki/Papers,_Please).
