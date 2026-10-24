# Builder A — aim help that helps without fighting (23 Oct 2026)

Copy: `/root/wt23/A`. Rules: `/root/review23/builders-common.md` (read it first).

## His note (verbatim)
"I also noticed when we're working with aim help, you're like locked to the way that, like, for example, if I grab a cup and like someone ordered an espresso, I grab the cup and I go to the espresso machine, I'm like locked to the cup. It's like pushing me. It's like hard for me to even look to press the nozzle to push the espresso machine to work. It's like the aim is so strong. I would rather it be, you know, like helpful, but not so overpowering that it's like fighting me to like for it to move the analog stick."

Context:
- On 19 Oct aim help became on by default (`GameSettings` resets it on once per machine) because his girlfriend, on a pad, found it helped once she turned it on. It must stay helpful for her.
- He plays on a pad too.

## What's there (verify, then go deeper)
`Assets/Scripts/AimAssist.cs`, for first person and the drinks close-up, with a pad only:
- **Friction:** over or near a target the view turns at 0.4 of its speed, fading out over 3° past the target's edge.
- **Magnetism:** within 7° past a target's edge, turning draws the crosshair toward its middle at 22°/s. The header says "never against the stick".
- **Settling:** let go within 4° and it slides on.
- **Stepping:** D-pad left or right steps to the next target.

Targets come from `PlayerInteractor` (`AimTargets`: what's in reach and in sight).

At the drinks machine (`BeverageStation`, `BeverageControl`, `BeverageSlot`, `BeverageCupSupply`, `CupStack`), the cup stack, the cup in a slot or in a hand, the nozzles, the paddles and the buttons sit a few degrees apart. So friction is almost always on and the magnet always has something to pull toward.

His "locked to the cup" may be either:
- the cup he holds being a target; or
- the cup placed under the nozzle pulling the crosshair back down when he tries to look up at the nozzle or button.

Find out which from the code. It may be both.

## What to build
1. **Help that fades with intent.** Friction and magnetism scale down as the stick is pushed harder:
   - full help for a gentle, aiming nudge;
   - none when the stick is pushed most of the way (the player is going somewhere).

   Choose the curve and say why. A common shape: full help below 35 % deflection, zero above 75 %, smooth between.
2. **Pull only toward what's ahead.** The magnet only draws toward a target within a cone around the stick's direction of travel (e.g. ±40°). Never toward one beside or behind the motion. Never toward the target the crosshair is leaving.
3. **Never the cup in his hand.** Whatever Ace holds is never an aim target, nor its slot while he holds it, if that's how the code counts it.
4. **Crowded places get lighter help.** Where targets crowd (the drinks close-up, or anywhere two targets sit within e.g. 6° of each other), scale friction and the magnet down further. A dense field doesn't need pulling: any nearby target is fine.
5. **Keep settling** (a released stick near a target slides on). It's the part that helps without fighting. Make sure it can't pull onto the cup he holds.
6. **His setting.** Propose whether the phone's Settings should offer Aim help as Light / Strong / Off rather than On / Off:
   - Light: the new default.
   - Strong: today's values minus the fighting.

   If yes, implement the levels in `AimAssist` and `GameSettings` (keep the saved key's meaning for old saves: on means Light). **Do not edit `RecapPhone.cs`** (another builder owns it this session); write the exact edit for its Settings row in your report.
7. **The bench's pointer** (`PadCursor`, separate help on the mat) is not in scope unless you find the same fight there. If you do, report it with evidence; don't change it.

## The proof: a lab
Extend `Assets/Scripts/Diagnostics/AimHelpCheck.cs` (today 26 checks, menu "Fixit Fidget/Playtest/Aim help - play check (lab, drives itself)"). Its editor launcher is in Assets/Editor: grep the menu name. Add a drinks-machine section driven by the same synthetic pad input the lab already uses:
- **Holding a cup in the drinks close-up:**
  - push the stick from the cup toward the espresso nozzle or its button, and measure the time to land on it;
  - count frames where the view moved against the stick's direction: must be **0**;
  - count frames where the crosshair was pulled onto the held cup: must be **0**.
- **Strong pushes are free:** at full deflection the view turns at its full speed even across targets (measure degrees per second against no-help).
- **Help still helps:** a small nudge that ends 2–3° off a target still settles onto it (so her case keeps working).
- Photos of each moment through the lab's Photo.

Keep the existing 26 checks passing, or say exactly which change on purpose and why.

## Files you own
`Assets/Scripts/AimAssist.cs`, `Assets/Scripts/PlayerInteractor.cs` (only the aim-target parts; it is large and shared, so touch the minimum), `Assets/Scripts/GameSettings.cs` (only if you add levels), `Assets/Scripts/Diagnostics/AimHelpCheck.cs` and its editor launcher.

Do **not** edit `BeverageStation.cs` (builder D owns it) or `RecapPhone.cs` (builder J). If a change there is needed (e.g. what the drinks machine offers as targets), describe it exactly.
