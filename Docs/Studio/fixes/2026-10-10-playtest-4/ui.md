*The UI/UX lead's report to the Producer, 10 October 2026, as returned (the playtest-4 fixes: every finding in both playtesters' first reports). Paths under `/home/claude/cafe/s7/` are the cloud workspace the leads wrote in; every file they name is in the repo at the same path after `Assets/`, `Tools/` or `Docs/`. What was integrated, and how it was checked on the Mac, is in the commits (parts 1-7) and `Docs/Studio/sessions/2026-10-11.md`.*

## Lead 2, UI/UX: playtest 4 fixes, report (common.md §8)

Every file is under `/home/claude/cafe/s7/ui/`. All 14 C# files pass the syntax check. The scene doesn't need to change: everything new is built at runtime.

### 1. What I did, finding by finding

**U1. Day 1's cards** ("Day 1 teaches the loop in 6-second cards that never come back"; "the steps that need a card most come while the eye is on the bench")
- **The cards stay.** `DayOneHintTimer` was rewritten. A step's card shows in full for the day's `openingHintDuration` (6 s), then shrinks to one smaller line (18 px). If the step is still the same 10 s later, the card comes back in full with one pulse (scale ×1.06 and a gold edge, about 0.6 s, no sound) and stays full.
  - A step seen before comes back compact, for example after a conversation.
  - "Waiting" cards shrink but never pulse.
  - The visit-over note shows once for 6 s, then goes.
- **At the bench** the card sits bottom-centre, from 164 px up (the prompt's top is about 142 px there), 580 px wide.
- **Pins (overhead only).** A green pin names the step's target: Grace / "Behind the counter" / "Intake tray" (over the device) / "Repair bench" / "Drinks" / whoever to hand back to. This is `UiPins.ShowGuide`.
  - When E's name pin is already on the same thing, only the name shows.
- **"You" over Ace.** It shows from Day 1's start until he moves 1 m, or 15 s pass, or a conversation opens.
- **First card:**
  - "You're Ace. Your café's first day: Grace is on the way with a repair." (69 characters)
  - When he isn't behind the counter, it ends "…Go behind the counter." (78). "Start behind the service counter" is gone when he's already there.
  - It stays up while Grace walks in, so it isn't a 3-second card.
- **Movement strip.** Day 1's controls strip starts with "WASD Move · E Use" (pad: "Left stick Move · A Use") until Ace has walked 3 m and used E once.
- **The Officer's coffee** gets its own cards: "DAY 1 · THE OFFICER", "E talks to the Officer; … E takes the order", "Walk to the drinks machine on the back bar…" with the Drinks pin, then the cup stack, nozzle, paddle and serving lines (shortened).
  - When the Officer reaches the counter mid-repair, Grace's card adds "The Officer is at the counter too."
- **True ending.** "Finish the remaining requests" is now "Grace's visit is over. Closing time brings the day's recap." With orders open, it says "…Finish the open orders; …".
- **Words:** "intake shelf" is now "intake tray" everywhere ("Press E to pick up Grace's reunion film camera from the intake tray.").
- **The check:** `Checks › Day 1 onboarding` now tests the scene's `schedule[0]` (Day_01_GraceCamera) and still tests LearnTheShop. It no longer asserts arrival times, patience or cap (those are Systems' numbers).

**U2. The first walk-out nobody explains**
- One card, "GETTING IMPATIENT", on any day: "Walter is getting impatient. Walk up to them and press E to reassure them." (74 characters; pad says A).
- It fires the first time a waiting customer gets frustrated: their sweat drop (`NpcBeats.CurrentMood() ≥ Frustrated`, patience below 0.3), only if they're accepted and Reassure is possible. Their name is pinned over them.
- It lasts at most 12 s, ends when they're reassured or calmer, and shows once a day until the player has reassured anyone (once per session after that).

**U3. Who and where from above** ("Grace floats over the empty floor"; the bar was "23–26 × 3 px")
- **New `UiPins` screen** (sorting order 30). In the overhead view, the name of what E acts on is pinned over their head, above any pop, bark or speech bubble.
  - Stations get plain names: "Repair bench", "Drinks", "Intake tray". A device gets its owner: "Grace's reunion film camera".
  - First person keeps the crosshair name, as before.
- **Patience bars** are now a constant 56 × 7 px at 1080p. They sit just under the head point, so badges and barks are always above them.
  - Colour runs green → gold → red, as on the ticket, and pulses under 25%.
  - Once the job is taken, a dot in the job's colour sits beside the bar.
- `PatienceBar` keeps its API, so CustomerBrain, GraceAtHome, Lodger, NightNeighbours, CafeArrivals and NpcSeating are unaffected. The world strip is switched off while playing.

**U4. "#2" on the dial**
- `JobMarker` shows no digit anywhere. It's now a 15 px disc in the job's colour on a paper rim, just over the device (beside a cup's freshness ring).
- It's hidden in the bench close-up, during conversations, during the recap, and in first person for the item in Ace's hands.
- Its colour is `JobColor`, the same as the ticket tape and the dot by the bar. Callers are unchanged.

**U5. The phone**
- "no stars yet" is now "N to your first star" (N from `ReputationRules`), on Tonight and on the pause-phone Today screen. Today counts today's reviews in and shows "a new star at closing" once they reach it.
- Review lines and review cards show their verdict as a coloured pill: Loved it / Liked it / Fine / Let down / Never again. No stars on reviews.
- The summary's five bars are labelled by verdict, in verdict colours.
- Takings split: "$132 from your work · $10 from café guests" on the Reviews takings card and on Today. The Details row "Café walk-ins" is renamed "Café guests".

**U6. HUD**
- **Clock line:** the day and the time are measured, with the time at its widest. They stay at 16 px when there's a 12 px gap; otherwise both shrink, down to 12 px at least. The `ClockLine` format is unchanged.
- **Mouse tools line at the bench:** "[Click a tool] Take it · [Drag] Turn it · [R] Flip it · [Right-click] Put item down". With a tool in hand: "[Hold click] Use the screwdriver · [Click a tool] Swap · [Right-click] Put tool down". These are the real `ItemInspector` bindings; the pad line is unchanged.
- Bench card wording: "Select the X" becomes "Take the X (click it)" with a mouse.

**U7. Other report items**
- The A-frame's words are the street sign's art, not mine. Everything else that was mine is handled above.

### 2. Decisions, and decisions yours to overrule

| # | Decision | Reason |
|---|---|---|
| 1 | Timing is 6 s full, then compact, then full again at 16 s with one pulse, then it stays full | "Comes back when you're stuck", without nagging |
| 2 | Bench card goes bottom-centre; the drinks close-up card stays top-left | The Bench lead is re-framing the dispenser, and a card there could cover paddles |
| 3 | Pins also show on Day 2's Grace return card | It's the same guided mechanism |
| 4 | Movement words are added in ShopUI (which builds the strip), not in `CafeViewMode.ControlsHint` | Nothing that reads `ControlsHint` changes. "Used E" means E/A pressed while a prompt was showing, or inside a conversation |
| 5 | U2 is once a day per session, not once per save | SaveData has no store for small flags, and adding one changes the save format |
| 6 | Reassuring is detected heuristically: patience jumps ≥ 0.1 while Reassure cools down | Avoids reading CustomerBrain's private fields at runtime |
| 7 | The takings split isn't on Tonight | Tonight has a 40-word budget; the split would add 4 words. "N to your first star" already adds 2 words on starless evenings |
| 8 | After the first star the line still reads "1 star" / "N stars" | Could become "N to your next star" if you want it |
| 9 | Pin labels say "Officer"; sentences say "the Officer" | His name is his job |
| 10 | The new Day 1 lab leaves Play mode by itself | Saves a manual stop after each run |
| 11 | Patience bars show in first person too | Same as the old world strip |
| 12 | The first card is marked PLACEHOLDER COPY | It's the game's first words; your sister may want to rewrite them |

### 3. Files (all under `/home/claude/cafe/s7/ui/Assets/`)
- `Scripts/UiPins.cs` (NEW): the screen for the patience bars, the focus name, Day 1's pin and "You" (order 30; hidden for the recap, at night, in conversations and close-ups).
- `Scripts/DayOneGuideUI.cs`: cards that stay, compact and pulse, the bench placement, pins, "You", the movement flag, the Officer's coffee, the impatience card, true words, the intake tray. `DayOneHintTimer` has the new policy. The names the checks reflect on are kept: `FeaturedAction`, `RepairAction`, `inspector`, `hintDay`.
- `Scripts/FocusNameUI.cs`: the name pinned over E's target overhead; crosshair name in first person; the station name constants.
- `Scripts/PatienceBar.cs`: same API; drawn by UiPins while playing.
- `Scripts/JobMarker.cs`: the colour tag; the "#1" label is cleared and never shown.
- `Scripts/ShopUI.cs`: makes the pins at Start, the Day 1 movement words, the mouse tools line, and the `ViewHint` / `PromptLine` accessors.
- `Scripts/HudCorners.cs`: the clock-line layout, plus `ClockGap`, `ClockLineSize` and `GapFor()`.
- `Scripts/RecapPhone.cs`: verdict pills and bars, `StarsLine` / `LiveStarsLine`, `TakingsSplit`, `VerdictOrder` / `VerdictWord`, the "Café guests" row.
- `Scripts/Diagnostics/Day1GuideCheck.cs` (NEW): the Day 1 guide lab.
- `Scripts/Diagnostics/RecapPhoneCheck.cs`: verdict checks, a no-star Preview (photo 01b) and the takings split.
- `Scripts/Diagnostics/PauseCheck.cs`: the clock gap for every weekday (and Day 10/100), the Today star line, the split.
- `Editor/DayOneOnboardingChecks.cs`: new timing tests, the scene's own Day 1, word checks; LearnTheShop kept.
- `Editor/ReputationRuleChecks.cs`: verdicts instead of star asserts; new phone-word checks (only inside `#if UNITY_EDITOR`, so `RunAll` stays pure).
- `Editor/Playtest4UISteps.cs` (NEW): the read-only survey and the lab launcher.

### 4. Unity steps, in order
1. `Fixit Fidget/Playtest 4/UI/0 - Check first (read-only)`. Expect "READ-ONLY: 16 passed, 0 failed" (Logs/Playtest4/ui-0-check-first-last.txt), with info lines about the Officer's Day 1 visit. There are no scene steps: nothing to save.

### 5. Labs, in order (café scene open)
1. **`Fixit Fidget/Checks/Day 1 onboarding`** (Edit Mode, instant). Now needs the café scene open, because it reads the spawner.
   - Pass: "[Day 1 onboarding] PASS: … the scene's own Day 1 (Assets/GraceShowcase/Day_01_GraceCamera.asset) …".
2. **`Fixit Fidget/Checks/Reputation rules`** (instant). Pass: "[Reputation rules] PASS: N assertions".
3. **`Fixit Fidget/Playtest 4/UI/1 - Day 1 guide - play check (lab, drives itself)`**: about 2–3.5 min. Keep the Game view in front, 16:9.
   - Output goes to Logs/UI/day1-guide-<time>/: report.txt plus up to 12 photos saved at 1920×1080, including 02-grace-at-the-counter-name-and-bar (the default camera).
   - Pass: about 50 checks, 0 failed.
   - A note "the Officer didn't reach the counter in time" is not a failure; it depends on Systems' arrival rules. It also notes a missing keyboard focus and opens the conversation directly.
   - It drives Grace's visit with the game's own calls up to her camera on the bench (the repair itself is Bench 3's), then the impatience card, then the Officer's first steps.
   - What a player sees: the card always up, short after a few seconds; pins over Grace, the tray and the bench; names over heads; small coloured bars.
4. **`Fixit Fidget/Recap phone/Play check (lab, drives itself)`**: about 40 s. All PASS, plus the new photo 01b-tonight-before-the-first-star ("3 to your first star") and verdict pills in 01 and 02.
5. **`Fixit Fidget/Playtest/The phone by day - pause and HUD check (lab, drives itself)`**: about 2 min. Adds the clock-gap checks (≥ 10 px on every weekday) and the star and split lines.

**Labs that could break:**
- Already updated: the four above.
- Visual only, should still pass:
  - Stations as reach (overhead and first person): repair-5-working-on-it.jpg should show no "#2".
  - Bench 3: tags hidden; the pad line is unchanged.
  - Juice: pins sit above pops; bars sit under the head point.
  - Barks 2: barks (order 80) draw over the pins (30).
  - Conversation check and Controller check: `ControlsHint` is unchanged.
  - Night 1/2 and Break-ins 7/8: everything of mine is hidden at night. GraceAtHomeCheck and JuiceCheck read `ClockLine`, whose format is unchanged.
- Performance check: about 0 KB a frame of garbage from my code (strings are built only when they change). Watch it anyway.

### 6. Needs another lead's file
- **Nothing is required.**
- Optional, comment only, in `Scripts/ReputationRules.cs` (nobody owns it): replace the Review enum summary with
  `/// <summary>A customer's verdict on their visit. The number orders the verdicts (1 Never again ... 5 Loved it). Since playtest 4 the recap phone shows each review's verdict in words (RecapPhone.VerdictWord), not stars: "stars" only ever means the café's (claude/reputation-spec.md §10.2).</summary>`
  and the `StarsOf` summary with
  `/// <summary>A review's verdict as a number, 1-5 (0 for no review). The phone no longer shows it (playtest 4); kept for older recaps. Never averaged.</summary>`
- **Private names my checks and lab read by reflection.** Keep them, or tell me:
  - CustomerBrain `wasAccepted`, `repairReturned`, `drinkOrdered`, `activeJob`, `patienceLeft`
  - CustomerSpawner `schedule`, `today`, `lastSeenDay`, `openingDrink`, `drinks`, `devicePrefabs`, `RollJob`, `RollDrinkWish`
  - DayClock `dayLengthSeconds` and the private setter of `TimeRemaining`
  - ItemInspector `focusedItem`
- **Public APIs the lab uses:** ConversationController `Begin`, `Advance`, `ChooseReply`, `Replies`, `ReplyKind.Accept`; `CustomerBrain.ForceRemove` (walk-ins are sent home to make room for the Officer).

### 7. Open questions, flags
- **No new mechanics.** All of this is presentation of existing rules (Reassure, patience, stations, verdicts).
- **Locks are untouched:**
  - Grace's Day 1 latte suppression: the guide never asks for a drink she didn't order.
  - The deal at the bins.
  - Night: everything of mine is hidden then.
- **Your calls:**
  - Stations are named from afar only on Day 1's steps, or when E targets them. Do you want permanent subtle labels?
  - The prompt's verb stays at the bottom.
  - The "G" portrait box stays until the art.
- **Not mine:** the A-frame's words (street-sign art).
- **The veteran's question:** "in the bench close-up, how did you know someone reached the till?" Only Day 1's "…at the counter too" line covers it; a general cue isn't built.
- **Once-per-save impatience card:** needs a SaveData field, which belongs to the Technical Director.

### 8. Syntax check (`/home/claude/cafe/s7/tools/syntaxcheck` on all 14 files)
```
OK    ./Assets/Editor/DayOneOnboardingChecks.cs
OK    ./Assets/Editor/Playtest4UISteps.cs
OK    ./Assets/Editor/ReputationRuleChecks.cs
OK    ./Assets/Scripts/DayOneGuideUI.cs
OK    ./Assets/Scripts/Diagnostics/Day1GuideCheck.cs
OK    ./Assets/Scripts/Diagnostics/PauseCheck.cs
OK    ./Assets/Scripts/Diagnostics/RecapPhoneCheck.cs
OK    ./Assets/Scripts/FocusNameUI.cs
OK    ./Assets/Scripts/HudCorners.cs
OK    ./Assets/Scripts/JobMarker.cs
OK    ./Assets/Scripts/PatienceBar.cs
OK    ./Assets/Scripts/RecapPhone.cs
OK    ./Assets/Scripts/ShopUI.cs
OK    ./Assets/Scripts/UiPins.cs
exit 0
```