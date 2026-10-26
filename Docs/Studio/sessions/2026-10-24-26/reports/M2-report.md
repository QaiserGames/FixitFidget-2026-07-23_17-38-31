# M2 report: the HUD, round 2 of design A (24 Oct 2026)

Everything is in `/root/review24/designs/M2/`. **Start with `README.md`**: it opens with the phone summary, then the research, the three money treatments with their timing tables, San Francisco and the franchise swap, the tickets, the "level above" changes, the build cost, the decisions his to overrule, and five questions. No game code was changed; `/root/wt24/M` was only read.

## Summary (as in the README)

He picked **A, the Shop window**. This round takes it one level above:

- **The money, after studying 14 games:** three treatments, each animated over the café (1 the bill clip, 2 the till, 3 the coin saucer). **My pick: 2, the till.** His bill stays and pops when he's paid; the figure rolls on number wheels behind brass and glass; a bell rings; a receipt prints the pay, then the tip, and tears away. Spending opens the drawer and rolls the wheels down in plum, no bell.
- **San Francisco at a glance:** the Golden Gate in its orange, the Transamerica Pyramid, fog, the Painted Ladies, a cable car. The sun sets behind the bridge. The city is one swappable layer (Seattle as a placeholder).
- **Tickets never wrap:** the most urgent stay open, the rest fold to slim spines (face, patience ring, the job's icon). Shown at 1, 3, 5 and 7.
- **The whole HUD:** one light, one shadow, real materials, small movements in the window; the goal's café front lights up when a star lands.
- **Every word ≥ 22 px and ≥ 4.5:1**, measured: 226 words across 43 pictures and GIF frames, none failing. The lowest is OPEN at 4.53:1, today's own colours.
- **Five questions** at the end of the README.

## The pictures (all 1920 × 1080 photos of the game, or crops of them; GIFs at 30 fps)

| File | What it shows |
|---|---|
| `M2-compare-round1.png` | One page: round 1's A against round 2, the same moment, with what changed |
| `cash-T2-pay.gif` | **The pick, being paid** (2.6 s): bill flight, bell, wheels, receipt "+$12" then "tip +$3", tear |
| `cash-T2-spend.gif` | The pick, spending at closing (2.0 s): drawer, bill to the new table, "−$80" in plum, wheels down |
| `cash-T1-pay.gif`, `cash-T1-spend.gif` | Treatment 1, the bill clip |
| `cash-T3-pay.gif`, `cash-T3-spend.gif` | Treatment 3, the coin saucer |
| `M2-cash-compare.png` | The three treatments at rest (2×) |
| `M2-day.png` | 10:22 AM, Day 1, the bright street, one ticket |
| `M2-late-afternoon.png` | 4:25 PM rush: five tickets in one row, the Now line |
| `M2-last-orders.png` | 7:19 PM, golden hour, LAST ORDERS |
| `sign-last-orders.gif` | 7:00 PM: the card turns to LAST ORDERS and sways faster; the till swings a beat behind |
| `M2-closing.png` | 8:00 PM, dusk, CLOSED |
| `M2-night.png` | 11:05 PM: only the window, top centre, with the night's hour |
| `M2-first-person.png` | First person at the counter |
| `M2-bench.png` | The bench strip and two ticket chips |
| `M2-sky-sf.png` | The window at 9 AM, 11 AM, 2 PM, 6:30 PM, 8 PM, 11 PM (2×) |
| `M2-sky-true-size.png` | The same six hours at true size: San Francisco at a glance |
| `window-day-in-4s.gif` | The window's whole day in 4 s |
| `window-tiny-life.gif` | 8 PM in real time: beacons, the tower's crown, fog, the cable car |
| `M2-franchise-swap.png` | The skyline as one swappable layer: San Francisco and Seattle (placeholder), and the window taken apart |
| `M2-tickets.png` | Tickets at 1, 3, 5 and 7: one row, always |
| `M2-tickets-zoom.png` | Two open dockets and three spines (2×) |
| `M2-new-star.png` | Day 1's 6 s goal hint, a star landing, the quiet row |
| `star-landing.gif` | The star lands; the café front jumps and lights up; the toast |
| `M2-parts.png` | One level above, part by part (2×) |

## Decisions his to overrule (the full list, with reasons, is README §8)

1. The till over the bill clip and the coin saucer.
2. Coit Tower, Sutro Tower and the Bay Bridge left out (they don't read at 316 × 100).
3. The sun now sets on the left, behind the Golden Gate.
4. The fog's times.
5. The time in gilt.
6. Tickets:
   - the most urgent open, the rest as spines;
   - the 16 px mood word replaced by the patience ring and the face;
   - the device's name replaced by a badge on the face (its words stay in the hover details);
   - the fault kept at 22 px.
7. The receipt hangs over the middle stars for about 1.6 s.
8. The game's pay pop moves to the deep green chip (a contrast fix).
9. `money.paid` and `money.tip` retimed to when the money lands in the HUD.
10. F's Arrange "in the till" row becomes the till.
11. The gold leaf dims to 62 % at night.
12. Seattle as the placeholder city.
13. The Now line's header and the stock chips' line from 20 to 22 px.
14. Grace's Day 1 docket shows no latte (the lock).

## Outside my files (for the HUD build; exact edits in README §7)

- **`Juice.cs` line 239:** the pay pop's chip `UiSkin.Brand` → `UiSkin.BrandDeep` plus a `BrandBright` rim. Measured, "+$3 tip" is **4.28:1 today, a fail**, and 6.9:1 after. This is a finding about the game as it is now.
- **`TicketRailUI.cs`:** one row with a centre reserve.
- **`HudCorners.cs`:** `FranchiseLine` and "today" go; the column's bottom is about 270 px.
- **`ShopUI.cs`:** `clockText` at night gives way to the window.
- **Two 20 → 22 px proposals:** `DayOneGuideUI.TitleSize` and `AlsoSize`; `HudCorners.StockLineSize`.
- **The labs** that read these: PauseCheck, JuiceCheck, GraceAtHomeCheck.

## Things to know

- **The research timings marked ≈ are estimates.** They come from my memory of the games, not frame counts: I couldn't play the games or watch video here. Every fact with a link was fetched and checked. Two Point Hospital's floating figures and Spiritfarer's glim are flagged "as I remember it".
- **The Golden Gate's colour.** The Bridge District gives only CMYK 0/69/100/6 (closest paint SW Fireweed). The #C0362C I draw is my daylight reading of it, not their value.
- **Three new photos were patched:**
  - the 4:25 PM rush (its old column filled as round 1 did);
  - first person (the ceiling rebuilt along the beams' vanishing point);
  - the night (its old clock removed).

  Small seams can show if you look for them; none is near a word.
- **The payment GIFs are about 8 MB each** (78 frames, 1220 × 560 px, true size). The others are 0.2–3.4 MB.
- **Build cost: about 8.5 builder-days in UGUI**, all code-drawn. Round 1's A was 3 of those. The city layer, the till's moving parts, the flight, the dockets and the goal's moments add the rest. His girlfriend's art is optional: the skyline as tintable layers, the bill, the café front, a second city.
- **To redraw:** `cd /root/review24/designs/M2/src && python3 make_m2.py` (stills, about 1–3 min), then `python3 gifs.py T1 T2 T3 S1 S2 S3 star sign windowday windowlife measure` (GIFs, about 20 min).
