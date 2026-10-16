# Brief — UI/UX (Opus): checklist v2, show-dirt, and the bench's word budget

## Read first
1. `~/cafe/brief/common.md` (all; §6 decision 7 is your contract).
2. Your role file `~/cafe/repo/Docs/Studio/agents/ui-ux.md`; the Bible §2 (objects are objects, information is projected) and §4.
3. Project docs: `claude/studio/playtest-first-hour-2026-10-13.md` (#3: about 120 words on the first repair's screen; #4: "[E] Put it on the intake tray" after a pickup), `claude/hud-spec.md`, `claude/playtest-3-notes-and-plan.md` (his "WAYYY too many words" — a cut line must not return), `claude/playtest-4-findings-and-proposals.md` §1.2 and §5 (how the checklist and controls card came to be).
4. The code: `Assets/Scripts/BenchCards.cs`, `RepairChecklist.cs`, `DayOneGuideUI.cs`, `HoverTooltipUI.cs`, `FocusNameUI.cs`, `ControlHints.cs`, `PadInput.cs`, `UiSkin.cs`, `UiPins.cs`, `JobTicket.cs`/`TicketRailUI.cs` (the ticket's words at the bench), `GameSettings.cs`, `PlayerInteractor.cs` (the prompts after a pickup), and the checks that read these words (`Assets/Editor/DayOneOnboardingChecks.cs`, the Day 1 guide lab, Bench 3, Controller).

## The job
1. **The paper card.** Exact layout at 1080p and 1440p (size, position, safe margins; it must never cover the device in any of the three devices' framings): the customer's name and what's wrong (one line, the customer's words), then the job as three to six **tool icons** that tick off, the current one larger with its verb in two or three words ("Unscrew", "Pry off", "Lift out", "Brush", "Wipe", "Press in", "Screws back"). Icon art: propose rendering the bench's own five tool models (and a screw, a "new part") as small icons — the Blender lead can render them; say sizes and style.
2. **The glow on the next part.** With Systems: what glows (the part the current step needs), how (a soft rim/pulse in the brand's light green? check `UiSkin`), when it stops; colour-blind safe (shape/motion as well as colour).
3. **The controls card**: full on the very first repair only, then hidden behind one button (which, on keyboard and pad — check what's free; Tab/View is "hold for controls" today); one line hint when hidden or none?
4. **Show dirt** (Systems specifies the mechanism): the binding on keyboard and pad (check `ControlHints`/`PadInput` for free buttons: D-pad ↔ is "next part", D-pad ↕ zoom, Y flip, X take away, B back, LB/RB tools), the look of the flash.
5. **One place for the job's words.** List every text element on the bench screen today (the checklist strip and its header, the ticket's fault line, "Clean: lens, film path, film guide · replace shutter", the Day 1 card, the prompt line "[E] Pick up item and step back" in the largest type, the controls card, hover cards) with its word count, and what each becomes. Target: **under 25 words** on the bench screen in steady play; under 40 on the very first repair with the controls card.
6. **The Day 1 guide at the bench**: the guide's card and the checklist say the same step twice today; make the highlighted step the guide.
7. **The pickup prompt** (First Hour #4): after picking a device up on Day 1's carry step, E offers "Put it on the intake tray" under a card that says take it to the bench. Fix the wording/offer.
8. **A word-budget check**: a lab/editor check that counts visible words on the bench screen per state and fails over budget; nothing overlapping the device's screen rect.

Deliverables: a plain-text wireframe per state (first repair; steady play; a hidden part; finished: "Fixed!"), the before/after word table, every file to change and what changes, the checks, what to watch in playtest 6. No code this wave beyond tiny fragments.
Output: `~/cafe/specs/uiux-checklist-v2.md`. Your final message is the spec.
