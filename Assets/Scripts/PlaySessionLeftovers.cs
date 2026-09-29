using UnityEngine;

// ---------------------------------------------------------------------------
// WHAT AN EDITOR PLAY SESSION LEAVES BEHIND
//
// The game makes a few helpers while playing: the sound player and the soundscape, the NPC attention
// director, and the straight-face meter. They were marked HideFlags.DontSave, and in the editor a
// DontSave object outlives its Play session: it is no part of the scene, so stopping Play never
// destroys it. Every session left another set behind, still switched on, and the next session ran
// them again beside its own: two attention directors choosing glances, two soundscapes asking for
// the same beds, and a meter that was on screen when Play stopped staying there. The night cycle had
// the same fault (fixed in NightCycle on 29 Sept, where a night's note stayed over the next recap).
//
// Now they are ordinary objects of the Play session (RuntimeFlags), and in the editor each session
// starts by putting away any left from before. A player build never had the problem.
// ---------------------------------------------------------------------------
public static class PlaySessionLeftovers
{
    /// <summary>
    /// The flags for a helper made in code: none while playing, so it ends with the Play session;
    /// DontSave when an editor tool makes one in Edit Mode, so the scene never keeps it.
    /// </summary>
    public static HideFlags RuntimeFlags => Application.isPlaying ? HideFlags.None : HideFlags.DontSave;

#if UNITY_EDITOR
    /// <summary>How many leftovers this Play session put away as it started (reports and checks).</summary>
    public static int PutAway { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Sweep()
    {
        PutAway = 0;
        // The soundscape before the sound player: putting it away stops its loops on the player's voices.
        Clear<CafeSoundscape>();
        Clear<SoundPlayer>();
        Clear<NpcAttentionDirector>();
        Clear<StraightFaceUI>();
        if (PutAway > 0)
            Debug.Log($"[Play session] Put away {PutAway} helper(s) left behind by an earlier Play session " +
                      "(the sound player, the soundscape, the NPC attention director, the straight-face meter).");
    }

    // Only objects marked DontSave: this session hasn't made any yet, and made now they never are.
    static void Clear<T>() where T : Component
    {
        foreach (T left in Resources.FindObjectsOfTypeAll<T>())
        {
            if (left == null || (left.gameObject.hideFlags & HideFlags.DontSave) == 0) continue;
            try
            {
                Object.DestroyImmediate(left.gameObject);
                PutAway++;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Play session] Couldn't put away a leftover {typeof(T).Name}: {e.Message}");
            }
        }
    }
#endif
}
