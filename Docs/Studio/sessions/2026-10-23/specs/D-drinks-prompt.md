# Builder D — the drinks station's prompt, in the style he loves (23 Oct 2026)

Copy: `/root/wt23/D`. Rules: `/root/review23/builders-common.md` (read it first).

## His notes (verbatim)
- "I do like the way that we're adding how prepared drinks and that the logo looks, because that does look so clean. I love that look."
- "The way that we've done the prepared drinks and that, I do kind of like. I just don't like it when we're working on the drink station, because that just looks really clustered as well. Looks really blocky and big."

"Prepare drinks" is the café's prompt line: big words with the button's icon and the skin's shadow, no plate. It's the `promptText` that `ShopUI` dresses (`UiSkin.UseFont`, `UiSkin.Shadowed`, `BandText`), at the bottom centre of the overhead view. That is the look to match.

## What's there
`Assets/Scripts/BeverageStation.cs` (255 lines). In the drinks close-up it builds a dark rounded card, 600 × 176 reference px (about 720 × 210 at 1080p), at the bottom centre, with:
- a title (the thing under the crosshair: "Cup stack");
- an action line ("Take empty cup"; "Pouring latte" with a small gold ring that fills with the pour; "Take the coffee to Beatriz");
- two control lines ("[LB] left hand [RB] right hand", "[A] use").

The 19 Oct header explains each choice (no percentages; never cut off; who the cup is for; the cup stack glows; ≥ 20 px). Keep all of those reasons.

The photo he means: `Logs/Stations/stations-first-person-2026-10-20_014046/drink-2-at-the-dispenser.jpg` on his Mac. You can't see it, so here is the description: the card covers the bottom centre of the machine's drip tray, four lines stacked, the hands line with two pill icons, "A use" alone on the last line.

## What to build
1. **One clean prompt line** in the café prompt's style: the action with its button's icon first ("(A) Take a cup", "(A) Pour espresso", "(A) Take the coffee to Beatriz"). Same font, weight, colour and shadow as `ShopUI`'s prompt line, bottom centre. No dark card.
   - Find how `ShopUI` builds that line and reuse the same `UiSkin` calls. Don't copy magic numbers that differ.
   - If the café prompt line itself is visible in the close-up, don't show two lines: decide which owns the moment and say why.
2. **The hand choice only when it matters.** When either hand could take or put something, show the two hands as a small secondary line or icons beside the action (LB / RB as icons, "left" / "right" words at 20–24 px). When it doesn't matter, show nothing.
3. **The pour ring stays,** beside the action line, the same gold ring. During a pour the line reads "Pouring latte".
4. **The thing's name** ("Cup stack", "Espresso", "Latte cup") moves off the card. Either drop it (the action already names it) or show it as a small label at the target like the bench's hover label ("New screen"). Choose and say why; don't add a second big text.
5. **Leaving:** "Step back" with its icon (`ControlHints`) as the last, smallest hint, or in the controls strip if the close-up shows one. Say which.
6. **Sizes:**
   - the action line 30–34 px at 1080p, bold or semibold;
   - the hands 22–24 px;
   - nothing under 20 px;
   - contrast ≥ 4.5:1 against a bright machine (use the shadow).
7. **Keep the lab-facing members:** `ActionText`, `ControlsText`, `RingShowing`, `RingFill`, `StackGlowing`. If their content changes (e.g. `ControlsText` now holds the hands line only), update every lab that reads them. Grep: `Day1GuideCheck.cs`, `StationsCheck.cs`, `BenchWordsCheck.cs`, `BenchLab.cs`, `BenchViewProbe.cs` and others. Keep every intended assertion, e.g. Day 1's "in its close-up the card says … cup stack".

## The proof
- The labs that photograph the drinks close-up:
  - "Stations as reach - play check, first person / overhead";
  - "Day 1 guide";
  - "Aim help";
  - "Juice".

  They must pass. Their photos at the drinks machine show the new prompt.
- The word check (`ReviewScreen`) must show no S1 or S2 for the prompt's words in those photos.
- List the photo names a reviewer should look at.

## Files you own
`Assets/Scripts/BeverageStation.cs`, plus the labs above where they read the drinks card's text (only those lines).

Don't edit `ShopUI.cs`, `AimAssist.cs`, `PlayerInteractor.cs` or `RecapPhone.cs` (other builders own them this session). If one needs a change, describe it exactly.
