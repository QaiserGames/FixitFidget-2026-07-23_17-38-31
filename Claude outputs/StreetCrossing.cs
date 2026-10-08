using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A pedestrian crossing that café visitors use on their way to and from the door.
///
/// Two kinds:
///  * At a junction (<see cref="CrossingTrafficPhase"/> 0 or 1): people wait at the
///    kerb until the traffic walking alongside them has a green with enough of it
///    left to get across, exactly like a walk signal.
///  * Mid-block, unsignalled (phase -1, the zebra outside the café): cars give way
///    to anyone waiting at the kerb or on the crossing; people step out once no car
///    is on it or too close to stop.
///
/// While someone is on it (or, mid-block, waiting at it) its StreetLife road block is
/// active and traffic stops short of it. A keep-clear crossing also stops queues from
/// ending on top of it when nobody is there.
/// </summary>
public sealed class StreetCrossing
{
    public readonly string Name;
    public readonly int CrossingTrafficPhase;
    public readonly StreetLife.RoadBlock Block;

    private readonly HashSet<NpcJourney> waiting = new HashSet<NpcJourney>();
    private readonly HashSet<NpcJourney> onIt = new HashSet<NpcJourney>();

    public int Waiting => waiting.Count;
    public int OnIt => onIt.Count;
    public bool Signalled => CrossingTrafficPhase >= 0;

    public StreetCrossing(string name, Vector3 center, Vector2 halfSize, float yaw, int crossingTrafficPhase, bool keepClear)
    {
        Name = name;
        CrossingTrafficPhase = crossingTrafficPhase;
        Block = StreetLife.AddRoadBlock(center, halfSize, yaw, false, true, keepClear, "Crossing - " + name);
    }

    public void Dispose()
    {
        waiting.Clear();
        onIt.Clear();
        StreetLife.RemoveRoadBlock(Block);
    }

    public void StartWaiting(NpcJourney walker) { if (walker != null) waiting.Add(walker); Refresh(); }
    public void StopWaiting(NpcJourney walker) { waiting.Remove(walker); Refresh(); }
    public void Enter(NpcJourney walker) { if (walker != null) onIt.Add(walker); waiting.Remove(walker); Refresh(); }
    public void Leave(NpcJourney walker) { onIt.Remove(walker); Refresh(); }

    public void Forget(NpcJourney walker)
    {
        waiting.Remove(walker);
        onIt.Remove(walker);
        Refresh();
    }

    private void Refresh()
    {
        waiting.RemoveWhere(w => w == null);
        onIt.RemoveWhere(w => w == null);
        // At a junction the signals already hold the crossing traffic; the block only
        // guards the people actually on it. Mid-block, drivers give way to anyone
        // standing at the kerb too.
        Block.active = onIt.Count > 0 || (!Signalled && waiting.Count > 0);
    }

    /// <summary>Whether someone at the kerb may step out now, needing <paramref name="crossSeconds"/> to get across.</summary>
    public bool CanStep(float crossSeconds)
    {
        StreetLife life = StreetLife.Main;
        if (life == null) return true;
        if (Signalled)
        {
            // Walk with the parallel traffic: its green, with time to reach the far kerb.
            if (life.GreenRemaining(1 - CrossingTrafficPhase) < crossSeconds + 1f) return false;
        }
        if (life.AnyCarOn(Block, 0.5f)) return false;
        if (!Signalled && life.CarClosingOn(Block, 3.5f)) return false;
        return true;
    }
}
