# Roadmap checkpoint — 2026-09-05

This records progress against the existing GDD v4 and recent playtests. It does
not replace or reschedule the GDD. Review branch: `codex/grace-showcase-memory`.

| Area | Evidence/status | Next gate |
| --- | --- | --- |
| M0 core loop | Owner completed Days 1–5. Uploaded logs reconciled; reported losses came from workload/timeouts. | Keep regression checks while extending the loop; fresh-player validation is still valuable. |
| Day 1 onboarding | Owner reports the timed top-left hints and repair hints improved the experience. | Preserve authored pacing and Inspector hint duration. |
| Recap checkpoint | Owner confirmed the previous save/reload change worked. | Retest cursor isolation on this branch; do not assume every save-failure/purchase case was owner-tested. |
| M1 customer showcase | Existing Grace profile and two scheduled visits; this branch adds factual callbacks, identity reservations, and expression hooks. | Unity checks and two-visit playtest, then signature interaction/content work. |
| M1 final content | Storyteller focus boundary, film-camera repair/strap detail, photo consequence, final expressions, and recognition test are incomplete. | Make those individual slices reviewable before marking M1 complete. |
| Custom character/portrait art | Custom modelling paused by the owner. No final customer portraits yet; Hades-like individuality is being considered. | Decide from references and in-game scale when ready; current code needs no art commitment. |
| Espresso redesign | Free brewing before orders, machine choices, cooling, and waste recorded as ideas. | Explicit design review before implementation. |

The current authored schedule places Grace's first featured visit late in Day 1
and her return early in Day 2. The GDD's full camera/photo story describes later
beats. This branch keeps the current assets' timing; reconciling the full story
schedule is a deliberate future content decision.

The next focused session should validate this branch, then choose the smallest
remaining M1 behaviour slice. No merge into main, progress deletion, economy
rebalance, espresso overhaul, art purchase, or story rescheduling has been done
as part of this work. Ask the owner before a critical action.
