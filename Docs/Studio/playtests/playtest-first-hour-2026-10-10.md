# Playtest: the First Hour, 10 Oct 2026 (build `playtest-2` at `b8cbf6e`, after the café redesign)

## Verdict

**Reads as a solid game to a newcomer: Not yet.** Day 1 teaches the whole loop in 6-second cards that never come back, and the first job is the hardest kind of job in the game. The likeliest first hour is Grace walking out, a "Never again" review, and −3 reputation, all before the night's very good hook arrives. Everything after that reads well up close: the bench, the dispenser, the phone, the deal at the bins. None of the fixes below is a new mechanic. They change presentation and data.

## The run

- **What I played, from evidence (about 85 min):**
  - The three recordings: Day 1 with no input, frame by frame from the video against `trace.csv` and `frames.csv`; Day 5 on autopilot; the Day 3 stress run.
  - Every screenshot in `screens/`.
  - The lab photos and reports: Stations (both views), Bench 3, Night 0, Night 2 (visit, note), Juice, the phone by day, the redesign's before/after and probe photos.
  - His own day logs, Days 1–3 (7 Oct).
  - The words in code and data: the Day 1 guide, Grace, the walk-ins, the deal, the phone, the menu board.
  - His notes: playtest 4, playtest 3, the HUD spec, today's session notes.
- **The instruments' limits:**
  - Nobody played Day 1 by hand on this build. The only Day 1 recording has no input.
  - The autopilot hears customers instantly and never moves Ace.
  - Bench 3 repairs a phone, not Grace's camera.
  - Every capture is 2560×1440 or 960×540, not 1080p. Text sizes below are computed for 1080p from the canvas (reference 1920×1080).
  - No audio.
- **What that leaves unjudged:** the hands-on Day 1, keyboard tool selection at the bench, camera comfort, sound. These are under *Needs his hands*.

## The five that matter most (ranked by how likely a newcomer quits over it in the first two hours)

### 1. Day 1's teaching disappears: each step is a 6-second card, shown once, in the top-left corner

- **What happens:**
  - Each step's card stays up for 6 s (`openingHintDuration: 6`). A line that has been shown can never return: `DayOneHintTimer` ("Expired hint cannot repeat").
  - Only Grace's visit gets cards. The Officer's coffee and the walk-ins get none (`DayOneGuideUI.cs:80–94`). So the game's first drink is taught by the dispenser's own caption alone.
  - Nothing anywhere says how to move. The bottom strip lists only "V First person · Middle-drag Orbit · Scroll Zoom · Esc Pause".
- **Where:**
  - Day 1, 11:26 AM: "E talks to Grace; when your replies appear, E takes the job." is on screen at REC 33–36 s and gone by REC 45 s. Grace then waits, unheard, until she walks out at 3:22 PM.
  - At 1:31 the screen has no card at all, and Grace is 5 s from walking out.
- **Evidence:**
  - [seen] `Logs/CafeLife/2026-10-10_161003/video.mp4` frames 351, 383, 478; `screens/day1-01m31s-objective-card-gone.jpg`
  - [read] `DayOneGuideUI.cs:99, 464–490`; `GraceShowcase/Day_01_GraceCamera.asset`; `CafeViewMode.cs:141`
- **Why it matters:** this player once bounced off a sim "because I didn't know what I was supposed to do". The steps that need a card most come while the eye is on the bench in the centre of the screen, not on the top-left corner: carry it to the bench, pick a tool, take it back.
- **Fix:** keep the current step's card up until the step changes (show it again after about 10 s idle), and put "WASD Move · E Use" in Day 1's strip.

### 2. The first job is the game's hardest kind, and the built practice day isn't the one that runs

- **What happens:**
  - The scene's Day 1 is the Grace showcase. Grace comes in first, with her reunion camera: clean the lens, clean the film path, replace the shutter, and leave the strap alone.
  - Once she sits she gives you about 121 s: 45 s × 1.15 (Grace) × 1.4 (Day 1), drained at 0.6× on a seat.
  - The "learn one drink, then one repair" day is built and passes `Checks › Day 1 onboarding`, but the scene's schedule doesn't use it.
- **Evidence:**
  - [read] Scene schedule (`AcesCafeLayoutPlaytest.unity:58291–58296`): slot 1 is guid `829551…`, not `Day_01_LearnTheShop` (`8e6fb0af…`). Slot 2 isn't `Day_02_Juggle` either.
  - [read] The check's sequence and job-roll tests load `Day_01_LearnTheShop` (`DayOneOnboardingChecks.cs:143–158, 186`).
  - [seen] The recording behaves exactly as `Day_01_GraceCamera`: Grace walks in from t 0.1; the card title is "DAY 1 · GRACE"; with at most 2 customers, the Officer only reaches the counter at 6:47 PM.
  - [measured] His own Day 1, 7 Oct (`DayLogs/AcesCafeLayout/Day01_customers.csv`): he took Grace's job, she walked out after 136.0 s ("Never again"), 0 orders completed, −3 reputation. That was the older bench, but the same day, cards and patience.
- **Why it matters:** fail the first job and Day 1 ends with nothing done. That night the man says "Grace likes you." anyway (Notes, Night 1).
- **Fix (his call):** run LearnTheShop's coffee and one-fault repair first, then Grace at 55% of the day (that asset's own setting). Or keep Grace first and make her cards stay (#1).

### 3. From the default camera, nothing points at who or where

- **What happens:**
  - The name of whoever E will talk to floats at the screen's centre, about 160 px from the person at 1080p (`FocusNameUI` is a fixed label). At 11:26 AM "Grace" hangs over the empty floor while she stands at the counter. At 8 PM "Walter" hangs over a seated patron at the communal table.
  - The prompt sits at the bottom of the screen, over the storefront.
  - Ace has no marker.
  - The queue's patience bar is 0.6 × 0.08 m: about 23–26 × 3 px at 1080p, a red dot by the end.
  - The three stations take up about a fifth of the frame and are unlabelled:
    - The bench is a wood strip at the counter's left end; its mat, tools and lamp don't read.
    - The enamel REPAIRS sign over the bench doesn't show from above.
    - The dispenser is a dark box on the back bar.
- **Evidence:**
  - [seen] `screens/day1-00m36s-talk-to-grace.jpg`; `screens/day1-03m00s-closed-nobody-served.jpg`; a 5× crop of `screens/day1-01m31s`; `Stations/stations-overhead-…/drink-1-at-the-counter.jpg` and `repair-1-at-the-counter.jpg`; a 2× crop of `Bench/…/00-placed-from-above.jpg`
  - [read] `FocusNameUI.cs`; `Customer.prefab` PatienceBar `m_LocalScale {0.6, 0.08}`; `CafeViewMode.cs:105` (`isoDistance 34`)
- **Why it matters:**
  - "E talks to Grace": which one is Grace?
  - "Carry it to the repair bench": where is it? "[E] Set down" only appears once Ace is already standing there.
- **Fix:** in the overhead view, pin the name and the bar over the person's head, the way the badges already are (`Barks.HeadPointOf`).

### 4. From Day 3, people walk out and the screen doesn't say why, while the money rises on its own

- **Walk-outs:**
  - Day 3 brings the first Impatient customers (`Day_03_TheRealGame` `allowedArchetypes`). They wait standing, so they have about 27 s once you take their job: 45 × 0.7 ÷ 1.15 standing drain. After Day 2 there are no cards.
  - His own Day 3 (7 Oct): both Impatient customers who waited standing walked out, a coffee at 30.0 s and a phone at 33.1 s.
  - The labs: Day 3 at 29.2, 32.2 and 32.2 s; Day 5 at 23.3 s three times and 26.4 s. All of them were standing.
  - For scale, Bench 3's robot hand needs 28.5 s for one cracked screen.
  - The only remedy on screen is "[E] Reassure" (up to 3 times). It shows only when Ace stands beside them, and nothing teaches it. Sitting versus standing (0.6× vs 1.15× drain) can't be seen.
- **Money:**
  - Patrons pay $5 at the door, with a "+$5" pop.
  - The no-input Day 1 ends at $65 on the HUD ($70 in the log) with 0 people served.
- **Evidence:**
  - [measured] `CafeLife/2026-10-10_150206/Day03_customers.csv`; `…_145339/Day05_customers.csv`; `DayLogs/AcesCafeLayout/Day03_customers.csv`
  - [read] `CustomerBrain.cs:20–21, 745–749`; `CustomerInteractable.cs:63`; `PatronSpawner.cs:186–193`
  - [seen] `screens/day1-03m00s` ("$65 TODAY"); the `sheets/day1-*-1` sheet ("+$5" at 0:45 and 1:00 video time)
- **Why it matters:** in hour one or two the newcomer gets a run of "Never again" reviews and can't tell what to do differently, while the biggest number on screen goes up anyway.
- **Fix:**
  - Show one card the first time someone gets frustrated, e.g. "Tired of standing. E reassures them; a seat buys time."
  - Drop the patrons' "+$5" pop so the player's own pay is what pops.

### 5. Walk-ins read their fault labels aloud

- **What happens:** every walk-in intake drops the fault label into speech word for word, in Title Case with dashes:
  - Impatient: "This is Frozen Boot Sequence - Reconnect Signal. Can you do it now?"
  - Sentimental: "It's Calls light up the screen, but the phone never rings. This Phone means a lot to me."
  - Cheerful: "Hi! My Pocket Watch — Debris Inside, Won't Tell Time. Any chance?"
- Next to the brown "G" portrait box, this makes conversations the least finished-looking part of the game. The box is still true, and it's his 28 Sept call until the art comes.
- **Evidence:**
  - [read] `AcesCafeLayoutPlaytest.unity:58310–58460` (the intake lines); `CustomerIdentity.cs:455–457` (swapped in as-is); `PhoneRepair.prefab:1210–1229`; `PocketWatch.prefab:2067–2073`
  - [measured] `Day05_customers.csv` ("Service disconnected - call support to restore it")
- **Why it matters:** after the prompts, this is the most-read text in the game. A *Coffee Talk* player reads every word.
- **Fix:** write a spoken form of each fault, in lower case, made to sit inside the line ("grit inside, won't keep time").

## Notes in his format

*B bug · F feel · L looks · S story. Headings follow his playtest-4 doc (his "Night 1" is the game's Night 0).*

### Day 1

- **9:00 AM · L.** The first frame shows the café from 34 m up, taking about a fifth of the screen, with the city around it. Nothing says who Ace is or what the place is. The A-frame says it ("Bring us the broken thing. We'll pour you a coffee while we fix it."), but from the game camera it's about 40 px across at 1080p and its words can't be read. [seen] `screens/day1-00m01s`; `CafeRedesign/street-1…/2-after-05-a-frame.png` and `2-after-07-game-camera.png`. [read] There is no intro text anywhere; grep for "mortgage" finds nothing.
- **9:05 AM · F.** The first card reads "Grace is on the way with a repair. Start behind the service counter." Ace already stands there. Then "Wait for Grace to reach the counter." She appears within the first 10 s and reaches the counter at about 40 s. Those first 40 s have nothing to do. [seen] `Pause/…/01-the-corners-by-day.jpg`. [measured] `trace.csv` (Grace at t 0.1, x −17.4 on the street; in the queue at t 31.6).
- **11:26 AM · L.** "Grace" floats over the empty floor, not over her (#3). [seen] `screens/day1-00m36s`
- **11:26 AM · B/L.** The how-to card is gone after 6 s, for good (#1). [seen] video frames 383 → 478
- **3:22 PM · S.** Grace walks out unheard ("Let down", −1). The next card reads "Grace's visit is over. Finish the remaining requests; closing brings the recap and save." (14 words). "Remaining requests" to a player who has made none. [measured] `Day01_customers.csv`. [read] `DayOneGuideUI.cs:107`
- **All day · F.** The money rises to $65 with "+$5" pops while nobody is served (#4). [seen] `screens/day1-03m00s`
- **7:05 PM · L.** The LAST ORDERS sign with its amber vignette reads clearly. [seen] `screens/day1-02m37s`
- **8:00 PM · L.** The prompt says "[E] Talk to Walter" while the Officer is first in line. [seen] `screens/day1-03m00s`
- **Counter · L.** The intake shelf has no object of its own: devices lie on the bare stone in front of the till (slots at x −1.56…1.56, z 13.65), but the card says "pick up … from the intake shelf". [read] `Logs/CafeRedesign/bar-1-last.txt:73`; `DayOneGuideUI.cs:221, 265`
- **Bench close-up · L.** A big blue "#2" lies across the pocket watch's dial (from 12 to 3 o'clock) while you repair it. HUD spec §4 (24 Aug, agreed) said the digit goes, from the device and from over customers. The ticket shows no number to match it. [seen] `Stations/stations-overhead-…/repair-5-working-on-it.jpg`. [read] `CustomerBrain.cs:1387, 1448`
- **Bench close-up · L.** After "Back together · Perfect", hovering a screw still says "Screw: Hold to unscrew". [seen] `Bench/…/10-fixed.jpg`
- **Bench close-up · F.** The tools line ("[LB/RB] Hands · [RT] Use · [B] Put item down") appears on a pad only. With a mouse, the close-up says just "[E] Pick up item and step back", so nothing on screen says how to pick a tool except the 6 s card. [read] `ShopUI.cs:255, 275–277`. Needs hands.
- **Dispenser, first person · L.** Only 4 of the 6 paddles are fully in view: Americano is cut by the screen's edge and Latte is off it. Latte is Grace's drink. [seen] `Stations/stations-first-person-…/drink-2-at-the-dispenser.jpg`
- **Conversation · L.** The portrait is a brown box with "G", and later "G / Concerned". Still true; his 28 Sept call until the art. [seen] `screens/day2-morning-grace-latte-dialogue.jpg`; `Night/night-zero…/15-the-meter-with-nerve.jpg`

### Night 1 (the deal, the bins, Barnaby)

- **11:00 PM · L.** "Last job: the bins. Out the back." with "[E] Take the bins out" is clear. [seen] `night-zero…/02`
- **11:00 PM · S.** The reveal and the deal read well: 8 lines, about 65 words, two reply chips beside his head (What reads right). [seen] `06`, `07`. [read] `Night lines.asset`
- **11:00 PM · S.** "Grace likes you." plays even after Grace walked out "Never again" that day. Nothing in the code branches on it. [read] `lodger.night0.05`; `NightZero.cs:315` only animates. [measured] His Day 1 log.
- **11:04 PM · F.** "Barnaby: Grace's front step, the saffron house on the corner." There's no marker, and at night the saffron house looks brown. [seen] `08`, `09`. Needs hands.
- **11:24 PM · B (test save only).** At her step, "[E] Let yourself in" shows while the note says "Bring Barnaby back…": two directions at once. [seen] `09`. [inferred] This is the labs only: her door opens every night only in the test save (`WholeGamePlaytest.cs:15–16`, [read]).
- **11:50 PM · S.** "Learned: Nerve. Your straight face holds a little longer." comes before the player has ever seen the straight face; it only makes sense the next morning. [seen] `13`
- **Night · F.** Nothing outside the houses can catch Ace. "Caught." exists only inside Grace's house, so "don't get caught" has no teeth on the first night. [read] `NightCycle.cs:343–365`; `GraceAtHome.cs:1845`

### Day 2

- **9:16 AM · L.** "Grace is visiting again. Talk at the counter to hear how things went." is clear. [seen] `night-zero…/14`
- **Grace's complaint · S.** "Who steals a garden gnome, Ace?" with "[Space] Keep a straight face" reads well. [seen] `15`, `16`

### Day 3

- **9:09 AM · L.** The clock line reads "Wednesday · Day 3 9:09 AM", with no gap between the day and the time on long weekday names. [seen] `Night/night-two-check-note…/05-his-mess.jpg`, bottom left
- **9:09 AM · S.** "The man at the bins left his mess. Clear the tables." with a used-cup badge over each table reads well. [seen] Same photo.
- **All day · F.** Customers who wait standing walk out about 30 s after you take their job (#4). [measured]

### Day 5 (recap)

- **8:00 PM · L.** The phone shows "no stars yet · −3 reputation" and nothing says what a star needs. The goal is there, one tap deep behind **More** in Franchise HQ: "Reach 5 reputation for your first star · N to go" and "At five stars, HQ sends the franchise offer".
  - [seen] `screens/day5-lab-recap-phone-8pm.jpg`. [read] `RecapPhone.cs:1216–1280`; `ReputationRules.cs:94`
  - **Fix:** swap "no stars yet" for "5 to your first star", the same number of words.

## Numbers that matter to a newcomer

| What | Number | Source |
|---|---|---|
| Day length | 180 s, 9 AM to 8 PM (16.4 s per café hour) | [measured] `frames.csv` |
| First customer reaches the counter | about 40 s into the day | [measured] `trace.csv` |
| How long the first customer waits for someone who doesn't know what to do | 64.4 s (Grace), then 72.8 s (the second) | [measured] `trace.csv` |
| Grace's patience after you take her camera | about 121 s sitting, about 63 s standing | [read] 45 × 1.15 × 1.4, ÷0.6 or ÷1.15 |
| Standing Impatient customer, Day 3 | about 27 s after you take the job; observed 29–33 s | [read] + [measured] |
| One cracked-screen repair, robot hand | 28.5 s in the close-up (a cleaning-only job: about 7 s) | [measured] Bench 3 |
| Presses for one drink / one repair (bench work excluded) | 9 / 9 | [measured] Stations |
| His Days 1–3 (7 Oct) | Day 1: 0 done, −3. Day 2: 3/3 served, +6, first star. Day 3: 5/7 served, 2 walked out | [measured] his day logs |
| Day 1 with no input | 2 walked out, $70 (all patrons), −2 reputation | [measured] |
| Day 5, autopilot | 9 came, 5 walked out waiting, −3 | [measured] |
| Reputation per review | first star at 5. Loved it +2, Let down −1, Never again −2 | [read] + [measured] |
| Words per screen | Day 1 cards 7–14 at the counter, up to 21 at the bench; Grace's intake 24 in 2 lines; the deal about 65 in 8 lines; Tonight phone about 25–35 | [read] + [seen] |
| Text size at 1080p | Day 1 card 16–23 px; ticket name 17 px, its fault line 14 px; bottom strip 19 px; queue patience bar about 23–26 × 3 px | [read] |

## What reads right (leave it alone)

1. **The deal:** "You didn't see me." / "You were in my bin." Staged at the bins in 8 short lines, with the reply chips beside his head.
2. **The bench up close:** the cracked screen looks cracked; every hover names the part and the verb ("Screw: Hold to unscrew", "Smudge: Needs the cloth"); the fresh part waits in the tray; then "Fixed!" and "Back together · Perfect".
3. **The dispenser in first person:** big named paddles in colour, and a ticket that says "Coffee to make".
4. **The phone:** one screen at closing, one button. "Back to work" midday. Notes is one line per person. His "WAYYY too many words" is fixed.
5. **The consequences land:** Grace's complaint with the straight-face meter, and his mess with a badge over each table. One note per beat, as he asked.

## Needs his hands (questions to answer tonight)

1. **The first frame:** before reading anything, could you tell which figure is Ace and which is Grace? How many seconds before you pressed E?
2. **The cards:** did you catch "E talks to Grace…" before it vanished? Later, at the bench, did you ever look up at the top-left card?
3. **Grace's camera:** time it from "I'll take a look" to "Fixed!", and on to the hand-back. Did she stay? (About 121 s if she sits.)
4. **The bench from above:** without the prompt, did you know where the repair bench was?
5. **With a mouse:** how did you pick the brush, then the tweezers? Did anything on screen tell you?
6. **The first drink:** for the Officer's coffee (no card), did you know to go to the machine on the back bar?
7. **Night 1:** after the deal, how long did it take to find Barnaby? Did "saffron" help in the dark?
8. **Day 3:** when a standing customer walked out, did you know why? Did you ever see "[E] Reassure"?
9. **The goal:** did you open More → Franchise? If you didn't, what were you playing for by Day 3?
10. **Sound:** does anything play when a customer reaches the counter, at the sweat drop and the red burst, and when a patron pays?

## Runs I'd need

1. **A hands-on Day 1, recorded:** Playtest › Play the whole game from Day 1 (test save), then Café life › Record 3 minutes (Game view), with someone playing on keyboard and mouse. I'd look at where the card is while the eye is on the bench, how long each hand-off takes, and whether Grace stays.
2. **Grace's camera close-up, step by step, with the Day 1 card in frame:** the Grace camera tweezers check, or Bench 3 on the camera. Bench 3 photographs only the phone.
3. **The Day 1 recap after Grace walks out:** the no-input run reached 8 PM, but the pack has no photo of its phone. I'd look at what Tonight says to a newcomer who failed.
4. **A Day 3 lab day, with Ace walked over to a standing waiter:** one photo of "[E] Reassure", and of the sweat drop and red burst at real size from the default camera.
5. **The same four frames captured at 1920×1080:** the first screen, a conversation, the bench close-up, the recap. That gives a true-size legibility pass.

## Flags for the leads

- **The "Day 1 onboarding" check doesn't test the Day 1 a player meets.** Its sequence and job checks run against `Day_01_LearnTheShop`, which the scene's schedule doesn't use (#2). (QA, Systems)
- **The HUD spec and the build disagree on the job digit.** HUD spec §4 (24 Aug, agreed) removed the floating "#N"; the build still shows it over customers and devices. (UI/UX)
- **Probably test-save-only:** "[E] Let yourself in" at Grace's step on Night 1 looks like it exists only in the test save, not in his own save ([inferred]). (Night)

Source: Steam's refund window is 14 days and under 2 hours played ([TechRadar](https://www.techradar.com/news/gaming/steam-s-new-refund-policy-should-make-gamers-very-happy-1295631)).
