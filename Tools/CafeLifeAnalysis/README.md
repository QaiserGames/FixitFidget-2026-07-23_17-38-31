# Café life analysis (Python)

Turns a `Logs/CafeLife/<stamp>/trace.csv` (written by `Fixit Fidget > Café life > Record …`) into the
tables used in `claude/cafe-npc-observations-2026-09-26.md`.

Needs: python3 with pandas, numpy, matplotlib, pillow; ffmpeg on the PATH for frames/clips.

- `analyse.py <trace.csv> [label]` — stuck episodes, spins/pivots, walk-animation flicker, walking on the spot,
  body contacts, Ace being pushed, journey times, frame times. Thresholds are documented at the top of the file.
- `hotspots.py <out.png>` — plots every stuck episode from the recordings listed in the file onto the floor plan.
  Needs `map.json` from `Fixit Fidget > Café life > Export café map` in the working directory.
- `mapplot.py` — the floor plan drawing (NavMesh triangles, furniture footprints, seats, slots, exit).
- `frames.py <recording dir> <out.jpg> t1,t2,... [crop=x,y,w,h] [cols=3] [scale=1]` — contact sheet of video
  frames at trace times (uses `frames.csv` to map time to frame).
- `project.py <whole|counter|tables|lounge> x,y,z` — where a world point lands on one of the recorder's fixed cameras.

Run from a folder containing `map.json` and the recording folders (or edit the paths at the bottom of `hotspots.py`).
