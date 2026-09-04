# Day 1 onboarding — focused playtest

This is a branch prototype, not a declaration that the five-day M0 test has passed.

## What changed

- Day 1 opens with a Coffee-only walk-in, then a repair-only walk-in from the existing Day 1 device pool. The two introductory service visits arrive one at a time.
- A compact top-left HUD pop-up describes the next action from actual customer, cup, carrying, and inspection state. Each distinct hint appears once per lesson for three seconds, then disappears; returning to an old action does not replay it. New actions replace stale hints. Existing E/F prompts and conversations remain authoritative and remain visible independently.
- Refusal, timeout, or removal ends an introductory attempt too; no reward or success is fabricated. Normal spawn pacing resumes after the second attempt.
- Repair customers do not request an additional beverage on Day 1, including Grace. Profiles and later-day drink wishes are untouched.
- Grace remains the featured Day 1 customer, eligible from 55% of the day once the two introductory attempts finish. She still needs an available arrival slot before closing. This is not an unconditional arrival guarantee if the opening attempts take the entire day.
- The day clock, patience values, grade/payment logic, espresso machine, cup/bean accounting, save schema, later-day assets, and daytime patron system are unchanged. Cafe patrons may still visit during the introduction; they are not service orders.

## Before Play Mode

1. Pull `codex/day1-onboarding` into the existing Unity project. Do not merge main yet.
2. Let Unity import and compile. Stop if there are red Console errors; copy the first error and its stack trace.
3. Run **Fixit Fidget > Checks > Day 1 onboarding** outside Play Mode. Expect one `[Day 1 onboarding] PASS` entry. This checks the sequencing policy, actual job factory, opt-in settings, and Grace reference/timing without editing a scene, asset, or save.
4. Do **not** run the older **Create days 1-5** command. It overwrites authored day tuning and is not needed for this change.
5. Start from a fresh Day 1 test save. Back up your existing save first if it contains progress you want to keep. No code in this branch automatically deletes or resets it.

## First run: normal service

- [ ] Day 1 opens with a top-left `FIRST DRINK` pop-up; first service customer orders Coffee only. The hint does not cover the centre ticket or right-side clock/stock.
- [ ] Stand still: the hint disappears after three seconds and stays hidden. The next new action (such as taking a cup) shows its own three-second pop-up. Going back to an already shown action does not replay it.
- [ ] Opening a conversation hides the guide immediately. Closing it may show a new action, but never resumes the old pop-up. Hovering over different repair parts does not trigger toasts; selecting the brush can show one cleaning hint.
- [ ] Enter the service counter from its staff side with F, aim at the customer and press E to talk. After their line, E takes the job; Q declines. F steps away.
- [ ] After accepting: take an empty cup (E), use the espresso machine (E), wait, collect (E), approach the correct customer and press E at the Serve prompt. The hint should follow each successful change, not just a key press.
- [ ] No second service customer starts during the first visit. After the first customer leaves, a repair-only customer arrives with `FIRST REPAIR` guidance.
- [ ] Accept, pick up the intake item (E), set it on the bench (E), enter the bench (F), aim at the item and left-click to inspect.
- [ ] Hover guidance and existing tool prompts remain readable. Complete the fault, reassemble, exit inspection, carry the item back and press E at Hand it back (no intake conversation needed).
- [ ] The guide disappears after the second visit ends. Normal arrivals resume. No Day 1 repair gains a second beverage ticket.
- [ ] Grace can still arrive once after the opening and after the 55% mark. Her intake/return dialogue and memory work as before. Note her actual arrival time.
- [ ] Capture the recap and Console. Record any overlap/clipping of the guide with tickets, clock, stock, dialogue, or bench view (include resolution).

## Additional checks, after the first run

- [ ] Decline the first Coffee: the repair attempt still arrives; no false successful-service count.
- [ ] Let an introductory customer run out of patience: their existing departure/cleanup runs and the sequence can continue.
- [ ] Pick up an extra cup or set a finished cup down: guidance handles full hands and finds the actual ready cup. Losing/returning that cup permits the existing machine to brew again if stock remains.
- [ ] A zero-stock test does not claim a drink can be accepted/made; the existing out-of-stock conversation remains usable.
- [ ] Return an incomplete but reassembled repair: normal grading applies and the lesson ends without awarding an artificial Perfect.
- [ ] If closing begins mid-lesson, finish/decline existing work normally; no new customers spawn after closing and recap hides the guide.
- [ ] Advance to Day 2, and separately load a Day 2 save: no guide, no forced opening order, normal repair drink wishes. Continue the outstanding five-day M0 checks separately.

## Verification boundary

Source and reference checks can be performed outside Unity, but they do not establish compilation, valid NavMesh paths, visible layout, or gameplay correctness. The editor checks and the playtest above must run in the full Unity project before this is merged.

Deferred: free-choice/pre-brew espresso redesign, temperature/waste system, seating animation, custom character art, and broader HUD makeover.
