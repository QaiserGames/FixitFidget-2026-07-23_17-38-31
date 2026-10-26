# Builder Y report: Days and first table (26 Oct 2026)

Branch `work-Y`, commit `d298316`; all 15 files pass `csparse`.

## 1. Files
- `FurnitureRules`: one price function (`PriceOn`, `PriceToday`, `FreeToday`, `PriceWords`), used by the placing check.
- `CafeLayout`: charges today's price, remembers free pieces, `SellBackOf`, `TableBought`.
- `SaveData`: additive `PlacedPieceData.free`, with a guard in `ValidateAndMigrate`.
- `ArrangeMode`, `ArrangeUI`: the prices, the free card, the words.
- `Downtime`, `ThriftShelf` (`Begun`): the Now line's order.
- `CafeLab`: the find is worked in the close-up, then `TheLabsHand` does its steps.
- `CustomerSpawner`: `drinkServiceSeconds`, `shortestWalkSeconds`.
- The Days and furniture labs and checks.

## 2. Causes and fixes
**1. Free table.** On Day 1, a kind with seats on sale that day costs $0 until one bought table exists. Card and plate: "$80" struck, then "Free today". A free piece sells back for $0, saved.

**2. The find.**
- A find was on the shelf: lab saves have no thrift record, so the day's authored find goes out ("nothing to do after 15 s: 0" on all six runs is impossible without one).
- **Cause (a): time.** Every table came before the find; tables and chats took 60–66 of the 60–105 s nobody needed Ace, leaving 0–45 s for a 35–70 s find.
- **Cause (b): the hand.** Day 2's screen is under a back cover with two screws (`PhoneRepair.prefab`). `ReplaceablePart.Activate` does nothing while covered.
- **Fix.** A find that's begun now comes right after a refill. The autopilot opens the find the player's way, works in the close-up, then runs StationsCheck's steps: screws and cover only while a part is covered, each piece landing before the next. It stops for a customer. No bench files edited.
- **For the player:** a started find stays on the Now line until done.

**3. 7:42.** Every late run ended on a drink walk-in (Walter, Mansoor, Yuki). Each set off at 357–367 s, before drinks' last orders (the 45 s walk alone), then took 54–57 s from the till to the door. The game's rule, not the lab's hand. Fix: drinks' last orders = the walk + 25 s; repairs unchanged.

**4. 1:29.** With a spawn jitter of 0, the first walk-in sets off at 60.6 s, and a 29.3 s walk reaches the till at 89.9 s. Fix: the gap after the opener grows by the planned walk minus the shortest walk (22 s, or shorter once measured). Now ≥96.5 s.

**5. ArrangeUI.**
- Cards not on sale aren't faded as a whole (that caused 2.5–2.7:1); only the background and the picture are muted.
- Seats are bold 22, the Day pill bold 24, the prices bold 26.
- "+2" sits on its own dark plate, over 7:1.
- Names stay at the 24 px floor; "Square" is a false button-word match.

## 3. Decisions he may overrule
- The free-table rule and its words. Selling the free table gives $0, and placing it again is free (like a move).
- The Now line order: refill, a begun find, tables, gossip, a find not begun.
- The 25 s drink time. The first-walk-in cushion applies on every day that has an opener.
- The Day 2 and 3 half-finished lab saves treat the Day 1 bistro as free (Day 2 starts with $369).

## 4. Others' files
Optional, for R: in `RecapPhone.cs:2373`, change "Place your first table" to "Place your first table: free today". The lab still passes.

## 5. Mac steps, in order
1. Checks > Furniture rules: item 11 passes ($70: free fits; $80 says "You need $10 more").
2. Checks > Downtime rules.
3. Furniture > Place, move and sell; then Save and load (3b: the free piece sells for $0, keeps its mark after a reload).
4. Days > Day 1 (half-finished): placed for $0, 10→12 seats; the next table full price; sold for $0; placed again free.
5. Day 2 ×3, then Day 3 ×3 (human speed):
   - bench jobs median ≥3;
   - the notes say "finished the thrift find … in the close-up";
   - Day 2's first walk-in ≥1:30, and its end median ≤7:30.
6. The four waiting-jobs labs: unchanged, all pass.

## 6. Risks
- The close-up now opens in the Day labs: photos may show the bench. `TheLabsHand` is untested.
- Fewer late arrivals: watch dead air after last orders and at Day 3's open.
- The rebuy check calls `CafeLayout.Buy` while Arrange is open.
