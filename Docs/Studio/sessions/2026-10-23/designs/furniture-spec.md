# The café grows with you: buy and place furniture (spec, designer F, 23 Oct 2026)

*For his approval. Nothing is built yet. Code facts are quoted from the code copy at `8033112` (`/root/wt23/F`) with file and line; Builder S's seat tags from `/root/wt23/S` (in progress). Every number was re-run in Python (`/root/review23/mockups/src/arrange_numbers.py`). Mockups: `/root/review23/mockups/arrange-*.png`.*

---

## On your phone (the whole spec in one screen)

- **Day 1 opens half-finished.** The counter, the bench, the drinks machine and the repair bar's 3 stools. The seating floor is under dust sheets and boxes. The intro says why: years at a coffee cart bought this place.
- **At closing, the phone's Shop has a new button: Arrange the café.** The overhead camera, a floor grid, a strip of furniture with prices.
- **A see-through ghost** shows the piece: green where it fits, red where it doesn't, with the reason in a few words. 25 cm grid, quarter turns. ✕ place, △ turn, ○ back. Point at a piece to pick it up, or hold □ to sell it back for half.
- **Only while the café is closed.** People hold seats and cups sit on tables by day. PlateUp! plans "when the restaurant closes for the day" too.
- **v1 sells 6 pieces and plants.** Bistro $80 and Square $90 (2 seats each) from Day 1. Window counter $150 (3) and Banquette bay $140 (2) from Day 2. Communal table $360 (8) and Lounge corner $160 (2) from Day 3. Plants $35, looks only. Tier 2 later.
- **Seats count:** up to about 15 seats, each seat brings about $12 a day. Past about 21, no more guests come, so extra tables only take walking room.
- **Old saves keep today's 28-seat café.** New games start half-finished.
- **5 parts to build, 4 of them in parallel.**
- **5 questions for you at the end**, each with my answer.

---

## Contents
1. The player's experience, step by step
2. The catalogue for v1
3. "Seats count": the numbers
4. Technical design (and every file, in build order)
5. The labs that prove it
6. Risks, and what to cut
7. Questions for him
8. Where this touches Designer P (P must agree)
9. Builder S's seat tags: the contract
10. Decisions his to overrule
11. Sources, numbers and notes

---

## 1. The player's experience, step by step

### 1.1 Day 1: the half-finished café

**What's there.** Only what Day 1 already teaches:
- the counter and the till;
- the bench;
- the drinks machine;
- the repair bar's three stools across from the bench;
- the four waiting spots;
- the olive trees by the door and the fig.

The repair bar and the waiting spots are not furniture. They never move and they can't be sold.

**What's under dust sheets.** The seating floor: everything south of the counter's aisles that today holds the banquette, the communal table, the bistro tables, the window counter, the reading corner and the lounge.
- Five canvas drop cloths with soft folds lie on the floor.
- A few cardboard boxes stand on them and in the corners (a stack where the reading corner will be).
- The aisle from the door to the counter, the queue and the waiting nook stay bare, so nobody walks over a sheet.
- See `arrange-1-first-table.png`: the café at Day 1's closing, with the first table's ghost.

**How the story says it.**
- The day intro's Day 1 line (`DayIntro.LineForDay(1)`, DayIntro.cs:988; "PLACEHOLDER COPY: his sister rewrites these", DayIntro.cs:978; never over 90 characters, DayIntro.cs:981) becomes: **"Years at a coffee cart bought this place. Now make it a café."** (60 characters). The cart lives there and nowhere else, as he chose.
- Today's line ("Fix what they bring. Pour what they order. Make the rent.") is the hook of the game. If he'd rather keep it, the cart line can go on the Day 1 recap instead. **P must agree** (P owns Day 1's beats).
- The world says the rest: sheets, boxes, the empty floor. No extra text.

**What Day 1 plays like.**
- Customers wait at the repair bar's stools and the waiting spots. A seated customer drains patience at 0.6×, one standing at a waiting spot at 1.15× (WaitingSpot.cs:31-33). "That difference is the entire pressure model" (WaitingArea.cs:91-97).
- His Day 1 of 22 Oct (7 customers, arrivals 30–86 s apart) never needs more than three seats at once. My model of that day gives **0.00 customers without a seat** in the half-finished café (§3.1).
- **No café guests on Day 1.** Patrons come only while the free seats, less those walking over, are *more* than the 3 kept for customers (PatronSpawner.cs:50, 179-180). Three stools are not more than three.
- **So Day 1 earns less.** His $354 included about $156 from guests in a 28-seat café (my model, §3; the economy check says "a 420 s day makes about $175" from guests, UpgradeEconomyChecks.cs:332-334). The same day half-finished closes with about **$198**. **P must agree**: P is reshaping Day 1 anyway, with more repairs.
- The opening's settled guests (a reader, a pair, a laptop worker: PatronSpawner.cs:83-85, NpcWave2Steps.cs:79-82) need seats that aren't there yet. They stay away on Day 1, quietly (today the spawner warns, PatronSpawner.cs:339-342; that warning goes for a seat the layout has switched off).

### 1.2 The first purchase

**When:** Day 1's closing, after the recap. The phone opens on Reviews as every evening (RecapUI.cs:102-106).

**What:** a bistro table for two, $80. Day 1 earns about $198, so it's affordable with money left for the cheapest upgrade (Bulk Restock, $100). A $125 tool upgrade then waits a day: the first "table or tool?" choice.

**How it's taught:**
1. The phone's **Shop** tab carries the badge it already has for low stock (`Badge(App.Shop, …)`, RecapPhone.cs:935), on Day 1's evening only.
2. The Shop app gets a new first card, **YOUR CAFÉ**: the chair icon, "3 seats", and one big button, **Arrange the café**. On Day 1 the card's line says "Place your first table".
3. Arrange opens with the Bistro card already chosen. Its ghost waits at the nearest green spot to the counter.
4. The Now card (Arrange's own, in the Now line's style: the Now line itself hides once the day is over, DayOneGuideUI.cs:236-237) says **DAY 1 · YOUR CAFÉ** over **Place your first table**.
5. One ✕ places it: $80 counts down in the till column, "+2" pops beside the seats, a soft thud and a puff of dust. The sheet under it folds away.
6. The Now card then says **Customers can sit there tomorrow.** (6 words), and the controls strip shows ○ **Back**.
7. ○ goes back to the phone. "Close up for the night" works as before.

Nothing is forced: a player who closes the phone without buying gets the same card tomorrow. **P must agree** on this end-of-Day-1 beat and its words.

### 1.3 Arrange mode

#### Entering it, and when you can't: only while the café is closed

You enter from the phone's Shop at closing, as many times as you like before "Close up for the night". Not by day, not at night, not in the morning intro.

Why closed only:
- **Seats in use can't move.** A customer keeps the seat they claimed (`waitingSpot`, CustomerBrain.cs:238), drains patience by it (CustomerBrain.cs:733) and only claims again when they have none (CustomerBrain.cs:1136). Switch a seat off under them and they keep waiting at nothing.
- **Cups sit loose on tables.** A dirty cup is its own object at a seat's cup spot (`DirtyCup.Place`, DirtyCup.cs:35-56). Move the table and the cup floats where it was. Clearing tables is one of his four waiting activities, so cups will be everywhere.
- **Buying is already a closing thing.** The Shop sells "only at closing, and saved at once" (RecapPhone.cs:45-46; `UpgradeShopUI.TryBuy`, UpgradeShopUI.cs:59-65). One money moment a day, the same rule for furniture.
- **The day is the pressure.** A 420 s day with customers waiting is no time to shop. Pausing the day to arrange would be a pause button that dodges the pressure.
- **People walk on the routes.** Moving a table carves a new hole in the walking surface (§4.3); doing that under 20 walking people is where stuck agents come from.
- **The market.** PlateUp!'s own store page: "when the restaurant closes for the day, the planning begins!" ([Epic Games Store](https://store.epicgames.com/en-US/p/plateup-a99631)).

The case for "any time" is his "sense of their own freedom". The freedom that matters is *what* and *where*, and that is unlimited at every closing: move, turn and sell as often as you like, for free. **Question 1** asks him.

**What entering does:**
- The phone slides down; the camera eases to the Arrange view (below).
- Anyone still sitting (patrons stay seated through closing) leaves under a short fade: `DayClock.ClearTheShop()`, which "empties the shop… Stats, save and recap are untouched" (DayClock.cs:349-358). Nightfall already calls it (NightCycle.cs:363).
- The world holds still, as at closing (`Time.timeScale = 0`, DayClock.cs:305). Arrange runs on unscaled time, as the camera already does (CafeViewMode.cs:347, 633, 683).
- A floor grid fades in over the customer floor. The catalogue strip slides up. The till column shows the money and the seats.

#### The camera

The day's overhead camera (`CafeViewMode`'s isometric camera), not a new one. Bible §2's lock: the overhead is for moving and delivering; placing is moving.
- It eases to the home angle and 26 m from the café's focus (0, 0.6, 9) (CafeViewMode.cs:43), with `OrbitTo` (CafeViewMode.cs:446-453). 26 m is inside its 24–48 m limits (CafeViewMode.cs:51-52): a 25 cm cell is about 15 px wide at 1080p. The mockups are drawn at that distance.
- Orbit and zoom work as by day: right stick and L2/R2 on the pad; middle-drag and the wheel on a mouse.
- Today the camera takes no input while the time scale is 0 or the recap owns input (`OverlayOwnsInput`, CafeViewMode.cs:236-238; `RecapOwnsInput`, DayClock.cs:89). That one line gets an exception while Arrange is up.
- The cut-away walls drop as they do by day (CutawayWall).

#### Picking a piece

- **A new piece:** L1/R1 (or Q/E, or a click on its card) moves along the strip. The chosen card lifts and gets an amber ring. Its ghost appears where the cursor is.
- **A placed piece:** with nothing in hand, move the cursor onto it. It gets an amber outline and a plate with its name and seats, and the □ icon with "Hold to sell it back: +$40". ✕ picks it up; hold □ sells it. See `arrange-3-move-or-sell.png`.
- **The cursor** is a point on the floor. Pad: the left stick moves it, relative to the camera as Ace walks (`MovementYaw`, CafeViewMode.cs:179), from one grid cell to the next; the D-pad nudges one cell. Mouse: it follows the pointer on the floor.

#### The ghost

The piece itself, drawn see-through:
- its own meshes in a flat see-through colour, outlined, with a dark under-stroke so it reads on the light floor;
- its footprint filled on the floor;
- a ring with a dot where each of its seats' people will stand to sit down (their stand points);
- a plate above it: **name · seats · price** (bold 26 px).

Green (the skin's bright brand green) when it fits, a lighter red when it doesn't. Red adds the reason as the plate's first line, and lights up what's in the way (the aisle, the queue's apron, the chair it would block). Place greys out on the controls strip. See `arrange-2-does-not-fit.png`.

#### Snapping and turning

- **A 25 cm grid**, square to the room (x from the door's middle, z from the shop front). The ghost jumps cell to cell; it never sits between cells.
- **Quarter turns** with △ (R on a keyboard).
- **Wall pieces** (the window counter, the banquette bay) have their back to the west or the east wall and slide along it. △ greys out for them: they face the room. A bay next to a bay snaps to it and they join into one run, as the café's banquette is today.
- **Why a grid, not free placement:** see Question 2. In short: a dyslexic player on a pad needs to hit a spot and stay there; every rule below is a distance, which a grid makes predictable; and TCG Card Shop Simulator's guide tells players to use its grid snapping "for consistent spacing" ([Play Table wiki](https://tcg-card-shop-sim.upcomer.com/wiki/Play_Table)).

#### Green or red, and why

The rules are the café's own editor steps' rules (each cited in §4.2), so what the player may build is what the steps were already allowed to build. The first rule a ghost breaks gives its reason:

| What's wrong | What the plate says |
|---|---|
| Off the customer floor (behind the counter, outside) | Only on the café floor |
| In the queue's apron | Keep the queue clear |
| In Ace's way out from behind the counter | Keep Ace's way from the counter clear |
| In front of the repair bar's stools | Keep the repair bar clear |
| Within 10 cm of another piece or a wall | Too close to the bistro table (its name) |
| In the 2 m aisle from the door to the counter, or the walks to the queue | Keep the way from the door clear |
| Within 75 cm of another seat's stand point | A chair there would be blocked |
| Within 80 cm of a waiting spot (or its own seat within 1.05 m) | Too close to where people wait |
| Within 90 cm of a queue place | Too close to the queue |
| Within 1 m of the door | Keep the door clear |
| Its own seat has no room to stand and sit | No room to sit down there |
| Its seat within 70 cm of another seat | Too close to another seat |
| Someone could no longer walk to a seat, a waiting spot or the queue | That would block someone's way |
| With the café full, Ace couldn't reach someone | Ace couldn't get past with the café full |
| Not enough in the till | You need $35 more (the price pill turns red) |

All of these are at most seven words, bold, on the dark plate (`UiSkin.Band`). Searching the whole floor with every piece in both layouts, the model meets all the local reasons as the first one somewhere, except two: the queue's and the door's are always caught first by the aisle or the queue's apron. They stay as safety nets.

#### Place, move, turn, sell

- **Place (✕ / click):** pays the price, puts the piece down, saves at once (as the Shop's purchases do: `PersistPurchase` → `TrySaveRecap`, UpgradeShopUI.cs:76-82).
- **Move:** ✕ on a placed piece picks it up. Its old place shows as a dashed outline. ✕ puts it down anywhere green, free. ○ puts it back exactly where it was, even off the grid (the café's own pieces stand to the millimetre: CafeSeatsSteps.cs:188-230).
- **Turn (△ / R):** a quarter turn, in hand.
- **Sell back (hold □ / hold F):** a ring fills round the button's icon in 0.6 s (mockup 3 shows it part-way), then the piece goes with a puff of dust and **half its price** comes back to the till. Let go early and nothing happens. Supermarket Simulator sells the same way: "Box up old furniture and long-press F to sell" ([Steam discussion](https://steamcommunity.com/app/2670630/discussions/0/4413046572166878131)). Why half: Question 3.
- **Picking up or selling a table clears its cups** (Ace clears them as he lifts it): `DirtyCup.Remove`, which frees the seat (DirtyCup.cs:71-75). **P must agree** (clearing tables is P's activity).

#### The controls

Buttons are icons, from `ControlHints` (four new verbs, §4.10). Pad (PlayStation shown; Xbox and Switch through `PadInput.Label`):

| Doing | Pad | Mouse and keyboard | ControlHints verb |
|---|---|---|---|
| Move the cursor | Left stick (D-pad: one cell) | The pointer | `Walk` (existing) |
| Place / pick up | ✕ | Left click | **`Place`** (new: South / Click) |
| Turn | △ | R | **`Turn`** (new: North / R; the same buttons as `Flip`) |
| Sell back | Hold □ | Hold F | **`SellHold`** (new: West / Hold F) |
| Browse the strip | L1 / R1 | Q / E, or click a card | **`Browse`** (new: L1 / R1 or Q / E) |
| Put it back / leave Arrange | ○ | Esc or right-click | `Back`, `Cancel` (existing) |
| Orbit, zoom | Right stick, L2 / R2 | Middle-drag, wheel | `Orbit`, `Zoom` (existing) |

The controls strip at the bottom shows only what works now, three or four icons:
- carrying a new piece: ✕ **Place**, △ **Turn**, ○ **Back**, L1 R1 **Browse**;
- pointing at a placed piece: ✕ **Pick up**, hold □ **Sell +$40**, ○ **Done**;
- a ghost that doesn't fit: **Place** greyed.

#### Leaving

○ with nothing in hand. The grid and the strip go, the camera eases back to where it was, the phone comes back up on the Shop app. The layout is already saved. Leaving also refreshes what remembers the room (§4.8).

### 1.4 What customers do with the new seats

- **They use them from the next claim on.** A seat registers itself when it's switched on (`WaitingSpot.OnEnable` → `WaitingArea.Register`, WaitingSpot.cs:68-76; WaitingArea.cs:50-59). Nothing else needs telling.
- **Customers choose them by what they are and where they stand** (Builder S's SeatChoice): a Cheerful person likes a window, a Chatty one the communal table, a Relaxed or Sentimental one something soft; personal space spreads them out; the walk breaks ties (SeatTags.cs, S's copy, lines 18-33). A table placed by a window becomes a window seat by itself, because the room's areas tag whatever stands in them (SeatTags.cs:47-51; §9).
- **Café guests come because of them.** The valve counts free seats (`WaitingArea.FreeSeats`, WaitingArea.cs:287-299; PatronSpawner.cs:179-180), so every seat past the 3 kept lets one more guest sit, at $5 at the door (PatronSpawner.cs:58, 453-467), until the street runs out of guests (§3).
- **The opening's settled guests find placed seats.** Today they're tied to seats by name (NpcWave2Steps.cs:79-82). When those aren't there (a new game, or sold), they take a free placed seat of the same kind: the reader a soft one, the pair a two-seat table, the laptop worker the communal table. Only while the free seats, less them, stay above the 3 kept, as for any guest. So on Day 2 someone may already be sitting at the table you placed.
- **The bins man's mess** goes on the live seats nearest the door (LodgerDay.cs:188-203): placed seats included, nothing to change.
- **Night:** placed pieces are solid for Ace at night through their own colliders; the night's solid list skips the café room because its furniture has its own (NightCollisionList.cs:47-63).

---

## 2. The catalogue for v1

### 2.1 The pieces

Every piece is a set the café already has, so it looks exactly like today's café, grain and all (§4.9). Sizes are the kit's, to the millimetre (CafeSeatsSteps.cs:130-149).

| Piece | Made of | Seats | What its seats are (S's tags) | Solids, metres (with its seats' room) | Price | Per seat | For sale from |
|---|---|---|---|---|---|---|---|
| **Bistro table** | `CK_Table_BistroRound` + 2 `CK_Chair_Spindle` (today's bistro 2-tops) | 2 | none ("bistro") | 1.72 × 0.72 (3.48 × 0.80) | **$80** | $40 | Day 1's closing |
| **Square table** | `CK_Table_Square2` + 2 `CK_Chair_Cafe` (the banquette 2-top's table and chair, plus a second chair) | 2 | none ("square table") | 1.92 × 0.72 (3.64 × 0.80) | **$90** | $45 | Day 1's closing |
| **Window counter** | `CK_WindowCounter` + 3 `CK_Stool_Bar` | 3 | Bar ("window counter"); Window by a window | 4.00 along the wall × 1.10 out (4.00 × 1.96) | **$150** | $50 | Day 2's closing |
| **Banquette bay** | `CK_BanquetteBay` + its `CK_Table_Square2` + `CK_Chair_Cafe`; `CK_BanquetteEnd` caps at the ends of a run | 2 | the cushion Lounge, the chair none ("banquette") | 2.10 along the wall × 2.06 out (2.55 × 2.50) | **$140** | $70 | Day 2's closing |
| **Communal table** | `CK_CommunalTable` + 8 `CK_Stool_Low` | 8 | Communal ("communal table") | 1.93 × 3.60 (3.74 × 3.60) | **$360** | $45 | Day 3's closing |
| **Lounge corner** | 2 `Chair_TubChair` + `Table_Lounge` + `LI_Rug_Corner` (the 20 Oct lounge) | 2 | Lounge ("lounge") | 2.18 × 0.90 (2.29 × 1.70) | **$160** | $80 | Day 3's closing |
| **Plant** | `CK_Monstera`, `CK_RubberPlant`, `CK_FiddleLeafFig` or `CK_OliveTree`, in its pot (△ picks which while in hand) | 0 | (looks only) | 0.40 × 0.40 (the pot) | **$35** | – | Day 1's closing |

- The repair bar's three stools are not for sale and can't move: the bench, the hand-back over the counter and the stations lab depend on them (StationsCheck.cs:758; S keeps them for repairs, SeatTags.cs:31-32).
- The window counter and the bay go only on the west and east walls in v1, where today's café has them. The front wall is the side the camera looks through, and a piece's back shows there (see the bay's missing back faces, §4.9).
- **A piece's seats always pay the same.** A seat is a seat to the valve (§3). The comfy pieces cost more per seat because they take more floor per seat and draw particular people (S: a Relaxed or Sentimental person, and a long repair, likes a soft seat).

### 2.2 The order they come in, and why

- **Day 1's closing: the bistro table, the square table, plants.** One thing to learn: put a table where it's green.
- **Day 2's closing: the window counter and the banquette bay.** The second thing: these go against a wall, and bays join.
- **Day 3's closing: the communal table and the lounge corner.** The big decision ($360 for 8 seats) arrives when the till can just afford it (about $547 at Day 3's closing in a typical week, §3.3), against the upgrades.
- It gives each closing something new, which plan.md §8 asks for ("Something new every day, said in the recap"). The recap can say it: **"New in the Shop: the window counter and the banquette."** **P must agree** (P owns the recap's "tomorrow" line).
- Locked cards show on the strip with a gold "Day 2" pill, so the player sees what's coming (the mockups show it).

### 2.3 Why these prices

- **Day 1 earns about $198 half-finished (§1.1), so the first table ($80) is about 40 % of it.** The first table and the cheapest upgrade (Bulk Restock, $100) fit together ($180); a $125 tool upgrade waits a day.
- **A seat earns about $12.5 a day while the street brings more guests than the café can seat** (between 5 and 11 seats, Day 3's customers: §3.1). So every piece pays for itself in 3 to 7 days: bistro 3.2, square 3.6, communal 3.6, window counter 4.0, banquette 5.6, lounge 6.4 days.
- **The first week still leaves money for the upgrades** ($100–$700, P's brief): a player who buys all six pieces by Day 6 (22 seats) holds about $1,548 at Day 7's closing, or $548 in a slow week (§3.3).
- **P must agree**: the work income per day is P's to set; my week uses his Day 1 as the start and placeholders after.

### 2.4 Décor: v1 and later

- **v1: plants only**, one card, four kinds. Looks only, as he chose: no tags, nobody reacts (Question 5).
- **Later: lamps.** A lamp has to register with the café's lighting, which captures its warm lights once (`warmLights`, CafeDaylight.cs:36; captured with their inside-the-café flag, CafeDaylight.cs:286-291). A runtime register is new code.
- **Later: rugs.** The lounge's rug reads as a pale sheet of paper from above (art-R2, LI_Rug_Corner): tint it first.
- **Later: pictures, shelves, signs.** Grace's frames read empty (art-R1, GH_Frame_*); the café's walls are someone else's step.

### 2.5 Tier 2: not in v1, ready for it

**How it will work.** Point at a placed piece in Arrange and △ says **Upgrade $240**. Holding it swaps every part's mesh for its `T2_` twin, in place. The catalogue was built for exactly that: "Every Tier 1 item has a direct Tier 2 counterpart at identical dimensions and seat heights, so upgrading swaps the mesh without disturbing a layout the player already built", "Colliders carry over unchanged between tiers", and "Naming is `T2_<Tier1Name>`" (Project doc `claude/furniture-catalog.md`). Seats, footprint, validation and the save's place stay the same; the save's `tier` becomes 2. The catalogue suggests Tier 2 at about 3–4× Tier 1.

**Why not in v1:**
- **The marble and brass don't read yet.** "The 'marble' is one flat cream… the premium tops read as cream laminate; and T2_Brass at metallic 0 reads as mustard paint… S2 the day Tier 2 is for sale" (art-R2). It needs a "Cafe marble" material applied the way `CafeFurnishing.Remap` turns flat woods into grain (CafeFurnishing.cs:447-472), and a paler brass.
- **Half the café's pieces have no twin.** Twins exist for the bistro table and spindle chair, the square table and café chair, the bar stool, the low stool, the tub chair and the lounge table (the catalogue's pairs). None for `CK_CommunalTable` (3.6 m long; the catalogue's communal is 1.80 × 0.80), `CK_WindowCounter` or `CK_BanquetteBay`: Blender work.
- **v1 doesn't need it to make money matter.** The upgrades and the six pieces already take a week's earnings (§3.3).
- **The save is ready now:** every placed piece saves a `tier` from day one, so Tier 2 needs no migration (§4.5). **Question 4.**

---

## 3. "Seats count": the numbers

*All from `arrange_numbers.py`: 300 simulated days per row, PatronSpawner's own rules (a try every 12 s ± 35 %, at most 14 guests alive, 3 seats kept for customers, a pair 30 % of the time, $5 at the door: PatronSpawner.cs:36-58, 175-186, 453-467), stays of 40–90 s (PatronBrain.cs:50-51; ×1.3 a pair, ×2 a laptop worker: PatronBrain.cs:85-87), the walk over from a car or a door first (CafeArrivals), and the day's customers seated first (a drink 50 s, a repair 130 s).*

### 3.1 Capacity → who can stay → money

Guests a day and their money, Day 3 (10 customers); customers with nowhere to sit on Day 1 (his 7, at his times), Day 3 and Day 5 (14):

| Seats | Guests in | Guests' $ a day | $ a day for each extra seat | Seats in use | Customers with no seat: Day 1 / 3 / 5 |
|---:|---:|---:|---:|---:|---|
| 3 (half-finished Day 1) | 0 | $0 | – | 49 % | 0.00 / 1.73 / 4.28 |
| 5 | 4 | $20 | 10.2 | 48 % | 0.00 / 0.56 / 1.87 |
| 7 | 9 | $46 | 12.8 | 48 % | 0.00 / 0.23 / 0.86 |
| 9 | 14 | $72 | 12.8 | 48 % | 0.00 / 0.08 / 0.41 |
| 11 | 19 | $96 | 12.2 | 47 % | 0.00 / 0.02 / 0.15 |
| 13 | 24 | $118 | 10.8 | 46 % | 0.00 / 0.01 / 0.05 |
| 15 | 27 | $136 | 9.2 | 44 % | 0.00 / 0.00 / 0.02 |
| 18 | 30 | $152 | 5.3 | 42 % | 0 / 0 / 0 |
| 21 | 31 | $156 | 1.4 | 37 % | 0 / 0 / 0 |
| 24 | 31 | $156 | 0.0 | 32 % | 0 / 0 / 0 |
| **28 (today's café)** | 31 | $156 | −0.1 | 27 % | 0 / 0 / 0 |
| 32 | 31 | $156 | 0.2 | 24 % | 0 / 0 / 0 |
| 38 (the most that fits) | 31 | $156 | 0.0 | 20 % | 0 / 0 / 0 |

What it says:
- **A bigger café pays, up to about 18 seats:** each seat brings $9–13 a day, and the café fills with guests as you build it. Day 1's and Day 5's own tables say the same (Day 1: $11.8–13.8 a seat up to 13 seats; Day 5: $8.5–11.8).
- **Seats also let customers sit**, which is the pressure model: with 3 seats, Day 5's 14 customers leave 4.3 standing (draining 1.15× instead of 0.6×); with 9 seats, 0.4; with 13, almost none (0.05).
- **Past about 21 seats nobody more comes.** The street sends about 31 guests in a 420 s day whatever the seats: a try every 12 s, and each stays about a minute. With 18 seats there's nearly always one free when someone arrives. (The 14-alive cap only binds when guests come faster: at a try every 7–9 s the curve flattens at 35–38 guests.)
- So the **money-best café is about 18–21 seats**. Today's 28 seats are three quarters empty on average (27 % in use). That is his 9 Oct hint, with numbers: fewer tables leave more room to walk around, and past a point they cost you nothing to lose.

### 3.2 What a crammed café costs: room and paths

The same rules and both guards that colour the ghost were used to fill the floor (§4.2). The model finds:

| Layout | Seats | Floor Ace can still walk with every seat taken | Ace's walk from the drinks machine to a table seat: mean / longest | There and back at 5 m/s |
|---|---:|---:|---:|---:|
| The half-finished café (Day 1) | 3 | 153.6 m² | – | – |
| End of a typical first week (§3.3) | 22 | 96.5 m² | 15.4 / 22.3 m | 6.2 s |
| Today's café (old saves) | 28 | 77.8 m² | 16.2 / 23.7 m | 6.5 s |
| Crammed: today's + 5 bistro tables | 38 | 56.9 m² | 17.0 / 22.4 m | 6.8 s |

- **38 seats is the most that fits.** After 5 more bistro tables (south first: two by the front window each side, two behind them, one between the bistros), no spot passes both guards. The ghost goes red with "That would block someone's way". A cramped café can't block paths, because the rules won't let it.
- **Each seat past today's takes 2.1 m² of walking floor** (77.8 → 56.9 m² for 10 seats): 27 % less room, for no more guests (§3.1).
- **The walk barely grows** (+0.3 s a delivery from 28 to 38 seats): the extra tables are near the door, the counter is where it is. The cost of a crammed café is the room and the cups: more guests' cups to clear once P's clearing tables is in (**P must agree**).
- **Every piece of today's café passes the proposed rules** against the rest (16 of 16), and both guards pass for today's café (35 of 35 places served, 35 of 35 reached) and for the half-finished one (10 of 10). So an old save's café is a legal layout, and any piece of it can be picked up and put back.

### 3.3 Prices against the first week's earnings

A typical player buys one piece a closing, in the order they unlock, and restocks every other day ($30). Placed by the model where the rules allow, nearest the counter first. Guests' money from §3.1; **the work income is a placeholder until P's spec** (Day 1 is his own: $354 less the $156 his 28-seat café's guests paid).

| Day | Seats open | Work $ (placeholder) | Guests $ | Till at closing | Bought at closing | Till after | Seats tomorrow |
|---:|---:|---:|---:|---:|---|---:|---:|
| 1 | 3 | 200 | 0 | 200 | Bistro table $80 | 120 | 5 |
| 2 | 5 | 220 | 21 | 361 | Square table $90, restock $30 | 241 | 7 |
| 3 | 7 | 260 | 46 | 547 | Window counter $150 | 397 | 10 |
| 4 | 10 | 300 | 84 | 781 | Banquette bay $140, restock $30 | 611 | 12 |
| 5 | 12 | 330 | 108 | 1,048 | Communal table $360 | 688 | 20 |
| 6 | 20 | 360 | 155 | 1,203 | Lounge corner $160, restock $30 | 1,013 | 22 |
| 7 | 22 | 380 | 155 | 1,548 | – | 1,548 | 22 |

A slow week ($150 of work every day) still buys every piece on the same day, ending Day 7 with $548. So the prices never stall a player; they set the pace of the café's look. **P must agree** on the earnings, and on whether the first upgrade should come before or after the second table.

Where the model put the week's pieces (centres, metres): the bistro at (3.05, 11.55), turned a quarter (its chairs north and south), by the counter's east end; the square table at (−3.20, 11.05); the window counter on the east wall, z 7.2–11.2; the bay on the west wall, z 7.0–9.1; the communal table at (−2.70, 7.55), its length north–south; the lounge corner at (2.30, 8.30), turned a quarter. End of week: 22 seats, both guards pass (29 of 29).

### 3.4 A what-if I don't recommend: guests that follow the seats

If the street sent more guests as the café grows (up to seats − 6 alive above 20 seats, a try every 12 × 18/seats s above 18 seats), every seat would keep paying $8–10 a day right up to 38 ($243 at 28 seats, $328 at 38). Then the best café is always the most crammed one, and building becomes filling a spreadsheet. His choice of "seats count, décor is looks" was for a café he shapes, not a spreadsheet. **My recommendation: keep today's valve.** If later days need a bigger café to pay, the lever is guests that grow with the café's stars (P's economy), so the café has to grow to catch them.

---

## 4. Technical design

### 4.1 The runtime furniture object

**A piece is a `PlacedPiece`.** It is the root of one set: the table and its chairs, the counter and its stools, a bay with its 2-top. Two kinds of piece share the one component:
- **the café's own pieces**, the sets the scene already has (old saves' café). They stay scene objects, so every name and every reference stays as it is (§4.6; what moving them under piece roots risks: §6.1, risk 2);
- **bought pieces**, made at run time from their kind's prefab.

What a piece holds (the prefab, and the converted scene sets alike):
- **its meshes**, the café kit's own (`CK_…`), with the materials the scene gave them (grain woods, §4.9). No static flags: a static-batched mesh stays where the scene was built when its transform moves, and today `CafeFurnishing` and `CafeRealismPass` mark their pieces `BatchingStatic` (CafeFurnishing.cs:153-154; CafeRealismPass.cs:151-152);
- **one `BoxCollider` per solid**, the kit's own box as Seats 1 sets it (`x.box.center = x.kit.boxCentre; x.box.size = x.kit.boxSize`, CafeSeatsSteps.cs:783-787; the boxes: CafeSeatsSteps.cs:138-147). They make it solid for Ace (`CharacterController`) and at night;
- **one carving `NavMeshObstacle` per solid**, the same box (§4.3);
- **its seats**: a `TableSeat` each, with `SeatPose`, `CupSpot` and `StandPoint`, drain 0.6, made exactly as Seats 1 makes them (CafeSeatsSteps.cs:800-831), copied from the scene's own seats so they sit to the millimetre;
- **S's `SeatTags` on each seat** (§9), and the kind's zone name;
- **its dressing**: the napkin dispenser, the laptops and the tote (off until a laptop guest sits), the left cups (off), the lounge's rug (§4.8);
- for a bay: **two end caps** (`CK_BanquetteEnd`), each shown only where no bay joins it, and the **back panel** (§4.9).

**A kind is a `FurnitureDefinition`** (a ScriptableObject in `Assets/Data/Furniture/`): its name on the card, its prefab (plants: one prefab per kind of plant), price, seats, the day it's for sale from, whether it goes against a wall, its zone. **The asset's name is its save identity: never rename it**, as for upgrades ("upgrade assets must NEVER be renamed after shipping", SaveData.cs:191-194). A `FurnitureCatalogue` asset lists them in the strip's order.

**`CafeLayout`** (one in the scene, beside `SaveManager`) knows every piece, applies the save at start, takes the save's snapshot, and does every buy, move and sale (the contract in §4.11). `ArrangeMode` only asks it.

### 4.2 Placement validation

`FurnitureRules`: plain C# that the ghost, `CafeLayout`, the editor step and the labs all call, so there is one set of rules.

**The rules are the café steps' own rules** (with their sources), so the player may build exactly what the steps were allowed to:

| # | Rule | Numbers and where they come from |
|---|---|---|
| R1 | On the customer floor: inside the room, south of the counter (its front at z 13.3, less 0.4), out of three aprons: the queue's (x −2.6..2.6), Ace's way out from behind the counter (x 4.6..7.4) and the repair bar's (x −5.9..−2.5), each from z 11.3–11.6 to the counter | the room: `CafeDaylight.CafeInside` (CafeDaylight.cs:66); the aprons replace a blanket band so plants may stand at the counter's ends, as today's do |
| R2 | Nothing within 0.10 m of another solid or a wall (bays may touch bays) | Lived in 2's `FreeMargin` (CafeDressing3Steps.cs:118) |
| R3 | Out of the door-to-counter aisle (x −1..1, z −1.2..12.5) and the 1.2 m walks from the door to each queue place | CafeFurnishing.cs:1026; CafeSeatsSteps.cs:107; `QueueWalkHalf` 0.6 (CafeDressing3Steps.cs:116) |
| R4 | Its solids clear of people's places: 0.75 m from seats' stand points, 0.80 waiting spots, 0.90 queue places, 0.80 station stand points, 1.0 the door | `CafeFurnishing.BuildKeepClear` (CafeFurnishing.cs:992-1007); CafeSeatsSteps.cs:106 |
| R5 | Its own stand points: 0.40 m from any solid, 0.70 from another stand point, 1.05 from a waiting spot, 0.90 from a queue place, never in the aisle | `NavMargin`, `SeatClaim`, `SeatToWaiting`, `SeatToQueue` (CafeSeatsSteps.cs:102-105) |
| R6a | Every seat, waiting spot and queue place still reached from the door by a 0.35 m walker | `ValidateLayout`'s question (AcesCafeLayoutSetup.cs:640-663; its 0.22 m snap, line 651), asked on a 0.10 m grid |
| R6b | With every one of them taken, Ace still reaches within 2.05 m of each | `ValidateOccupiedCirculationV2` (AcesCafeLayoutSetup.cs:196-283): Ace's capsule + 0.03 m, people 0.65 m, a 0.20 m grid flooded from his start |
| R7 | The money is in the till | `ShopEconomy` |

How it runs:
- **The floor map is built once** when Arrange opens: every solid that isn't a piece (walls, counters, the back bar, fixed props), the people's places, Ace's start, the door. Physics is asked once, then never per ghost.
- **A candidate is the piece's boxes and stand points at a pose.** R1–R5 are box and point distances; R6 rasterises every piece's boxes onto the two grids (29,400 and 7,957 cells) and floods them. A few milliseconds, and only when the ghost moves to another cell or turns.
- **The answer is the first broken rule**, with its words (§1.3) and the thing to light up.
- **The editor and the labs use the same function** for a whole layout (`CheckWholeLayout`), so the old editor guards' meaning survives the move to carving (§4.3).

The arithmetic beside these rules is my Python replica (`arrange_cafe_model.py`, `arrange_placement.py`). It passes today's café (35 of 35 on both guards; builder C's 19 Oct report expects exactly "Cafe layout: 28 seats … PASS" and "Occupied circulation: 35/35 … PASS": `Docs/Studio/sessions/2026-10-19/reports/builder-C-cafe.md:47`).

### 4.3 NavMesh updating: carving, not a runtime re-bake

**What the project does today:**
- One `NavMeshSurface`, "the café's routes" (Playtest4ProducerSteps.cs:226), baked in the editor from physics colliders at 0.08 m voxels into `Cafe routes.asset` (`BakeRoutes`, AcesCafeLayoutSetup.cs:597-638; AI Navigation 2.0.13, Bible §6).
- Furniture groups are marked Not Walkable for the bake (Playtest4ProducerSteps.cs:259-267), because otherwise "a chair seat is ~0.45 m and a tabletop ~0.72 m, so both are 'a step up' to the pathfinder" and people walked over furniture (NavMeshFurnitureTool.cs:7-21).
- People are non-carving obstacles (NpcJourney.cs:374-380; StreetLife.cs:531-540; CafeNpcPass1Setup.cs:186-201). One lab already blocks a route with a carving box (CafeLab.cs:462-466).

**The two ways to re-route people round a placed piece:**
- **(a) Re-bake at run time** (`NavMeshSurface.UpdateNavMesh`) after arranging. It keeps the editor's exact pipeline. But it collects every source again at run time; in a built game, meshes read at run time must be readable (I believe this covers mesh colliders too: **to verify**); the surface covers the street, because guests walk over from cars and doors (CafeArrivals), so it's not a small rebuild; and the editor checks would then need the rebuilt mesh.
- **(b) Carve.** Bake the routes once without the placeable pieces, and give every piece a carving box per solid. **Chosen.**

Why (b):
- **Nothing is rebuilt**, in the editor or in a build: the same behaviour on his Mac and in a Steam build.
- **A piece's hole moves with the piece.** Pick it up and the hole goes; put it down and it carves.
- **Carving only happens when a piece is placed, moved, sold or loaded,** which is never during service (Arrange is closed-only). That bounds the "performance of carving" risk the brief names to a closing and a load.
- **It's how the project already blocks a route in a lab** (CafeLab.cs:462-466), with the same settings.

How:
- The conversion step (§4.6) marks every placeable piece `NavMeshModifier.ignoreFromBuild = true`, as the café's decor and street groups already are (AcesCafeLayoutSetup.cs:379, 484, 508), and re-bakes the routes once. The repair bar's stools, the counters and everything fixed stay in the bake as today.
- Each solid's obstacle: `shape = Box`, the collider's centre and size, `carving = true`, `carveOnlyStationary = false` (carve as soon as it's switched on, as CafeLab does: CafeLab.cs:465-466).
- Carving cuts the box plus the agent's 0.35 m, as the bake's erosion did. Stand points stay 0.40 m from every solid, "the agent's 0.35 and a 0.08 voxel's slack" (CafeSeatsSteps.cs:102), so every seat stays on the routes.
- **What it costs the editor check.** Edit Mode doesn't carve, so `ValidateLayout`'s NavMesh paths (AcesCafeLayoutSetup.cs:640-663) stop seeing placeable furniture. So the conversion step checks with `FurnitureRules.CheckWholeLayout` (the same question on the geometry) and with `ValidateOccupiedCirculationV2` (physics, which still sees every collider), and the placement lab asks the real NavMesh in Play mode after carving (§5).
- **Not known:** whether carving updates while the time scale is 0. It doesn't matter: nobody walks during Arrange, and it carves when the world runs again. The labs check paths after Arrange closes.

### 4.4 Seat registration

- A placed piece's seats are ordinary `TableSeat`s (a `WaitingSpot`: TableSeat.cs:19). Switched on, each registers itself (`WaitingSpot.OnEnable` → `WaitingArea.Register`, WaitingSpot.cs:68-71; WaitingArea.cs:50-54); switched off, it leaves (WaitingSpot.cs:73-76). "Seats register themselves at runtime, which is good for placed furniture", as the brief says.
- **Picking a piece up switches it off**; putting it down switches it on. Selling destroys a bought piece, or leaves one of the café's own switched off.
- Nobody holds a seat during Arrange: `ClearTheShop` on entry (DayClock.cs:351-358), and the day is over.
- **The valve sees new seats at once** (`FreeSeats`, WaitingArea.cs:287-299). The opening's settled guests and the bins man's mess too (§4.8).
- **Tags: S's.** Each prefab seat carries a `SeatTags` with S's tags for its zone, and the room's areas add where it stands (§9). Nothing in the choosing knows a seat's name (SeatTags.cs:47-51).
- **Dirty seats:** picking up or selling a piece clears its cups first (`DirtyCup.Remove`, which cleans the seat: DirtyCup.cs:71-75), so no cup floats and no seat stays dirty with nothing on it (`IsAvailable` is `Occupant == null && !IsDirty`, TableSeat.cs:112).

### 4.5 The save format and migration

**Additive, with no version bump**, the house way for the notebook and the night: "older builds skip a field they do not know, so this save still loads on a branch without the notebook" (SaveData.cs:207-215). His saves keep loading on any branch. (`CurrentVersion` stays 5, SaveData.cs:177-178.)

```csharp
// SaveData.cs, additive (23 Oct 2026): the café's furniture.
public CafeLayoutData cafe = new CafeLayoutData();

[Serializable]
public class CafeLayoutData
{
    public int format;                                          // 0: absent (a save from before 23 Oct 2026); 1: this format
    public PlacedPieceData[] pieces = new PlacedPieceData[0];   // every piece that is ON, the café's own included
    public string[] sheetsLeft = new string[0];                 // the dust sheets still lying (their ids)
    public int nextId = 1;                                      // the next bought piece's number
}

[Serializable]
public class PlacedPieceData
{
    public string id = "";        // "scene:<the piece root's name>" for the café's own; "p<n>" for a bought one
    public string kind = "";      // its FurnitureDefinition asset's name: SAVE IDENTITY, never rename
    public float x, z, yaw;       // its pivot on the floor (world metres) and its turn (degrees)
    public int tier = 1;          // 1 Workshop; 2 Marble & Brass (not sold in v1, saved from day one)
    public string variant = "";   // a plant's kind ("Monstera"); "" otherwise
}
```

**Loading** (`CafeLayout.Apply`, called from `GameBootstrap.Start`, which "pushes loaded save data into every system, once", before the clock is restored and before either spawner's first Update: GameBootstrap.cs:3-5, 31-38):
- **No save file: a new game** (`HasSave` false: SaveManager.cs:94-101). The opening layout: the door's olives and the fig on, every other own piece off, all five sheets lying.
- **`format` 0: an old save** (or a lab save written by today's labs). Today's café: every own piece on where the scene has it, no sheets, nothing bought. The café his saves were played in, exactly.
- **`format` 1:** what's listed is on, at its saved pose; the café's own pieces that aren't listed are off; bought ones are made from their kind's prefab; the sheets in `sheetsLeft` lie, the rest are gone. An unknown kind is skipped with a warning, as an unknown upgrade is (GameBootstrap.cs:27).

Why the "old save" rule lives in `CafeLayout` and not in `ValidateAndMigrate` (SaveData.cs:217-268): the migration can't see the scene, and a new game's `new SaveData()` is also `format` 0. Only `HasSave` tells them apart. `ValidateAndMigrate` gets null guards only (`cafe ??= new CafeLayoutData()`, the arrays, no empty ids), like its notebook lines.

**Saving:**
- `CaptureState` (SaveManager.cs:150-193) adds `data.cafe = CafeLayout.Instance.Snapshot()`: always `format` 1, every piece that's on.
- `TryCreateNextDay` clones the save for tomorrow (SaveData.cs:270-281), so the layout carries into the morning checkpoint.
- **When:** after every purchase, move, turn and sale, through `TrySaveRecap` (SaveManager.cs:123-129), as the Shop's purchases do ("Only save after a successful transaction… Never charge on load", UpgradeShopUI.cs:76-82). The lock "saves at Open Tomorrow" already has this exception for purchases. If a save fails, the change is kept on screen and the Shop's own save error shows; nothing is charged twice.

### 4.6 Converting today's café: the editor steps

`Assets/Editor/FurnitureSteps.cs`, through the café redesign's shared step (`CafeRedesignShared.Step`: survey first, which changes nothing; one undo group and a record; the guards; photos; any FAIL puts everything back; only the latest step may be undone):

```
Fixit Fidget > Cafe redesign > Furniture 0 - Check first (read-only)
Fixit Fidget > Cafe redesign > Furniture 1 - The café's own pieces become placeable (scene, with Undo)
Fixit Fidget > Cafe redesign > Furniture 2 - Build the furniture prefabs (assets)
Fixit Fidget > Cafe redesign > Furniture 3 - Lay the dust sheets (scene, with Undo)
Fixit Fidget > Cafe redesign > Furniture 1/3 - Undo (scene)
```

**Furniture 0 (the survey)** finds the café's own sets by the names their steps gave them: Seats 1's groups "Bistro 2-top (south/north)", "Banquette (west wall)", "Banquette 2-top (south/middle/north)", "Communal table", "Window counter" (CafeSeatsSteps.cs:184-186); Lived in 2's reading corner; Café dressing 3's lounge; the floor plants. It requires S's Seat tags 1 and C's Art tidy 1 (the coats out) to have run first, and lists every object it will move, by path.

**Furniture 1 (the scene):**
1. Each set becomes one piece: a new root at its pivot under `31 - furniture/The café's own pieces`, with the set's parts moved under it (world positions kept, names kept). A bay's piece is the bay and its 2-top group; the banquette's two ends become the end bays' caps. Napkins and other table-top dressing on a table's top join that table's piece. Object references don't change when a parent does, so `PatronSpawner`'s settled guests still point at the same seats.
2. Each piece gets its `PlacedPiece` (its kind, id `scene:<name>`), its carving obstacles, and `NavMeshModifier.ignoreFromBuild` on its root.
3. Static flags are cleared on every part.
4. The three bays get their back panel (§4.9).
5. The routes are re-baked without the placeable pieces (`BakeRoutes`, AcesCafeLayoutSetup.cs:597-638).
6. Checks: every own piece on, `FurnitureRules.CheckWholeLayout` passes (28 seats, 35 of 35 places reached and served), and so does `ValidateOccupiedCirculationV2`; every own piece passes the rules against the rest (16 of 16, as the model); no static flag on a placeable part; every seat's tags read as Seat tags 1 left them (`TableSeat.Tags`).

**Furniture 2 (the prefabs):** each kind is built from its source set, copied: the bistro from the south bistro 2-top; the square table from the south banquette 2-top's table and chair, plus a second chair mirrored; the window counter; a bay with both caps; the communal table; the lounge corner from the 20 Oct lounge; each plant. Saved under `Assets/Prefabs/Furniture/`, with the kinds under `Assets/Data/Furniture/` and the catalogue. Checks: every prefab placed at its source's pivot gives the same solids and stand points within 1 mm; every renderer passes the grain rule: no material named `CC_Wood_*` or `DC_Fabric` left (the very test `CafeFurnishing.Remap` makes, CafeFurnishing.cs:458-461); its seat count matches its kind's.

**Furniture 3 (the dust sheets):** five sheets and their boxes under `31 - furniture/Dust sheets`, each with a `DustSheet` (id, floor footprint), switched off in the scene. The runtime switches them on for a new game.

Undo restores every parent, removes the components and the sheets, and re-bakes the routes as they were.

### 4.7 The dust-sheet state

- **What a sheet is:** a `DustSheet` (id "sheet-1" to "sheet-5", its floor footprint), a canvas cloth and the boxes on it.
- **Where:** over the seating floor, never on the aisle, the queue, the waiting nook or the repair bar's apron (the mockup's five sheets).
- **New game:** all five lie. **Old save:** none.
- **A sheet goes when a piece is placed on it**: it folds away with a soft cloth sound, and its boxes go with it. It never blocks the ghost; the floor map ignores sheets and their boxes.
- **A sheet never comes back.** Sheets still lying show where the café can still grow: room nobody has used yet.
- **Solid?** The cloth is flat and has no collider. The boxes have colliders and carving boxes like any piece (Ace shouldn't walk through boxes), and go with their sheet. On Day 1 no route crosses the seating floor.
- **Saved** as `sheetsLeft`.
- **Art:** the cloth is a Blender job (one draped canvas in three sizes, soft folds, warm canvas about #CEC3AE), or for a first pass in Unity a flat quad with a canvas texture lit by the room. Boxes: Grace's house kit's `GH_BoxStack` and `GH_Cardboard`, or POLYGON City's cardboard boxes (as NightTwoSteps.cs:580-585 uses them).

### 4.8 How the dressing props follow

| Prop | Where it is today | In v1 |
|---|---|---|
| Laptops, the tote, the left cups | children of their seats (Lived in 2: CafeLivedInSteps.cs header, "each a CHILD OF ITS SEAT") | follow: they're under the piece's seats. `NpcLifeProps` finds them by name prefix and distance and caches the search (NpcLifeProps.cs:42-64), so `NpcLifeProps.Refresh()` runs when Arrange closes |
| Napkin dispensers | on every table (Lived in 2) | part of their table's piece, and in the prefabs |
| The reading corner's rug and lamp | Lived in 2 | part of the reading corner's piece. The lamp keeps its light, which the café's lighting captured once at start with "inside the café" (CafeDaylight.cs:286-291); moved within the room it stays inside. Sold, it goes dark with the corner |
| The lounge's rug | Café dressing 3 | part of the lounge's piece and the Lounge corner prefab |
| Coats on chairs | Lived in 2 | gone: spec C's Art tidy 1 (art-R2's S1) |
| Dirty cups (at run time) | loose at a seat's cup spot (DirtyCup.cs:35-56) | cleared when their table is picked up or sold; the mess's tables regroup by position (`MessTable.Group`, DirtyCup.cs:178-205) |
| What hangs near the cut-away walls | one shared list, kept "until something says otherwise… the furnishing tools" (CutawayWall.cs:240-247) | `CutawayWall.ForgetCandidates()` then `PrepareCandidates()` when Arrange closes (CutawayWall.cs:273-277) |
| The banquette's ends | two `CK_BanquetteEnd` at the run's ends (CafeSeatsSteps.cs:198, 201) | each bay has two caps; after every change a cap shows only where no bay joins it |
| The opening's settled guests | tied to seats by name (NpcWave2Steps.cs:79-82; `SeatsFor`, PatronSpawner.cs:358-369) | their own seats if on, else a free seat of the same kind by S's tags; only while free seats, less them, stay above the 3 kept |
| The bins man's mess | the live seats nearest the door (LodgerDay.cs:188-203) | unchanged: placed seats included |
| The night's solids | the café room is skipped, its furniture has its own collision (NightCollisionList.cs:47-63) | unchanged: every piece has its colliders |
| The bins station, the condiment station, the notice board, the mats | fixed | unchanged, not furniture |

### 4.9 The art: what the audit found, and how this design meets it

- **"A piece spawned at runtime would come in flat, beside grain-textured built-ins"** (art-R1, Tier-1 shop furniture). The prefabs are copies of the café's own dressed sets, so they carry exactly the materials the scene gave them: the `CK_` kit imports with the "Cafe grain" woods, and the lounge's tub chairs already wear InteriorOchre (art-R1). Nothing is made from a raw FBX at run time. Furniture 2's check fails any prefab with a flat wood or `DC_Fabric` left (§4.6). Anything built later from a raw FBX (Tier 2, a new piece) goes through `CafeFurnishing.Remap` when its prefab is built. It's editor-only by nature: it saves grain-UV mesh copies as assets (CafeFurnishing.cs:474-490, 879-891), so it belongs in the prefab step, never at run time.
- **"CK_BanquetteBay… the tufted backrest has no back faces… It would be S1 if the banquette ever became placeable"** (art-R1). The bay is for sale, so the fix comes first: art-R1's own Unity option, a thin dark box behind the backrest, in the bay prefab and on the three scene bays (Furniture 1); or the Blender panel (about 12 triangles). v1 also keeps bays on the side walls, backs to the wall.
- **Tier 2's flat cream marble and mustard brass** (art-R2): Tier 2 waits (§2.5).
- **The lounge rug reads as paper from above** (art-R2): rugs aren't sold in v1; the lounge corner's is today's, tinted when that fix lands.
- **The olive tree's spiky canopy** (art-R1, low confidence): olives are sellable plants; the fix is R1's.
- **New art:** the dust sheets and their boxes (§4.7).

### 4.10 Every file to add or change, in build order

Scripts stay flat in `Assets/Scripts`, as the house does. The contract in §4.11 is what lets Parts 2–5 be written while Part 1 is.

**Part 1: the placeable runtime and the save** (Builder 1; first, but its §4.11 signatures are fixed now)

| File | New or changed | What |
|---|---|---|
| `Assets/Scripts/FurnitureDefinition.cs` | new | a kind (§4.1) |
| `Assets/Scripts/FurnitureCatalogue.cs` | new | the kinds in the strip's order; the ghost's material (an asset Furniture 2 makes, so nothing calls `Shader.Find` in a build) |
| `Assets/Scripts/PlacedPiece.cs` | new | a piece: pose, on/off, its seats, solids and stand points |
| `Assets/Scripts/CafeLayout.cs` | new | applies the save, snapshots it, buys, moves, sells, refreshes what remembers the room (§4.8) |
| `Assets/Scripts/FurnitureRules.cs` | new | the floor map and rules R1–R7 (§4.2), the words, the whole-layout check |
| `Assets/Scripts/DustSheet.cs` | new | a sheet (§4.7) |
| `Assets/Scripts/BanquetteRun.cs` | new | which bay end caps show |
| `Assets/Scripts/SaveData.cs` | changed | `CafeLayoutData`, `PlacedPieceData`, the `cafe` field, null guards in `ValidateAndMigrate` |
| `Assets/Scripts/SaveManager.cs` | changed | `CaptureState` writes `cafe` |
| `Assets/Scripts/GameBootstrap.cs` | changed | `if (CafeLayout.Instance != null) CafeLayout.Instance.Apply(data, SaveManager.Instance.HasSave);` before the clock is restored (not `?.`: a destroyed object is Unity's fake null) |
| `Assets/Scripts/PatronSpawner.cs` | changed | the settled guests' fallback by S's tags; no warning for a seat the layout switched off |

**Part 2: Arrange mode: input, camera, the screen** (Builder 2; in parallel)

| File | New or changed | What |
|---|---|---|
| `Assets/Scripts/ArrangeMode.cs` | new | the states (browsing, carrying, pointing), the cursor, the controls, entering and leaving, the time scale and camera handling |
| `Assets/Scripts/ArrangeGhost.cs` | new | the see-through copy of a piece, its footprint, its stand rings, green and red, the amber outline |
| `Assets/Scripts/ArrangeUI.cs` | new | the strip, the cards, the plates, the Now card, the till column, the controls strip, the hold ring: in `UiSkin`, on unscaled time (J's new `UiTween` runs on unscaled time: J-builder-report.md:15) |
| `Assets/Scripts/CafeViewMode.cs` | changed | `OverlayOwnsInput` (lines 236-238) is false while `ArrangeMode.Active`; nothing else |
| `Assets/Scripts/ControlHints.cs` | changed | four verbs: `Place` (South / Click), `Turn` (North / R), `SellHold` (West / Hold F), `Browse` (L1, R1 / Q, E), with their words. Additive |

**Part 3: the Shop's way in** (Builder 3; after J's `RecapPhone.cs` changes are merged)

| File | New or changed | What |
|---|---|---|
| `Assets/Scripts/RecapPhone.cs` | changed | `BuildShop` (lines 1489-1544): the YOUR CAFÉ card first, with the seats and the Arrange button; the Day 1 badge (line 935) |
| `Assets/Scripts/RecapUI.cs` | changed | hide the phone while Arrange is up and bring it back on the Shop app, without a second `Show()` (its camera and selection set-up, lines 109-144) |
| `Assets/Scripts/DayIntro.cs` | changed | Day 1's line (line 988). **P must agree** |

**Part 4: the conversion, the prefabs, the sheets, the bay's back** (Builder 4; in parallel; editor only)

| File | New or changed | What |
|---|---|---|
| `Assets/Editor/FurnitureSteps.cs` | new | Furniture 0–3 and their undo (§4.6) |
| `Assets/Data/Furniture/*.asset` | new | the seven kinds and the catalogue |
| `Assets/Prefabs/Furniture/*.prefab` | new | the prefabs (plants: four) |
| the dust sheet cloth | new art | Blender or a Unity quad (§4.7) |

No old step changes: they keep finding their pieces by name.

**Part 5: the labs** (Builder 5; in parallel against §4.11; run last)

| File | New or changed | What |
|---|---|---|
| `Assets/Editor/FurnitureRulesCheck.cs` | new | edit-mode rules, no scene (§5.1) |
| `Assets/Scripts/Diagnostics/FurnitureCheck.cs` | new | place, move, sell; paths; a placed seat used (§5.2) |
| `Assets/Scripts/Diagnostics/FurnitureSaveCheck.cs` | new | save and load, the old save, the new game (§5.3, §5.4) |
| `Assets/Scripts/Diagnostics/CafeLab.cs` | changed | a lab save for a new game (the half-finished café) |
| `Assets/Scripts/Diagnostics/DayShapeCheck.cs` | changed | Day 1–3 menus on the half-finished café (S owns its seat-variety part, P its day bars: coordinate) |

**The order that keeps builders apart:**
1. Part 1's §4.11 signatures are fixed by this spec, so Parts 2, 3, 4 and 5 can be written at the same time as Part 1.
2. Part 3 waits only for J's merge of `RecapPhone.cs`.
3. The integrator merges 1 → 4 → 2 → 3 → 5 and runs each part's `csparse` check.
4. On his Mac: S's Seat tags 1 → C's Art tidy 1 → Furniture 1 → 2 → 3 → save the scene → the labs (§5).
5. Files other builders also touch: `ControlHints.cs` (additive), `PatronSpawner.cs` and `DayShapeCheck.cs` (S, and P's builders), `RecapPhone.cs` (J). The integrator merges those by hand.

### 4.11 The contract between the parts

The public members Parts 2–5 may use. Builder 1 implements exactly these (bodies are Part 1's).

```csharp
// FurnitureDefinition.cs — a kind of furniture. The ASSET'S NAME is its save identity: never rename it.
[CreateAssetMenu(menuName = "Fixit Fidget/Furniture kind")]
public sealed class FurnitureDefinition : ScriptableObject
{
    public string displayName = "";       // "Bistro table"
    public GameObject prefab;             // a PlacedPiece at its root
    public GameObject[] variants = new GameObject[0];   // plants: one prefab per kind; empty otherwise
    public string[] variantNames = new string[0];       // "Monstera", "Rubber plant", "Fiddle-leaf fig", "Olive tree"
    public int price;                     // selling gives back price / 2
    public int seats;                     // shown on its card (Furniture 2 checks it against the prefab)
    public int forSaleFromDay = 1;        // on sale from this day's closing
    public bool againstAWall;             // the window counter, the banquette bay
    public string zone = "";              // its seats' zone for S's labs ("bistro")
}

// FurnitureCatalogue.cs
public sealed class FurnitureCatalogue : ScriptableObject
{
    public FurnitureDefinition[] kinds = new FurnitureDefinition[0];   // in the strip's order
    public Material ghostMaterial;                                      // see-through, made by Furniture 2
    public FurnitureDefinition Find(string kind);                       // by asset name; null if none
}

// PlacedPiece.cs
public sealed class PlacedPiece : MonoBehaviour
{
    public FurnitureDefinition Kind { get; }
    public string Id { get; }                       // "scene:<name>" or "p<n>"
    public bool FromScene { get; }                  // one of the café's own pieces
    public Vector3 Pivot { get; }                   // on the floor
    public float Yaw { get; }                       // degrees
    public int Tier { get; }
    public string Variant { get; }
    public bool On { get; }
    public IReadOnlyList<TableSeat> Seats { get; }
    public void Solids(List<Bounds> into);          // its colliders' world boxes
    public void StandPoints(List<Vector3> into);    // its seats' stand points
    public void SetPose(Vector3 pivot, float yaw);  // moves everything on it as one
    public void SetOn(bool on);                     // off: hidden, seats unregistered, obstacles off, its cups cleared first
}

// FurnitureRules.cs
public static class FurnitureRules
{
    public enum Verdict { Fits, OffTheFloor, QueueApron, AceWay, RepairBar, TooClose, Aisle, BlocksAChair,
                          NearWaiting, NearQueue, NearDoor, NoRoomToSit, NearSeat, BlocksAWay, AceCantPass, NotEnoughMoney }
    public struct Result { public Verdict verdict; public string words; public Bounds inTheWay; }   // words: the plate's line
    public sealed class FloorMap { }                            // built once per Arrange (physics, then plain arrays)
    public static FloorMap BuildMap();
    public static Result Check(FloorMap map, IReadOnlyList<PlacedPiece> layout, FurnitureDefinition kind,
                               Vector3 pivot, float yaw, PlacedPiece moving = null, int money = int.MaxValue);
    public static Result CheckWholeLayout(FloorMap map, IReadOnlyList<PlacedPiece> layout);       // both guards (steps, labs)
    public const float Grid = .25f;                             // the ghost's cell, metres

    // The data core, with no scene (the Edit-mode check, §5.1, uses these; the versions above read pieces into Shapes):
    public struct Shape { public string name; public List<Bounds> solids; public List<Vector3> stands; public bool againstAWall; }
    public static FloorMap MapFromData(IReadOnlyList<Bounds> fixedSolids, IReadOnlyList<Vector3> waitingSpots,
                                       IReadOnlyList<Vector3> queuePlaces, IReadOnlyList<Vector3> stationStands,
                                       Vector3 door, Vector3 aceStart);
    public static Result Check(FloorMap map, IReadOnlyList<Shape> layout, Shape candidate, int price = 0, int money = int.MaxValue);
    public static Result CheckWholeLayout(FloorMap map, IReadOnlyList<Shape> layout);
}

// CafeLayout.cs — one in the scene
public sealed class CafeLayout : MonoBehaviour
{
    public static CafeLayout Instance { get; }
    public FurnitureCatalogue Catalogue { get; }
    public IReadOnlyList<PlacedPiece> Pieces { get; }           // on and off; the café's own and bought
    public int Seats { get; }                                   // seats on pieces that are on, plus the repair bar's 3
    public void Apply(SaveData data, bool hasSave);             // GameBootstrap.Start
    public CafeLayoutData Snapshot();                           // SaveManager.CaptureState
    public bool ForSale(FurnitureDefinition kind);              // the day's closing and after
    public PlacedPiece Buy(FurnitureDefinition kind, Vector3 pivot, float yaw, string variant = "");  // rules + money; pays; saves; null if not
    public bool Move(PlacedPiece piece, Vector3 pivot, float yaw);     // rules; saves
    public bool Sell(PlacedPiece piece);                        // half back; saves
    public void AfterArranging();                               // NpcLifeProps.Refresh, CutawayWall.ForgetCandidates + PrepareCandidates, end caps
    public event System.Action Changed;                         // after every buy, move or sale
}

// ArrangeMode.cs — Part 2
public sealed class ArrangeMode : MonoBehaviour
{
    public static bool Active { get; }                          // CafeViewMode's input exception
    public static ArrangeMode Instance { get; }
    public bool CanOpen { get; }                                // the day is over and the night hasn't begun
    public bool TryOpen();                                      // from the phone's Shop
    public void Close();                                        // back to the phone
    public event System.Action Opened, Closed;
    public FurnitureDefinition Chosen { get; }                  // labs
    public FurnitureRules.Result GhostResult { get; }           // labs: green or red, and the words
}
```

---

## 5. The labs and checks that prove it

All new labs follow the PlayLab pattern (builders-common.md): a menu under `Fixit Fidget/...`, a lab save (never his playtest save), a report folder under `Logs/`, photos through the lab's Photo (which runs the word check, `ReviewScreen`), and the last console line `"[Tag] All N checks passed."` or `"[Tag] N of M checks FAILED. <folder>"`. Labs compare buttons through `ControlHints.Words`, never literal button words.

### 5.1 The rules, in Edit Mode (no scene)

`Fixit Fidget > Checks > Furniture rules` (`Assets/Editor/FurnitureRulesCheck.cs`), pure functions as S's seat-choice rules are. `[Furniture rules]`:
1. Today's café as data (Seats 1's plan to the millimetre, CafeSeatsSteps.cs:188-271, and the reading corner and lounge as Furniture 0 measured them): each of its 16 pieces fits against the rest.
2. The same café passes both guards: 35 of 35 places reached, 35 of 35 served.
3. A square table at (0.9, 9.5) in today's café: "Keep the way from the door clear" (mockup 2).
4. In the half-finished café, a bistro at (3.1, 9.75) fits (mockup 1), and one turned a quarter at (3.05, 11.55) fits (the model's first pick).
5. A bay touching a bay on the west wall fits; overlapping it by 1 cm doesn't.
6. The cram: today's café plus §3.2's five bistros fits (38 seats); no sixth bistro fits anywhere on the 25 cm grid, either way round.
7. A bistro whose stand point is 0.9 m from a waiting spot: "Too close to where people wait".
8. The communal table with $300 in the till: "You need $60 more".
9. Every verdict's words are at most seven words.

### 5.2 Place, move and sell (Play mode)

`Fixit Fidget > Furniture > Lab - Place, move and sell` (`FurnitureCheck.cs`), on a lab save of today's café at Day 2's closing with $1,000. `[Furniture]`:
1. The phone's Shop shows the YOUR CAFÉ card. Pressing its button (its own `onClick`, as a player) opens Arrange: `ArrangeMode.Active`, the camera at the Arrange view, the strip up, nobody left in the café.
2. Choose the bistro, move the cursor to (3.55, 0.80) (a spot by the front window the model found): green. Place: the till 1,000 → 920; seats 28 → 30; `WaitingArea.RegisteredSpots` and `FreeSeats` up by 2; a save on disk lists it.
3. Choose the square table at (0.9, 9.5): red, plate "Keep the way from the door clear" in the aisle; ✕ does nothing; the till unchanged.
4. Pick up the new bistro and put it at (−3.70, 1.05): green, no money moves, seats unchanged. Turn it: the ghost's verdict equals a direct `FurnitureRules.Check`.
5. Hold □ on it: nothing before 0.6 s; then sold, till +$40, seats 28.
6. Sell one of the café's own pieces (the north bistro): +$40, seats 26, its two seats out of the registry.
7. Leave Arrange: `NpcLifeProps` and the cut-away candidates refreshed (their counts reported).
8. Close up, skip the night, open tomorrow: from the `SpawnPoint`, a complete `NavMesh.CalculatePath` to every stand point, waiting spot and queue place, carving in place (`ValidateLayout`'s question, in Play mode).
9. With a bistro placed again, run the day on the lab's autopilot: a customer or a guest sits at a placed seat at least once.
10. Load the crammed layout (today's café + §3.2's five bistros): the worst of the next 30 frames is under 50 ms on his Mac (the carving cost; reported every run).

Photos: the green ghost, the red ghost and its plate, the placed table, the strip, the hold ring; their words measured.

### 5.3 Save and load a layout

`Fixit Fidget > Furniture > Lab - Save and load` (`FurnitureSaveCheck.cs`). `[Furniture save]`:
1. In today's café: buy a bistro, a square table and a monstera (each at the first green spot nearest the counter, as the model searches); move the south bistro to a green spot; turn the square table; sell the lounge.
2. Save, then reload the scene from the lab save: every piece's id, kind, pivot (within 1 mm), turn (within 0.1°), tier and variant as before; the lounge off; the money and seats as before; both guards pass; no piece twice.
3. Open tomorrow: the morning's save holds the same layout.

### 5.4 An old save, and a new game

Same lab file:
1. **An old save** (version 5 with no `cafe`, written by today's lab code): 28 seats; 35 of 35 places; all 16 of the café's own pieces on at their scene poses (1 mm); no sheets; nothing bought. Save: `format` 1 listing the 16. Reload: the same.
2. **A new game** (no save file): 3 seats; the olives and the fig; five sheets; all Day 1 no guest comes (the valve).
3. **A save with an unknown kind:** that piece is skipped with one warning; everything else loads.

### 5.5 Days 1–3 on the half-finished café

`Fixit Fidget > Days > Lab - Day 1/2/3 at the counter (half-finished café)` (DayShapeCheck, on a new-game lab save from CafeLab, and saves holding the first week's model layouts for Days 2 and 3):
1. **Day 1:** 3 seats; no guests; every customer served; customers with no seat counted (the model: 0 on his Day 1). At closing: the Shop badge and the YOUR CAFÉ card; the lab opens Arrange and places the bistro where the ghost first offers it: green, $80, 5 seats.
2. **Day 2:** 5 seats; guests come (the model: about 4); the window counter and the bay go on sale at its closing.
3. **Day 3:** 7 seats; the communal table and the lounge corner go on sale at its closing.
4. S's seat-variety checks are reported, not judged, below 10 seats in 4 zones (S's `RoomAtLeast` and `RoomZonesAtLeast` in their DayShapeCheck: they name this spec's half-finished Day 1).
5. P's new day bars apply as P sets them.

### 5.6 The conversion's own checks

Furniture 1–3's checks (§4.6). A pass: each step's report says DONE with every check PASS, and Furniture 1's report lists every object it moved.

---

## 6. Risks, and what to cut

### 6.1 The risks

1. **Scope.** The biggest feature since the scope freeze: 5 parts, 14 new code files, 11 changed, plus prefabs, assets and a new cloth. Playtest 8 waits for it, by his choice. The cut list below keeps a smaller v1 possible.
2. **Moving today's café under piece roots (Furniture 1).** Anything that finds furniture by its path would break. I found only lookups by name: S's tag rules by seat name (SeatTagsSteps.cs:95-106), the settled guests by seat name (NpcWave2Steps.cs:79-82, references at run time), laptops by name prefix (NpcLifeProps.cs:56-64). Names don't change. The survey lists every path it moves, and the undo puts them back.
3. **Carving.** Up to about 60 boxes in a crammed café, carved once at load and on each change at closing. The lab measures the frames after loading (§5.2, check 10). If it's too slow: carve tables only (chairs are small and stand between their table and the stand points), or merge each piece's boxes into one per table.
4. **Static batching.** A placeable part left static would stay drawn where the scene was built while its colliders move. Furniture 1 clears the flags and checks; the lab's photos would show it.
5. **Labs tied to seat names.** None at run time: the stations lab finds the repair bar by where it is (StationsCheck.cs:758). Old editor steps that name seats (CafeSeatsSteps, CafeLivedInSteps, CafeDressing3Steps, NpcWave2Steps, S's SeatTagsSteps) keep working because the names stay. Playtest 4's step checks for "26 seats" and "33/33" (Playtest4ProducerSteps.cs:282-289), which today's café already isn't: it only matters if that old step is run again.
6. **Save migration.** An old save and a new game both have `format` 0; `HasSave` tells them apart. A lab save written by an old lab gets today's café, which is what those labs expect. An unknown kind is skipped with a warning.
7. **An emptier Day 1.** No guests, and about $156 less than his Day 1 (§1.1), on the day most demo players will ever see (plan.md: the median demo player plays 14 minutes). The sheets and boxes must read as "opening week", not "empty". The first table at Day 1's closing gives the first "the café grew" moment within the demo's first day. **P must agree.**
8. **The world held still in Arrange.** Everything Arrange draws must run on unscaled time (J's `UiTween` does). The camera's exception must be only while `ArrangeMode.Active`, never during the recap.
9. **The strip covers the shop front.** At 26 m the café fills the screen and the strip sits over the front row. When the cursor nears the strip, the focus slides up to 1.5 m south, which lifts the front row clear of it; or the strip lowers while the cursor is in the bottom quarter.
10. **S's tags are still being built.** This spec uses S's names as they are in `/root/wt23/S` today (§9). A rename means a find-and-replace in Part 4's step.
11. **APIs I'm not sure of** (to check on his Mac): whether `NavMeshObstacle` carving updates at time scale 0 (harmless either way, §4.3); that a child's `NavMeshModifier.ignoreFromBuild` wins over a parent's Not Walkable modifier with "apply to children" (AI Navigation 2.0.13; Furniture 1 can also move the pieces out from under that parent, which it does anyway); that saving a prefab from scene objects that are FBX prefab instances keeps them as nested prefabs (`PrefabUtility.SaveAsPrefabAsset`); whether a runtime re-bake would need readable meshes (only for the option I rejected).

### 6.2 What to cut if it runs long (first to go first)

1. Plants (looks only).
2. The square table (the bistro does its job).
3. The lounge corner, then the banquette bay (the bay needs the back fix first).
4. The settled guests' fallback (they just stay away while their own seats are gone).
5. The sheets' fold animation (they just vanish) and any dimming of the street.
6. Mouse polish (clicking cards); pad and keyboard stay.

**Never cut:** the rules and both guards, the save with the old-save rule, closed-only Arrange, and the Day 1 teaching beat. **The smallest v1 that still makes seats count:** the bistro, the window counter and the communal table; the half-finished Day 1; Arrange with place, move and sell; the save.

---

## 7. Questions for him (each with my recommendation)

**Q1. Arrange any time, or only while the café is closed?**
*My answer: only while closed: at every closing, as often as you like, every move free.*
- The code: a customer keeps the seat they claimed and drains patience by it (CustomerBrain.cs:238, 733); cups are loose on tables (DirtyCup.cs:35-56); people walk the routes a moved table would cut.
- The game: the day's pressure is time; arranging mid-rush either costs customers or becomes a pause button.
- The house rule: buying is already closing-only (RecapPhone.cs:45-46; UpgradeShopUI.cs:59-65).
- The market: PlateUp!: "Upgrade and rearrange your restaurant to your personal taste: when the restaurant closes for the day, the planning begins!" ([MobyGames](https://www.mobygames.com/game/189063/); [Epic Games Store](https://store.epicgames.com/en-US/p/plateup-a99631)).
- Your "sense of their own freedom" is in *what* and *where*: nothing limits that. If you want more later, a morning slot before opening is the next step (the clock has no "before opening" state today, so it's new flow).

**Q2. Grid or free placement?**
*My answer: a 25 cm grid with quarter turns; wall pieces slide along their wall; ○ puts a piece back exactly where it was.*
- Your girlfriend plays on a pad and is dyslexic: a grid lets the stick land on a spot and stay there, and the same spot is always green or always red.
- Every rule is a distance (75 cm from a stand point, 1.05 m from a waiting spot): on a grid they behave the same every time.
- TCG Card Shop Simulator's guide recommends "using grid snapping for consistent spacing" for its play tables ([Play Table wiki](https://tcg-card-shop-sim.upcomer.com/wiki/Play_Table)); its players fix overlapping shelves by turning them ("If you rotate them they look fine", [Steam](https://steamcommunity.com/app/3070070/discussions/0/4849904427681508027)). PlateUp! is tiles throughout ("half-tile hitboxes", "within 2 tiles of a hob": [Two Average Gamers](https://www.twoaveragegamers.com/plateup-intermediate-guide/)).
- Free placement can come later as "hold L2 to place off the grid", if players ask.

**Q3. Can you sell back, and at what refund?**
*My answer: yes, always half the price, for every piece; moving and turning are free.*
- TCG Card Shop Simulator's play table costs $200 and sells for $100 ([Play Table wiki](https://tcg-card-shop-sim.upcomer.com/wiki/Play_Table)); its furniture has "sell-back values if the player wishes to rearrange or upgrade their setup" ([Furniture wiki](https://tcg-card-shop-sim.upcomer.com/wiki/Furniture)).
- Supermarket Simulator's players asked for exactly that: "it would be nice if we could sell it for at least half of what we paid for it" ([Steam](https://steamcommunity.com/app/2670630/discussions/0/4413046572166878131)); an early player stuck with a misplaced counter asked to "'delete' or get a refund" ([Steam](https://steamcommunity.com/app/2670630/discussions/0/4304949638719645140)).
- Not full: a full refund makes every choice free to undo, so nothing is a decision. Not less: a beginner's bad buy shouldn't hurt, and a misplaced piece costs nothing to move.
- Held, not pressed (0.6 s), so a sale is never an accident: Supermarket Simulator also sells on a long press ("long-press F to sell").

**Q4. Do Tier 2 upgrades come in v1?**
*My answer: no. v1 saves every piece's tier, so Tier 2 can come without touching saves.*
- The marble reads as flat cream and the brass as mustard (art-R2: "S2 the day Tier 2 is for sale"); the fix is a marble material and a paler brass.
- The communal table, the window counter and the banquette have no Tier 2 twin (§2.5): Blender work.
- v1's money is already spoken for in week one: six pieces and six upgrades (§3.3).

**Q5. Do customers react to décor?**
*My answer: no, as you chose: "seats count, décor is looks".*
- What draws people is what a seat *is* and where it stands (S's tags: the window, the soft chairs, the long table). That's the character you asked for, without décor scores.
- PlateUp! does make décor count (its themes change what customers do: "The Formal theme's Level 2 targets the 'Delivery phase'", [Two Average Gamers](https://www.twoaveragegamers.com/plateup-intermediate-guide/)). That is the spreadsheet your choice avoids.

---

## 8. Where this touches Designer P: P must agree

P's spec is being finished as I write; I designed to P's brief. Each of these is my proposal, for P to accept or change:

1. **The half-finished Day 1:** the repair bar's 3 stools and the 4 waiting spots; no café guests (the valve); about $198 for his Day 1's customers instead of $354 (§1.1).
2. **The first purchase at Day 1's closing:** the Shop badge, the YOUR CAFÉ card, the Now card's words ("Place your first table"; "Customers can sit there tomorrow.") (§1.2).
3. **Day 1's intro line** with the cart, "Years at a coffee cart bought this place. Now make it a café." (his sister rewrites it: DayIntro.cs:978).
4. **Something new each closing:** the pieces' sale days and the recap's line, "New in the Shop: the window counter and the banquette." (§2.2).
5. **Prices against the first week's earnings:** my work income is a placeholder; the first table against the first upgrade (§2.3, §3.3).
6. **Clearing tables:** placed seats are ordinary `TableSeat`s, so `DirtyCup` and `MessTable` work on them unchanged; picking up or selling a table clears its cups; whether cups left at closing stay overnight is P's. Note: a dirty seat isn't free (TableSeat.cs:112), so with P's rule uncleared cups also hold guests back.
7. **The settled guests at placed seats** from Day 2 (§1.4).
8. **Seats by Day 3:** with 7 seats a Day 3 of 10 leaves 0.2 customers standing, with 3 seats 1.7 (§3.1): P's customer counts decide what Day 3 needs.
9. **The Day 1–3 labs on the half-finished café** carry P's new bars (§5.5).

---

## 9. Builder S's seat tags: the contract

From S's work in progress (`/root/wt23/S/Assets/Scripts/SeatTags.cs`, `Assets/Editor/SeatTagsSteps.cs`):
- **The tags:** `SeatTag.Window`, `SeatTag.Lounge`, `SeatTag.Bar`, `SeatTag.Communal`, `SeatTag.Quiet`, `SeatTag.NearDoor` (a `[Flags]` enum, SeatTags.cs:57-74).
- **Where they come from:** a `SeatTags` on the seat or a parent ("a piece of furniture tags all its seats"), else the seat's own shape (a raised seat is Bar, a bench seat is Lounge: `DefaultFor`, SeatTags.cs:183-190), plus every room area its sit point is in (SeatTags.cs:161-166, 192-200). "So a table placed anywhere picks up where it stands" (SeatTags.cs:49-51).
- **The room's areas** (Seat tags 1, SeatTagsSteps.cs:125-138): by the west window, the east window, the front window either side of the door (Window); by the door (NearDoor); the quiet front-left corner (Quiet).

What the furniture does with them:
- **Every prefab seat carries a `SeatTags` on the seat itself**, copied from the café's tagged seats after Seat tags 1 (zones as S names them, SeatTagsSteps.cs:95-106): bistro none ("bistro"); square table none ("square table", new); window counter Bar ("window counter"); banquette bay: the cushion Lounge, the chair none ("west banquette" for the café's own, "banquette" for bought ones); communal table Communal ("communal table"); lounge corner Lounge ("lounge").
- **On the seat, not the piece**, because a parent's `SeatTags` tags every seat under it (`On`, SeatTags.cs:169-177): on a bay it would make the chair soft too.
- **The areas do the rest**: a window counter on the east wall is Bar + Window, a bistro by the door gets NearDoor, a lounge corner in the front-left corner is Lounge + Window + Quiet.
- **S's Day labs** already allow for the half-finished café: seat variety is reported, not judged, under 10 seats in 4 zones (`RoomAtLeast`, `RoomZonesAtLeast`, S's DayShapeCheck).
- **Asks of S:** keep `SeatTags` on a seat honoured before a parent's; keep `TableSeat.Tags` and `TableSeat.Zone`; run Seat tags 1 before Furniture 1. If a name changes before merging, Part 4's step changes with it.

---

## 10. Decisions his to overrule

Every choice I had to make, so he can say no to any one:
1. Arrange only at closing, from the phone's Shop (Q1).
2. The world holds still in Arrange (time scale 0, as the recap), and the last guests leave as it opens.
3. A 25 cm grid and quarter turns; wall pieces on the west and east walls only (Q2).
4. Sell back for half, always, held for 0.6 s; moving and turning free (Q3).
5. The v1 pieces, their prices and their sale days (§2).
6. The repair bar is fixed: not for sale, not movable.
7. The half-finished café keeps the olive trees by the door and the fig.
8. Day 1's intro line becomes the cart line (or the cart goes in Day 1's recap).
9. Dust sheets go when something is placed on them and never come back.
10. Today's guest valve stays as it is, so money flattens past about 21 seats (§3.4).
11. Old saves keep today's café; new games start half-finished.
12. Carving, not a runtime re-bake (§4.3).
13. Today's café's own pieces stay scene objects, moved under piece roots; bought pieces come from prefabs.
14. Tier 2 later (Q4); décor reacts to nothing (Q5).
15. Picking up or selling a table clears its cups.
16. The opening's settled guests take placed seats by kind when theirs are gone.
17. Plants are one card; △ picks the kind while it's in hand.
18. Mouse: the piece follows the pointer; hold F to sell (Supermarket Simulator's key).
19. The Arrange camera: the day's overhead, at 26 m.
20. Placing, moving and selling save at once, like the Shop's purchases.

---

## 11. Sources, numbers and notes

### 11.1 Market evidence (web, 23 Oct 2026)
- PlateUp!: "Players have free rein to design their restaurant which will expand and develop between shifts… Decide what new equipment to purchase, and place it wherever you want… when the restaurant closes for the day, the planning begins!" ([Epic Games Store](https://store.epicgames.com/en-US/p/plateup-a99631); [MobyGames](https://www.mobygames.com/game/189063/)). Mid-run rearranging is a later unlock ("Advanced Build Mode… pick up and reposition appliances… mid-run"), tables "combine… to form long rows", tiles throughout ([Two Average Gamers](https://www.twoaveragegamers.com/plateup-intermediate-guide/)); "squeeze in as many tables as you can manage… you can remove excess chairs to provide more space" ([TheGamer](https://www.thegamer.com/plate-up-how-to-reach-day-15-final/)). Its sales: plan.md's [Game Developer](https://www.gamedeveloper.com/press-release/plateup-reaches-1-5-million-steam-copies-sold).
- TCG Card Shop Simulator: the play table: $200, sells for $100, two chairs, "Customers need unobstructed access to their seats", grid snapping recommended ([Play Table wiki](https://tcg-card-shop-sim.upcomer.com/wiki/Play_Table)); furniture "can be strategically placed to optimize space and enhance customer flow" and has "sell-back values" ([Furniture wiki](https://tcg-card-shop-sim.upcomer.com/wiki/Furniture)); turning fixes snapping overlaps ([Steam](https://steamcommunity.com/app/3070070/discussions/0/4849904427681508027)).
- Supermarket Simulator: "Design your store, optimizing for efficiency and aesthetics… Expand your store's physical space, upgrade interiors" ([Steam store](https://store.steampowered.com/app/2670630/Supermarket_Simulator/)); "Box up old furniture and long-press F to sell", players asking for "at least half" ([Steam](https://steamcommunity.com/app/2670630/discussions/0/4413046572166878131)); an alpha player asking to "'delete' or get a refund" of a misplaced counter ([Steam](https://steamcommunity.com/app/2670630/discussions/0/4304949638719645140)).
- Not found: whether Supermarket Simulator or TCG Card Shop Simulator let you move furniture while customers are in the shop. I didn't claim either way.

### 11.2 The numbers
- Everything in §1.1, §2.3 and §3 comes from `/root/review23/mockups/src/arrange_numbers.py` (about 70 s; `quick` for a fast pass), which uses `arrange_cafe_model.py` (the floor, today's café, the guard replicas), `arrange_placement.py` (rules R1–R6 and the searches) and `arrange_seats_money.py` (a day of PatronSpawner's rules). Re-run on 23 Oct after the last change: every figure in this spec matches its output.
- The reading corner's and the lounge's places are estimated from the 20 Oct photos (they're placed by searches at run time). Everything else is Seats 1's plan to the millimetre. The game's own guards on his Mac are the truth; the model is the arithmetic beside them.
- **The brief's seat count is out of date.** It says 24 seats plus the lounge's 2. Today's café has **28**: the reading corner's 2 came on 13 Oct (Lived in 2), and builder C's 19 Oct report expects "28 active seats", "Cafe layout: 28 seats … PASS" and "Occupied circulation: 35/35 … PASS" (Docs/Studio/sessions/2026-10-19/reports/builder-C-cafe.md:47).

### 11.3 The mockups
- `/root/review23/mockups/arrange-1-first-table.png`: Day 1's closing in the half-finished café: the dust sheets and boxes, the repair bar, the first table's ghost in green with its two stand rings and its plate, the Now card, the till column ($200, "3 seats +2"), the strip (Bistro chosen, Day 2 and Day 3 locks), the controls.
- `/root/review23/mockups/arrange-2-does-not-fit.png`: today's café (an old save, 28 seats): a square table's ghost in red in the aisle, the aisle lit, "Keep the way from the door clear", Place greyed.
- `/root/review23/mockups/arrange-3-move-or-sell.png`: today's café, nothing in hand, pointing at the north bistro table: its amber outline, its plate ("Bistro table · 2 seats", □ "Hold to sell it back: +$40"), the controls strip with the hold ring part-filled round □, and no card chosen on the strip.
- Scripts: `/root/review23/mockups/src/arrange_mockups.py` and `arrange_mock_lib.py` (Pillow, drawn at twice the size and shrunk; the game's Baloo 2 font; the game's own button icons, `Tools/UI/draw_button_icons.py`, imported unchanged; `UiSkin`'s colours).
- They're drawn over the lab photo `CafeLife_days-1-3-day1-2026-10-20_031410__01-the-first-customer-at-the-till.jpg`, not the brief's `02-calm`: the same camera, but the first frame of the day has fewer people to paint out. It's a daytime frame although Arrange happens at closing: the closing frame (`…__06-closing-time.jpg`, 8 pm) is dark and the recap's phone covers the café. In the game Arrange has that evening light, the café lit warm and the street dark; the mockups dim the street to stand for it. The view is zoomed 1.5× to stand for the Arrange camera at 26 m. World positions are projected with the camera fitted to the photo (`arrange_camfit.py`: 3.6 px mean error on 14 known points), so a ghost stands where a real piece would.
- Mockup 1's ghost is at (3.1, 9.75), one green spot among many; the first-week model's first pick is (3.05, 11.55), turned a quarter. Both pass every rule and both guards.
- Checked with the Read tool after the last render. Two fixes came from it: a locked card's name in `INK_SOFT` was only 3.9:1 on the locked card's grey, now #4F4943 (5.9:1); and the "Day 2" pill's words in `UiSkin`'s `GOLD_INK` on `GOLD_SOFT` measure 4.1:1, under the 4.5:1 rule for 20 px words, now #8C4F0A (5.6:1). **Part 2 should use those two inks** (or the skin should darken `GOLD_INK`). Every other pair on the three pictures is 4.6:1 or better (white on the price pill 4.6:1, the green "+2" 9.1:1, the plate's second line 9.5:1, its first 15:1), except a greyed-out Place (3.8:1), which is meant to recede while the plate above says why.
