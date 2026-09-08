using System.Collections.Generic;
using UnityEngine;

// Opt-in character behaviour, kept out of the repair verbs and queue state machine.
// Uses the existing speech bubble and normal customer interactable, not a quiz.
public sealed class CustomerStoryteller : MonoBehaviour
{
    private CustomerBrain owner;
    private StorytellerRun run;
    private readonly List<string> lines = new();
    private PlayerInteractor player;
    private Camera view;
    private float retryAt;

    public bool FocusRequested => run != null && run.FocusRequested;
    public bool CanRequestFocus => isActiveAndEnabled && owner != null
        && owner.CanTellStory && run != null && run.CanRequestFocus;

    public void Initialize(CustomerBrain customer)
    {
        owner = customer;
        lines.Clear();
        CustomerProfile profile = owner != null ? owner.Identity?.Profile : null;
        if (profile == null || !profile.storyteller) { run = null; return; }
        foreach (string line in profile.storyLines ?? System.Array.Empty<string>())
            if (!string.IsNullOrWhiteSpace(line)) lines.Add(line);
        run = new StorytellerRun(profile.storyFirstDelay, profile.storyInterval,
            Mathf.Min(Mathf.Max(0, profile.storyMaxLines), lines.Count), owner.Identity.RemembersFocusBoundary);
        retryAt = 0f;
    }

    public bool RequestFocus() => CanRequestFocus && run.RequestFocus();

    private void Update()
    {
        if (run == null || owner == null || !run.HasMore) return;
        if (!run.Tick(Time.deltaTime, owner.CanTellStory && !owner.SpeechBusy)) return;
        // Scene queries happen only when a line is due, at most twice a second.
        if (Time.time < retryAt) return;
        retryAt = Time.time + 0.5f;
        if (!RoomCanHearStory()) return;
        if (owner.TrySayStoryLine(lines[run.LinesSpoken])) run.MarkSpoken();
    }

    private bool RoomCanHearStory()
    {
        if (player == null) player = FindAnyObjectByType<PlayerInteractor>();
        if (player == null) return false;
        var conversation = player.GetComponent<ConversationController>();
        if (conversation != null && conversation.InConversation) return false;
        var counter = player.GetComponent<CounterRepairView>();
        if (counter != null && counter.OwnsInput) return false;

        // Never spend a story line behind the camera: this prototype has no
        // voice acting/off-screen dialogue card yet. The next look back can reveal it.
        if (view == null) view = Camera.main;
        if (view == null) return false;
        Vector3 point = view.WorldToViewportPoint(owner.LookTarget != null
            ? owner.LookTarget.position : owner.transform.position + Vector3.up * 1.5f);
        if (point.z <= 0f || point.x < 0f || point.x > 1f || point.y < 0f || point.y > 1f) return false;

        foreach (CustomerBrain customer in FindObjectsByType<CustomerBrain>(FindObjectsSortMode.None))
        {
            // An intake, drink request, reassurance or departure already owns speech.
            if (customer.SpeechBusy) return false;
            if (customer.ActiveJob is HoldCallJob call && call.CanOperate && call.WantsPlayerPresent) return false;
        }
        return true;
    }
}
