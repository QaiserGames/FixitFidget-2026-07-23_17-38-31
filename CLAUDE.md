# Fix It Fiasco — working rules for Claude

Read `Docs/Studio/BIBLE.md` before anything else. It holds the pitch, the three pillars, the locked decisions, the current state, the document map and the studio's roster. This file is the short version.

## The person
Mansoor, solo developer (QaiserGames), new to Unity and Blender when he started; his girlfriend does the 2D art, his sister writes and playtests dialogue. He decides everything. Propose, build, critique; never decide for him. He wants debate, not agreement; the reasoning behind every recommendation, extremely thorough; evidence (what sold, and why) behind any verdict on direction.

## The game in three lines
A 3D repair-café sim in San Francisco: by day the bench (held to reStory's bar) and the hectic counter and drinks; by night petty errands for the man at the café's bins; the dream is to franchise, and the man follows. Steam, 2027, $12.99. Tone: chaos with a hint of cozy.

## Hard rules
- **Discuss before building.** If he asks to discuss, write no code. Once he has agreed a plan, build it straight through and list the calls you made for him to overrule.
- **Complete files, never diffs.** Every changed file is delivered whole, with its repo path.
- **No new mechanics (October 2026).** Finish, tune, fix, polish. A new verb, resource, meter or loop is flagged, not built.
- **One ordered list of next steps.** Not parallel threads for him to manage.
- **Unity steps are numbered** and name the exact menu, window, field and value, plus what he should see.
- **Blender work is its own session.** Unity sessions are code and mechanics.
- **Don't nag him to merge branches.** Do ask him to Fetch/Pull at the start of a session and to push at the end (two machines, GitHub in between). Commits on the Mac carry his name.
- **Objects are objects, information is projected.** World space carries colour and shape; the HUD carries numbers.
- **Camera:** five Cinemachine cameras; isometric to move and deliver, first-person where Ace uses his hands, plus an optional walking first-person (V). Never assert first-person-only.
- **Grace's Day 1 latte suppression is intentional.** Not a bug.
- **The purpose rule:** every part of a model must mean something; build from measured real references. A collider is the piece's solid, never its bounds.
- **Brands are fictional and legal-friendly** (a playful nod, never a real name, logo or look).
- Dialogue lines aim for 90 characters, never over 150; one line per bubble.

## The repo
Live branch: `playtest-2`. The running handoff is `Docs/ProjectDirection/Current-Handoff-2026-09-12.md` (newest entry at the top). The café scene is `Assets/Playtests/AcesCafeLayoutPlaytest.unity`. Runtime scripts are flat in `Assets/Scripts/`; build steps and labs are in `Assets/Editor/` under the menu `Fixit Fidget ›`; models are built by `Tools/Blender/*.py`. Unity 6000.5.2f1, URP, Cinemachine 3.1.7. Labs photograph their checks into `Logs/` (local). After changing files on disk: `Fixit Fidget › Checks › Pick up scripts changed from outside (reimport)`, never during Play mode.

## The studio
Fourteen leads live in `Docs/Studio/agents/` (installed into `.claude/agents/` on each machine with one copy, Bible §10), every one on `model: opus`, `effort: max` until he has Fable usage again (Bible §3.7). Ranked with story and game design first (Bible §3.8, §9). The Producer runs the session loop: audit → one list → leads → QA read → discussion → integrate → notes → push. Research, design and Blender work can run in parallel; anything that touches the Unity project runs one lead at a time. Every lead returns the Bible's §5 output contract.
