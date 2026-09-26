using UnityEngine;

/// <summary>
/// Notices a walking NPC going round its destination instead of reaching it.
///
/// WHY THE BRAINS NEED THIS
/// Their stuck watchdogs measure movement: a body that stays put for a few
/// seconds is wedged. That misses the case people actually saw in play. When
/// someone is standing on (or right beside) the spot an NPC is heading for,
/// crowd avoidance steers round that person, the path steers straight back at
/// the spot, and the NPC walks a circle round them. A circle is plenty of
/// movement, so nothing ever called it stuck, and it kept circling until the
/// other person happened to leave.
///
/// This watches the one thing a circle never does: get closer. Once the NPC is
/// near its goal and has not gained ground for a moment, the spot counts as
/// blocked, and the brain does what a person would do instead - take the spot
/// from a step away, wait for a moment, or choose somewhere else.
/// </summary>
public sealed class NpcGoalWatch
{
    // Gaining at least this much counts as getting closer.
    private const float Gain = 0.1f;

    private Vector3 goal;
    private bool watching;
    private float best;
    private float since;

    /// <summary>Flat distance to the goal at the last check, metres.</summary>
    public float Distance { get; private set; } = float.PositiveInfinity;

    /// <summary>Forget the current goal, e.g. after a pause or a new destination.</summary>
    public void Reset()
    {
        watching = false;
        Distance = float.PositiveInfinity;
    }

    /// <summary>
    /// True once <paramref name="from"/> has stayed within <paramref name="radius"/>
    /// of <paramref name="target"/> for <paramref name="patience"/> seconds without
    /// getting any closer. A new target starts the watch again.
    /// </summary>
    public bool Blocked(Vector3 from, Vector3 target, float radius, float patience)
    {
        Vector3 offset = target - from;
        offset.y = 0f;
        Distance = offset.magnitude;

        Vector3 moved = target - goal;
        moved.y = 0f;
        float now = Time.time;
        if (!watching || moved.sqrMagnitude > 0.01f)
        {
            watching = true;
            goal = target;
            best = Distance;
            since = now;
            return false;
        }

        // Still on the way in, or genuinely getting closer: not blocked.
        if (Distance > radius || Distance < best - Gain)
        {
            best = Distance;
            since = now;
            return false;
        }

        return now - since >= patience;
    }
}
