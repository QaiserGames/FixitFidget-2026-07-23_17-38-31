using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Observes the existing interactions. Does not issue orders, grant stock,
// complete repairs, change patience, or consume input.
[DisallowMultipleComponent]
public class DayOneGuideUI : MonoBehaviour
{
    private TMP_Text sourceText;
    private PlayerInteractor interactor;
    private ItemInspector inspector;
    private ConversationController conversation;
    private GameObject recap;
    private CustomerSpawner spawner;
    private PlayerCarry carry;
    private EspressoMachine machine;
    private DropSpot[] drops;
    private GameObject panel;
    private TMP_Text hint;
    private float nextRefresh;
    private readonly DayOneHintTimer toast = new();

    public void Initialize(TMP_Text source, PlayerInteractor player,
        ItemInspector itemInspector, ConversationController dialogue, GameObject recapPanel)
    {
        sourceText = source;
        interactor = player;
        inspector = itemInspector != null ? itemInspector : (player != null ? player.GetComponent<ItemInspector>() : null);
        conversation = dialogue != null ? dialogue : (player != null ? player.GetComponent<ConversationController>() : null);
        recap = recapPanel;
        carry = player != null ? player.GetComponent<PlayerCarry>() : null;
        drops = FindObjectsByType<DropSpot>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
    }

    private void LateUpdate()
    {
        DayClock clock = DayClock.Instance;
        bool blocked = clock == null || clock.Day != 1 || clock.DayOver
            || (recap != null && recap.activeInHierarchy)
            || (conversation != null && conversation.InConversation)
            || sourceText == null || !sourceText.gameObject.activeInHierarchy;
        if (blocked)
        {
            // Dialogue/recap must never leave an old toast waiting to pop back
            // up. A fresh action after closing the panel may show its own hint.
            if (clock == null || clock.Day != 1 || clock.DayOver) toast.Reset();
            else toast.Dismiss();
            Show(false);
            return;
        }

        // Expire on time even between the slower state refreshes below.
        if (!toast.IsVisible(Time.unscaledTime)) Show(false);
        // Hints need no per-frame scene searches or string/layout rebuilding.
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.15f;
        if (spawner == null) spawner = FindAnyObjectByType<CustomerSpawner>();
        if (spawner == null || !spawner.IsGuidedOpening
            || (!clock.IsOpen && spawner.OpeningCustomer == null))
        {
            toast.Reset();
            Show(false);
            return;
        }
        if (panel == null && !CreatePanel()) return;

        CustomerBrain customer = spawner.OpeningCustomer;
        bool drinkLesson = spawner.OpeningStep == DayOneOpening.Step.Drink;
        string title = drinkLesson ? "FIRST DRINK" : "FIRST REPAIR";
        string next = NextAction(customer, drinkLesson);
        string text = $"<b>{title}</b>\n{next}";
        // These messages describe stable actions, never the hovered part.
        // Title separates the two lessons, so shared instructions can appear
        // once for each visit without repeating when the player moves around.
        if (toast.Observe(text, Time.unscaledTime, spawner.OpeningHintDuration)) hint.text = text;
        Show(toast.IsVisible(Time.unscaledTime));
    }

    private string NextAction(CustomerBrain customer, bool drinkLesson)
    {
        if (customer == null || customer.IsLeaving)
            return "A customer is on the way. Start with one request.";
        if (!customer.WasAccepted)
        {
            if (interactor != null && interactor.CurrentStation != null && interactor.CurrentStation.IsWorkSurface)
                return "Press F to leave the bench. Head behind the service counter.";
            if (interactor == null || interactor.CurrentStation == null
                || interactor.CurrentStation.IsWorkSurface)
                return "Press F behind the service counter to take orders.";
            if (customer.OutOfStock)
                return "No stock: E to talk, then Q to apologise.";
            if (customer.ShelfFull)
                return "Shelf full. Move an item to a free bench slot.";
            if (customer.CanHearIntake || customer.CanDecide)
                return $"Aim at {customer.CustomerName}. E talks; E after their line accepts.";
            return $"Wait for {customer.CustomerName} to reach the counter.";
        }
        return drinkLesson ? DrinkAction(customer) : RepairAction(customer);
    }

    private string DrinkAction(CustomerBrain customer)
    {
        DrinkJob held = carry != null ? carry.Carried as DrinkJob : null;
        DrinkDefinition wanted = customer.WantedDrink;
        if (held != null && !held.IsEmpty && held.Drink == wanted)
        {
            if (interactor != null && interactor.IsAtStation)
                return $"Press F to step back, then E near {customer.CustomerName} to serve.";
            return $"Take the {customer.WantedDrinkName} to {customer.CustomerName}. E serves it.";
        }
        if (carry != null && carry.IsCarrying && (held == null || !held.IsEmpty))
            return "Hands full. Set the item down or return your cup.";

        if (machine == null) machine = FindAnyObjectByType<EspressoMachine>();
        if (machine != null && machine.IsBrewing)
            return "Brewing... wait for your drink.";

        foreach (DrinkJob cup in DrinkJob.Live)
        {
            if (cup == null || cup.Owner != customer || cup.IsEmpty) continue;
            if (held != null)
                return "Return the spare cup to its stack with E.";
            return $"{customer.WantedDrinkName} ready. Press E at the machine or cup.";
        }
        // A previous customer may have left a cup in the machine. Do not tell
        // the player to load another one into an occupied slot.
        if (machine != null && machine.HasCup)
            return "Machine occupied. Clear its cup before making another drink.";

        ShopInventory stock = ShopInventory.Instance;
        if (customer.CanApologiseForDrink &&
            (stock == null || !stock.CanBrew(wanted) || (held == null && stock.Cups <= 0)))
            return $"No stock. E near {customer.CustomerName} apologises; restock after closing.";
        if (stock != null && !stock.CanBrew(wanted))
            return "Not enough beans. Restock after closing.";
        if (held != null && held.IsEmpty)
            return "Empty cup ready. Press E at the espresso machine.";
        if (stock != null && stock.Cups <= 0)
            return "No cups. Collect a spare or restock after closing.";
        return "Press E at the cup stack to take an empty cup.";
    }

    private string RepairAction(CustomerBrain customer)
    {
        JobBase job = customer.ActiveJob;
        if (job == null) return "The customer's item will appear on the intake shelf.";
        bool inspecting = inspector != null && inspector.FocusedItem == job;
        bool carrying = carry != null && carry.Carried == job;

        if (job.IsComplete)
        {
            if (inspecting)
                return inspector.CurrentTool != ToolType.Hand
                    ? "Fixed! Right-click twice: put down the tool, then leave inspection."
                    : "Fixed! Right-click to leave inspection. E picks up the item.";
            if (carrying)
            {
                if (interactor != null && interactor.IsAtStation)
                    return "Press F to step back. E near the customer returns their item.";
                return $"Take the item to {customer.CustomerName}. Press E at Hand it back.";
            }
            return "Press E to pick up the repaired item for delivery.";
        }

        if (inspector != null && inspector.IsHoldingItem && !inspecting)
            return "Wrong item. Right-click puts down tools, then leaves inspection.";
        if (carrying)
        {
            if (interactor != null && interactor.IsAtStation)
                return "Press F to step back. E at the bench sets items down.";
            return "Carry the item to the repair bench. E sets it down.";
        }
        if (carry != null && carry.IsCarrying)
            return "Hands full. Set the item down or return your cup.";
        if (inspecting)
            return RepairBenchAction(job);

        bool onBench = false;
        if (drops != null)
            foreach (DropSpot drop in drops)
                if (drop != null && drop.Kind == DropSpot.SpotKind.Bench && drop.Holds(job))
                    onBench = true;
        if (!onBench)
            return "Press E to pick up the customer's item from the intake shelf.";
        if (interactor != null && interactor.CurrentStation != null && !interactor.CurrentStation.IsWorkSurface)
            return "Press F to step back. Head to the repair bench.";
        if (interactor == null || interactor.CurrentStation == null || !interactor.CurrentStation.IsWorkSurface)
            return "Item placed. Press F at the repair bench to work there.";
        return "Aim the centre crosshair at the item. Left-click to inspect.";
    }

    private string RepairBenchAction(JobBase job)
    {
        ToolType tool = inspector.CurrentTool;

        if (job.Quality >= 0.999f)
        {
            if (job.HasDetachedComponent<RemovablePart>())
                return tool == ToolType.Pry
                    ? "Pry tool selected. Click the cover in the tray to refit it."
                    : "Fault fixed. Select the pry tool to refit the cover.";

            if (job.HasDetachedComponent<Screw>())
                return tool == ToolType.Screwdriver
                    ? "Screwdriver selected. Click each tray screw to refit it."
                    : "Cover fitted. Select the screwdriver for the tray screws.";

            // A detached part unregisters as its return animation begins. Keep
            // the message accurate during that short transition.
            foreach (Screw screw in job.GetComponentsInChildren<Screw>())
                if (screw.IsOut || screw.IsBusy)
                    return "Finishing the screws...";
        }

        foreach (Screw screw in job.GetComponentsInChildren<Screw>())
            if (!screw.IsOut)
                return tool == ToolType.Screwdriver
                    ? "Screwdriver selected. Click each case screw to remove it."
                    : "Select the screwdriver to remove the case screws.";

        foreach (RemovablePart cover in job.GetComponentsInChildren<RemovablePart>())
            if (!cover.IsRemoved)
                return tool == ToolType.Pry
                    ? "Pry tool selected. Click the loosened cover to lift it off."
                    : "Select the pry tool to lift the loosened cover.";

        if (job.GetComponentInChildren<GrimeSpot>() != null)
            return tool == ToolType.Brush
                ? "Brush selected. Hold left-click and move over the exposed grime."
                : "Select the brush to clean the exposed grime.";

        foreach (ReplaceablePart part in job.GetComponentsInChildren<ReplaceablePart>())
            if (!part.IsReplaced)
                return tool == ToolType.Tweezers
                    ? "Tweezers selected. Click the broken part to replace it."
                    : "Select the tweezers to replace the broken part.";

        return "Follow the part label: select the tool it names, then click the part.";
    }

    private bool CreatePanel()
    {
        Canvas canvas = sourceText.GetComponentInParent<Canvas>();
        if (canvas == null || canvas.rootCanvas.renderMode == RenderMode.WorldSpace) return false;
        canvas = canvas.rootCanvas;
        panel = new GameObject("Day 1 next action (runtime)", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.SetParent(canvas.transform, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(24f, -24f);
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(Mathf.Min(460f, canvasRect.rect.width * 0.34f), 104f);
        Image background = panel.GetComponent<Image>();
        background.color = new Color(0.08f, 0.09f, 0.10f, 0.9f);
        background.raycastTarget = false;

        GameObject label = new GameObject("Hint", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.SetParent(rect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(16f, 10f);
        labelRect.offsetMax = new Vector2(-16f, -10f);
        hint = label.GetComponent<TextMeshProUGUI>();
        hint.font = sourceText.font;
        hint.fontSize = 23f;
        hint.enableAutoSizing = true;
        hint.fontSizeMin = 16f;
        hint.fontSizeMax = 23f;
        hint.color = Color.white;
        hint.alignment = TextAlignmentOptions.MidlineLeft;
        hint.raycastTarget = false;
        return true;
    }

    private void Show(bool visible)
    {
        if (panel != null && panel.activeSelf != visible) panel.SetActive(visible);
    }

    private void OnDisable()
    {
        toast.Dismiss();
        Show(false);
    }
    private void OnDestroy()
    {
        if (panel != null) Destroy(panel);
    }
}

// Time/input-independent policy, covered by the Edit Mode checks. Re-observing
// an unchanged action never extends its configured duration. Returning to an already
// shown action hides any stale text rather than flashing the old hint again.
public sealed class DayOneHintTimer
{
    private readonly HashSet<string> shown = new();
    private string current;
    private float visibleUntil;

    public bool Observe(string key, float now, float duration)
    {
        if (key == current) return false;
        if (string.IsNullOrEmpty(key) || !shown.Add(key))
        {
            Dismiss();
            return false;
        }
        current = key;
        visibleUntil = now + Mathf.Clamp(duration, 3f, 10f);
        return true;
    }

    public bool IsVisible(float now) => current != null && now < visibleUntil;
    public void Dismiss() => current = null;
    public void Reset()
    {
        Dismiss();
        shown.Clear();
    }
}
