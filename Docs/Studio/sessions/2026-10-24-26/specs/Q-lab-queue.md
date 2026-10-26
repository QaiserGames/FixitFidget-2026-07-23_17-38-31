# Builder Q: a lab queue, so his Mac runs the labs one after another by itself (26 Oct 2026)

You are the Technical Director, building a small editor tool. Read `/root/review24/builders-common.md` first (the rules, especially Labs and Unity 6.5). Your copy is `/root/wt24/Q`. Your report is `/root/review24/reports/Q-report.md`.

**Be economical.** He asked us to preserve usage. This is one file of about 300–450 lines: read the few files you need, then write it.

## Why
Today the integrator starts each lab by hand from the cloud, through a menu on his Mac, then polls its log every 40 s. A full run is about 40 labs and 3 hours, and each poll costs usage. The queue starts the labs one after another inside Unity and writes one summary. The integrator presses one menu and comes back when it's done.

## What to build: `Assets/Editor/LabQueue.cs` (new; nothing else changes)
**Menus**, under `Fixit Fidget/Lab queue/`:
- `Run the queue (Logs/LabQueue/queue.txt)`
- `Write the full run into Logs/LabQueue/queue.txt` (the default list below)
- `Stop after the current entry`
- `Status (what's running)`

**The queue file:** one entry per line. `#` starts a comment, and blank lines are skipped. Fields are separated by ` | `:
```
reimport
edit    | <menu path> | <end regex> | <timeout s>
play    | <menu path> | <end regex> | <timeout s>
session | <session menu path> | <ready regex> | <check menu path> | <end regex> | <timeout s>
```
- `reimport` runs `Fixit Fidget/Checks/Pick up scripts changed from outside (reimport)`, then waits until the editor isn't compiling or updating for 10 s in a row. It records the newest compile result from `Logs/Compile/last.txt`'s first line (see `Assets/Editor/CompileLog.cs`), and stops the queue if it reports errors.
- `edit` runs the menu in Edit Mode and waits for the end regex in the console.
- `play` runs a lab menu (the lab enters Play Mode itself) and waits for the end regex.
- `session` starts a lab session, waits for its ready line, runs the check's menu (it needs Play Mode), and waits for the end regex.

**Between entries:**
- If Unity is still playing, call `EditorApplication.ExitPlaymode()`. Some labs end Play Mode themselves (the Day 1 guide, Hands on it), so read `EditorApplication.isPlaying` first, never toggle blind.
- Wait for Edit Mode and for no compiling, then wait 2 s.

**A timeout:**
- Record TIMEOUT with the last console line seen.
- Leave Play Mode and go on to the next entry.

**Also record and go on when:**
- a menu item doesn't exist or is disabled (`EditorApplication.ExecuteMenuItem` returns false): record MISSING;
- an exception reaches the console during an entry: count it and keep the first one's text.

**Survive domain reloads.** Entering Play Mode reloads the scripts.
- Keep the queue's state in `SessionState`: the file path, the index, each entry's start time and result.
- An `[InitializeOnLoad]` static class resumes the driver on `EditorApplication.update`, and re-subscribes to `Application.logMessageReceived` after every reload.
- An entry's end line is logged in the domain that is live at that moment, so the listener must be live in both Edit and Play Mode.

**The summary:** `Logs/LabQueue/run-<yyyy-MM-dd_HHmmss>.txt`.
- One line per entry: the number, the menu's last part, PASS, FAIL, TIMEOUT or MISSING, the end line itself, and the duration.
- The totals at the bottom.
- Write it after every entry, so a stopped run still has its results.
- The last console line is `[Lab queue] Done: N entries, P passed, F failed, T timed out, M missing. <file>`.
- An entry counts as PASS when its end line matches `All \d+ checks passed|PASS|passed, 0 failed|\bdone\b` and contains no `FAIL`. Otherwise it is FAIL.

**Safety.** The queue never starts plain Play and never touches a save. It only presses the house's own menus; the labs already keep his save safe.

## The default list
`Write the full run…` writes these lines exactly, with these timeouts. Check every menu path against the `[MenuItem(...)]` strings in your copy (grep them): if one differs, use the code's, and list the difference in your report.
```
reimport
edit | Fixit Fidget/Checks/Night notebook rules | \[Notebook rules\] (PASS|FAIL) | 120
edit | Fixit Fidget/Checks/Seat choice rules | \[Seat choice rules\] (All \d+ checks passed|.*FAILED) | 120
edit | Fixit Fidget/Checks/Waiting space reservations | \[Waiting space\] The café scene: \d+ passed, \d+ failed | 120
edit | Fixit Fidget/Playtest 4/UI/2 - Settings: the bench's sticks and the morning (edit mode, instant) | \[Playtest 4\] UI - settings: (PASS|FAIL) | 60
edit | Fixit Fidget/Checks/Probe - the bench's hand-over reach (edit mode, read-only) | \[Bench hand-over probe\] done | 120
play | Fixit Fidget/Playtest/Aim help - play check (lab, drives itself) | \[Aim help check\] .*checks (passed|FAILED) | 600
play | Fixit Fidget/Playtest/Stations as reach - play check, first person (lab, drives itself) | \[Stations check\] .*checks (passed|FAILED) | 600
play | Fixit Fidget/Playtest/Stations as reach - play check, overhead (lab, drives itself) | \[Stations check\] .*checks (passed|FAILED) | 600
play | Fixit Fidget/Playtest 4/UI/1 - Day 1 guide - play check (lab, drives itself) | \[Day 1 guide check\] .*checks (passed|FAILED) | 900
play | Fixit Fidget/Playtest/Juice - Photograph the feedback (lab, Play Mode) | \[Juice check\] .*checks (passed|FAILED) | 600
play | Fixit Fidget/Playtest 4/Bench/2 - The dispenser's frame - play check (lab, Play Mode) | \[Dispenser frame\] .*checks (passed|FAILED) | 300
play | Fixit Fidget/Bench/Bench 3 - The bench - play check (lab, drives itself) | \[Bench check\] .*checks (passed|FAILED) | 900
play | Fixit Fidget/Bench/Bench 5 - The close-up's view (lab, a check) | \[Bench view check\] .*checks (passed|FAILED) | 600
play | Fixit Fidget/Bench/Bench 6 - Words on the bench screen (lab, a check) | \[Bench words lab\] .*checks (passed|FAILED) | 600
play | Fixit Fidget/Bench/Bench 8 - The clumsy hand (lab, drives itself) | \[Clumsy hand\] .*checks (passed|FAILED) | 1800
play | Fixit Fidget/Recap phone/Play check (lab, drives itself) | \[Recap phone check\] .*checks (passed|FAILED) | 600
play | Fixit Fidget/Playtest/The phone by day - pause and HUD check (lab, drives itself) | \[Pause check\] .*checks (passed|FAILED) | 600
session | Fixit Fidget/Café life/Lab/Play a lab session - Day 3, autopilot serves | \[Café lab\] Lab session | Fixit Fidget/Checks/Night notebook (Play Mode, lab session) | \[Notebook page\] .*checks (passed|FAILED) | 600
play | Fixit Fidget/Night/Night 0 - Play check (lab, drives itself) | \[Night 0 check\] .*checks (passed|FAILED) | 900
play | Fixit Fidget/Night/Night 1 - Play check, keeping a straight face (lab, drives itself) | \[Night 1 check\] .*checks (passed|FAILED) | 900
play | Fixit Fidget/Days/Lab - Day 1 at the counter (the autopilot at human speed) | \[Day shape check\] .*checks (passed|FAILED) | 900
play | Fixit Fidget/Days/Lab - Day 1 at the counter (the autopilot at human speed) | \[Day shape check\] .*checks (passed|FAILED) | 900
play | Fixit Fidget/Days/Lab - Day 1 at the counter (the autopilot at human speed) | \[Day shape check\] .*checks (passed|FAILED) | 900
play | Fixit Fidget/Days/Lab - Day 2 at the counter (the autopilot at human speed) | \[Day shape check\] .*checks (passed|FAILED) | 900
play | Fixit Fidget/Days/Lab - Day 2 at the counter (the autopilot at human speed) | \[Day shape check\] .*checks (passed|FAILED) | 900
play | Fixit Fidget/Days/Lab - Day 2 at the counter (the autopilot at human speed) | \[Day shape check\] .*checks (passed|FAILED) | 900
play | Fixit Fidget/Days/Lab - Day 3 at the counter (the autopilot at human speed) | \[Day shape check\] .*checks (passed|FAILED) | 900
play | Fixit Fidget/Days/Lab - Day 3 at the counter (the autopilot at human speed) | \[Day shape check\] .*checks (passed|FAILED) | 900
play | Fixit Fidget/Days/Lab - Day 3 at the counter (the autopilot at human speed) | \[Day shape check\] .*checks (passed|FAILED) | 900
session | Fixit Fidget/NPC/Life 4 0 - Play its lab session (Day 5, I serve) | \[Café lab\] Lab session | Fixit Fidget/NPC/Life 4 - Stools, settled guests, looks (Play Mode, Life 4's lab session) | Life 4 - Stools, settled guests, looks: (PASS|FAIL) | 900
session | Fixit Fidget/Café life/Lab/Play a lab session - Day 5, I serve | \[Café lab\] Lab session | Fixit Fidget/NPC/Sit 4 - Live sit check (Play Mode) | \[Sit check\] (PASS|FAIL) | 600
session | Fixit Fidget/Café life/Lab/Play a lab session - Day 5, I serve | \[Café lab\] Lab session | Fixit Fidget/NPC/Café life check (Play Mode) | \[Café life check\] (PASS|FAIL) | 600
session | Fixit Fidget/Café life/Lab/Play a lab session - Day 5, I serve | \[Café lab\] Lab session | Fixit Fidget/Checks/Controller (Play Mode)/Run controller check | \[Controller check\] (PASS|FAIL) | 600
play | Fixit Fidget/Checks/Hands on it - play check (lab, drives itself) | \[Hands on check\] .*checks (passed|FAILED) | 600
play | Fixit Fidget/Dialogue/Conversation - play check (lab, drives itself) | \[Conversation check\] .*checks (passed|FAILED) | 600
edit | Fixit Fidget/Review/Screens 1 - Gather the newest run of every lab's word checks (edit mode, read-only) | \[Review screens\] | 300
```
Notes:
- The Café life lab menu has an "é". Keep the file UTF-8.
- A session's ready line is logged once Play Mode has started. The check's menu is only enabled in Play Mode (its validator).

## Check it
- Unity isn't here, so make sure it compiles in Unity 6000.5.2f1 with `python3 /root/tools/csparse.py`.
- List any API you're unsure of: `SessionState`, `EditorApplication.ExecuteMenuItem`, `ExitPlaymode`, `isCompiling`, `isUpdating`, `playModeStateChanged`.
- Walk through one `play` entry and one `session` entry across their domain reloads in your report, step by step, to show the state survives.
