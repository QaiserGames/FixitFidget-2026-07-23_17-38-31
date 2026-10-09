using System.Collections;
using UnityEngine;

// ---------------------------------------------------------------------------
// THE DEVICE WAKES (the bench, v2, 4b, 9 Oct 2026; claude/hero-models-spec.md §5, claude/session-2026-10-09-late-notes.md §6)
//
// The moment a repair's last task completes, the thing itself says so, with its own kept parts, before any word on
// the screen does: the phone's screen lights up (its glass glows, a soft boot chime); the pocket watch is wound (the
// crown turns) and starts to tick; the camera's advance is wound and the new shutter fires. "Fixed!" (Juice) pops with
// it. One component per device prefab, wired by Fixit Fidget > Bench > Bench 4; Fire() is what Juice calls, and a
// device without one just gets the words.
// ---------------------------------------------------------------------------
public class DeviceWake : MonoBehaviour
{
    public enum Kind { None, ScreenLights, CrownWinds, ShutterFires }

    [SerializeField] private Kind kind = Kind.None;
    [Tooltip("ScreenLights: the screens that may be on (the fresh one, the working one): whichever is active lights. CrownWinds: the crown. ShutterFires: the working blade.")]
    [SerializeField] private Transform[] parts;

    public Kind What => kind;
    public bool Woken { get; private set; }
    /// <summary>The screen that was lit (checks): its renderer, once the glow is on.</summary>
    public Renderer LitScreen { get; private set; }

    Material lit;   // the lit screen's own material (the asset is shared), cleaned up with the device

    static readonly Color ScreenGlow = new Color(.55f, .68f, .95f);

    /// <summary>The device's own cue for "it works again", once; nothing without a DeviceWake on the job.</summary>
    public static void Fire(JobBase job)
    {
        if (job == null) return;
        DeviceWake wake = job.GetComponent<DeviceWake>();
        if (wake != null) wake.Play();
    }

    public void Play()
    {
        if (Woken || !Application.isPlaying || kind == Kind.None) return;
        Woken = true;
        switch (kind)
        {
            case Kind.ScreenLights: StartCoroutine(ScreenLightsUp()); break;
            case Kind.CrownWinds: StartCoroutine(WindAndTick()); break;
            case Kind.ShutterFires: StartCoroutine(WindAndFire()); break;
        }
    }

    // ---------- the phone: the glass glows, as a lock screen does, and stays lit ----------

    IEnumerator ScreenLightsUp()
    {
        Renderer screen = null;
        if (parts != null) foreach (Transform t in parts)
        {
            if (t == null || !t.gameObject.activeInHierarchy) continue;
            screen = t.GetComponent<Renderer>();
            if (screen != null) break;
        }
        Sfx.Play("phone.boot", transform.position);
        if (screen == null) yield break;
        // Its own material (the asset is shared), with emission on.
        Material m = screen.material;
        lit = m;
        LitScreen = screen;
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        Color baseColour = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
        yield return new WaitForSeconds(.25f);     // a beat: the boot
        for (float t = 0f; t < 1f; t += Time.deltaTime / .55f)
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            m.SetColor("_EmissionColor", ScreenGlow * (k * 1.35f));
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.Lerp(baseColour, Color.Lerp(baseColour, ScreenGlow, .6f), k));
            yield return null;
        }
        m.SetColor("_EmissionColor", ScreenGlow * 1.35f);
    }

    // ---------- the watch: the crown turns (wound), then it ticks ----------

    IEnumerator WindAndTick()
    {
        Transform crown = parts != null && parts.Length > 0 ? parts[0] : null;
        Sfx.Play("watch.wind", crown != null ? crown.position : transform.position);
        if (crown != null)
        {
            // The crown turns about its own stem (its outward axis, +Z as built): three turns in under a second, in
            // the ratchet's short pulls.
            float turned = 0f;
            const float total = 3f * 360f;
            for (float t = 0f; t < 1f; t += Time.deltaTime / .9f)
            {
                float want = total * (t - .06f * Mathf.Sin(t * Mathf.PI * 8f));   // a pull, a pause, a pull
                float step = want - turned;
                crown.Rotate(Vector3.forward, step, Space.Self);
                turned = want;
                yield return null;
            }
            crown.Rotate(Vector3.forward, total - turned, Space.Self);
        }
        yield return new WaitForSeconds(.1f);
        Sfx.Play("watch.tick", transform.position);
    }

    // ---------- the camera: the advance is wound, and the new shutter fires ----------

    IEnumerator WindAndFire()
    {
        Transform blade = parts != null && parts.Length > 0 ? parts[0] : null;
        Sfx.Play("camera.wind", transform.position);
        yield return new WaitForSeconds(.5f);
        Sfx.Play("camera.shutter", blade != null ? blade.position : transform.position);
        if (blade == null || !blade.gameObject.activeInHierarchy) yield break;
        // The blade snaps across and back: a leaf shutter's blink, about its own axis (the blade's up).
        Quaternion rest = blade.localRotation;
        for (float t = 0f; t < 1f; t += Time.deltaTime / .05f) { blade.localRotation = rest * Quaternion.AngleAxis(Mathf.Lerp(0f, 55f, t), Vector3.up); yield return null; }
        yield return new WaitForSeconds(.06f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / .12f) { blade.localRotation = rest * Quaternion.AngleAxis(Mathf.Lerp(55f, 0f, t), Vector3.up); yield return null; }
        blade.localRotation = rest;
    }

    private void OnDestroy()
    {
        if (lit != null) { if (Application.isPlaying) Destroy(lit); else DestroyImmediate(lit); }
    }

#if UNITY_EDITOR
    /// <summary>The build step's hand on the private fields.</summary>
    public void Configure(Kind what, params Transform[] with)
    {
        kind = what;
        parts = with;
    }
#endif
}
