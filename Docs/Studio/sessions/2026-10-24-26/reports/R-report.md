# Builder R: readability pass (26 Oct 2026)

Committed in `/root/wt24/R` (work-R) as `201c3be`. All 16 files pass `csparse`.

## 1. Files
- **RecapPhone**: one type table replaces 74 literal sizes (Body 24, Label 22, Tab/Badge 20, Button 26, Lead 28, Title 32). The phone is now 620×990 (was 470×960) and sits 28 px up.
- **JobTicket**: the initial sits on a solid disc. Initials 22, fault 24.
- **HudCorners**: the stock chip is on Ember #9A4C17 at 24/22. Sign line 24, toast 26/24.
- **HudSky**: the day line is 24, never under 22.
- **UiPins** 24; **FocusNameUI** semibold, shadowed; **HoverTooltipUI** 24, shadowed; **NoticeMark** later colour switch; **BenchCards** 24, chip row 74; **ConversationUI** 26/24; **ShopUI** strip 26; **StraightFaceUI** 28; **NightCycle** notes 28; **Barks** key line 24 and the ask plate.
- **PauseCheck, HudLabCheck**: the day-line floor is 21.9 (was 20).

## 2. S1s
- **Phone lines** ("earned today", quotes, "8 reviews today"): fixed, 16–18 → 24. Their 1.1–1.8:1 is DayShapeCheck shooting while the phone rises (about 9.5:1 settled).
- **Kickers, verdicts, "New star!", "Lv.1", tags**: fixed, 16 → 22.
- **"31 / 120 reputation"**: fixed, 15 → 24.
- **Tab names**: fixed, 16 → 20. The open tab was 3.6:1, now 6.5:1.
- **Badges "2", "6", "!"**: fixed, 14 → 20, contrast 3.6 → 4.6.
- **Phone hint**: fixed, 22 → 24 and shadowed.
- **Avatar initials**: fixed, 15 → 20.
- **Ticket initials**: fixed, 17 → 22, 3.4 → 15.5 (item 4).
- **Names over heads and devices**: fixed, 18–22 → 24.
- **"Drinks", "Dirty table", "Repair bench"**: fixed, 21 → 24, contrast 4.5 → 7.3.
- **"Under the back cover"**: fixed, 24 and shadowed.
- **"?"**: fixed. It was paper on white mid-fill.
- **"Refill from the back shelf"**: fixed, 20 → 22, contrast 3.3 → 5.6.
- **"Tonight"**: fixed. It is the phone's tab.
- **Stale** (old HUD): the mood words, "Come in" at 20 (now 24), the old names.
- **Transient**: "OPEN" 10/165; "$0" 6/104; "$15" 1/27; "Fixed! · Perfect" 2/29; the tooltip fade-in 1/17 and 1/1; the stock chip's spring-in.
- **Builder Y's**: "2 seats", "Day 2", "Day 3", "+2".

## 3. S2
**Fixed**: the ticket fault, the HUD lines, every phone line and button, the bench cards (21), the conversation, night notes, the straight face, the strip (118 photos), Barks, prices out of reach (3.9 → 5.5:1).

**Left as they are**:
- The narrow docket's name stays 22 (88 px wide; 24 would cut it). The receipt stays 24. "Step back" and "left right" are builder K's.

**S3**: only the bench's mouse controls card is over 25 words (28); it is a list, so kept.

## 4. Chip and "V"
- **Chip**: when the 560 px plate would cost the chip a line, a line with an icon may use an 820 px plate (`AskMaxWidth`). The check is unchanged.
- **"V"**: with no portrait, the face is a Paper disc with Ink (15.5:1). When urgent it is RedSoft with #8E2A20 (6.3:1; it was 3.9). The lab floor is unchanged.

## 5. Labs on his Mac
1. **HUD lab**: 0 words under, day line at 22 or more.
2. **Pause**: day line at 21.9 or more, and the phone fits.
3. **Recap phone lab**: Tonight shows "nothing to scroll" and "every line fits".
4. **Night 2**: "his ask wears the cups chip".
5. **Bench 6**: the cards read at 22 or more.
6. **Start screen**: Settings on the wider phone.
7. **Screens 1**, then **the gather**.

## 6. Decisions he may overrule
- The phone is wider rather than having rows cut.
- The pin words leave the bar's size family.
- The chip is Ember; the guide pin is deep green.
- Weekdays go short sooner ("Wed · Day 3").
- Asks may take an 820 px plate.

## 7. Risks
- **Tonight's height is estimated** (about 764 of 790 px); thrift lines plus low stock may scroll.
- Tight fits: "Franchise" in a 118 px tab; the 112 px sign; the bench controls card; the 66 px stock chips.
- **Edit for builder Y** in `DayShapeCheck.cs`, before the `-the-day-ends` photo:
  `var ph = FindAnyObjectByType<RecapPhone>(); float t0 = Time.realtimeSinceStartup; while (ph != null && ph.Root != null && ph.Root.activeInHierarchy && !ph.Settled && Time.realtimeSinceStartup - t0 < 2f) yield return null;`
- **World words**: signs and house numbers are set dressing (larger lettering is a scene step); the paddles are builder K's.
