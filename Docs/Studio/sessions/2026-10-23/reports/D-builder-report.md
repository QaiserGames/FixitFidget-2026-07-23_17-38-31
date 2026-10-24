# Builder D: the drinks prompt, report (23 Oct 2026)

(As handed back by builder D. Copy: /root/wt23/D, uncommitted.)

## 1. What I did
- `/root/wt23/D/Assets/Scripts/BeverageStation.cs` is rewritten and uncommitted (+509 −123). The dark card is gone. The machine now speaks in the café prompt's look:
  - one action line, with its button's icon first;
  - the two hands in a small line under it, only when the choice matters;
  - the pour ring in the icon's place;
  - "Step back" as the last, smallest hint, in the close-up only.
- No other file is changed. No lab needed an edit: no code anywhere reads the drinks card (details in §4).
- Checks I ran:
  - `python3 /root/tools/csparse.py` on the file: **clean**.
  - Type-check with Mono `mcs` against stubs that mirror every real signature the file calls (Unity, TMP, project): 0 errors, 0 warnings.
  - A simulation of 28 machine states, from the empty pad to the drink in hand, the linger and the close-up: 28/28 as designed. Harness: `/tmp/claude-0/-home-claude/34d36940-48ac-5627-8ac2-a986b7b0a6aa/scratchpad/sim/run.cs` (with `unity.cs`, `project.cs`).

## 2. The decisions and their reasoning
- **The look he loves, measured from his 20 Oct photos.** The café prompt is the scene's 36 px Baloo 2. Its stems are 0.139 em, the same as the card's SemiBold words and thinner than its Bold title, so its weight is `UiSkin.SemiBold`. Its words' middle sits about 200 px up at 1080p.
- **The drinks line reuses ShopUI's own calls:** `UiSkin.Text` (the skin's font), `UiSkin.BandText`, `UiSkin.SemiBold`, `UiSkin.Shadowed`. No plate. Bottom middle, same height, 34 px (the spec's 30–34).
- **The canvas** is now `UiSkin.ScreenCanvas` (1920 × 1080), so the sizes hold on any screen. The old card's scaler capped at 1.4, so it shrank on screens taller than 1260 px.
- **An icon only when that button does it here.**
  - "(A) Take a cup", "(A) Pour Latte".
  - Next-step lines carry no icon, e.g. "Aim at the Latte paddle to pour".
  - The drink in hand carries the drink's picture (the ticket's and the called order's): "(coffee) Take the Coffee to Beatriz". At the machine A doesn't take the drink to her. It takes a cup, or pours onto the tray when no cup is there. That is the misread the 19 Oct reviewers flagged. At Beatriz the café line already says "(A) Serve the Coffee".
- **The hands only when they matter:**
  - both hands free to take (the stack, a ready drink);
  - or both holding cups at the cup return, where which cup goes matters.
  - Never while pouring, never at a paddle, never when placing a cup: two empty cups are the same cup.
  - When only E's other hand can do it, that hand's button is the line's icon: "(RB) Put the cup back".
- **The thing's name is dropped.** The line already names its thing ("Take a cup", "Pour Latte", "under Latte"). The paddles print their own names, and the target is outlined or glows. A label at the target would be a third naming.
- **Step back** is 22 px on the café controls strip's own plate, in the strip's place, close-up only. The café strip itself steps aside in every close-up, so this is the close-up's strip. In first person there is nothing to leave.
- **One line at a time.**
  - In the close-up and in first person, ShopUI already clears its prompt (`UsingDrinks`).
  - The half-second linger now holds the last words, ends at once on another target, and gives way if the café line has words ("In hand …"). No overlap is possible.
- **Two flow fixes:**
  - After the first cup the crosshair is still on the stack, so the line says "Take another cup", not "Take a cup" again.
  - After a pour the paddle says "Coffee is ready: take the cup".
- **Short lines:** Ace's held cups are drawn at 28 % and 72 % of the width at the line's height. Every line is measured to fit between them; the longest is 704 px with a ~714 px gap. That's why "Both hands are full" is short.
- **Word check.** The spec asks the hands to be 22–24 px and "no S1 or S2 for the prompt's words". ReviewScreen marks anything under 26 px S2. I read "the prompt's words" as the action line.

| Text | Size at 1080p | Weight | Background | Word check |
|---|---|---|---|---|
| Action line | 34 px | semibold | skin's shadow | clean |
| Hands | 24 px | semibold | skin's shadow | S2 (<26) |
| Step back | 22 px | semibold | dark plate, ≈9.8:1 | S2 (<26) |
| Pour ring | 36 px tall | — | — | — |

  Nothing is under 20 px.
- **Smaller:** "who it's for" now requires the customer to want this cup's drink. It is asked at most twice a second, where it used to search every frame while nobody wanted the drink. Words are built only when what the line says changes, with no per-frame garbage in the common path.

## 3. Decisions his to overrule (each is one number or one string)
1. Action line 34 px (the café's own is 36).
2. Its place is the café line's height (`ActionMiddle` 200). From above that's over the white counter, so the shadow carries it, as "Talk to Valerie" does.
3. The thing's name dropped rather than labelled at the target.
4. The hands' rule (§2), at 24 px, with the words "left" / "right".
5. One hand's own button as the icon when only that hand can do it.
6. Step back 22 px on the strip's plate; B / Esc only (walking out is not named).
7. The drink's picture, not (A), on "Take the Coffee to Beatriz", against the spec's example.
8. Drink names as the data writes them ("Pouring Latte"), not lowercased like the spec's examples. They match the paddle and the Day 1 card.
9. The ring is left of the words (the icon's place), 36 px, same sprite and colours.
10. The wording of every line:
    - "Take a cup" / "Take another cup"
    - "Place the cup under Latte"
    - "Aim at" (pad) / "Look at" (mouse) "the Latte paddle to pour"
    - "Pour Latte" / "Pouring Latte"
    - "Latte is ready: take the cup" / "This Latte is cold: take the cup"
    - "Take the Latte" / "Take the cold Latte"
    - "No cup under Latte"
    - "Take a cup from the stack first"
    - "Look at the cup stack to take a cup"
    - "Place the cup under a nozzle"
    - "Put the cup back" / "Pour away the Latte" / "Spare cups go back here"
    - "Nobody ordered this one: pour it away" / "This Latte is cold: pour it away"
    - "Both hands are full"
    - "Out of cups: restock at closing" / "Out of beans: restock at closing"
11. No "pop" when the words change (the café line pops 0.9→1). A mid-pop photo would read 31 px and be marked "thin".

## 4. Outside my files
- **Required: none.** I grepped `ActionText`, `ControlsText`, `RingShowing`, `RingFill`, `StackGlowing` (plus `CupStack.Glow.IsOn` and the card's object names) across Assets, Tools and Docs. The only hits are other classes: `BenchCards.ControlsText` and `RecapPhone.FocusRingShowing`.
  - No lab reads the drinks card. That includes Stations, Day 1 guide, Aim help, Juice, BenchLab, BenchViewProbe and BenchWordsCheck.
  - Day 1's "in its close-up the card says … cup stack" reads `DayOneGuideUI.CardText` and is untouched.
  - All five members are kept. `ControlsText` now holds only what is shown: the hands line and Step back.
  - I added `HandsShowing`, `StepBackShowing` and a static `LineUp`.
- **Recommended, ShopUI.cs (not mine).** Without this edit, in first person with something in hand, the café's "In hand …" line and the controls strip flash up while the crosshair crosses the gaps between controls. The old card used to cover that area. No overlap is possible either way.
  - line 193: `bool closeUp = interactor != null && (interactor.InCloseUp || interactor.UsingDrinks) || BeverageStation.LineUp;`
  - line 294: `if (interactor.UsingDrinks || BeverageStation.LineUp)`, with a comment line: "23 Oct 2026: and while the drinks line stays its half second after the crosshair slips off the machine (BeverageStation.LineUp, as of the last frame)".
- **Optional, StationsCheck.cs** (it reads nothing of the card today, so I didn't edit it):
  - after `yield return Photo($"{customer}-2-at-the-dispenser");`:
    `BeverageStation drinksLine = drinks.GetComponent<BeverageStation>(); if (drinksLine != null) Check(drinksLine.ActionText == ControlHints.Interact + "  Take a cup" && drinksLine.HandsShowing && drinksLine.StepBackShowing != firstPerson, $"the drinks line: \"{RecapPhone.Plain(drinksLine.ActionText)}\", the hands under it, Step back {(firstPerson ? "not shown" : "shown")}");`
  - after `yield return Until(() => slot.IsPouring, 2f, "A on the paddle starts the pour");`:
    `if (drinksLine != null) { yield return Frames(2); Check(drinksLine.RingShowing && drinksLine.ActionText == "Pouring " + drink.drinkName && !drinksLine.HandsShowing, $"pouring: \"{drinksLine.ActionText}\", the ring {drinksLine.RingFill:P0}"); }`
  - after `yield return Photo($"{customer}-3-the-drink-made");`:
    `if (drinksLine != null) Check(RecapPhone.Plain(drinksLine.ActionText).Contains($"Take the {drink.drinkName} to ") && !drinksLine.HandsShowing, $"the drink in hand: \"{RecapPhone.Plain(drinksLine.ActionText)}\"");`
- **For A:** if your new drinks section in AimHelpCheck reads the line, compare against `ControlHints.Interact + "  Place the cup under Espresso"` and the other words in §3.10.

## 5. Proof on his Mac
First: Fixit Fidget › Checks › Pick up scripts changed from outside (reimport). Then run each lab and expect "All N checks passed":

| Lab (menu) | Pass on 20 Oct |
|---|---|
| Fixit Fidget › Playtest › Stations as reach - play check, first person (lab, drives itself) | 95/95 |
| Fixit Fidget › Playtest › Stations as reach - play check, overhead (lab, drives itself) | 96/96 |
| Fixit Fidget › Playtest 4 › UI › 1 - Day 1 guide - play check (lab, drives itself) | 81/81 |
| Fixit Fidget › Playtest › Aim help - play check (lab, drives itself) | 26/26, or A's new count |
| Fixit Fidget › Playtest › Juice - Photograph the feedback (lab, Play Mode) | 50/50 |
| Optional: Fixit Fidget › Playtest 4 › Bench › 2 - The dispenser's frame - play check (lab, Play Mode) | 14/14 on 15 Oct |

Then Fixit Fidget › Review › Screens 1. A pass looks like this: in the drinks photos, "Next action" has no flag; "Hands" (24 px) and "Step back" (22 px) show S2 only.

Photos to look at (each with its `.text.jpg` and the folder's `text-check.txt`):
- `Logs/Stations/stations-first-person-<time>/`: `drink-2a-the-whole-dispenser.jpg` ("Take a cup from the stack first", no icon); `drink-2-at-the-dispenser.jpg` ("(A) Take a cup" with "(LB) left (RB) right", the moment he complained about); `drink-3-the-drink-made.jpg` (the coffee picture with "Take the Coffee to <name>"); `bar-drink-2a-…`, `bar-drink-2-…`, `bar-drink-3-…`.
- `Logs/Stations/stations-overhead-<time>/`: `drink-2-at-the-dispenser.jpg` (the above plus the "(B) Step back" plate); `drink-3-…`, `bar-drink-2-…`, `bar-drink-3-…`.
- `Logs/UI/day1-guide-<time>/`: `12-at-the-drinks-machine.jpg` (close-up, keyboard; "Look at the cup stack to take a cup" and "(Esc) Step back"); `11-his-coffee-the-drinks-machine.jpg` (from above; no drinks line).
- `Logs/AimHelp/aim-help-<time>/`: `01-settled-on-a-cup-pad.jpg`, `02-dpad-stepped.jpg`, and A's new drinks photos.
- `Logs/Bench/dispenser-frame-<time>/`: `3-overhead-at-the-stand-point-before-E.jpg` (the café's own "(E) Prepare drinks", for comparison); `1-close-up-as-it-opens.jpg`, `1b-close-up-on-the-cup-stack.jpg`, `5-first-person-at-the-stand-point.jpg`, `6-first-person-on-the-cup-stack.jpg`.
- Juice: no photo is at the machine (its autopilot makes no drinks). It only has to keep passing.

## 6. Risks and APIs
- **APIs:** I'm not unsure of any; every one is already used elsewhere in the project. `UiSkin.ScreenCanvas` under a world object is how the old card was parented too.
- **The flash in first person** without the recommended ShopUI edit (§4).
- **Contrast:** from above, the line sits over the white counter and relies on the skin's shadow, exactly like the café prompt.
- **Hand choice after a failed LB/RB press:** `PlayerCarry.Carried` honours a hand chosen by a failed LB/RB press until the next action. For that moment the line's "which button" can be off.
- **Day 1 repeats itself:** the Day 1 card still says the same step as the line, as it did before.

## Question for him
The hands and Step back are 24 / 22 px, as the spec asked and as the café strip is. The word check marks anything under 26 px as S2; her note was "your text needs to be bolder". Keep 24 / 22, or go to 26? **Recommendation: keep 24 / 22 until she plays.** The button icons carry the meaning, and his note was "blocky and big". If she squints, it's one number each (`HandsSize`, `StepBackSize`).
