# Roadmap checkpoint — 2026-09-06

This records progress against the existing GDD v4 and recent playtests. It does
not replace or reschedule the GDD. Current review branch: `codex/circuit-fault-verb`.
The owner confirmed the previous Grace/recap branch was merged and tested.

| Area | Evidence/status | Next gate |
| --- | --- | --- |
| M0 core loop | Owner completed Days 1–5. Uploaded logs reconciled; reported losses came from workload/timeouts. | Keep regression checks while extending the loop; fresh-player validation is still valuable. |
| Day 1 onboarding | Owner reports the timed top-left hints and repair hints improved the experience. | Preserve authored pacing and Inspector hint duration. |
| Recap checkpoint | Owner confirmed the previous save/reload change worked. | Retest cursor isolation on this branch; do not assume every save-failure/purchase case was owner-tested. |
| M1 customer showcase | Grace callbacks, identity reservations, and expression hooks merged; owner reports tests worked. | Signature interaction/content work remains; this does not certify all M1 acceptance checks. |
| Phase 1 reusable software verb | Owner's `81a478c` Spacebar implementation reviewed and preserved, including its serialized multiplier and route-clear hint. Visible boost, reset/input safeguards and 58,804 rule assertions pass. | Playtest visible acceleration and input transitions, then assess puzzle decisions separately from waiting time. Unity/player-build gates remain. |
| Phase 1 Human fault | Silent-call phone conversation implemented: three checks, useful feedback, suspend/resume, partial grading and normal physical handback. 52 pure rule assertions pass. | Run the supplied Unity integration checks and practice-customer playtest; validate reading time and choices before declaring the family complete. |
| M1 final content | Storyteller focus boundary, film-camera repair/strap detail, photo consequence, final expressions, and recognition test are incomplete. | Make those individual slices reviewable before marking M1 complete. |
| Custom character/portrait art | Custom modelling paused by the owner. No final customer portraits yet; Hades-like individuality is being considered. | Decide from references and in-game scale when ready; current code needs no art commitment. |
| Espresso redesign | Free brewing before orders, machine choices, cooling, and waste recorded as ideas. | Explicit design review before implementation. |

The current authored schedule places Grace's first featured visit late in Day 1
and her return early in Day 2. The GDD's full camera/photo story describes later
beats. This branch keeps the current assets' timing; reconciling the full story
schedule is a deliberate future content decision.

The owner chose Phase 1 content foundation next. The next Human family is now
implemented for review; see `human-fault-review.md`. After its playtest, integrate
the existing HoldCallJob as Bureaucratic. The supplied Phase 1 roadmap also
places a reusable device scaffolding tool before devices 3–8. The prototype is
on the bench as specified by the uploaded scripts; reconcile the roadmap's
counter interaction before finalizing that family. Default circuit routes are
6–9 tiles, new tiles take four seconds and verified retry tiles take 0.3 seconds.
Other gameplay timers and payouts are retained; the new phone fault provisionally pays $45. No main
merge, save reset, espresso overhaul, art purchase, or story rescheduling is
included. Ask the owner before a critical action.

The appended Human phone fault provisionally pays $25. It joins the eligible
phone pool without changing earlier serialized fault indices or the save format.
No claim is made that all fault families or device-content milestones are done.

## Approved circuit difficulty direction

Implement and playtest hold-Space acceleration first. Default 4x is adjustable;
the pulse stays visible, every connection is checked, and acceleration adds no
grade penalty. This first slice is implemented; the progression below is planned.

| Fault tier | Intended challenge | Status |
| --- | --- | --- |
| Easy | Short guided route, few scrambled wires; a quick repair that teaches the interaction. | Current guided mechanic is the starting point; tune after boost playtest. |
| Standard | More scrambled wires and route turns, encouraging looking ahead. | Author and test a distinct preset after measuring easy repairs. |
| Hard | Route choices or branching, requiring the player to work out connections. | Separate mechanic design needed; current rules follow a predetermined route. |

Later days should introduce harder faults gradually while retaining quick easy
jobs. Do not increase board size, error count and pulse speed together; customer
pressure already grows. Set day thresholds and reward adjustments after the tier
playtests, rather than treating a longer forced verification time as difficulty.
Automatic day scaling and branching are not part of the boost patch.
