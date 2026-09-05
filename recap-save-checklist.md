# Recap save checkpoint — focused test

Branch: `codex/recap-save-checkpoint`, based on tested `codex/day1-onboarding` commit `69bd134`.
Do not merge to `main` until these checks pass. This is a save-lifecycle change, not mid-day autosaving.

## Before testing

1. Stop Play Mode and save any scene edits. Back up the current `save.json` outside its save folder. The SaveManager component's **Print Save Path** context-menu command shows its location in the Console. Do not delete the current save or your day logs.
2. Fetch and switch to this branch in GitHub Desktop. Let Unity import and compile; send the first red Console error if compilation fails. No new Inspector wiring is required.
3. Outside Play Mode, run **Fixit Fidget > Checks > Recap save checkpoint**. Expect `[Recap save] PASS`. These checks use a unique temporary directory, never the real save or scene. The temporary test files are removed afterward.
4. Existing v1/v2 saves still begin on their stored morning. This change cannot recover a completed day the old format never saved; finish one day to create the first recap checkpoint.

## One completed day is enough

- [ ] At the completed-day recap, note the day, recap figures, current money, cups, beans, and owned upgrades. Keep a copy of that day's customer CSV and summary.
- [ ] Stop Play Mode **without Continue**, then Play again. The same recap opens, the shop remains paused/closed, and all those values match. Earnings and regular-customer visits are not applied twice. F/E must not enter or use a station behind the recap.
- [ ] Confirm the copied day logs and originals still match; reopening a recap must not regenerate them from an empty scene. The cafe's transient people/cups/positions are not restored; this is a recap checkpoint, not a whole-scene snapshot.
- [ ] Buy one affordable restock at the recap. Note the new money and stock; stop/restart before Continue. Both the deduction and added stock remain exactly once.
- [ ] Buy one affordable upgrade. Note its new owned level and money; stop/restart. Both persist exactly once. If nothing is affordable, do not edit your real save just to force this test; report which purchase check remains pending.
- [ ] **Closing till** remains the balance at day close, even after shopping. The shop's money label shows the current spendable balance. Recap earnings and grades do not change when you buy.
- [ ] Click Continue. Only the next day starts, with purchases intact. Try a quick double-click: it must not skip another day. Stop/restart during that new day: it restarts that day's morning checkpoint, not yesterday and not the day after.
- [ ] No red Console errors, stuck recap, duplicate rewards, or changed hint/customer behavior. Keep a screenshot of the reopened recap and the next morning's HUD.

## Save-failure behavior

A failed write keeps the previous checkpoint and shows a `SAVE FAILED` message on the recap. Continue remains at the recap until its checkpoint can be written. A purchase made while disk writing fails is only in the current session until a later save succeeds; do not quit while this warning is present.

Writes use a sibling temporary file, then replace the primary file while retaining its previous version as `save.json.bak`. An unreadable or newer-format existing save is not overwritten; the Console explains the problem. Backup recovery is manual in this pass, not automatic.

The automated storage check simulates a failed write in its isolated folder. It does not test the on-screen warning or disabled advancement in a running scene. Do not change permissions or fill your disk to test this on your real progress.

## Compatibility and verification boundary

- New schema: v3. Old saves load forward; older code cannot understand the new completed-recap state. Restore your pre-test backup before switching back to an older code branch and playing.
- Completed recaps save before UI/log listeners. Loading restores data and opens the UI without firing `OnDayEnded`. Continue saves tomorrow before opening it. Purchase checkpoints include both their cost and result.
- Bootstrap runs its `Start` before recap UI/spawner startup; other scene objects initialize in `Awake`. See Unity's [execution-order attribute](https://docs.unity3d.com/6000.5/Documentation/ScriptReference/DefaultExecutionOrder.html). Storage retains the prior checkpoint using [.NET File.Replace](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.replace).
- Source review and static checks outside Unity do not establish compilation or successful Play Mode behavior. Run the menu checks and the playtest above in the full project before merging.

Deferred: espresso/free-choice brewing, heat/waste, balancing, seating animation, character art, and recap customer-count relabeling.
