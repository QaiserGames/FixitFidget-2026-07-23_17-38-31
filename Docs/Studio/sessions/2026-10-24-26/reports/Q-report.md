# Builder Q report: the lab queue (26 Oct 2026)

## 1. What I did
- `Assets/Editor/LabQueue.cs` (new, 586 lines, about 45 of them the default list). Nothing else changed. `python3 /root/tools/csparse.py Assets/Editor/LabQueue.cs`: clean.
- It adds four menus under `Fixit Fidget/Lab queue/`: Run the queue (Logs/LabQueue/queue.txt), Write the full run into Logs/LabQueue/queue.txt, Stop after the current entry, Status (what's running).

## 2. Decisions and reasoning
- **Menu paths:** I checked all 33 distinct paths in the default list against the `[MenuItem]` strings in the code, with the prefix constants worked out (`Playtest4Shared.Menu` = "Fixit Fidget/Playtest 4/", `CafeLifeRecorder.LabMenu`, `Playtest4SystemsSteps.DaysLabMenu`, `NpcWave2Steps.MenuLabSession/MenuLab`, and so on). **None differ.** The written list matches the brief's 37 lines exactly (checked by script). The file is UTF-8 with no BOM, so the "é" is kept.
- **State lives in SessionState** (phase, index, the queue's lines, the summary path, the stop flag, the end line seen, the last line, the exception count and the first exception, one result row per entry). Times are `DateTime.UtcNow` ticks saved as strings: SessionState has no long, and a float loses seconds. The parsed lines are copied into SessionState when the run starts, so editing queue.txt during a run can't shift the entries.
- **`[InitializeOnLoad]` static constructor** unhooks and then re-hooks `EditorApplication.update` and `Application.logMessageReceived` after every reload. The listener writes the end line or ready line it sees straight into SessionState, in whichever domain is live. So a lab that ends Play Mode right after its last line doesn't lose it.
- **Only lines logged after an entry starts count.** The phase is set *before* `ExecuteMenuItem`, because edit labs log their end line inside that call. The queue's own "[Lab queue]" lines are ignored.
- **A session's ready line counts only while `EditorApplication.isPlaying`.** CafeLifeRecorder logs "[Café life] Lab session: …" in Edit Mode. The runtime CafeLab logs "[Café lab] Lab session: …" once Play has started, and the queue waits for that one.
- **The session's check menu is retried.** The queue waits 2 s after the ready line, then presses the check's menu once a second. If it is still disabled after 30 s, the entry is MISSING. Reason: the validators need Play Mode, and some may also need the lab director to be awake.
- **Between entries:** read `isPlaying` first. Call `ExitPlaymode()` only if still playing, and ask again after 30 s if it hasn't ended. Then wait for `!isPlayingOrWillChangePlaymode && !isCompiling && !isUpdating`, then 2 s.
- **Reimport:** presses the house's reimport menu. It waits for 10 s in a row with no compiling, updating or Play Mode change, then reads the first line of `Logs/Compile/last.txt` and parses `N error(s)`. If N > 0 (or `EditorUtility.scriptCompilationFailed`), the entry is FAIL and the queue stops after it. If last.txt is missing, the result falls back to `scriptCompilationFailed`. Timeout 900 s.
- **PASS/FAIL** uses the brief's rule exactly: the end line matches `All \d+ checks passed|PASS|passed, 0 failed|\bdone\b` (case-sensitive) and does not contain `FAIL`.
- **TIMEOUT** records the reason (no ready line, check never ran, or no end line) plus the last console line. **MISSING** is recorded when `ExecuteMenuItem` returns false. **Exceptions** are counted from `LogType.Exception` during an entry, and the first one's text is kept.
- **The summary** `Logs/LabQueue/run-<yyyy-MM-dd_HHmmss>.txt` is rewritten at the start and after every entry. Each line has the number, the menu's last part (for a session: the check's last part plus "(in <session's last part>)"), the result, the end line, the duration, and any exceptions. Totals are at the bottom, with how many entries didn't run and why the run stopped early. The last console line is `[Lab queue] Done: N entries, P passed, F failed, T timed out, M missing. <file>`, where N counts the entries that ran.

## 3. Decisions his to overrule
- **A bad queue file refuses to start.** An unknown kind, the wrong number of fields, a timeout that isn't a number, or a regex that doesn't parse stops the run before it begins, with every bad line and its line number listed. This is better than finding a typo three hours into a run.
- **"Write the full run" adds a 5-line `#` comment header** above the 37 entries. Comments are skipped, so the entries themselves are exact. If an existing queue.txt is different, it is first copied to `queue-<time>.bak.txt`. There's no dialog because the integrator presses this remotely.
- **At the end of the run (or a stop), the queue leaves Play Mode if still playing,** so the editor is left in Edit Mode.
- **N in the Done line counts the entries that ran**, not the entries in the file. The summary also says how many were not run.
- **The extra timings are mine:** 30 s for the check menu to become enabled, 2 s after the ready line, 30 s before asking to leave Play Mode again, and 900 s for the reimport.
- **The run stops when the editor restarts** (SessionState is cleared on quit). That seemed right: an editor that crashed shouldn't silently carry on.

## 4. Outside my files
Nothing needs changing.

## 5. How to prove it on his Mac (in order)
1. Let the scripts compile: Fixit Fidget > Checks > Pick up scripts changed from outside (reimport). `Logs/Compile/last.txt` should say 0 error(s).
2. Fixit Fidget > Lab queue > Write the full run into Logs/LabQueue/queue.txt. Expect `[Lab queue] Wrote the full run (37 entries) to …`.
3. A short smoke run first: edit queue.txt down to `reimport`, one `edit` line, one `play` line (e.g. Aim help) and one `session` line (e.g. the Sit 4 one). Press Run the queue. Expect `[Lab queue] Entry k: PASS …` lines and then the Done line. The summary file should have 4 rows.
4. Status while it runs should print the entry, the phase and the elapsed time. Press Stop after the current entry once, to see that it stops with "stopped by hand" in the summary.
5. Then write the full run again and press Run. About 3 hours. A pass is the Done line with 0 timed out and 0 missing, and failures only where the labs' own known misses are.

### Walkthrough: a `play` entry across its reloads
1. Edit domain: Settle sees Edit Mode with nothing compiling for 2 s, and sets phase Begin. Begin clears EndLine and Exceptions, marks entryStart (ticks in SessionState), sets phase WaitEnd, and calls `ExecuteMenuItem(lab)`. The lab calls `EnterPlaymode`.
2. The domain reloads. The statics are gone, but SessionState keeps phase=WaitEnd, the index, entryStart and the lines. The `[InitializeOnLoad]` constructor runs in the Play domain and re-hooks update and the log listener before the scene's scripts start.
3. The lab logs `[Aim help check] All 9 checks passed.` OnLog (Play domain) matches the entry's end regex and writes EndLine to SessionState.
4. Either the next update in the Play domain sees EndLine, or the lab ends Play Mode first. In that case the reload happens, the constructor runs in the Edit domain, and update sees EndLine there. Either way Record works out PASS/FAIL, writes the result row and the summary, moves the index on, and sets phase Settle.
5. Settle: if Unity is still playing, it calls ExitPlaymode once. There is one more reload, and the state survives as before. Then Edit Mode with nothing compiling for 2 s, and on to the next entry.

### Walkthrough: a `session` entry
1. Edit domain: Begin sets phase WaitReady and presses the session menu. CafeLifeRecorder writes the lab save, logs "[Café life] Lab session…" (ignored: not playing, and it wouldn't match anyway), and calls EnterPlaymode.
2. The domain reloads. The constructor re-hooks the listener, and phase=WaitReady survives.
3. Play domain: CafeLab logs "[Café lab] Lab session: …". OnLog sees isPlaying and the ready regex match, marks readyAt and sets phase PressCheck.
4. Update, 2 s later: it sets phase WaitEnd and presses the check menu. If the menu is disabled, it goes back to PressCheck and tries again each second, up to 30 s, then records MISSING.
5. The check logs `[Sit check] PASS …`. OnLog stores EndLine. Update records the result, and Settle exits Play Mode (still playing) across one more reload.
6. All the way through, the timeout is measured from entryStart in SessionState, so it spans the reloads.

## 6. Risks and APIs I'm unsure of
- `EditorApplication.ExecuteMenuItem`: it returns false for a path that doesn't exist. I believe it also honours the item's validator and returns false when the item is disabled, but if a Unity version runs a disabled item anyway, the session check's retry still works because the queue only presses it after Play has started.
- `SessionState` (GetInt/SetInt/GetString/SetString/GetBool/SetBool/EraseString), `EditorApplication.ExitPlaymode`, `isPlaying`, `isPlayingOrWillChangePlaymode`, `isCompiling`, `isUpdating`, `EditorUtility.scriptCompilationFailed`: these are all long-standing editor APIs and I expect them to compile in 6000.5. I did not use `playModeStateChanged`: polling isPlaying in update is enough, and it survives reloads with no handler state.
- If his project turns off domain reload in Enter Play Mode Options, it still works: the statics are never trusted, and everything is read from SessionState.
- The last entry's end regex `\[Review screens\]` matches the gather's *first* line with that tag. By the PASS rule that's FAIL unless the line contains PASS/done/"All N checks passed". The integrator should read that row as "it ran" and judge the gather's report itself. (The regex is the brief's; I didn't change it.)
- The `Day shape check` regex and others with `.*checks (passed|FAILED)` could match a progress line mid-lab if a lab ever logs "… checks passed" before its final line. The Stations check runs twice in a row and shares a tag, but the queue only counts lines logged after its own start, so the two runs can't read each other's lines.
- The file is 586 lines, a bit over the brief's 300–450, mostly because of the 45-line default list and the comments.
