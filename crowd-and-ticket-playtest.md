# September 9: floor visibility and overlapping patrons

The screenshots show two overlapping people near a chair and five large tickets
wrapping over the player. Screenshot evidence cannot determine the exact movement
path that led to the overlap.

Changes:
- The current Patron prefab has a one-metre stopping distance. Runtime patron
  stopping distance is now 0.08m (serialized), bringing them to their actual seat
  marker rather than settling in the shared aisle a metre away.
- Settled customers and patrons now have priority zero, so moving agents consider
  them obstacles to avoid. Patrons clear their path when settling and restore their
  yielding priority when moving. Customer recovery still escalates among movers.
- Patron recovery refuses a warp across a NavMesh boundary or into another agent's
  radius. A successful recovery restores its destination. Invalid/partial paths
  cannot count as arrival, and abandoning a seat cannot immediately become sitting.
- Claims reject physically overlapping reserved waiting spots, even if they are
  separate components. This can expose chair layouts that need more spacing;
  it does not move furniture or rebake the NavMesh.
- Cards now share the available width, with 150-unit minimum width and 118-unit
  height defaults. At the screenshot's 1920-wide canvas and default rail settings,
  five or six tickets fit in one row. Calls, drinks and patience remain on tickets.
  Narrow canvases or unusually many tickets can still wrap; nobody is silently hidden.

## Short live test

Use a busy day without resetting your save. Accumulate five accepted customers,
including a support call and a customer with a drink plus repair. Confirm names,
pending drinks, call status/countdown and patience remain readable. Deliver one
item: its obligation should disappear while any remaining drink stays visible.
Check the bench view, recap hiding and your usual display resolution as well.

Watch customers pass occupied chairs and patrons leave them. Check for overlap,
new prolonged stalls or visibly unused nearby chairs. If it recurs, select both
characters in Play Mode and capture their NavMeshAgent radius/priority plus the
nearby seat Stand Points. That distinguishes model size, destination spacing,
path recovery and avoidance. Sitting animations are still pending.

Unity compilation, actual navigation and rendered readability must be tested in
Unity; static checks cannot certify these visual fixes.
