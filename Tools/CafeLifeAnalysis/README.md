# Café life analysis (Python)

Turns a `Logs/CafeLife/<stamp>/trace.csv` (written by `Fixit Fidget > Café life > Record …`) into the
tables used in `claude/cafe-npc-observations-2026-09-26.md` (baseline) and
`claude/npc-pass1-movement-results-2026-09-26.md` (before/after).

Needs: python3 with pandas, numpy, matplotlib, pillow; ffmpeg on the PATH for frames/clips.

- `analyse.py <trace.csv> [label]` — stuck episodes, spins/pivots, walk-animation flicker, walking on the spot,
  body contacts, Ace being pushed, journey times, frame times. Thresholds are documented at the top of the file.
- `compare.py` — the before/after table: expects `rec1..rec4/trace.csv` (baseline) and `after1..after4/trace.csv`
  in the working directory; adds mean/worst stall, stage-3 stalls, real spins vs arrival turns, "pushed backwards"
  events, yaw wobble per minute walked, door-funnel crowding, per-hotspot counts. Writes `results/compare.json`.
- `classify_pivots.py` — splits pivot events into arrival turns (turning to face a spot on arrival) and real spins.
- `seated_contacts.py <dir> [<dir> …]` — walkers passing through seated bodies (closest approach).
- `hotspots.py [before|after] [out.png] [map.json]` — plots every stuck episode of the four recordings onto the floor
  plan. Needs `map.json` from `Fixit Fidget > Café life > Export café map` (the after map is the rebaked one).
- `mapplot.py` — the floor plan drawing (NavMesh triangles, furniture footprints, seats, slots, exit).
- `frames.py <recording dir> <out.jpg> t1,t2,... [crop=x,y,w,h] [cols=3] [scale=1]` — contact sheet of video
  frames at trace times (uses `frames.csv` to map time to frame).
- `project.py <whole|counter|tables|lounge> x,y,z` — where a world point lands on one of the recorder's fixed cameras.

Run from a folder containing `map.json` and the recording folders (`rec1..4`, `after1..4`).

Pass 2 / 2b (27 Sept, `claude/npc-pass2-life-results-2026-09-27.md`):

- `life.py <recording dir> [label]` — the life layer from the trace's info column: movement profiles and walk
  styles seen, who sat where (chair / bench), idle beats, ambient chats, head-look episodes, the queue's
  eyes-on-Ace share, and the attention director's own totals (`life.txt`).
- `seatmotion.py <recording dir> [label] [--detail]` — every walk into a seat (NpcSeating Approaching) and out of
  one (Returning): share of moving time with the Walk clip playing, longest slide without it (the "hover"),
  degrees turned and worst turn rate; the swivel while sitting down; and every customer leaving the counter:
  fastest turn on the spot, stops before the seat, seconds walking backwards.
- `sheet.py <recording dir> <whole|counter|tables|lounge> x z t0 t1 step out.jpg [w h]` — contact sheet of
  video frames cropped around a world point.
- `cut.py <recording dir> t0 t1 out.mp4 [cx cy zoom] [speed]` — a clip by trace time (optionally a zoomed crop,
  optionally faster).
