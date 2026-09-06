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
| Phase 1 reusable software verb | Owner supplied circuit scripts. Reviewed and integrated into a phone fault on this branch; rule tests pass. | Run Unity checks and live-customer playtest from circuit-fault-review.md before marking the verb complete. |
| M1 final content | Storyteller focus boundary, film-camera repair/strap detail, photo consequence, final expressions, and recognition test are incomplete. | Make those individual slices reviewable before marking M1 complete. |
| Custom character/portrait art | Custom modelling paused by the owner. No final customer portraits yet; Hades-like individuality is being considered. | Decide from references and in-game scale when ready; current code needs no art commitment. |
| Espresso redesign | Free brewing before orders, machine choices, cooling, and waste recorded as ideas. | Explicit design review before implementation. |

The current authored schedule places Grace's first featured visit late in Day 1
and her return early in Day 2. The GDD's full camera/photo story describes later
beats. This branch keeps the current assets' timing; reconciling the full story
schedule is a deliberate future content decision.

The owner chose Phase 1 content foundation next: validate the new circuit verb,
then tune it and select the next fault family. The supplied Phase 1 roadmap also
places a reusable device scaffolding tool before devices 3–8. The prototype is
on the bench as specified by the uploaded scripts; reconcile the roadmap's
counter interaction before finalizing that family. The original gameplay timers
and payouts are retained; the new phone fault provisionally pays $45. No main
merge, save reset, espresso overhaul, art purchase, or story rescheduling is
included. Ask the owner before a critical action.
