#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ---------------------------------------------------------------------------
// STABILITY PASS — the scene-data half, 2026-08-28
//
// Found by external audit: the loiter spots in SampleScene carry a drain
// multiplier of 1.0, not the 1.15 every design document specifies.
//
// This is the class of bug code review can't catch. WaitingSpot's field
// defaults to 1f; CafeStep1Setup wrote 1 into every spot it created; and a
// serialized Inspector value beats the script default forever after. So the
// docs, the comments and the spec all say 1.15 while the game runs 1.0.
//
// What it cost: the entire standing-is-worse half of the pressure model. A
// customer who wanted to sit and couldn't was supposed to drain nearly twice
// as fast as a seated one (1.15 vs 0.6). In practice they drained at 1.0 —
// merely normal — so falling through to a loiter spot was almost free, and
// bussing, seat scarcity and patron competition were all pushing against
// nothing.
//
// Scene edits, so Ctrl+Z works. Nothing saves until Ctrl+S.
// ---------------------------------------------------------------------------

public static class StabilityPass
{
    private const float LoiterDrain = 1.15f;
    private const float SeatDrain = 0.6f;

    [MenuItem("Fixit Fidget/Content/11 · Fix waiting-spot drain rates")]
    public static void FixDrain()
    {
        WaitingSpot[] spots = Object.FindObjectsByType<WaitingSpot>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (spots.Length == 0)
        {
            EditorUtility.DisplayDialog("Drain rates",
                "No WaitingSpots in the open scene. Is SampleScene open?", "OK");
            return;
        }

        List<string> changed = new();
        int seats = 0, loiter = 0;

        foreach (WaitingSpot s in spots)
        {
            if (s == null) continue;

            bool isSeat = s is TableSeat || s.Kind == WaitingSpot.SpotKind.Seat;
            float want = isSeat ? SeatDrain : LoiterDrain;

            if (isSeat) seats++; else loiter++;

            SerializedObject so = new SerializedObject(s);
            SerializedProperty p = so.FindProperty("drainMultiplier");
            if (p == null) continue;

            if (Mathf.Approximately(p.floatValue, want)) continue;

            changed.Add($"   {s.gameObject.name,-28} {p.floatValue:0.00} -> {want:0.00}");

            Undo.RecordObject(s, "Fix drain multiplier");
            p.floatValue = want;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(s);
        }

        string body = changed.Count == 0
            ? "Everything already matches the design. Nothing changed."
            : string.Join("\n", changed);

        string text =
            $"Seats:        {seats}  (target {SeatDrain:0.00}x)\n" +
            $"Loiter spots: {loiter}  (target {LoiterDrain:0.00}x)\n\n" +
            $"{changed.Count} changed:\n{body}\n\n" +
            "A customer who wanted a seat and couldn't get one now drains " +
            "nearly TWICE as fast as a seated one. That gap is the whole " +
            "reason seats are worth competing for.\n\nPRESS CTRL+S.";

        Debug.Log("[Stability pass — drain rates]\n" + text);
        EditorUtility.DisplayDialog("Drain rates", text, "OK");
    }

    // A single readout for everything the audit flagged as scene-level, so the
    // answer to "is the build trustworthy" is one menu item rather than five
    // separate hunts through the Inspector.
    [MenuItem("Fixit Fidget/Content/12 · Stability check")]
    public static void Check()
    {
        List<string> r = new();

        // --- drain rates ---
        int badSeat = 0, badLoiter = 0, seats = 0, loiter = 0;
        foreach (WaitingSpot s in Object.FindObjectsByType<WaitingSpot>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (s == null) continue;
            bool isSeat = s is TableSeat || s.Kind == WaitingSpot.SpotKind.Seat;

            if (isSeat) { seats++; if (!Mathf.Approximately(s.DrainMultiplier, SeatDrain)) badSeat++; }
            else { loiter++; if (!Mathf.Approximately(s.DrainMultiplier, LoiterDrain)) badLoiter++; }
        }

        r.Add(badSeat + badLoiter == 0
            ? $"Drain rates      OK   ({seats} seats, {loiter} loiter)"
            : $"Drain rates      ⚠ {badSeat + badLoiter} wrong — run '11 · Fix waiting-spot drain rates'");

        // --- patrons ---
        PatronSpawner ps = Object.FindAnyObjectByType<PatronSpawner>();
        r.Add(ps != null ? "Patron spawner   OK" : "Patron spawner   ⚠ missing — run '9 · Set up patrons'");

        // --- day schedule ---
        CustomerSpawner cs = Object.FindAnyObjectByType<CustomerSpawner>();
        if (cs == null) r.Add("Customer spawner ⚠ MISSING");
        else
        {
            SerializedObject so = new SerializedObject(cs);
            SerializedProperty sched = so.FindProperty("schedule");
            int n = sched != null ? sched.arraySize : 0;
            r.Add(n > 0 ? $"Day schedule     OK   ({n} authored days)"
                        : "Day schedule     ⚠ empty — run '5 · Create days 1-5' and wire them");

            SerializedProperty regs = so.FindProperty("regulars");
            int rn = regs != null ? regs.arraySize : 0;
            r.Add(rn > 0 ? $"Regulars         {rn} wired"
                         : "Regulars         — none wired yet (architecture exists, content doesn't)");

            SerializedProperty jit = so.FindProperty("spawnJitter");
            if (jit != null)
                r.Add(Mathf.Approximately(jit.floatValue, 0f)
                    ? "Spawn jitter     0 — deterministic, right for measuring"
                    : $"Spawn jitter     {jit.floatValue:0.00} — runs won't be comparable");
        }

        // --- logging ---
        r.Add(Object.FindAnyObjectByType<DayLog>() != null
            ? "Day log          OK"
            : "Day log          ⚠ missing — add DayLog to GameManager");

        string text = string.Join("\n", r);
        Debug.Log("[Stability check]\n" + text);
        EditorUtility.DisplayDialog("Stability check", text, "OK");
    }
}
#endif
