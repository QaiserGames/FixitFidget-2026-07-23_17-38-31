# H3: the HUD built in the engine (26 Oct 2026)

Branch `work-H`, commits `5f431bd` + `2da5f77`; csparse clean on 16 files; nothing compiled here. The window, jar and café front were previewed in a Python mirror of the painters over his photo: the six hours read as San Francisco.

## What I built

**1. Tickets.** One row, always. Up to three are wide dockets (236 × 144: the face in its **patience ring**, name 24 px, fault 22 px); from four every ticket is a narrow docket (96 × 144: ring over name over icons), all open, 8 px apart (seven end 20 px short of the window); from eight they overlap, newest on top. Every form shows **big bold icons** (36 px): the device, each drink, and **a clock** once patience is under half (red, pulsing under a quarter); without the sheet, a chip with an initial. Bench chips keep the ring and icons.

**2. Money: Ace's jar, not the till.** A glass jar of his bills and coins on a notched green board under the sign; the figure 40 px cream with a gilt "$"; the jar **fills a step per $24**. From the pay pop (ms): 180–660 the bill flies (Bézier lifted 95 px, u = 0.2·OutQuad + 0.8·InQuad, half to full size by 25 % OutBack, −24° to −6°, two ghosts); 660 lands: jar squash x1.06/y0.92 (OutBack 220), board jolts 3 px, glow 500, 4 sparkles, `hud.money.land`; 700–1320 count-up OutCubic with Kick 0.28 (1 + 0.28·e^(−8τ)·sin 22τ), a tick a step; 740/860/980 the receipt prints "+$12"; 500–1020 the tip's coin flies (lift 150, 3.2 turns/s), lands (`money.tip`), counted by 1320 (Kick 0.12); 1100 "tip +$3"; tear 1950–2350 (−6°, 26 px InQuad 250, fade 400). Spending: board dips (Squish 320), the bill lifts 0–100 and glides to the piece 100–580 (OutQuad, +30°, −30 %), digits plum-pink counting down 60–660, "−$80" at 120/220/320, tear 1200–1600, `hud.money.spend`. Guests' $5 count quietly. One object: the column, the bench strip (jar at ½, figure 30 px), the Arrange till.

**3. Window.** 360 × 170 top centre: painted frame, gilt line, keystone, the glass set back, M2's sky by the minute (the sun rises right, sets behind the Gate; moon, stars, clouds); the time 32 px gilt, the day 22 px mint. **San Francisco** as 25 tinted layers: the Golden Gate in International Orange, fog (opening; back from 5:36 PM), the Presidio, the Pyramid, the Salesforce crown, the Painted Ladies, a cable car, beacons, lit windows. **Seattle** is the placeholder (`HudCity.CityName`). By night only the window.

**4. Sign.** Kept whole (sway, flip, bounce), on a brass bracket with chains; "Come in" 22 px.

**5. Stars.** Five pips, a dotted road, a 26 px café front. A landing: drop from 46 px (245 ms), overshoot, a glint down the road 450–850, the front kicks 0.55 and lights; toast "New star! / 4 more stars to franchise" 4 s; Day 1 hint "Five stars to franchise" 6 s.

## Files
New: `HudArt.cs`, `HudSky.cs`, `HudCity.cs`, `HudMoney.cs`, `Diagnostics/HudLabCheck.cs`, `Editor/HudLabSteps.cs`. Rewritten: `HudCorners.cs`, `JobTicket.cs`, `TicketRailUI.cs`, `Editor/TicketLayoutChecks.cs`. Marked edits: `Juice.cs` (`Paid` event; pop chip on BrandDeep), `ShopUI.cs` (night clock yields), `DayOneGuideUI.cs` (CompactWidth 740), `SoundBank.cs` (7 cues), `ArrangeUI.cs` (till = jar), `PauseCheck.cs` (new layout, never weakened).

## On his Mac, in order
1. Checks › Pick up scripts changed from outside; UI › Button icons - Make.
2. Checks › Compact portrait tickets: "[Ticket layout] PASS".
3. Playtest › The phone by day - pause and HUD check, then Juice: "All N checks passed."
4. **HUD › Lab - The HUD at every hour (drives itself)**: 18 photos in `Logs/HUD/hud-<time>/`; "[HUD] All N checks passed." Then Night 1, Grace-at-home, furniture, Sound bank 1.

## Decisions his to overrule
The jar instead of the till (his bill still flies in). Narrow dockets drop the fault's words (hover details keep them). The clock icon means "under half patience". Dockets 144 tall (Now line 20 px lower). The toasts on the HUD.

## Risks
Uncompiled: `Image.Type.Tiled` on code sprites, `VertexHelper.AddVert`'s Vector2 overload, `Mask` over a TMP initial, `GetRawConstantValue` in the lab. Painting the frame and city costs ~100–200 ms at scene start. Nine tickets overlap. The night window needs `RecapUI.Showing` false during the walk (else the old clock returns, still passing). The lab's stand-ins use reflection into CustomerBrain, like the ticket check.
