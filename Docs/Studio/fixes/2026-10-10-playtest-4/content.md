*The Content & Dialogue lead's report to the Producer, 10 October 2026, as returned (the playtest-4 fixes: every finding in both playtesters' first reports). Paths under `/home/claude/cafe/s7/` are the cloud workspace the leads wrote in; every file they name is in the repo at the same path after `Assets/`, `Tools/` or `Docs/`. What was integrated, and how it was checked on the Mac, is in the commits (parts 1-7) and `Docs/Studio/sessions/2026-10-11.md`.*

# Lead 3, Content & Dialogue: report (playtest 4 fixes, 10 Oct)

All 14 files are in `/home/claude/cafe/s7/content/` as complete files. All 14 pass the syntax check. I compiled most of the new code for real here with Roslyn, against the repo's own Unity-free files plus stubs copied from the real signatures:
- `FaultSpeech` and `LodgerStory` compile with their actual neighbours.
- All four editor files compile, together with the real `Playtest4Shared`, `CustomerProfile`, `DeviceDefinition`, `DayDefinition` and `ReviewLines`.
- The new logic was run in that build: every template filled with every fault; the eight "Grace's day" cases; the sibling scene copy and its mirror check.
- The runtime lab edits (`ConversationCheck`, `NightZeroCheck`) and the small edits in `NightZero`, `ConversationController` and `CustomerIdentity` depend on too much of the scene to stub. For those I checked every member they use against the repo by hand.

## 1. What I did, finding by finding

**C1. "Walk-ins read their fault labels aloud"** (first hour #5: "This is Frozen Boot Sequence - Reconnect Signal. Can you do it now?")
- **The evidence needs one correction; the finding stands.**
  - The quotes were rebuilt from the data, not seen on screen. No walk-in conversation panel ever appears in the evidence: the lab autopilot calls `HearIntake()` without opening it, and the Day 1 run had no input.
  - `CustomerIdentity.SetFault` already lower-cased the label, and `SpokenName` already said "this phone". So the game really said "This is frozen boot sequence - reconnect signal. Can you do it now?" That is still a label read aloud. "This Phone" in capitals never reached the screen.
- **Where a label reaches speech:** only `{fault}` in `CustomerIdentity.Format`. That covers the walk-ins' lines on the scene's `CustomerSpawner` and Grace's profile lines.
  - Tickets, the bench, the carry line and the day log keep the label. `TicketLayoutChecks` asserts it on the ticket.
  - The Human fault's own scenario lines and the hold call's UI are already written as speech. Grace's camera intake is authored.
- **Built:** `FaultSpeech.cs`, a spoken form for each of the 8 labels the game can roll (4 on `PhoneRepair`, 1 on `PhoneJob`, 2 on `PocketWatch`, 1 on Grace's camera). `SetFault` now uses it. An unknown label falls back to lower case, as before, so `CustomerMemoryChecks`' "jammed gears" still passes.

| Label (ticket keeps it) | Said (PLACEHOLDER COPY) |
|---|---|
| Cracked Screen | the screen's cracked |
| Gunked Up, Won't Charge | it's gunked up and won't charge |
| Frozen Boot Sequence - Reconnect Signal | it's frozen on the start-up screen |
| Calls light up the screen, but the phone never rings | calls light up the screen, but it never rings |
| Service disconnected - call support to restore it | the service is cut off and support won't answer |
| Debris Inside, Won't Tell Time | there's grit inside and it won't keep time |
| Seized Movement | it's seized up and won't wind |
| (Grace's camera label) | the lens is dirty and the shutter's jammed, but the strap stays (never filled into a line today) |

- **Every line re-read.** 21 lines name a device or a fault: 10 walk-in requests and 11 of Grace's. Each was filled with each of the 7 faults a visit can roll, which is 147 fills.
  - **Before the steps:** 6 lines can't take a spoken fault: Impatient "This is {fault}", Rushed "Sorry — {device}, {fault}", Sentimental "It's {fault}", and three Grace callbacks with "this {device} has {fault}".
  - **After the steps:** 0 fail. The longest filled line is 96 characters; the walk-ins top out at exactly 90.
- **Step 1** rewrites the 3 walk-in lines. **Step 2** rewrites Grace's 7 callbacks: the 3 broken ones, plus 4 that reached 108–114 characters once filled. Each gets "My {device}: {fault}." on a line of its own.
  - Both steps change a line only while it still reads exactly as it did (the way `DialoguePassSteps` does).
- **Dialogue rules check** gains the two rules from the brief:
  - every rollable label has a spoken form that is a lower-case clause, not the label;
  - every filled line has no label word for word, no " - ", no Words In Title Case, nothing over 150, and "a"/"an" right.
  - It also gains two writing rules that make "reads" checkable: `{fault}` must follow "my {device}:", "—" or ","; and `{device}` needs my/this/the/your before it.

**C2. Fewer presses for a coffee** (veteran: "talk, hear them out, hear them out, take the request … On the tenth coffee of the day, those four are busywork")
- **What the four presses actually are.** Lines already move on by themselves. The whole line shows at once; the replies come up after the listening beat (0.35–1 s); the thanks closes itself after about 2.1–2.8 s for a walk-in.
  - The first "hear them out" was thrown away: `inputDelay` 0.2 ignores every press for 0.2 s after opening, and the lab's press lands about 6 frames after its talk press. In 3 of the 4 Stations runs it needed two.
  - The second "hear them out" only cut a 0.7–1.0 s beat short.
- **A player who reads needs 2 presses** per conversation (talk, take). Someone pressing as fast as the lab needs 3, which is §6.1's agreed fast path.
- **Fix: the timing, not auto-advance.**
  - One press now does one thing: the frame that opened, asked or picked never counts again, and any later press counts.
  - Step 4 sets the scene's `inputDelay` from 0.2 to 0. Until it runs, nothing changes.
  - I left the closing timing alone. It is already within 0.4 s of Claude's example formula: 1.8 s vs 1.8 s for "Right. Good.", 2.1 vs 2.15 for "Amazing, thank you.", 4.0 vs 4.4 for Grace's thanks.
- **Presses per drink / per repair, first person:**
  - before: 9 / 9;
  - after, with the same lab driver: 8 / 8;
  - a player who reads: 7 / 7 (the floor the lock allows);
  - overhead: drink 9 → 9, repair 9 → 8 (each includes 1 station press).
  - With my proposed `StationsCheck` change (§6), the lab reports 7 / 7 first person and 8 / 8 overhead.
- **Lock check (Bible §2, proposal §6.7):** confirmed safe. An E before the replies still only brings them up and never takes the job. The listening beat is the lock's "finished revealing", and it stays.

**C3. "Learned: Nerve. Your straight face holds a little longer." comes before the player has seen the straight face**
- Now: "Learned: Nerve. When Grace asks, your face will give less away." (11 words; the note budget is 12). It points at her question, which the man has just set up ("Keep it when she asks.").
- I avoided "tomorrow": `NightLedger.Unfaced` uses any earlier night, so she asks on her next visit, which isn't always the next day.
- Soft feet: "Your steps carry half as far." (was "Ace's steps", now the same "your" voice). It makes sense the night it's learned. Doors is unchanged; nobody pays it.
- No check quotes the lesson text. The Night lead needs nothing from me.

**C4. "Grace likes you." after Grace walked out**
- New line `lodger.night0.05b`: "Grace walked out. Café owners who talk don't last." It is 50 characters (the original is 49) and 9 words (the scene budget is 10, so "walked out on you" didn't fit).
- It plays from a sibling scene, `night0.deal.letdown`. That scene is the deal with only that one line swapped: same 7 other lines, same 2 choices, same hold. The deal itself is untouched.
- `NightZero.GraceLetDownOn` decides it from her saved memory for that day: she left unhappy (walked out, turned away, no room), or her camera went home unfixed or not at all. Tested cases:
  - true: walked out; turned away; camera back "Rejected";
  - false: Good or Passable camera; she didn't come; a bad day earlier.
- The man's gesture plays on either line. Without the step, the deal plays exactly as now.
- I branched with a sibling scene because `Barks.Play` only takes a scene id, and changing the asset's text at runtime would dirty it in the Editor.

**C5. Anything else in the reports that's mine**
- Review line "Came for a {thing}, stayed for the vibes." said "a espresso" and "a americano". It becomes "Came for the {thing}…" (step 2, `ReviewLines.asset`). The rules check now holds all 28 review lines to "a"/"an".
- Not mine, left alone: the deal's other lines, the Day 1 latte lock, the cards, the review stars (UI), and the cover hint (Bench).
- "Saffron house": I left the hint's words, because the Night lead's N4 (a lit window, a porch light) answers it.

**What a player sees:**
- "Hi! My phone — the screen's cracked. Any chance?" / "Quick one. My pocket watch: there's grit inside and it won't keep time. Can you do it now?" The ticket still reads "Cracked Screen".
- A quick double-tap of E shows the replies at once instead of doing nothing.
- On Night 1, the lesson note says what it changes. After a Day 1 where Grace walked out, the man says "Grace walked out."

## 2. Decisions his to overrule
1. The 8 spoken forms. Each is a lower-case clause, keeps a word from its label so the player can match it to the ticket, and is 47 characters or less (the camera's is 63, but it's never filled in).
2. The 3 walk-in rewrites:
   - Impatient: "Quick one. My {device}: {fault}. Can you do it now?"
   - Rushed: "Sorry — my {device}: {fault}. I'm late."
   - Sentimental: "My {device}: {fault}. It means a lot to me."
3. Grace's 7 callbacks get the request on its own line. Note: today she only visits for drinks outside her camera episode, so these lines can't be heard yet.
4. **C2: no auto-advance change** (it already exists). A quick second E now counts; the closing timing is untouched.
5. "When Grace asks", not "tomorrow".
6. One line, "Grace walked out.", covers walked out, turned away and camera back unfixed. "Turned away" isn't literally walking out; it's the man being curt.
7. The deal's sibling scene, rather than changing `Barks.cs`.
8. The Dialogue rules check now **fails until steps 1–2 have run**, by design: 7 problems before, 0 after.
9. I took `Assets/Data/Regulars/Regular_Grace.asset`, which isn't in the ownership table. The change goes through step 2; regulars' lines are this role's.

## 3. Files (all under `/home/claude/cafe/s7/content/`)
- `Assets/Scripts/FaultSpeech.cs` (new): the label → spoken table, `Spoken`, `TryGet`, `ReadsLikeALabel`.
- `Assets/Scripts/CustomerIdentity.cs`: `SetFault` uses `FaultSpeech`; comments.
- `Assets/Scripts/ConversationController.cs`: the one-frame press guard (`pressedFrame`); `inputDelay` default 0; header explains the pace.
- `Assets/Scripts/LodgerStory.cs`: `GraceLine`, `GraceWalkedOutLine`, `DealSceneLetDown`; the Nerve and Soft feet notes.
- `Assets/Scripts/NightZero.cs`: plays the sibling scene on a let-down night; `GraceLetDownOn`, `DealPlayed`, `DealLines`; gesture on both lines.
- `Assets/Scripts/NightLines.cs`: `Reindex()` (a step changes the arrays in the Editor).
- `Assets/Scripts/DialogueSet.cs`: tooltip and comment on how `{fault}` reads (for his sister).
- `Assets/Scripts/CustomerArchetype.cs`: the wait-kind tooltip no longer says "Impatient types loiter" (Systems seats them).
- `Assets/Editor/DialogueRuleChecks.cs`: the two rules, the slot rules, review lines, shared helpers.
- `Assets/Editor/BarkRuleChecks.cs`: the sibling keeps the deal's 8-line budget; `LetDownProblem` mirror check.
- `Assets/Editor/BarkSteps.cs`: Barks 1 also knows `05b` and the sibling scene and its choices; `GraceWalkedOutText`.
- `Assets/Editor/Playtest4ContentSteps.cs` (new): steps 0–4 and the "Grace walked out" lab.
- `Assets/Scripts/Diagnostics/ConversationCheck.cs`: next walk-in skips regulars (the Officer's new Day 1 visit); quick-E check; spoken-fault check; presses noted.
- `Assets/Scripts/Diagnostics/NightZeroCheck.cs`: checks the Grace line fits her day and the deal's other lines were all said; `GraceWalkedOutKey` variant.

## 4. Unity steps, in order
Run `Fixit Fidget › Checks › Pick up scripts changed from outside (reimport)` first, outside Play mode, and let it compile. Each step refuses if the scene has unsaved changes, so **File › Save after each one**.
1. **`Fixit Fidget › Playtest 4 › Content › 0 - Check first (read-only)`**
   - Report: `Logs/Playtest4/content-0-check-first-last.txt`, ending "READ-ONLY: 23 passed, 6 failed".
   - All 8 labels PASS. The 6 failing lines are the ones steps 1–2 fix, each with its 7 fills listed.
2. **`… › 1 - Walk-ins speak their faults (scene)`**: "DONE: 3 change(s); 19 check(s) passed". Save.
3. **`… › 2 - Grace's callbacks and a review line (assets)`**: "DONE: 8 change(s); 24 check(s) passed". It saves `Regular_Grace.asset` and `ReviewLines.asset`. Save the scene (the step only marks it dirty).
4. **`… › 3 - The man's line on a night Grace walked out (night lines)`**: "DONE: 2 change(s); 8 check(s) passed". It saves `Night lines.asset`. Save.
5. **`… › 4 - Conversations: a quick second E counts (scene)`**: "inputDelay: 0.2 → 0", 2 checks. Save.
6. Run step 0 again: "29 passed, 0 failed". Running any step a second time reports "NOTHING TO CHANGE".

## 5. Labs, in order
1. **`Fixit Fidget › Checks › Dialogue rules`** (seconds)
   - Pass: "0 problem(s), 1 warning(s)" (the old 97-character Grace story line).
   - It also reports "faults: 8 labels … 21 lines … filled in 147 ways; 28 review lines; Grace's Day 1 conversation: 14 checks passed" (was 13).
2. **`Fixit Fidget › Checks › Bark rules`** (seconds): PASS, before or after step 3.
3. **`Fixit Fidget › Checks › Customer memory and identity`** and **`Checks › Night 1 rules`** (seconds): PASS. These are regressions, kept safe on purpose: Grace's lines keep "ran out of time", "unfinished" and "familiar counter", and both lesson notes stay within 12 words.
4. **`Fixit Fidget › Dialogue › Conversation - play check (lab, drives itself)`** (about 2 min)
   - 33/33 with the Game view focused (was 31): a quick second E brings the replies up in about 0.1 s against a 0.6–1.0 s beat, without taking the job; the walk-in says its spoken fault.
   - If step 4 wasn't run, the quick-E check fails and says so.
5. **`Fixit Fidget › Night › Night 0 - Play check (lab, drives itself)`** (3–4 min): **96/96** (was 95; one new check: "his line about Grace fits her day … Grace likes you.").
6. **`Fixit Fidget › Playtest 4 › Content › Lab - Night 0 after Grace walked out (lab, drives itself)`** (3–4 min, needs step 3)
   - 96/96. The deal says "Grace walked out." (`night0.deal.letdown`), and all 7 other lines are said.
   - Report: `Logs/Night/night-zero-check-grace-walked-out-<time>/`.
7. Regressions to re-run:
   - `Fixit Fidget › Playtest › Stations as reach - play check, first person` and `overhead`: presses 8/8 with today's driver.
   - `Fixit Fidget › Night › Night 1 - Play check, keeping a straight face`: no assertion touches my text.

**Labs this could break:**
- **Dialogue rules** fails until steps 1–2 have run.
- **Stations as reach** fails if it still asserts exactly 9 presses (Systems is changing that).
- **Night 0** changes from 95 to 96 checks; **Conversation** from 31 to 33.

## 6. Needs another lead's file
**Systems, `Assets/Scripts/Diagnostics/StationsCheck.cs`, `TakeOrder`:** let it press like a reader, not into the beat. Replace the `for (int i = 0; i < 10 …` loop with:
```csharp
        // A player who reads doesn't press for the replies: they come up by themselves after the listening beat (0.35-1 s).
        yield return Await(() => conversation.RepliesShowing || !conversation.InConversation, 2f);
        for (int i = 0; i < 10 && conversation.InConversation && !who.WasAccepted; i++)
        {
            yield return Press(GamepadButton.South, conversation.RepliesShowing ? "take the request" : "hear them out");
            yield return Seconds(.45f);
        }
```
With it, the lab counts 7/7 first person. Without it, my change alone gives 8/8.

## 7. Open questions and flags
- **`Assets/Editor/ArchetypeRebuild.cs`** (menu `Fixit Fidget › Content › 4 · Rebuild archetypes as personalities`, August, in nobody's list) replaces every walk-in with the old lines and makes Impatient and Rushed stand again. It asks before it runs, and the new rules would catch it. I'd retire that menu item; your call.
- **`Assets/Scripts/ReviewLines.cs`** (nobody's): its code default still says "Came for a {thing}". It only matters for a new asset; optional.
- No new mechanic. No lock touched: the deal's 8 lines and its reveal, Grace's latte, and the replies gate are all unchanged.
- His sister: every new or changed line is PLACEHOLDER COPY. Lines over 150 characters: 0.

## 8. Syntax check
```
OK    ./Assets/Editor/BarkRuleChecks.cs
OK    ./Assets/Editor/BarkSteps.cs
OK    ./Assets/Editor/DialogueRuleChecks.cs
OK    ./Assets/Editor/Playtest4ContentSteps.cs
OK    ./Assets/Scripts/ConversationController.cs
OK    ./Assets/Scripts/CustomerArchetype.cs
OK    ./Assets/Scripts/CustomerIdentity.cs
OK    ./Assets/Scripts/Diagnostics/ConversationCheck.cs
OK    ./Assets/Scripts/Diagnostics/NightZeroCheck.cs
OK    ./Assets/Scripts/DialogueSet.cs
OK    ./Assets/Scripts/FaultSpeech.cs
OK    ./Assets/Scripts/LodgerStory.cs
OK    ./Assets/Scripts/NightLines.cs
OK    ./Assets/Scripts/NightZero.cs
```