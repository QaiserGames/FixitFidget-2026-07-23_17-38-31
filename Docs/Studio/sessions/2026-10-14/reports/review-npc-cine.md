# Review: npc (`b-npc`) and cine (`b-intro`), 14 Oct, before the Mac deploy

*Code reviewer, read-only. Nothing was compiled or run. Every line number below is in the branch named, in its worktree (`/root/cafe/build/npc`, `/root/cafe/build/intro`), diffed against `431f081`.*

**What I read:**
- the brief (common §3 and §9, the feel targets T20, T21 and calls 12–13, the contracts), the NPC and Cinematics specs, Days §8, and both builders' reports;
- every changed runtime file in full, in the regions the diff touches: `NpcSeating`, `PatronBrain`, `PatronSpawner`, `PolygonNpcVisual`, `CustomerBrain`, `WaitingArea`, `TableSeat`, `NpcSocial`, `DayIntro`, `CafeViewMode` and `StationInteractable`;
- the editor steps and labs: `NpcWave2Steps` (the stools, guests, names and Life 4), `CafeSeatsSteps`, `CinematicsWave2Steps`, `NightZoomCheck`, `DayIntroCheck` (setup, judging, teardown), `ControllerChecks`, `BeverageStationSetup`;
- the unchanged code those files call: `DayClock`, `CafeDaylight`, `NightWalk`, `WaitingSpot`, `NpcLifeProps`, `NpcLocomotion`, `PlayLab`, `CafeLab`, `WholeGamePlaytest`, `CustomerSpawner` and `StreetLife`.

I only skimmed `DispenserFrameCheck`, `Playtest4BenchSteps`, the palette steps 6a/6b and `WaitingSpaceChecks`.

**Verdict:** no Blockers. Two Majors, both one- or two-line fixes. Five Minors.

## Blockers

None found.

## Major

### M1. In the whole-game playtest the intro plays but no settled guests appear, so the café opens empty

**Where:** `b-npc` `Assets/Scripts/PatronSpawner.cs:326`.

**Scenario:**
- `Fixit Fidget › Playtest › Play the whole game from Day 1 (test save)` (`WholeGamePlaytest.cs:45-48`) is the fresh-game path he asked for ("playtest everything as one whole"). It sets `CafeLab.PendingKey` and `DayIntro.LabKey`, so `CafeLab.Active` is true and the intro still plays (`DayIntro.WouldPlay`, `b-intro DayIntro.cs:197`, via `labWanted`).
- `SeatOpeningGuests` returns 0 whenever `CafeLab.Active && !ForceOpeningGuests`. So on Days 1–3 of that session the four guests are never seated:
  - not by `DayIntro.HoldTheWorld`;
  - not by the spawner's day-change branch.
- The result is the same empty room at the open, in the session he or the Producer is most likely to use for playtest 6 and for the report's own "by eye, on a Fresh game with the intro Full" check (npc report §3.5). That misses T21's "4 settled guests at the open on Days 1–3".
- The spec's rule is "skipped in labs … the same rule as the intro". The intro's rule makes an exception for this session; the guests' rule does not.

**Why no lab catches it:** Life 4 B sets `ForceOpeningGuests = true` and calls `SeatOpeningGuests()` mid-day (`NpcWave2Steps.cs:1013-1015`). It never goes through the real open path, `HoldTheWorld` or the day-change branch, so it can't see this gate.

**Fix: two files, so the guests follow the intro's lab rule exactly.**

`b-intro Assets/Scripts/DayIntro.cs:181`. Before:
```csharp
    static bool labWanted;
```
After:
```csharp
    static bool labWanted;
    /// <summary>This lab session plays as the real game (the whole-game playtest from Day 1): the morning's guests come too.</summary>
    public static bool LabWanted => labWanted;
```

`b-npc Assets/Scripts/PatronSpawner.cs:325-326`. Before:
```csharp
        // Off in a lab session unless the lab asks, as the intro is (not marked done, so a lab that asks later still gets them).
        if (CafeLab.Active && !ForceOpeningGuests) return 0;
```
After:
```csharp
        // Off in a lab session unless the lab asks, as the intro is: the whole-game playtest is the real game (DayIntro.LabWanted).
        if (CafeLab.Active && !ForceOpeningGuests && !DayIntro.LabWanted) return 0;
```
The day-change branch is gated by the same method, so the intro-Off case in that session is covered too. Re-check by eye through that menu, not only with Life 4.

### M2. NightZoomCheck can't fail T20's 3 m clause, and the view breaks it on a hitch frame

**Where:** `b-intro`:
- `Assets/Scripts/CafeViewMode.cs:632`;
- `Assets/Scripts/Diagnostics/NightZoomCheck.cs:413` and its message at `:430-432`.

A player won't see this. What's wrong is the lab's verdict on a gated target.

**Scenario, in the view:**
- T20: "on a frame with no zoom input it never moves > 3 m".
- The ease's step is capped at 0.1 s (`dt = Mathf.Min(Time.unscaledDeltaTime, .1f)`), and the distance part peaks at 35 m/s. So one frame of 86 ms or longer at the peak moves more than 3 m.
- Worked case: leaving the bins after zooming out inside, or entering from 40 m (gap 26 m, ease 1.11 s). A 100 ms frame at mid-ease moves 26 × (S(0.545) − S(0.455)) = **3.49 m** with no input.
- On his Mac at 15–20 fps, frames that long are hitches, not rare.

**Scenario, in the lab:**
- Its "one-frame jump" counts a frame only if `moved > 1 m && v > SpeedBar`.
- Any frame with `v > SpeedBar` has already failed the speed check, so the jump check can never fail on its own. That 3.49 m frame reads 34.9 m/s and passes both.
- The lab prints "All N checks passed" for T20 while never measuring its second clause, or its first as written (35 m/s × dt + 0.05 m).

**Fix, the view (`CafeViewMode.cs:632`).** Before:
```csharp
        float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
```
After:
```csharp
        float dt = Mathf.Min(Time.unscaledDeltaTime, 3f / LimitSpeed);   // at most 3 m of ease in any frame, a hitch included (T20); 15 fps unchanged
```

**Fix, the lab (`NightZoomCheck.cs:413`).** Before:
```csharp
                if (moved > JumpMetres && v > SpeedBar) jumps++;
```
After:
```csharp
                // T20 as written: a frame with no zoom input never moves over 3 m, nor over 35 m/s x dt + 0.05 m.
                if (!s.zoomInput && (moved > 3f || moved > CafeViewMode.LimitSpeed * s.dt + .05f)) jumps++;
```

**`NightZoomCheck.cs:430-431`.** Before:
```csharp
        Check(jumps == 0,
            $"no one-frame jump: no frame moved over {JumpMetres:0} m faster than the limit allows ({jumps}); the biggest single frame moved {biggest:0.00} m in {biggestDt * 1000f:0} ms " +
```
After:
```csharp
        Check(jumps == 0,
            $"T20: no frame without zoom input moved over 3 m or over 35 m/s x dt + 0.05 m ({jumps}); the biggest single frame moved {biggest:0.00} m in {biggestDt * 1000f:0} ms " +
```

## Minor

### m1. Neither lab drives the mouse wheel, though T20 says "mouse scroll and pad both tested"

**Where:** `b-intro NightZoomCheck.cs:286-287`, which uses the pad only, as does `ControllerChecks`.

The wheel goes through the same `Zoom()` (`CafeViewMode.cs:408`), so the risk is low, but the lab's claim to cover T20 is incomplete.

**Fix:** add a wheel step after the LT check.

Before (`:286-287`):
```csharp
            $"LT for 0.5 s zooms out at the trigger's 22 m/s: {before:0.0} -> {Distance:0.0} m ({rate:0.0} m/s over {movingTime:0.00} s of movement; ±10%; the spec's \"about +11 m\")");
        yield return Photo("3-her-pavement-after-LT");
```
After:
```csharp
            $"LT for 0.5 s zooms out at the trigger's 22 m/s: {before:0.0} -> {Distance:0.0} m ({rate:0.0} m/s over {movingTime:0.00} s of movement; ±10%; the spec's \"about +11 m\")");
        // T20: the mouse wheel too (the pointer at the screen's middle, off the HUD: the view ignores a wheel over UI).
        var wheel = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>("Night zoom check mouse");
        wheel.MakeCurrent();
        float beforeWheel = Distance;
        zoomHeld = true;
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(wheel, new MouseState { position = new Vector2(Screen.width * .5f, Screen.height * .5f), scroll = new Vector2(0f, -120f) });
        yield return Frames(3);
        zoomHeld = false;
        Check(Distance >= beforeWheel + 1.5f, $"one wheel notch out zooms out (+1.6 m): {beforeWheel:0.00} -> {Distance:0.00} m");
        UnityEngine.InputSystem.InputSystem.RemoveDevice(wheel);
        yield return Photo("3-her-pavement-after-LT");
```

### m2. The laptop worker can sit without a laptop

**Where:** `b-npc PatronSpawner.cs:382`, with `PatronBrain.cs:255-262`.

**Scenario:**
- The open happens in the same frame as `StartDay`, which is the case `ReleaseStaleSeats` exists for. The intro's `HoldTheWorld` runs from `DayClock.Update`, which can share that frame with the recap's button.
- Yesterday's laptop patron is still at communal east 1. Laptop stays are ×2 now, so this is likelier at closing.
- `ReleaseStaleSeats` frees the seat but not the laptop. `TakeTheLaptopAt` then fails `NpcLifeProps.Reserve`.
- At the end of the frame the old patron's `OnDestroy` releases the laptop and switches it off (`NpcLifeProps.Release`).
- Today's "laptop worker" sits at a bare table, with no screen to look at.

**Fix (`PatronSpawner.cs:382`).** Before:
```csharp
            if (seat != null && seat.Occupant is PatronBrain patron && patron != null && patron.Day != day) seat.Release(patron);
```
After:
```csharp
            if (seat != null && seat.Occupant is PatronBrain patron && patron != null && patron.Day != day)
            {
                seat.Release(patron);
                patron.LetGoOfTheTable();   // its laptop and bag too: today's laptop guest takes the laptop at this seat
            }
```

**`PatronBrain.cs`**, after `TakeTheLaptopAt` (`:262`). Before:
```csharp
        if (bag != null && !NpcLifeProps.Reserve(bag, this)) bag = null;
    }
```
After:
```csharp
        if (bag != null && !NpcLifeProps.Reserve(bag, this)) bag = null;
    }

    /// <summary>A patron from another day, cleared away this morning: gives up its laptop and bag now (PatronSpawner.ReleaseStaleSeats).</summary>
    public void LetGoOfTheTable()
    {
        if (social != null) social.Prop = null;
        if (laptop != null) { NpcLifeProps.Release(laptop, this); laptop = null; }
        if (bag != null) { NpcLifeProps.Release(bag, this); bag = null; }
    }
```

### m3. A passer-by shot that drops leaves its sound behind

**Where:** `b-intro DayIntro.cs:427`, with the drop path at `:470-483`.

**Scenario:**
- The J-cut plays the next shot's lead 0.15 s before its cut.
- For `car.pass`, `neighbour.front` and `neighbours.row`, the subject is only searched for at the cut (`Begin` throws "no car …").
- When the shot drops and its slot redraws (often `patio.flower`), the pass-by or footsteps play over a picture without a car or a walker. A redrawn alternate with its own lead stacks a second cue.

**Fix:** no early lead for a shot whose subject may be absent; its lead then plays on its own first frame (`TickShots`, `:422`).

Before (`:427`):
```csharp
        if (shotIndex + 1 < shots.Count && shotTime >= shot.Duration - LeadSeconds) PlayLead(shots[shotIndex + 1]);
```
After:
```csharp
        if (shotIndex + 1 < shots.Count && shots[shotIndex + 1].LeadEarly && shotTime >= shot.Duration - LeadSeconds) PlayLead(shots[shotIndex + 1]);
```

In `Shot` (after `:1560`). Before:
```csharp
        public virtual string LeadCue => null;
```
After:
```csharp
        public virtual string LeadCue => null;
        /// <summary>False for a shot whose subject is found only at its cut (a passer-by, a car): its lead waits for its picture.</summary>
        public virtual bool LeadEarly => true;
```

Add one line to each of `NeighbourFront` (`:1986`) and `NeighboursRow` (`:2023`). Before:
```csharp
        public override string LeadCue => "intro.steps";
```
After:
```csharp
        public override string LeadCue => "intro.steps";
        public override bool LeadEarly => false;
```

And to `CarPass` (`:2071`). Before:
```csharp
        public override string LeadCue => "intro.car";
```
After:
```csharp
        public override string LeadCue => "intro.car";
        public override bool LeadEarly => false;
```

### m4. The NPC scene steps save a dirty scene, and don't undo a half-made change

**Where:** `b-npc Assets/Editor/NpcWave2Steps.cs:131-158`. This is about the scene steps' safety (hunt item 4).

**Scenario:**
- The Producer runs a Playtest 4 step, which doesn't save, then Wave 2 1. `SaveScene` writes both changes as if they were this step's.
- An exception after `change(run)` has begun applying properties leaves the scene modified and unsaved, with the Undo group not collapsed.
- The drinks step refuses a dirty scene and reverts its group on failure (`CinematicsWave2Steps.cs:205`, `:277`); these steps should do the same.

**Fix.** Before:
```csharp
        var run = new Run(title);
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first: this step changes the scene.");
            run.scene = SceneManager.GetActiveScene();
            if (run.scene.path != Playtest4Shared.ScenePath)
                throw new InvalidOperationException($"Open the café scene ({Playtest4Shared.ScenePath}) first; the open scene is '{run.scene.path}'.");
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(title);
            int group = Undo.GetCurrentGroup();
```
After:
```csharp
        var run = new Run(title);
        int group = -1;
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first: this step changes the scene.");
            run.scene = SceneManager.GetActiveScene();
            if (run.scene.path != Playtest4Shared.ScenePath)
                throw new InvalidOperationException($"Open the café scene ({Playtest4Shared.ScenePath}) first; the open scene is '{run.scene.path}'.");
            if (run.scene.isDirty) throw new InvalidOperationException("The café scene has unsaved changes: save or revert them first, so this step's save holds only its change.");
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(title);
            group = Undo.GetCurrentGroup();
```

In the `catch` (`:154-158`). Before:
```csharp
        catch (Exception e)
        {
            run.Line("REFUSED: " + e.Message);
```
After:
```csharp
        catch (Exception e)
        {
            if (group >= 0) { Undo.RevertAllDownToGroup(group); run.Line("(the change is undone; nothing is saved)"); }
            run.Line("REFUSED: " + e.Message);
```

### m5. The stool sit's "float" is checked only from below

**Where:** `b-npc NpcWave2Steps.cs:942`, `:967`, and `NpcSeating.cs:845-850`.

The lift is closed-form from script time: 25–85 % of the 1.3 s Sit Down clip, which is frame-rate-safe. But whether the body rises before the clip's hips drop depends on the clip's hip curve. `NpcSitData` holds no curve, so I can't verify it from the code.

Life 4 A records only `headLowest` (no sink below seated − 2 cm). The failure he complained about on 26 Sept, the head rising above standing height, isn't measured; only the strip photos would show it. This is per the spec, but cheap to close.

**Fix (`:938-943`).** Before:
```csharp
        if (w.seating.Current == NpcSeating.Phase.SittingDown && w.head != null) w.headLowest = Mathf.Min(w.headLowest, w.head.position.y);
```
After:
```csharp
        if ((w.seating.Current == NpcSeating.Phase.Approaching || w.seating.Current == NpcSeating.Phase.Standing) && w.head != null) w.headStanding = Mathf.Max(w.headStanding, w.head.position.y);
        if (w.seating.Current == NpcSeating.Phase.SittingDown && w.head != null)
        {
            w.headLowest = Mathf.Min(w.headLowest, w.head.position.y);
            w.headHighest = Mathf.Max(w.headHighest, w.head.position.y);
        }
```

Add the fields to `SitWatch` (`:793`):
```csharp
        public float headStanding = float.NegativeInfinity, headHighest = float.NegativeInfinity;
```

And in `Judge` after `:967`:
```csharp
        if (!float.IsInfinity(w.headStanding) && !float.IsInfinity(w.headHighest))
            Check(w.headHighest <= w.headStanding + .02f, $"{who}'s head never rises while sitting down (highest {w.headHighest:0.000}, standing {w.headStanding:0.000} m): no float");
```

## Checked and sound

These are worth not re-checking before the deploy.

**The repair bar:**
- Ties come out middle → east → west (3.11 against 3.27 m, 0.1 m tie window, then nearer to the customer).
- Walk-ins and regulars prefer Seat (the 5 archetypes and both regulars), so bench jobs reach `ClaimRepairBar`.
- A turned stool is entered and left on the side away from the counter (`AwayFromTable` sign checked for all three).

**The raised sit:**
- The walk ends on the floor (`floorFeet`), the rise happens inside Sit Down, and a sit cut short stands up from where the rise had got to (`liftAtStand`).
- The way out starts on the floor (`SeatedAt(0)`), and the hand-back is on the floor.
- The locomotion stays out while the seating is busy.

**Settled guests:**
- They are seated under the intro's first shot through the contract §7 call, with idempotence per day.
- They are made at their own stand points, never the door, and pay nothing.
- The pair's second leaves with the first, about 175 s at 420 s, inside its 240 s cap. The `StayUntil` cap is lifted, with a 60 s backstop.
- Patrons never hold the day's end. `WaitingSpot.Release` only frees its own occupant, so a dying patron can't free a guest's seat.

**Looks:**
- They are dealt least-worn by name across both prefabs, a pair can't share a look, and stand-ins are skipped.
- The statics reset at SubsystemRegistration in all touched files.

**Intro:**
- Day 1 is fixed and never tied. Day 2 shows Grace; Day 3 is blocked by Day 2's entry, even if Day 2 was skipped, because the planned order is remembered.
- The history drops today's and later entries, so a fresh Day 1 clears it, and the lab restores it.
- The bench lamp and the café lights are restored on End, on a skip, on an exception and at Play end. Clock, spawners, Ace, the UI clock and the HUD come back through `ReleaseTheWorld`/`EndNow`.
- The skip guards against the opening frame's press. Shots use closed-form timing; the follow pan is exponential.

**Night zoom:**
- 8–48 m outside at night and 8–22 m in a view. By day 24–48 m is unchanged.
- A zoom chosen inside a view is kept on leaving; the view's own zoom is undone.
- Zoom input never snaps (the clamp is widened). Orbit cancels only the angle part. R3 eases by day too.

**Drinks:**
- The straight-on pose derivation matches the spec's local (0, 0.657, −1.05).
- The step refuses a dirty scene, reverts on failure, and is idempotent.
- The camera sits inside Ace's hidden capsule, which raycasts started inside it ignore.

**Integration:** merge `b-npc` with or before `b-intro`, because `DayIntro.cs:625` calls `PatronSpawner.SeatOpeningGuestsNow()`. Npc patch §5.3 is already in cine's branch; don't apply it twice.
