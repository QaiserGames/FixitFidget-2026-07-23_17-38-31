# Builder R: the readability pass from today's word gather (26 Oct 2026)

You are the UI lead for Fix It Fiasco. His girlfriend is dyslexic and plays on a PlayStation pad; her one ask was "your text needs to be bolder". Every lab photo measures its own words, and today's gather of all of them found 454 texts under the house's floor. Your job: make every word a player must read meet the floor in its steady state, without breaking a layout or a lab.

## Read first
- `/root/review24/builders-common.md` (house rules; "Readable words" is your contract). Two changes since it was written:
  - his Mac's current scene and prefabs are in `/root/ref26` (read-only), not ref24;
  - you commit in your copy (branch `work-R`); that wins over "commit nothing".
- The gather:
  - `/mnt/user-data/uploads/FixitFidget/Logs/Review/text-2026-10-26_112735/report.txt`: the header explains S1/S2/S3, then each finding with its photos;
  - `findings.csv` in the same folder: one row per text, with smallest/typical px at 1080p, lowest/typical contrast, thin, outlined and owners.
  - **S1:** under 18 px, or contrast under 3 with no outline.
  - **S2:** under 26 px (Xbox Accessibility Guideline 101), contrast under 4.5 with no outline, or a pad button written as a word.
  - **S3:** thin and under 32 px, or over 25 words.
- The house's own floor (builders-common): at least 24 px at 1080p (20 px only for a secondary label), the most important line 28–34 px, bold or semibold for anything that instructs, words over the world on a plate (`UiSkin.Band`) or shadowed (`UiSkin.Shadowed`), and contrast at least 4.5:1 under 26 px.

## Careful: some photos show code that no longer exists
- The gather took the newest run of each lab, from 06:58 to 11:27 Chicago.
- The HUD (`HudCorners`, `JobTicket`, `TicketRailUI`, the new `HudMoney`, `HudSky`, `HudCity`, `HudArt`) was rewritten by builder H3 and only drew from about 10:50. A photo stamped before that shows the old HUD: the old ticket with "Pleased"/"Waiting" mood words, the old "OPEN / Come in" card. The icons (NoteIcons; builders I and I2) and the start screen also went in mid-morning.
- **Work from the current code in your copy.** For each finding, grep the words, or the code that builds them, and the owner script.
  - Still drawn under the floor: fix it.
  - Gone or rewritten: check the new code's size and contrast instead, and list the finding as stale.

## Steady state, not a mid-tween frame
A finding whose typical value is fine but whose worst value comes from a few photos is usually a frame caught mid-flip, mid-fade or mid-slide: "OPEN" at 1.1:1 in 10 of 165 photos, "$0" at 30 px in 6 of 104, "Fixed! · Perfect" at 5 px in 2 of 29.
- Don't redesign an animation for that. List these as "transient" in your report, one line each, with the photo count.
- **The exception:** a fade or slide that makes words unreadable for a long, still moment, or that every photo catches. Fix those.

## Scope

### 1. Every S1 on the UI that is still live: fixed
The list, by owner, from the gather. The two biggest are both the phone, `RecapPhone.cs`:
- `WheelGlide` is its apps' window (the Recap, the Shop, Reviews, the day's lines: "Loved it", "earned today", "MONDAY · DAY 1", "New star!", "8 reviews today · +16 reputation", the review quotes, "Sold your thrift watch: +$55", "Lv.1", "31 / 120 reputation").
- `RootWatch` is its home screen (the app labels "Notes", "Shop", "Reviews", "Franchise", "Today", "Settings" at 16 px, the badges "2"/"6"/"!" at 14 px, and the hint "/ Apps - Jump / Scroll Press").

The rest:
- the tickets' initials and names (`JobTicket`, plus `FocusNameUI` for the name over a customer);
- the pins (`UiPins`: "Drinks", "Dirty table", "Repair bench", "Grace's reunion film camera");
- the hover card (`HoverTooltipUI`: "Under the back cover", "Jammed shutter mechanism");
- the notebook's "Tonight" (`NightNotebook`);
- the "?" (`NoticeMark`);
- "Refill from the back shelf" (the HUD's stock chip).

### 2. S2 on anything a player must read to play: fixed
The tickets, the HUD's lines, the phone's day lines and buttons, the bench's cards (`BenchCards`: 21 S2), the conversation lines (`ConversationUI`), the notebook and Night lines (`NightCycle`, `NightNotebook`, `NightWalk`), `StraightFaceUI`, `ShopUI`, `Nudge`, `DayIntro`, `TitleMenuUI` and the toasts.
- Raise to at least 24 px at 1080p (26 where the layout has the room) and bold or semibold.
- Make contrast at least 4.5:1, or put the words on a plate or under the skin's shadow or outline.
- S3 "over 25 words" (the phone's Shop texts, `Barks`): shorten only where it is ours to write and the meaning survives. Otherwise list it.

### 3. The Night 2 chip
Night 2's lab failed one check: "his ask wears the cups chip … ('Grace has a box of cups. Get me a sleeve.': '')".
- The chip is left off by `NoteIcons.Chip()` when the words would wrap once more beside it (the rule "a chip never costs a line"). His line on his 30 px speech plate is just too long for the chip.
- **Fix it so the ask keeps its chip on one line.** In order of preference:
  - a slightly wider plate for his asks;
  - a tighter chip;
  - a shorter line that keeps his voice.

  Grep the lines in `NightCycle`, `LodgerDay` and `Barks`. The gossip line itself is a placeholder for his sister's writing.
- Don't weaken the lab (`NightTwoCheck` or its equivalent: grep "wears the cups chip").

### 4. The HUD's "V"
The HUD lab: "'V' 24.3 px, 3.4:1" on Valerie's ticket. It is the face fallback, `JobTicket`'s `portraitInitial`, Ink on the patience ring's face.
- Make it at least 4.5:1 at its steady state (e.g. a solid backing disc in Paper behind the initial, or a darker ink).
- Make the "all HUD words" check pass without lowering its floor.

## Files

**Yours:**
- `RecapPhone.cs`: sizes, contrast and layout only. Don't change what it shows or does. The YOUR CAFÉ card's logic, the thrift card and the stock card are other builders' work; keep their behaviour.
- `HudCorners`, `HudMoney`, `HudSky`, `HudCity`, `JobTicket`, `TicketRailUI`.
- `UiPins`, `FocusNameUI`, `HoverTooltipUI`, `NoticeMark`.
- `NightNotebook`, `NightWalk`, `NightCycle`, `LodgerDay`, `Barks`, `NoteIcons` (only for item 3).
- `BenchCards`, `ConversationUI`, `StraightFaceUI`, `ShopUI`, `DayIntro`, `TitleMenuUI`, `Juice` (only if a steady-state word is under).
- The labs that assert those sizes, colours or strings (`HudLabCheck`, `PauseCheck`, the recap phone's lab, `BenchWordsCheck`, the Night labs, `TicketLayoutChecks`, `NoteIconChecks`).

**Not yours (others are building in them right now):**
- `ArrangeUI`, `ArrangeMode`, `FurnitureRules`, `CafeLayout`: builder Y, who also takes ArrangeUI's findings ("2 seats", "Day 3", "+2").
- `BeverageStation*`, `BeveragePaddleMotion`, `StationInteractable`: builder K, who is rebuilding the drink machine's buttons. The world words on the paddles are K's.
- The bench's logic (`GrimeSpot`, `RepairChecklist`, `ToolPickup`, `PadInput` and the like): builder N.
- `CafeLab.cs`, `DayShapeCheck.cs`: builder Y.

If one of theirs must change, write the exact edit in your report.

## Out of scope
- The words drawn in the world: house numbers, street signs, CUSTOMER PARKING, the machine's paddles. List them in one paragraph with your view.
- The start screen's design.

## How
- Before raising a size, look at how the element is laid out: card heights, fixed widths, `enableAutoSizing`, wrapping, `UiSkin.Text`'s defaults. A bigger word must not overflow, clip, or push a card off-screen at 1920×1080, and must still fit at 1280×720 (the phone at 16:10 too, if it has a case for it).
  - The phone is the biggest job. If its 16 px is a design-wide size, prefer one coherent change (a named size table, or the phone drawn larger) over a hundred edits. Say which you chose and why.
- Keep each file's header comment true, and mark edits `// 26 Oct 2026 (Readability)`, quoting her ask where it helps.
- Run `python3 /root/tools/csparse.py` on every C# file you change.
- Check every project member you call by grepping its definition. Nothing compiles here.
- **Be economical:** he asked us to preserve usage. Grep before you read, read only the ranges you need, and don't re-read. Keep a short progress log at `/root/review24/reports/R-progress.md`.
- Don't use any remote-device or Mac tools, and don't spawn agents.

## Report: `/root/review24/reports/R-report.md`, under 700 words
1. The files.
2. Each S1, one line each: fixed (old → new px and contrast), stale, or transient.
3. The S2 you fixed, grouped, and the ones you left, with why.
4. The chip and the "V".
5. The labs to rerun on his Mac, and what a pass looks like. At least: the HUD lab, Pause, the recap phone lab, Night 2, Bench 6, the start screen, then Screens 1, the gather.
6. Decisions he may overrule.
7. Risks: layouts you couldn't check without Unity.
