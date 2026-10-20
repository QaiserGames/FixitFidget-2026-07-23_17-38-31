# Builder N report — never lost (19–20 Oct 2026)

Every file parses with tree-sitter (21 changed, 1 new; `python3 -I cscheck.py` on all of them: ok). Unity could not compile here. The icon contract (ControlHints.Interact etc. returning sprite tags) is written against as specified; nothing wraps a label in "[ ]" and no line says "Press X" with a name.

## 1. What I did (files changed)

Runtime (Assets/Scripts):
- `DayOneGuideUI.cs` — rewritten: the Now line, day and night (one dark plate, amber bar, 26 px bold step, 20 px title, pulse + tick on change; the guide's step, a fallback for unguided days, "Closing time: serve who's inside.", the night's line from NightCycle.NowLine); first card says the goal; every line one step, icon contract, ≤ 10 words where it is the line; "Go behind the counter" from the staff side (till, bench, back bar); "Take the {drink} to {name}".
- `NightCycle.cs` — `NowLine` (read-only, built from existing state, cached by state); notes queue + 2.5 s + 0.45 s/word floor (`NoteSeconds`); `ClearNote(text)`, `NoteShowing`, `LastJobNote`; the "Caught." card holds for a press or 8 s (`CardWaiting`, `GoOn`) with the Cross/E icon + "Go on"; "You" over Ace for the night's first 7 s.
- `NightZero.cs` — reveal camera stays on through the deal, off at SceneDone; "Last job: the bins" cleared at the back door; keys note as icons ("Torch · Notes"); the notebook note "His notebook is yours now. {icon} Read it."
- `Barks.cs` — night replies work like the day's: highlighted reply (► + Cross/E icon, light green), up/down (D-pad, stick, W/S, arrows, wheel), Cross/E/Enter/click takes it, 1/2 direct, Square/Triangle gone, the day-style footer under the chips, chips at 30 px, 0.2 s guard kept (`PickGuard`); `Highlighted`, `MoveHighlight`, `PickHighlighted` for labs.
- `BarkRules.cs` — `readingMin` 1.4 → 2.5 s (every bark holds its box at least 2.5 s).
- `NightNotebook.cs` — `EverOpened` (session flag for the nudge); D-pad up ignored while `Barks.Choosing`; facts 24 px, footer 20 px.
- `UiPins.cs` — `ShowSelfAtNight`: the one pin the night allows; bars/name/guide stay off at night; `HideSelf` keeps the night's "You".
- `LodgerStory.cs` — one name for her house (`GraceHouseToken/Name`, street as a token); favour `now` lines; hint texts ("{Thing}: Grace's step, 12 West Street. Yellow house, porch light."; cups: "…kitchen worktop. Go in once she's asleep."); his page "Grace: 12 West Street, the yellow house, porch light. Asleep by 1:45."; `Now()`; `Hint()` resolves street tokens.
- `NightThings.cs` — Grace's mention names "12 West Street. The yellow house, porch light always on."; notebook mention "…on her step, 12 West Street. Porch light on."
- `HomeDoor.cs` — `GraceLooks = "the yellow house"` (default `looks`).
- `NightSeeThrough.cs` — the bins' set (NightZeroSet subtree) never fades; fades piece by piece (`fadePieces`, default on); a piece over Ace's body keeps 1 in 10 dots (`overAceAlpha`), one over the ground ahead 1 in 4; `PiecesFadedNow`.
- `WordBudget.cs` — `NowLine = 10`; `Words()` skips `<…>` tags (an icon is not a word).

Data/Editor:
- `Assets/Data/Resources/Night lines.asset` — lodger.night0.09 "Grace's gnome. It watches my alley. Bring it here."; lodger.night0.11 "It watches my alley. I want it watching the street."; lodger.waiting.02 "Tick tock." (format untouched).
- `Assets/Editor/BarkSteps.cs` — the same three lines in Lines and as once-only Retexts.
- `Assets/Editor/HomeSetup.cs` — writes `door.looks = HomeDoor.GraceLooks`.
- `Assets/Editor/NightGuidanceSteps.cs` (new) — "Fixit Fidget/Night/Night guidance 1 - Only Grace's house is yellow; her porch light reads from the street (scene, with Undo)" + its Undo item. Repaints house #6's Saffron slots to Dusty rose (name kept: StreetDoorSteps finds it by name), porch light intensity 2.2 → 6 (range/cone untouched: they are what she sees Ace by), hangs a 0.28 m emissive sphere "Her porch lamp…" at the light (new material `Assets/Playtests/AcesCafeLayout/GH_Porch lamp glass.mat`, URP Lit, emission ×2.4). One Undo group, report in Logs/Playtest4/night-guidance-1-*.txt, photos before/after in Logs/Night/night-guidance-1-<time>/, scene marked dirty, never saved.
- `Assets/Editor/BarkRuleChecks.cs` — the 1.4 s floor assertions → 2.5 s (behaviour change intended).
- `Assets/Editor/DayOneOnboardingChecks.cs` — the words it asserts (first card "Five stars…", icons via ControlHints, no "Press E", no brackets, TalkWords, FirstNow/TakeDrinkWords budgets). Not in my explicit list; it is the Edit-mode rule check that asserts the words I changed and nobody else owns it — flagging it.

Labs (Assets/Scripts/Diagnostics):
- `Day1GuideCheck.cs` — first card "Five stars", staff-side test, 26 px step/title ≥ 20, settled line ≤ 10 words, icon strings via ControlHints.
- `NightZeroCheck.cs` — Now line at each step (door, outside, hidden through the deal, Barnaby's address + "notes", Barnaby in hand, home), "You" pin, reveal camera on through the deal and off after, hint waited for and must say "12 West Street"/"porch light", replies taken by S then E (highlight path, `PressKey`), "Last job" gone after the door.
- `NightTwoCheck.cs` — Now line after his ask and with the cups in hand; his counter note waited for (queue); replies by highlight.
- `GraceAtHomeCheck.cs` — the Caught card holds; the lab presses on with `GoOn()` (both places).

Not touched (not needed): GraceHouse.cs, GraceAtHome.cs, Lodger.cs, LodgerDay.cs, NightZeroSet.cs, NotebookRecap/Entries, NightTrophy, NightShelfTake, NightBackDoor, NightBins, NightOneCheck, BarkCheck.

## 2. Decisions and reasoning
- The Now line is DayOneGuideUI's card made dark (UiSkin.Band) for both looks, not only the compact one: one object, one place, the one text style every reviewer photo found readable. Title amber 20 px bold, step 26 px bold (`<b>`: true bold once T's Baloo 2 lands, synthesized bold on Liberation), second line 20 px faint.
- Night words built in `NightCycle.NowLine` beside `CallItANightPrompt`, from NightZero.Step, Barks.ScenePlaying/Lodger.Returning, NightLedger, NightCarry, the notebook's names — no new state. The notebook nudge is a per-session flag (`NightNotebook.EverOpened`) rather than per-night `Opened`, so a player who opened it once isn't nagged every night; a new session nags again.
- Day fallback: the customer whose thing is in Ace's hands, else E's queue head, else the lowest patience among those waiting (with a 0.15 hysteresis so two similar customers don't swap the line and its tick), else whoever's walking in. Compact only, never the 6 s full card.
- "Take the job" is the conversation's own (the card hides in conversations), so TalkWords is just "{E} Talk to Grace." / "Look at Grace. {E} Talk."
- The deal's ask had to stay ≤ 10 words (scene-line budget) and the deal ≤ 8 lines, so the spec's 13-word ask became "Grace's gnome. It watches my alley. Bring it here." (9 words: Grace, the reason, the ask; the note 4 s later and the Now line carry the step and street). The "Why?" answer is the spec's text exactly.
- "You're going the wrong way." was reworded to "Tick tock." (a Retext keeps the id and the pool at three lines) rather than deleted.
- Note budgets: the hint is checked with "the reunion cups" substituted, so the template keeps 9 words; "Yellow house, porch light." is the telegraphic cost of that. The notebook address line uses `looks = "the yellow house"` (porch light would push it over 12).
- `WordBudget.Words` skips tags because an icon tag counts as 2 words otherwise, which would break every bench/note budget the moment T's icons land (e.g. the bench's "[E] Take it to the Officer" row).
- Reveal camera numbers (NightZeroSteps): 2 m east, 2.2 m up, 3 m in front, 40° lens, looking at his half 1.6 m up. At his ~4.4 m distance the frame spans about 0–3.2 m of height; his head ≈ 2.0 m and his line ≈ 2.35 m sit in the upper third, Ace at the bin spot is ~6° off the camera's heading on its left. In frame in both views; no runtime re-aim needed. If it ever clips, raise the look point with `set.revealCamera.transform.rotation = Quaternion.LookRotation((Barks.HeadPointOf(man.Speaker) + Vector3.up * .2f) - set.revealCamera.transform.position)` just before `Barks.Play` in NightZero.TheBins.
- See-through: per-piece fading reverses the 30 Sept "whole building, never pieces" choice because the reviewers' photos showed whole blocks dithered for one wall; the field `fadePieces` turns it back. Ace "never dithered" is only partly contained: fewer dots over his body (1 in 10). Fully fixing it needs the shader (an x-ray pass for Ace, or a round cut-out in "Fixit Fidget/Night see-through"), which isn't in the snapshot. Grace's breakfast bar dither is GraceHouse/GraceCover's (not mine).
- Porch light: intensity only (+ a glowing fixture), because `GraceAtHome.LitOutside` reads the light's range and cone for her sight; widening them would change the stealth balance her labs measure.

## 3. Decisions his to overrule
- Cross/E picks the night's highlighted reply (reverses Barks 6 Oct "E never picks one"; the 0.2 s guard keeps the press that raised the chips from answering).
- The full card is dark too (not paper); the title amber, not green.
- The change tick is the "bark.choose" cue (a soft tick); highlight moves use "phone.tap".
- "Tick tock." in place of "You're going the wrong way."
- The ask's 9-word wording (above) instead of the spec's 13 words.
- The notebook nudge per session, not per night.
- The fallback line on unguided days (Day 3) and its green pin following the most urgent customer.
- `readingMin` 2.5 s for every bark, not only Grace's.
- `WordBudget.Words` ignoring tags.
- See-through per piece by default; 1-in-10 dots over Ace.
- House #6 repainted Dusty rose; the lamp sphere's size/glow (0.28 m, ×2.4); porch intensity 6.
- The Caught card's "{Cross} Go on" line at 80 %.
- `HomeDoor.looks` = "the yellow house" (porch light only in the night's lines).

## 4. Outside my files (exact edits)
- `CafeViewMode.cs` ~407: `if (Mathf.Abs(scroll) > .01f && !Barks.Choosing) Zoom(-Mathf.Clamp(scroll / 120f, -3, 3) * 1.6f);` (the wheel moves the highlight while choosing, as by day).
- `Assets/Editor/BenchWordsChecks.cs` 211–213 (B's): replace `line.Contains("[" + ControlHints.Cancel + "]") && line.Contains("[" + ControlHints.Interact + "]") && !line.Contains("press E")` with `line.Contains(ControlHints.Cancel) && line.Contains(ControlHints.Interact) && !line.Contains("[") && !line.Contains("press E")` (BenchReassureWords has no brackets now).
- `ShopUI.cs` (T's) night strip order: Notes (D-pad up / N) first, then Torch (Square / F), then Use (Cross / E); the rest dropped under T's 3-hint rule.
- Optional, lab data only: `Assets/Editor/RecapPhoneSteps.cs:151` and `Assets/Editor/NightWalkSteps.cs:374` pass the literal "the saffron house" into `NotebookEntries.HomeSeen`; use `HomeDoor.GraceLooks`. `NightOneSteps.cs:369`, `NightTwoSteps.cs:479` log text says "the saffron house on the corner".
- Shader "Fixit Fidget/Night see-through": an Ace x-ray pass or a radial cut-out around Ace's screen point would finish "Ace never dithered".
- GraceHouse's door prompt stays "Let yourself in" (GraceHouseWalkCheck asserts it); the Now line now says "Go in once she's asleep" beside it.

## 5. What proves it on his Mac
- Fixit Fidget › Checks › Day 1 onboarding (Edit mode): PASS with the new words.
- Fixit Fidget › Checks › Bark rules: PASS; "A very short line still stays up 2.5 s". Checks › Night rules: PASS (hints ≤ 12 with "the reunion cups", pages ≤ 12).
- Fixit Fidget › Night › Barks 1: report says 3 placeholder line(s) reworded (or already there).
- Fixit Fidget › Playtest 4 › UI › 1 - Day 1 guide (lab): ~81 checks; photo 01 shows "DAY 1 · GRACE / Five stars and the café can franchise. Grace is on her way." on a dark plate with an amber bar; 03 the one line at 26 px, ≤ 10 words; 05 "{E} Pick up Grace's reunion film camera from the intake tray."
- Fixit Fidget › Night › Night 0 - Play check: ~110 checks; the Now line at the door ("Take the rubbish out the back door · {D-pad up} notes"), outside, hidden through the deal, "Barnaby: Grace's step, 12 West Street", "Bring Barnaby to the man at the bins", "Call it a night at a café door"; the reveal camera on at 06 and off after; replies "down moved the highlight… said reply 2 with E"; the note "…12 West Street. Yellow house, porch light."
- Night 2 - Play check (cups): the Now line after his ask and in hand; his counter note shows after the mess note's time.
- Grace at home checks: "the card holds, waiting for a press" then the morning.
- Night guidance 1: DONE: N changes; house #6 wears no saffron; porch intensity 6; the lamp hangs; photos in Logs/Night/night-guidance-1-*/ (3-west-street-after.png shows one yellow house).
- NightTour's report: "See-through (dotted; piece by piece; one dot in 4 in the way, one in 10 over Ace)".

## 6. Risks and APIs I'm unsure of
- Compiles only with T's ControlHints/PadInput in place for icons; with the old ones it compiles too (words). I did not call `.Word` anywhere.
- TMP: `<size=20>` absolute inside a 26 px text; `<space=…em>`; a `<sprite>` tag inside `<b>` — all standard TMP, untested here.
- `Undo.SetTransformParent(Transform, Transform, string)`, `Undo.DestroyObjectImmediate` on a collider, `MaterialGlobalIlluminationFlags.RealtimeEmissive`, the Light's serialized name "m_Intensity": believed right, unverified.
- NightSeeThrough: assumes `_SeeThrough` = 1 draws every dot (SeeThroughMaterials says so), so a partly-faded building's solid pieces look as they did.
- The lab timings around queued notes (Night 0's hint, Night 2's counter note) were changed to waits; untested at 15 fps.
- The porch lamp's emissive sphere glows by day too (as the street lamps' glass does); if that reads wrong, put it in NightWalk.nightOnly.
- Grace's spoken mention writes "12 West Street" as a literal (CustomerIdentity.Format doesn't resolve street tokens): renaming the street means rewriting that line.