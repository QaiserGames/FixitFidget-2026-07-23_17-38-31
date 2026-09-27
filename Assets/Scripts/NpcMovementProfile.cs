using UnityEngine;

/// <summary>
/// How one sort of person moves and carries themselves in the café: a small,
/// reusable bundle of presentation numbers that NpcSocial applies to the
/// NPC's locomotion, look-at, idles and gestures. Five archetypes ship
/// (Relaxed, Hurried, Shy, Social, Distracted - see NpcProfileLibrary); a
/// walk-in rolls one at the door, a regular keeps the same one every visit.
///
/// Presentation only. Nothing here changes queue order, patience, orders,
/// prices or what a customer wants - a Hurried customer is not served faster
/// and a Relaxed one does not wait longer; they only walk, stand, glance and
/// gesture differently.
/// </summary>
[CreateAssetMenu(menuName = "Fixit Fidget/NPC movement profile", fileName = "NpcProfile_")]
public sealed class NpcMovementProfile : ScriptableObject
{
    /// <summary>Which walk clip the animator plays (the WalkStyle parameter; see NpcGaitData).</summary>
    public enum WalkStyle { Normal = 0, Relaxed = 1, Brisk = 2 }
    /// <summary>Which standing idle the animator plays (the IdleStyle parameter).</summary>
    public enum IdleStyle { Normal = 0, Relaxed = 1 }

    [Header("Identity")]
    [Tooltip("Short name for traces and checks.")]
    public string displayName = "Relaxed";
    [Tooltip("How often walk-ins and patrons roll this profile, relative to the others.")]
    [Range(0f, 10f)] public float weight = 1f;

    [Header("Walking")]
    [Tooltip("Walking speed, as a share of the locomotion's base speed (1 = unchanged).")]
    [Range(.6f, 1.4f)] public float walkSpeed = 1f;
    [Tooltip("Acceleration, as a share of the base (1 = unchanged).")]
    [Range(.5f, 2f)] public float acceleration = 1f;
    [Tooltip("How briskly the body turns (1 = unchanged; lower is lazier).")]
    [Range(.5f, 1.6f)] public float turnSpeed = 1f;
    public WalkStyle walkStyle = WalkStyle.Normal;
    [Tooltip("0 = ambles, pauses to look around; 1 = sets off at once, short pauses, direct.")]
    [Range(0f, 1f)] public float urgency = .5f;
    [Tooltip("Extra room this person keeps round their body, metres (added to the body radius).")]
    [Range(0f, .08f)] public float personalSpace = 0f;

    [Header("Standing and waiting")]
    public IdleStyle idleStyle = IdleStyle.Normal;
    [Tooltip("How loosely they stand to a spot's facing (1 = the usual few degrees; more = more turned away).")]
    [Range(.5f, 2f)] public float facingLooseness = 1f;
    [Tooltip("Slow weight-shift sway while standing, degrees of torso tilt.")]
    [Range(0f, 3f)] public float standingSway = 1.2f;

    [Header("Idles (seated and standing)")]
    [Tooltip("How often they do something while idle (1 = every 6-14 s or so; 2 = twice as often).")]
    [Range(.4f, 2f)] public float idleFrequency = 1f;
    [Tooltip("How long an idle beat lasts (1 = usual).")]
    [Range(.5f, 2f)] public float idleDuration = 1f;

    [Header("Attention")]
    [Tooltip("How readily they look at things: passers-by, the door, the counter, Ace.")]
    [Range(0f, 1f)] public float lookTendency = .5f;
    [Tooltip("How readily they gesture (a seated hand gesture, a nod, talking with the hands).")]
    [Range(0f, 1f)] public float gestureLikelihood = .4f;
    [Tooltip("How readily they acknowledge and chat with other patrons.")]
    [Range(0f, 1f)] public float sociability = .5f;
    [Tooltip("Shy people look less at strangers and look away sooner.")]
    [Range(0f, 1f)] public float shyness = 0f;
    [Tooltip("Seconds before they react to something (a door opening, someone looking at them).")]
    [Range(0f, 1.5f)] public float reactionDelay = .2f;

    public string Name => string.IsNullOrEmpty(displayName) ? name : displayName;
}
