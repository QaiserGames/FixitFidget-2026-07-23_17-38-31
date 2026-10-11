# Playtest: the First Hour, run 2, 11 Oct 2026 (build `playtest-2` at `ceaf81d`, after the playtest-4 fixes)

## Verdict

**Reads as a solid game to a newcomer: Solid with fixes.** The first half hour now reads from the screen alone, except for three Day 1 moments where the card and E disagree. One of them hands Grace her camera back broken with a single press. From Night 3 on, the nights have nothing to read.

- What changed: the card stays, a pin marks who and where, the tray is a real object, faults are said like people, "Fixed!" comes once, and the phone says what a star needs.
- What's left on Day 1 is presentation, one line each.
- The empty nights are scope (Night 3, the cones), his call. No fix lead could have solved them.

## Since run 1

Fixed / still true / worse / can't tell from here.

**Run 1's top five**

| Run 1 | Now | Evidence |
|---|---|---|
| #1 Day 1 teaches in 6 s cards that never return; only Grace's visit has cards; nothing says how to move | **Fixed** (the card stays). It now disagrees with E three times: new #2 and #3 | [seen] Day 1 video, REC 0–180: a card on screen all day (full 6 s, then one line, full again after 10 s stuck; compact at REC 21, 95 and 112, full at 71, 86, 101 and 150). [seen] guide photo 08: at the bench it sits over the prompt. [seen] screens 0:01–2:05: "WASD Move · E Use". [seen] guide 10–12: the Officer's coffee has cards |
| #2 The first job is the hardest; the practice day doesn't run | **Still true, softer.** Day 1 is still Grace's four-task camera, and she now orders a latte too. She reaches the till about 15 s after opening, waits 92 s unheard, and gives about 172 s once seated | [read] `ui-0` log: the scene's Day 1 is `Day_01_GraceCamera`. [read] `systems-1` log: patience ×1.4 → ×2.0. [seen] guide 10–12: her ticket "+ Latte". [measured] trace |
| #3 From above nothing points at who or where | **Who: fixed, every day. Where: fixed on Day 1's steps; on later days a station is named only once E is on it** | [seen] screen 0:21: "Grace" over her head with a 56×7 bar under it. [seen] guide 05 "Intake tray", 06 "Repair bench", 11 "Drinks". [seen] video REC 1–6: "You" over Ace. [seen] Day 5 screens: "Nina", "Alishba's phone", "Beatriz's pocket watch". [read] ui.md §7 |
| #4 From Day 3, walk-outs go unexplained; the money rises on its own | **Walk-outs: fixed in data, now explained by a card. Money: half fixed.** The pop is gone; the figure and Tonight still count patrons' money as earnings | [measured] Day 3 stress: 10 of 10 served; all 9 who waited sat (the tenth was fixed at the counter). [read] Sit 4: "nobody waited standing while a seat was free". [seen] Day 5 REC 90–97 and guide 09: the GETTING IMPATIENT card. [seen] REC 14.9–28.2: $0 → $5 with no chip. [seen] Day 1 recap: "earned today" counting up to $65 with nobody served. [read] UI decision 7: the split isn't on Tonight |
| #5 Walk-ins read their fault labels aloud | **Fixed in the words. No frame shows one said** | [read] `FaultSpeech.cs`; scene intake lines, e.g. "Hi! My pocket watch — it's seized up and won't wind. Any chance?"; `content-1/2` logs. The only walk-in on screen in the pack (conversation lab, 73.2 s) ordered an espresso |

**Run 1's B and F notes, and the L notes I ranked**

| Run 1 note | Now | Evidence |
|---|---|---|
| Day 1, 9:05 · F: 40 s of nothing; "Start behind the counter" when Ace is already there | **Fixed** | [measured] trace: Grace in the queue 8.8 s into a recording that began 6.9 s into the day; day-shape labs 13.4–14.2 s. [seen] 0:01: "You're Ace. Your café's first day: Grace is on the way with a repair." |
| 11:26 · B/L: the card is gone after 6 s | **Fixed** | As #1 above |
| 3:22 PM · S: "Finish the remaining requests" when none was taken | **Fixed in the words** ("Grace's visit is over. Closing time brings the day's recap."). In the no-input run, though, the card went straight to the Officer at REC 101, and her walk-out was only a red burst | [read] `DayOneGuideUI.VisitOver`. [seen] REC 100.9–101.5 |
| All day · F: "+$5" pops with nobody served | **Half fixed** (see #4) | — |
| 8 PM · L: "[E] Talk to Walter" while the Officer was first | **Explained and fixed.** He was storming out, and the queue now orders by arrival at the counter. The same disagreement is back at 3–5 PM, in a new form (new #3) | [read] `PlayerInteractor.cs:482–508` |
| Counter · L: the "intake shelf" is bare stone | **Fixed.** Green felt in a chestnut tray; from above, green patches by the till. The card says "intake tray". The Shop still says "intake shelf" | [seen] tray `2-after-1…5`; guide 05–06. [read] `Upgrade_ShelfSpace.asset:16`; [seen] recap 05 |
| Bench · L: blue "#2" across the watch's dial | **Fixed** | [seen] guide 08: no tag on the camera. [read] guide report 40.1 s: "0 job tags". The only close-up of a customer's watch (Stations, first person, 22:34) ran before the UI part (22:42) and still shows "#2", so it proves nothing either way |
| Bench · L: "Hold to unscrew" after Perfect | **Fixed** | [seen] Bench 3 `10-fixed-once`: one "Fixed! · Perfect" on a dark plate, and the screw's hover reads "Screw" with no verb |
| Bench · F: the tools line is on a pad only | **Fixed** | [seen] guide 08: "[Click a tool] Take it · [Drag] Turn it · [R] Flip it · [Right-click] Put item down", and the card "Take the cloth (click it) to clean the smudge." |
| Dispenser · L: Americano cut, Latte off screen | **Fixed in the close-up. First person unchanged** | [seen] guide 12 at 1080p: all six paddles whole. [read] dispenser lab: smallest paddle 70 px, margin 4.3%. [read] same lab, 3.2 s: in first person, aimed at the cup stack (the card's first instruction), Americano and Latte are still cut |
| Conversation · L: the brown "G" box | **Still true** (his call until the art) | [seen] conversation 03; Night 0 15 |
| Night 0, 11:00 · S: "Grace likes you." after she walked out | **Fixed** | [read] Night 0 walked-out lab, 23.0 s: "Grace walked out. Café owners who talk don't last." |
| Night 0, 11:04 · F: no marker; the saffron house reads brown | **Half fixed.** Her step is lit and Barnaby's red hat shows on it. The house still reads dark brown. From above, her lit house and her dark house look the same, apart from a small warm pool on the pavement | [seen] Night 1 `03`; Break-ins 7 `00`, `17a`. [measured] pixel difference of `16a` and `16c`: changes only at the far block, a pool about 60 px across at her corner, and Ace |
| Night 0, 11:24 · B: "[E] Let yourself in" with Barnaby in hand | **Fixed** (see the correction below) | [seen] Break-ins 7 `00`: no prompt at her door with Barnaby in hand |
| Night 0, 11:50 · S: "Learned: Nerve" before the straight face exists | **Fixed** | [seen] Night 0 `12–13`: "Learned: Nerve. When Grace asks, your face will give less away." |
| Night · F: nothing outside the houses can catch Ace | **Still true** (his tone call; not built) | [read] `night.md` N6 |
| Day 3, 9:09 · L: no gap in the clock line | **Fixed** | [seen] Night 2 `10`: "Wednesday · Day 3   9:16 AM" |
| Day 3 · F: standing customers walk out about 30 s after you take the job | **Fixed in data. Still lost at human speed** | [measured] Day 3 stress: all 10 served, all 9 who waited seated (Impatient left at patience 0.5–0.7). [measured] Day 5 human speed: 4 of 8 accepted walked out waiting, all seated, patience 0.0. Both "Never again" were Rushed customers (Ali, 60.0 s in the shop; Beatriz, 61.3 s) |
| Day 5 recap · L: "no stars yet" | **Fixed** | [seen] Day 1 and Day 5 recaps: "5 to your first star", and verdict pills ("Loved it", "Never again") |
| Flag: the Day 1 onboarding check tested the wrong Day 1 | **Fixed** | [read] `ui-0` log |

**Two corrections to my run 1**

- **The fault quotes.** My Title Case quotes were rebuilt from data, never seen on screen. The game lower-cased the label ("This is frozen boot sequence - reconnect signal."). It was still a label, so the finding stood. The content lead caught this.
- **Grace's door.** "[E] Let yourself in" was not test-save only. Grace's door has been on in his real game since 30 Sept (the Night lead). My `[inferred]` was wrong.

## The run

About 80 minutes, all from the pack.

- **Played cold:**
  - The new game's Day 1 with no input: 3 minutes of video, frame by frame against `trace.csv`, its five screenshots and its own day log.
  - The Day 1 guide lab's 12 photos at 1080p, looked at as a player would.
  - Day 5 at human speed: video, four screenshots, the recap and the day log.
  - The Day 3 stress run, from its sheets and day log only (the whole-café camera's frames glitch).
- **Labs:**
  - Bench 3 (the phone and Grace's camera), the dispenser frame, Stations A and B, the trays, Juice, the conversation lab, the recap and pause labs.
  - Night 0 and its walked-out twin, Night 1, Night 2, Grace at home (her night, her Thursday, her cover).
- **The words, in code and data:**
  - The Day 1 guide, `FaultSpeech`, the scene's walk-in lines, Grace's and the Officer's lines.
  - The night notes, the review lines, the phone.

**What the instruments can't show**

- **Nobody played Day 1 by hand.**
  - The guide lab drives the game's own calls and teleports Ace.
  - It sent a walk-in (Alishba) home before the Officer came, and it stopped at the cup stack. Both shortcuts hide #3.
  - It picks the camera up 0.7 s after the talk, so its photo 06 caught #2, but it never pressed E there.
  - The Stations lab waits 5.6 s for its customer to sit before the pickup, which hides #2.
- **Stations A and B, Bench 3, the dispenser frame, Juice, the conversation lab and Night 0's walked-out lab all ran at 22:12–22:35, before the UI part (its check ran at 22:42).** Their photos show the old HUD: the name fixed mid-screen and "#2" on the watch. I used them for the bench, the reach and the presses, never for the HUD. The README says every run was on the final build or the one before part 7; for these runs it isn't.
- The autopilots never move Ace and never make a drink.
- Video is 960×540. Text sizes come from the guide lab's 1080p photos and the code.
- There is no sound in the pack.

## The five that matter most now

Ranked by how likely a newcomer quits over it in the first two hours.

### 1. From Night 3 on, the nights are empty

- **What happens:**
  - After the cups (Night 2), his next favour is the cones, and the cones aren't in the game.
  - So every later night opens at the bins, and the note reads "Nothing tonight. The street is yours."
  - Grace's Thursday page invites a look round her house, but there is no errand, nothing to take and no lesson.
- **When:** Night 3 comes about 17 minutes in with this build's 3-minute days, and about 30 minutes in with 7-minute days. The rest of the two-hour refund window has no night goal.
- **Evidence:**
  - [read] `NightZero.cs:463–464`. [read] Night 2 lab ledger: `"favour":"street.cones"`.
  - [read] `LodgerStory.Pages`: "the parking page waits for the cones". [read] `night.md` N7: there is no `NightThing` for the cones.
  - [inferred] the timing, from the day lengths and the labs' nights (Night 0 about 60 s, Night 2 about 170 s, robot-fast).
- **Why it matters:** The store page sells the night: the man, petty errands, the officer. Run 1's wall was Day 1, and it's gone. That moves the wall here.
- **Fix:** Not a fix lead's job. This is Night 3, which the Night lead put at 1–1.5 days (`night.md` N7). His call.

### 2. The first hand-off can give Grace her camera back broken with one press

- **What happens:**
  - E picks Grace's camera up off the tray, at the till.
  - The card turns to "Carry the reunion film camera to the repair bench. E sets it down.", with a green pin on the bench.
  - At the same moment the bottom line reads **"[E] Hand it back (Rejected)"**, because Grace is still within E's reach as she walks to her seat.
  - One more E hands the camera back unfixed. Nothing guards it. A Rejected hand-back is "Never again", the worst review, and that night the man says "Grace walked out."
- **Where:** Day 1, about 11:18 AM, 0.7 s after the conversation closed. The window lasts about 1–3 s, while she walks out of the 2.2 m reach.
- **Evidence:**
  - [seen] guide `06`: the card, the bench pin, Grace's name pinned over her, and "[E] Hand it back (Rejected)".
  - [read] `CustomerInteractable.cs:75–80, 107`.
  - [read] `CustomerBrain.cs:602–614`: E counts as a hand-back once the device is in one piece, in hand, and its owner is waiting.
  - [read] `CustomerBrain.cs:1922–1927` (`CompleteJob`); `ReputationRules.cs:111–112`; `NightZero.GraceLetDownOn`.
  - [inferred] the length of the window, from the till (−0.26, 14.54) to the queue line about 2.24 m away.
- **Why it matters:**
  - It's the game's first hand-off.
  - The newcomer has just pressed E twice to take the job, and a quick second E now counts in conversations.
  - To a stranger, "Rejected" doesn't say "she leaves with it broken".
- **Fix:** Don't offer an unfinished hand-back while the card's step is "carry it to the bench", or word it "Give it back unfixed".

### 3. On Day 1 the card names someone E isn't serving, twice

**At the till**

- **What happens:**
  - At 2:05 the card reads "DAY 1 · THE OFFICER / E talks to the Officer; when your replies appear, E takes the order.", with the green pin on him.
  - The prompt reads "[E] Talk to Tomas", with Tomas's name pinned.
  - From above, E talks to whoever reached the counter first: Tomas at 57.6 s, the Officer at 84.4 s.
  - In the no-input run the two disagreed for 52 s (REC 101–153).
- **Likely in play:** Tomas and the Officer arrive while the player is still at the bench with Grace's camera.
- **Evidence:** [seen] screen `day1-0125s`. [measured] trace. [read] `PlayerInteractor.QueueHead`; `DayOneGuideUI.PickGuided` (Day 1's story visitor).

**At the drinks machine**

- **What happens:**
  - The player goes to make the Officer's coffee. The card that says "DAY 1 · THE OFFICER / Walk to the drinks machine…" becomes **"DAY 1 · GRACE"** the moment Ace reaches the machine, because Grace's latte is waiting.
  - The next step will name her drink ("Look under the Latte nozzle and click"), not his coffee.
- **Evidence:** [seen] guide `11` → `12`. [read] `DayOneGuideUI.InHand` (an accepted customer with a drink order wins while Ace is at the drinks); `DayOneGuideUI.cs:383–385`.

**For both**

- **Why it matters:** The card is now what a newcomer trusts. In the first five minutes it names a person E won't serve, then a drink they didn't come to make. The guide lab never met either: it sent its walk-in home, and it stopped at the cup stack.
- **Fix:** At the till, let the card follow E's target. At the machine, keep the order the player just took, or name both drinks.

### 4. The first job is still the hardest one, and now it carries a latte

- **What happens:**
  - The scene's Day 1 is still the Grace showcase: clean the lens, the film path and the film guide, replace the shutter, and leave the strap alone.
  - Her latte is a second order on the first customer.
  - The cushion is much bigger than in run 1: 92 s at the till, then about 172 s once seated, with cards that stay and pins.
- **Evidence:**
  - [read] `ui-0` log; `systems-1` log; `Day_01_GraceCamera.asset` (`suppressRepairDrinkWishes: 0`); `Regular_Grace.asset` (`drinkWishChance: 1`, Latte).
  - [seen] guide 10–12: "+ Latte".
  - [measured] Bench 3 times the phone (about 26 s for a cracked screen) but does the camera with the check's own hand, so the camera has no time.
- **Why it matters:** Failing the first job is still the costliest minute in the game. Whether a stranger finishes the camera in 172 s needs hands.
- **Fix (his call, as in run 1):** Learn the Shop's coffee and one-fault repair before Grace, or keep Grace first.

### 5. Customers call for "coffee" whatever they want, and only the drink they want can be served

- **What happens:**
  - A walk-in who adds a drink while waiting calls it across the room.
  - 7 of the 10 lines say coffee: "Ooh — could I get a coffee too?", "Coffee. While I wait.", "Coffee? To go, if that's alright."
  - The wish is rolled from all six drinks. In tonight's runs, 0 of 7 wishes were coffee: espresso ×2, tea, Americano, hot chocolate ×2, latte.
  - The ticket says "+ Hot Chocolate", and a cup of anything else can't be served.
- **Evidence:**
  - [read] scene `CustomerSpawner` `orderedDrink` lines (`AcesCafeLayoutPlaytest.unity`, the five archetypes, from line 58411).
  - [read] `CustomerBrain.OrderLine` (fills `{drink}`; these lines have none); `CustomerBrain.FindServeableDrink` (`cup.Drink == WantedDrink`).
  - [measured] `drink_wish` columns, Day 1, 3 and 5.
  - [seen] Day 5, REC 95–131: from above at 960×540 the bubble isn't legible, so mostly the ticket is what reads.
- **Why it matters:** Whenever the line is read (first person, or close), the newcomer makes exactly what was asked and gets "Got half of what I came for." It happens every day from Day 1 (Tomas wanted an espresso).
- **Fix:** Put `{a drink}` in those lines (placeholder copy for his sister).

## Notes in his format

*B bug · F feel · L looks · S story. His "Night 1" is the game's Night 0.*

### Day 1

- **9:00 AM · L.** The first 15 s now say four things:
  - who you are: "You're Ace. Your café's first day: Grace is on the way with a repair." (14 words);
  - "You" over Ace;
  - a green "Grace" pin on her from the door;
  - "WASD Move · E Use" on the strip.

  [seen] screen 0:01; video REC 1–8.
- **9:00 AM · S.** Still nothing in the game says the premise (broke, the mortgage). The store page has to carry it. [read] "mortgage" appears nowhere in code or data.
- **9:16 AM · F.** Grace reaches the till about 15 s after opening (run 1: about 40 s). [measured] trace.
- **9:53 AM · L.** Her name and a 56×7 green bar sit over her head, not over the floor. [seen] screen 0:21; guide 02.
- **11:18 AM · B.** "[E] Hand it back (Rejected)" under the card "E sets it down" (top five #2). [seen] guide 06.
- **11:19 AM · L.** At the bench, the pin names the device ("Grace's reunion film camera") and the prompt matches it. [seen] guide 07.
- **11:25 AM · L.** In the close-up the card sits over the prompt, near the work. With the card up, the bottom of the screen carries about 40 words (card 12, prompt lines about 30). [seen] guide 08.
- **11:32 AM · F.** GETTING IMPATIENT: "Grace is getting impatient. Walk over and press E to reassure."
  - At the bench, E is her camera ("[E] Work on the reunion film camera").
  - From behind the bench, E reaches over the counter only when nothing nearer is in reach, and her device on the mat is always nearer. So for a customer on a bar stool, "walk over" means the walk round the counter.
  - Systems' "Reassure works over the counter to the stools" holds only away from the device.

  [seen] guide 09. [read] `PlayerInteractor.cs:436–443, 569–593`. Needs hands.
- **Waiting · S.** On the café's first day Grace orders "My usual latte, please. No sugar." [read] `Regular_Grace.asset:65` against the first card.
- **1:44 PM · L.** With nobody served the cash reads $20. It counts up quietly now, with no pop. [seen] screen 1:11.
- **3:15 PM · S.** Grace walks out after 92 s unheard. A red burst is the only sign; the card moves straight on to the Officer. [seen] REC 100.9–101.5.
- **3:15–5:30 PM · B.** The card says "the Officer" while E talks to Tomas, for 52 s (top five #3). [seen] screen 2:05.
- **3:15 PM · L.** The Officer's green pin and Tomas's name pin overlap side by side at the counter. [seen] REC 100.9–103.5.
- **4:12 PM · L.** When a step changes, the old green pin fades out where it was, already showing the new name. A half-faded "Officer" sits over the bench for about 0.13 s. [seen] guide 10. [read] `UiPins.Want` and `PlacePin`: the text changes at once; a covered pin stops moving and fades.
- **4:23 PM · B.** At the machine for the Officer's coffee, the card turns to "DAY 1 · GRACE" (top five #3). [seen] guide 11 → 12.
- **4:23 PM · L.** The dispenser close-up frames all six named paddles. [seen] guide 12.
- **The drinks machine, first person · L.** The card's first step, "Look at the cup stack and click to take a cup", is the look that cuts Americano and Latte. [read] dispenser lab, 3.2 s; `DayOneGuideUI.cs:388–389`.
- **8:00 PM · L.** Tonight after a day with nobody served:
  - "$65 earned today" (it counts up; the screenshot caught $51, [inferred] from `RecapPhone.CountUp`);
  - "3 reviews today · -3 reputation" and "5 to your first star";
  - "Grace · Let down · The queue didn't move, so I did."

  "$X from your work · $Y from café guests" is only on the Reviews and Today screens. [seen] Day 1 recap screen. [measured] day summary: $65, all from patrons.
- **8:00 PM · S.** "Grace · Went into the saffron house at 12 West Street." plants the night's address on Day 1's phone. [seen] Day 1 recap screen.

### Night 1 (the bins, the deal, Barnaby)

- **11:00 PM · L.** "Last job: the bins. Out the back." with "[E] Take the bins out" is clear. [seen] Night 0 `02`.
- **11:00 PM · S.** The deal still reads as in run 1. After a day she walked out, he says "Grace walked out. Café owners who talk don't last." [read] walked-out lab, 23.0 s.
- **11:05 PM · F.** "Barnaby: Grace's front step, the saffron house on the corner."
  - At her step the porch light marks the step, and Barnaby's red hat shows.
  - The house reads dark brown with white bays, not saffron.

  [seen] Night 1 `03`; Break-ins 7 `00`, `17a`. Needs hands.
- **11:43 PM · S.** "Learned: Nerve. When Grace asks, your face will give less away." [seen] Night 0 `12`.
- **His pages · S.** "Grace: the saffron house on the corner. Asleep by 1:45." matches her new hours (bed 1:20, lamp off 1:40). [read] `LodgerStory.cs:310–311`; `GraceNight`.

### Day 2

- **9:16 AM · S.** "Who steals a garden gnome, Ace?" with "[Space] Keep a straight face · Nerve" reads. [seen] Night 0 `15`.

### Night 2 (the cups)

- **11:00 PM · S.** His ask comes with two chips, then "The reunion cups: Grace's kitchen worktop. Her front door opens." [seen] Night 2 `03–04`.
- **11:15 PM · F.** Waiting her out costs 116 s on her stoop (run 1: 52 s).
  - From above, her lit house and her dark house look the same, apart from a small warm pool on the pavement.
  - In first person from the street, her lit front window with the TV in it reads at once.

  [measured] Night 2 report. [measured] pixel difference of `16a` and `16c`. [seen] `17a`.
- **Inside · L.** The rings read: a small one sneaking, a wide one walking; "?" then "Hello?"; then "Caught." with its card. [seen] Break-ins 7 `18a`, `18b`, `07`, `12–13`.

### Day 3

- **9:16 AM · L.** The clock line has its gap. [seen] Night 2 `10`.
- **Morning · S.** The Officer asks: "Have you seen a man of this description? Suit. Fifties. Looks like he sleeps rough." with "[Space] Say nothing". The best pull into Day 4. [seen] Night 2 `14`.
- **All day · L/S.** Customers call for "coffee" whatever they want (top five #5). [read].

### Night 3 onward

- **11:00 PM · S.** "Nothing tonight. The street is yours." (top five #1). [read].

### Day 5

- **All day · F.** One pair of hands that never walks:
  - 4 of 8 accepted walked out waiting, all seated, at patience 0.0;
  - both "Never again" were Rushed customers;
  - the GETTING IMPATIENT card showed once, at about REC 90, for Malcom.

  [measured] Day 5 log. [seen] REC 90–97.
- **8:00 PM · L.** The recap reads cleanly:
  - "$446 earned today", "5 to your first star", "8 reviews today · 0 reputation";
  - "Yuki · Loved it · Came for the americano, stayed for the vibes.";
  - "Ali · Never again · Took my phone, then forgot I existed."

  [seen] Day 5 recap screen.
- **The Shop · L.** "Shelf Space: One more slot on the intake shelf." Everywhere else the object is the "intake tray". [seen] recap `05`. [read] `Upgrade_ShelfSpace.asset:16`.

## Numbers that matter to a newcomer

| What | Number | Source |
|---|---|---|
| Day length | 180 s, 9 AM–8 PM (16.4 s a café hour); ship target 420 s | [measured] `frames.csv`; README |
| First customer at the till | about 15 s after opening (run 1: about 40 s); labs C/D/E 13.4–14.2 s | [measured] trace (8.8 s into a recording that began 6.9 s in); day-shape reports |
| How long the first customer waits for someone who doesn't know what to do | Grace 92.0 s; Tomas 96.0 s; the Officer 88.0 s | [measured] trace |
| Grace's patience after "yes" | about 172 s, seated or at a bar stool | [read] `systems-1` log |
| Day 1 card | full 6 s, then one 18 px line, full again after 10 s stuck, and it stays; 9–18 words a card (run 1: 7–14) | [seen] video; [read] `DayOneGuideUI` |
| Card and E naming different people, no-input Day 1 | 52 s (REC 101–153) | [measured] trace; [seen] 2:05 |
| Unfixed hand-back on offer after the pickup | about 1–3 s (seen at 0.7 s) | [seen] guide 06; [inferred] |
| Presses from above | drink 8, repair 7, drink to a bar stool 8; walk for a stool hand-back 0.00 m (run 1: about 20 m) | [measured] Stations A |
| Robot hand at the bench | cracked screen about 26 s; cleaning only about 7 s; Grace's camera not timed | [measured] Bench 3 |
| Day 1, nobody playing | 3 came, 0 served, 3 "Let down", −3 reputation, $65 (all from patrons) | [measured] day log |
| Day 3, fast autopilot with the stress tests | 10 of 10 served, everyone who waited sat, +20 reputation, $183 | [measured] day log |
| Day 5, human-speed autopilot (never walks, never makes a drink) | 11 came, 8 taken, 4 walked out waiting (seated, patience 0.0), 2 "Never again" (both Rushed), $446, ±0 reputation | [measured] day log |
| First star; first upgrades | 5 reputation ("Loved it" is +2); Wide Brush $300, Magnetic Driver $350 | [read] `ReputationRules`; [seen] recap `05` |
| Night 2 | 116 s on her stoop to wait her out, then a 48 s window | [measured] Night 2 report |
| Nights 3 onward | 0 errands | [read] |
| Text at 1080p | card 21 px (18 compact); pins 17–18 px; bars 56×7 px; "Fixed!" 9.2:1 on its plate | [read] code; [measured] Bench 3 |

## What reads right (leave it alone)

1. **The first 15 seconds:** who you are, who's coming, where she is, and how to move, all on screen.
2. **The repair bar:** "Hand it back" over the counter to the stool, with 0.00 m walked.
3. **One ending:** "Fixed! · Perfect" once, on a dark plate, and the cloth lifting off clean glass.
4. **The bins:** the deal and its notes; Nerve now says what it does; "Grace walked out." when she did.
5. **The phone:** "5 to your first star", verdicts in words, one screen.

## Needs his hands (questions for tonight)

1. **The pickup.** Right after you pick up Grace's camera at the till, what does the bottom line say? Did you press E before you walked? (Look for "Hand it back (Rejected)".)
2. **The till.** Back at the till after her camera, who did E talk to, and who did the card name?
3. **The Officer's coffee.** At the machine, whose name was on the card, and which drink did it name?
4. **Grace's camera.** Time "I'll take a look" → "Fixed!" → the hand-back. Did she stay? (The budget is about 172 s once she's seated.)
5. **Reassuring.** When GETTING IMPATIENT came up, could you reassure someone on a bar stool from behind the bench, or did you walk round?
6. **Drink calls.** When someone called for a drink while waiting, could you read it from above, and did it match their ticket?
7. **Night 1.** How many seconds to find Barnaby? Did the lit step catch your eye? Did the house look saffron?
8. **Night 2.** Did you go in while she was up, or wait the two minutes? Did the rings tell you anything?
9. **Night 3.** After "Nothing tonight", did you still go out on Night 4?
10. **Day 1's phone.** Did "$X earned today" feel like your own money?

## Runs I'd need

1. **A hands-on Day 1, recorded.** Still not done. Menu: Playtest › Play the whole game from Day 1 (test save), then Café life › Record 8 minutes (Game view). Look at the prompt in the 3 s after the camera pickup, who E talks to when the Officer arrives, the card at the machine, and Grace's camera time.
2. **The Day 1 guide lab without its two shortcuts.** A variant of Playtest 4 › UI › 1 that keeps the walk-in and carries on past the cup stack to the pour. Look at E's target against the card, and at the drink named at the machine.
3. **Stations A and B on the final build.** Playtest 4 › Systems › Lab A / B. Look at the over-the-counter hand-back with the new pins, and at a customer's watch in the close-up without "#2".
4. **A night after Night 2.** A lab save at Day 3's recap, then the night. Photograph the bins, what he says, the note, and what's offered.
5. **A 1080p capture of a mid-wait drink call,** from above and in first person (Café life › Lab › Day 3, I serve). Is the bubble legible, and does it name the drink on the ticket?

## Flags for the leads

- **A lock to check, not a bug report.** The Bible says "Grace's Day 1 latte suppression is intentional". On the scene's Day 1 (`Day_01_GraceCamera`, `suppressRepairDrinkWishes: 0`; Grace's `drinkWishChance: 1`) her ticket shows "+ Latte" (guide 10–12). Only `Day_01_LearnTheShop` suppresses it. If the lock means no latte on Day 1, the scene's Day 1 breaks it; if it meant Learn the Shop, ignore this. (Producer, Systems)
- **The README's instrument claim.** Seven lab runs ran before the UI part, and their HUD is the old one (see The run). (Producer)
- **The labs' shortcuts.** The guide lab's two shortcuts hid #3, and the Stations lab's wait hid #2. Only the guide lab's photo 06 shows #2, and only because it photographs every step. (UI, QA)
- **Reassuring over the counter.** "Reassure works over the counter to the repair-bar stools" (`systems.md` S1) holds only when nothing nearer is in reach. At the bench, the customer's own device is always nearer. (Systems, UI)

Source: Steam refunds are for purchases within 14 days with under 2 hours played ([TechRadar](https://www.techradar.com/news/gaming/steam-s-new-refund-policy-should-make-gamers-very-happy-1295631)).
