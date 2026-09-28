using System.Collections.Generic;
using UnityEngine;

// ---------------------------------------------------------------------------
// SOUND, THE PLUMBING: THE EARS (claude/sound-plan.md §4.1; Mansoor chose "At Ace, turned with
// the camera", 28 Sept)
//
// The scene's one AudioListener was on the Main Camera. From the overhead view that camera is 24-48 m
// from the café (12-34 m from Ace at night), while the pour goes silent at 4.5 m and the support line
// at 16 m: from above most of the game's sounds couldn't be heard. So the game hears from here:
//
//   - overhead: at Ace's head (the bank's earHeight above the feet), turned with the camera's heading,
//     so what's on the left of the screen is heard on the left, and loudness goes by how far a sound
//     is from Ace, not from a camera 30 m up. Zooming changes nothing;
//   - first person, at a station, in a close-up or a conversation: exactly where the camera is (Ace's
//     eyes), as before.
//
// There is always exactly one listener: the scene's own rest while the rig hears, and come back when
// it goes (leaving Play Mode, a scene change). Added to Ace while playing by SoundRig; never saved.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]   // after the cameras have moved this frame
public sealed class ListenerRig : MonoBehaviour
{
    CafeViewMode view;
    AudioListener ears;
    float earHeight = 1.6f;
    float nextCheck;
    readonly List<AudioListener> resting = new List<AudioListener>();

    /// <summary>True while the ears are at Ace (the overhead view); false while they're at the camera.</summary>
    public bool AtAce { get; private set; }
    public Vector3 EarsPosition => ears != null ? ears.transform.position : transform.position;
    /// <summary>The rig's own listener (never saved, so FindObjectsByType doesn't list it).</summary>
    public AudioListener Ears => ears;
    /// <summary>The scene's own listeners resting while the rig hears (for reports).</summary>
    public int Resting => resting.Count;

    void OnEnable()
    {
        view = GetComponent<CafeViewMode>();
        SoundPlayer player = SoundPlayer.Ensure();
        if (player != null && player.Bank != null) earHeight = player.Bank.earHeight;
        var go = new GameObject("Ears (while playing)") { hideFlags = HideFlags.DontSave };
        go.SetActive(false);
        ears = go.AddComponent<AudioListener>();
        RestOthers();
        Place();
        go.SetActive(true);
    }

    void OnDisable()
    {
        if (ears != null) Destroy(ears.gameObject);
        ears = null;
        foreach (AudioListener listener in resting) if (listener != null) listener.enabled = true;
        resting.Clear();
        AtAce = false;
    }

    // Every listener but the rig's rests (Unity hears from one; two would argue).
    void RestOthers()
    {
        foreach (AudioListener other in FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude))
        {
            if (other == ears || !other.enabled) continue;
            other.enabled = false;
            if (!resting.Contains(other)) resting.Add(other);
        }
    }

    void LateUpdate()
    {
        // Something may switch a camera's listener back on; once a second, rest it again.
        if (Time.unscaledTime >= nextCheck)
        {
            nextCheck = Time.unscaledTime + 1f;
            RestOthers();
        }
        Place();
    }

    void Place()
    {
        if (ears == null) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        Transform eye = cam.transform;
        if (view != null && view.OverheadShown)
        {
            ears.transform.SetPositionAndRotation(view.AceFeet + Vector3.up * earHeight, Quaternion.Euler(0f, eye.eulerAngles.y, 0f));
            AtAce = true;
        }
        else
        {
            ears.transform.SetPositionAndRotation(eye.position, eye.rotation);
            AtAce = false;
        }
    }

    public string Describe()
    {
        Camera cam = Camera.main;
        float toAce = Vector3.Distance(EarsPosition, (view != null ? view.AceFeet : transform.position) + Vector3.up * earHeight);
        float toCamera = cam != null ? Vector3.Distance(EarsPosition, cam.transform.position) : -1f;
        return $"Ears: {(AtAce ? "at Ace (overhead view)" : "at the camera")}, {toAce:0.0} m from Ace's head, {toCamera:0.0} m from the camera; " +
               $"{resting.Count} scene listener(s) resting.";
    }
}
