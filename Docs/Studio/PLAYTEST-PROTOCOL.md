# How an agent playtests Fix It Fiasco

*Written 10 October 2026, when Mansoor asked for "agents whose soul jobs are to playtest and see if it feels and reads as a solid game". Both playtesters (`playtester-first-hour.md`, `playtester-veteran.md`) read this file after the Bible and before their role file's mission. It says what an agent can honestly play, how, what it must capture, and how it reports.*

---

## 1. What an agent can and cannot judge

**Mansoor's hands are the ground truth.** The Bible says it plainly (§4): agents cannot hold the controller, judge input latency, hear the mix, or feel the "wow". A playtester agent never pretends to. Every finding names its **evidence**:

| Tag | Evidence | Good for | Weak for |
|---|---|---|---|
| `[seen]` | A frame you looked at: a Game-view recording, a lab's photo, a screenshot of the running game | Readability, layout, what draws the eye, UI legibility, staging, whether a thing looks like what it is | Motion feel, timing under 1 s |
| `[measured]` | A number from a log: the day CSV, a trace, a lab report | Pacing, wait times, storm-outs, repair times, money, idle time, distances walked | Why it feels that way |
| `[read]` | Text or data you read: a line of dialogue, a tutorial prompt, a price, a rule in code | Clarity of words, onboarding order, whether the rules are fair and knowable | How it plays |
| `[inferred]` | Your reasoning from the above | Hypotheses | Anything presented as fact |
| `[needs hands]` | Something only a human at the controls can settle | Input feel, camera comfort, audio, real-time juice | — |

A finding with no tag is not a finding. `[inferred]` and `[needs hands]` items go to the end of the report, never the top.

## 2. The harness: how an agent plays

All on his Mac, in Unity, scene `Assets/Playtests/AcesCafeLayoutPlaytest.unity`. **Only one agent drives Unity at a time** (Bible §3.9). In a claude.ai session the session that holds Unity runs the menus for you or hands you the controls; in Claude Code on his machine you read what a session or he produced (`Logs/`, `DayLogs/`) and ask for the runs you need in your output.

**Never** run a `Cafe redesign`, `Night … (scene)`, `Bench 2…` or any other step that changes the scene; never save; never commit; never touch his save (`Café playtest save`). Stop Play mode after every run (`Edit › Play Mode › Play`). If something looks broken, report it; do not fix it.

### 2.1 The day, watched (the main run)

1. `Fixit Fidget › Café life › Lab › Play a lab session - Day 3, autopilot serves` (or `Day 5`). A separate lab save; Ace serves on autopilot. Day logs go to `DayLogs/CafeLab/`.
2. Right away: `Fixit Fidget › Café life › Record 8 minutes (Game view)` (or a fixed camera: `… - whole café camera`, `… - counter camera`, `… - queue camera`). Writes `Logs/CafeLife/<time>/video.mp4` (960×540, 12 fps, a REC clock), `trace.csv` (everyone, 20 times a second) and `notes.txt`.
3. Stress, when the pillar needs it: `Café life › Lab › Send 3 customers in at once`, `Send 6 patrons in at once`, `Block the door for 15 seconds`, `Fill every free seat with patrons`.
4. Stop the recording (`Café life › Stop recording`), then stop Play mode.
5. Watch it: extract frames (`ffmpeg -i video.mp4 -vf fps=1/2 f_%04d.png`, a frame every 2 s; every frame around anything interesting) and look at them in order, as a flipbook. Read `trace.csv` and the day's CSV/summary for the numbers.

### 2.2 Day 1 as a newcomer sees it

The autopilot exists only inside a lab session (`CafeLabDirector`), and lab sessions start on Day 3 or Day 5, so **nobody plays Day 1 for you**. What an agent can do:
1. `Fixit Fidget › Playtest › Play the whole game from Day 1 (test save)` (never his own save), then `Café life › Record 3 minutes (Game view)` straight away, and touch nothing: the opening as a newcomer meets it (the first screen, the HUD, the first prompt, the first customer walking in and waiting). Screenshots of the running Game view (the session's app screenshot) for what the recording can't hold still: a dialogue box mid-reveal, a prompt.
2. Read the onboarding in code and data, in the order the player meets it (`Checks › Day 1 onboarding` names the rules it tests; find the prompts it gates): what is said, when, in how many words, and what the player must already know to follow it.
3. Say plainly in the report that Day 1's hands-on flow is `[needs hands]`, and list the exact moments for Mansoor to watch tonight.

### 2.3 The self-driving play checks (each plays one part of the game for real)

| Part | Menu (Fixit Fidget ›) | Report and photos |
|---|---|---|
| The bench: a full repair | `Bench › Bench 3 - The bench - play check (lab, drives itself)` | `Logs/Bench/bench-check-<time>/` |
| Stations as reach | `Playtest › Stations as reach - play check, first person` and `…, overhead` | `Logs/Stations/` |
| Aim help (pad) | `Playtest › Aim help - play check (lab, drives itself)` | `Logs/AimHelp/` |
| Feedback ("juice") | `Playtest › Juice - Photograph the feedback (lab, Play Mode)` | `Logs/Juice/` |
| The phone by day, pause, HUD | `Playtest › The phone by day - pause and HUD check (lab, drives itself)` | its folder in `Logs/` |
| Night 0, the deal | `Night › Night 0 - Play check (lab, drives itself)` | `Logs/Night/` |
| Night 1, Barnaby, the straight face | `Night › Night 1 - Play check, keeping a straight face` and `…, cracking` | `Logs/Night/` |
| Night 2, stalling | `Night › Night 2 - Play check` and the two stalling checks | `Logs/Night/` |
| Grace at home, cover | `Night › Break-ins 7 - …` and `Break-ins 8 - Grace's cover (lab, a check)` | `Logs/Night/` |

A play check's PASS says the machinery works. **It does not say it reads or feels right**; that is your job. Look at its photos as a player would.

### 2.4 The words, read cold

Every line the player reads in a day and a night: `DialogueSet.cs`, `Barks.cs`, `BarkRules.cs`, `NightLines.cs`, `ReviewLines.cs`, the onboarding (`Day 1 onboarding` code and its prompts), the HUD strings, the recap phone, the menu board (`Assets/Art/Textures/Cafe/menu-board.json`), the signs (`Tools/Blender/draw_cafe_textures.py`). Count words per screen; read each as a stranger would.

## 3. Reads first (both playtesters)

1. `Docs/Studio/BIBLE.md` §1 (the game and its three pillars), §2 (the locks: never report a locked decision as a bug; Grace's Day 1 latte suppression is intentional), §4 (his past verdicts).
2. His playtest notes, newest first (`claude/playtest-4-notes.md`, then 3 and 2): what he already said, in his words. Never re-report a note he made unless it's still true; then say "still true".
3. The repo handoff's newest entry (`Docs/ProjectDirection/Current-Handoff-2026-09-12.md`, newest at the top) and `git log --oneline -20`: what changed since he last played. Play the changes first.
4. The labs' newest reports in `Logs/` (what already passes, so you don't spend a run proving it again).

## 4. The report (Project doc `claude/studio/playtest-<who>-<date>.md`, and the last message)

1. **Verdict, one line**: does it read / feel like a solid game *today*, for the player you are? `Solid` / `Solid with fixes` / `Not yet`, and the one reason.
2. **The run**: what you played, in what order, how long, with which tools; what you could not run and why.
3. **The five that matter most**, ranked by how much they hurt the player you are, each: what happens, where (day/time or frame), the evidence tag and the file (frame number, log line), why it matters to that player, a fix in one line *if you have one* (fixes are the leads' job; never a new mechanic).
4. **Notes in his format**, timestamped by in-game day and clock, grouped Day / Night, each line tagged **B** (bug), **F** (feel), **L** (looks), **S** (story) and with its evidence tag, so they merge straight into his playtest notes.
5. **Numbers** (veteran: required; first hour: the ones that matter to a newcomer): customers, served, storm-outs and their patience at exit, the longest wait, repair times, money in and out, idle stretches, walking distance per order.
6. **What reads / feels right**: at most five lines, so nobody "fixes" it.
7. **Needs his hands**: what only Mansoor at the controls can settle, as questions he can answer while he plays tonight.

Style: the Bible's §5 style. Plain words, numbers over adjectives, no praise padding, no hedging without a reason, written for a phone at 2 a.m.

## 5. Hard rules

- **Play, don't build.** No code, no scene edits, no assets, no saves, no commits. A playtester that "quickly fixes" something has stopped being a playtester.
- **No new mechanics** (Bible §2): report the problem, not a feature.
- **Locks are not bugs.** Read §2 before calling anything wrong.
- **Specific or silent.** A frame number, a log line, a file and line; never "the UI feels cluttered" without the screen and the elements.
- **Honest about the instrument.** If a judgement needs hands, say so. If autopilot played it, say so (autopilot is a better player than a newcomer and a worse one than a veteran at choosing what to do next).
- **Compare by name.** Against *PlateUp!*, *ReStory*, *Overcooked*, *Papers, Please*, *Unpacking*, *Hitman*/*Thief*-likes or whatever fits, with the specific thing they do; cite a source when you state a fact about another game.
