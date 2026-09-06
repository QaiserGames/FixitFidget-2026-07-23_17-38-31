using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Device-attached status plus a compact room-wide alert for EACH ringing call.
// No global "first phone" selection, invisible penalties or numbered UI.
public sealed class HoldCallPresentation : MonoBehaviour
{
    private static readonly List<HoldCallPresentation> active = new();
    private HoldCallJob job;
    private TMP_Text label;
    private AudioSource speaker;
    private AudioClip music, ring;
    private HoldCallRun.State previous = (HoldCallRun.State)(-1);
    private bool audioPaused;
    private Camera cam;

    private void Start()
    {
        job = GetComponent<HoldCallJob>(); cam = Camera.main;
        var screen = new GameObject("Support call status");
        screen.transform.SetParent(transform, false);
        screen.transform.localPosition = new Vector3(0, .17f, 0);
        label = screen.AddComponent<TextMeshPro>();
        label.rectTransform.sizeDelta = new Vector2(.7f, .25f);
        label.fontSize = .35f; label.alignment = TextAlignmentOptions.Center;
        speaker = gameObject.AddComponent<AudioSource>();
        speaker.playOnAwake = false; speaker.loop = true; speaker.spatialBlend = 1f;
        speaker.minDistance = 1f; speaker.maxDistance = 16f; speaker.rolloffMode = AudioRolloffMode.Linear;
        music = RepairAudio.MakeTone("Support hold music", true);
        ring = RepairAudio.MakeTone("Support answer ring", false);
    }
    private void OnEnable()
    {
        if (!active.Contains(this)) active.Add(this);
        previous = (HoldCallRun.State)(-1);
    }
    private void OnDisable() { active.Remove(this); if (speaker != null) speaker.Stop(); }
    private void LateUpdate()
    {
        if (job == null || label == null) return;
        bool live = job.CanOperate;
        label.gameObject.SetActive(live);
        if (!live)
        {
            if (speaker != null && !audioPaused) { speaker.Pause(); audioPaused = true; }
            return;
        }
        if (audioPaused) { speaker.UnPause(); audioPaused = false; }
        label.text = job.StatusLine;
        label.color = job.CurrentPhase == HoldCallRun.State.Ringing ? new Color(1f, .8f, .3f) : Color.white;
        if (cam != null) label.transform.rotation = cam.transform.rotation;
        if (previous != job.CurrentPhase)
        {
            previous = job.CurrentPhase;
            speaker.Stop();
            speaker.clip = previous == HoldCallRun.State.OnHold ? music : previous == HoldCallRun.State.Ringing ? ring : null;
            speaker.volume = previous == HoldCallRun.State.OnHold ? .10f : .65f;
            if (speaker.clip != null) speaker.Play();
        }
    }
    private void OnGUI()
    {
        if (job == null || !job.CanOperate || job.CurrentPhase != HoldCallRun.State.Ringing) return;
        int slot = 0;
        foreach (var other in active)
        {
            if (other == this) break;
            if (other != null && other.job != null && other.job.CanOperate && other.job.CurrentPhase == HoldCallRun.State.Ringing) slot++;
        }
        string direction = "";
        if (cam != null)
        {
            Vector3 p = cam.WorldToViewportPoint(transform.position);
            direction = p.z < 0 ? "Behind you" : p.x < .35f ? "To your left" : p.x > .65f ? "To your right" : "Ahead";
        }
        var style = new GUIStyle(GUI.skin.box) { fontSize = Mathf.Clamp(Screen.height / 55, 14, 24), alignment = TextAnchor.MiddleLeft, wordWrap = true };
        string owner = job.Owner != null ? job.Owner.CustomerName : "Customer";
        GUI.Box(new Rect(Screen.width - 330, 165 + slot * 70, 310, 64),
            $"  {owner} · SUPPORT READY\n  {direction} · {Mathf.CeilToInt(job.SecondsRemaining)}s to answer", style);
    }
    private void OnDestroy()
    {
        active.Remove(this);
        if (music != null) Destroy(music);
        if (ring != null) Destroy(ring);
    }
}
