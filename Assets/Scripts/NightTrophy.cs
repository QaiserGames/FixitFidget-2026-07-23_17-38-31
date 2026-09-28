using UnityEngine;

// ---------------------------------------------------------------------------
// A THING ACE CAN TAKE AT NIGHT (the Night 1 slice; its words are in NightThings)
//
// Part of the scene, in the street: Grace's gnome by her front step (Fixit Fidget > Night > Night 1 -
// Put Grace's gnome and Ace's trophy shelf in the scene). By day it's scenery. At night Ace can take
// it (E, or A / Cross): it goes into the night's ledger, off the street for good (by day too, from
// then on) and onto Ace's shelf in the café (TrophyShelf). Ace notes it down (once Ace knows whose it
// is: otherwise the morning does that), and its owner will come in the next morning to tell Ace all
// about it (MorningFace).
//
// The prompt names it the way Ace knows it: "Take Barnaby" once Grace has mentioned him, "Take the
// garden gnome" before. While it's the thing E would take, a soft light catches it (small things are
// hard to see from above at night).
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class NightTrophy : NightInteractable
{
    [Tooltip("The thing's id in NightThings. Saved: never change it once saves exist.")]
    public string thingId = NightThings.GraceGnome;
    [Tooltip("What's seen in the street. Hidden once it has been taken.")]
    public GameObject visual;

    NightThing thing;
    NightLedger ledger;
    bool listening;
    Light glint;

    public NightThing Thing => thing ??= NightThings.Find(thingId);
    public bool Taken => Ledger != null && Ledger.HasTrophy(thingId);

    NightLedger Ledger
    {
        get
        {
            if (ledger == null && SaveManager.Instance != null) ledger = SaveManager.Instance.Night;
            return ledger;
        }
    }

    protected override bool AvailableTonight => Thing != null && !Taken;

    public override string Prompt
    {
        get
        {
            if (Thing == null) return "";
            Notebook notebook = SaveManager.Instance != null ? SaveManager.Instance.Notebook : null;
            bool heardOfIt = notebook != null && notebook.Knows(Thing.id);
            return "Take " + (heardOfIt ? Thing.name : Thing.unknownName);
        }
    }

    void OnEnable()
    {
        Listen();
        Refresh();
    }

    // The save manager may wake after this does.
    void Start()
    {
        Listen();
        Refresh();
    }

    void OnDisable()
    {
        if (listening && ledger != null) ledger.Changed -= Refresh;
        listening = false;
    }

    void Listen()
    {
        if (listening || Ledger == null) return;
        ledger.Changed += Refresh;
        listening = true;
    }

    /// <summary>Show it in the street, or not once it's been taken.</summary>
    public void Refresh()
    {
        bool here = !Taken;
        if (visual != null && visual.activeSelf != here) visual.SetActive(here);
        foreach (Collider found in GetComponents<Collider>()) found.enabled = here;
    }

    public override void SetFocused(bool focused)
    {
        if (focused && glint == null && Application.isPlaying)
        {
            var go = new GameObject("Glint (while it can be taken)") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, .95f, .55f);
            glint = go.AddComponent<Light>();
            glint.type = LightType.Point;
            glint.range = 1.8f;
            glint.intensity = 1.4f;
            glint.color = new Color(1f, .9f, .72f);
            glint.shadows = LightShadows.None;
        }
        if (glint != null) glint.enabled = focused && !Taken;
    }

    public override void Interact(PlayerInteractor player)
    {
        if (!IsAvailable || Ledger == null || Thing == null) return;
        int night = DayClock.Instance != null ? DayClock.Instance.Day : 0;
        Notebook notebook = SaveManager.Instance != null ? SaveManager.Instance.Notebook : null;
        bool heardOfIt = notebook != null && notebook.Knows(Thing.id);
        if (!Ledger.Take(thingId, Thing.owner, night)) return;
        if (heardOfIt) NotebookHooks.TookAtNight(Thing);
        Sfx.Play("night.take", transform.position + Vector3.up * .2f);
        NightCycle.Note(heardOfIt ? Thing.takenNote : Thing.takenNoteUnknown);
    }
}
