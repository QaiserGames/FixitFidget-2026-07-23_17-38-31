using TMPro;
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
            Show(false);
            return;
        }

        // Hints need no per-frame scene searches or string/layout rebuilding.
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.15f;
        if (spawner == null) spawner = FindAnyObjectByType<CustomerSpawner>();
        if (spawner == null || !spawner.IsGuidedOpening
            || (!clock.IsOpen && spawner.OpeningCustomer == null))
        {
            Show(false);
            return;
        }
        if (panel == null && !CreatePanel()) return;
        Show(true);

        CustomerBrain customer = spawner.OpeningCustomer;
        bool drinkLesson = spawner.OpeningStep == DayOneOpening.Step.Drink;
        string title = drinkLesson ? "FIRST DRINK" : "FIRST REPAIR";
        string next = NextAction(customer, drinkLesson);
        string text = $"<b>{title}</b>\n{next}";
        if (hint.text != text) hint.text = text;
    }

    private string NextAction(CustomerBrain customer, bool drinkLesson)
    {
        if (customer == null || customer.IsLeaving)
            return "The next customer is on their way. One request at a time for these first two visits.";
        if (!customer.WasAccepted)
        {
            if (interactor != null && interactor.CurrentStation != null && interactor.CurrentStation.IsWorkSurface)
                return "Press F to step back from the bench. Walk to the staff side of the service counter, then press F to work there.";
            if (interactor == null || interactor.CurrentStation == null
                || interactor.CurrentStation.IsWorkSurface)
                return "Walk to the staff side of the service counter and press F to work there. Intake conversations start from the counter view.";
            if (customer.OutOfStock)
                return "Not enough cups or beans. Aim at the customer and press E to talk, then Q to apologise when offered.";
            if (customer.ShelfFull)
                return "The intake shelf is full. Move an item to a free bench slot before accepting.";
            if (customer.CanHearIntake || customer.CanDecide)
                return $"Aim the centre crosshair at {customer.CustomerName} and press E to talk. After their line, press E for Take the job.";
            return $"Let {customer.CustomerName} reach the counter. Then aim the crosshair at them and press E to talk.";
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
                return $"Press F to step back, then carry the {customer.WantedDrinkName} to {customer.CustomerName}. E serves it when the Serve prompt appears.";
            return $"Carry the {customer.WantedDrinkName} to {customer.CustomerName}. Press E when the prompt says Serve the {customer.WantedDrinkName}.";
        }
        if (carry != null && carry.IsCarrying && (held == null || !held.IsEmpty))
            return "Free your hands first. Set the item on a free surface, or return an unwanted cup to the cup stack.";

        if (machine == null) machine = FindAnyObjectByType<EspressoMachine>();
        if (machine != null && machine.IsBrewing)
            return "The machine is brewing. Wait for it to finish; you do not need to hold a button.";

        foreach (DrinkJob cup in DrinkJob.Live)
        {
            if (cup == null || cup.Owner != customer || cup.IsEmpty) continue;
            if (held != null)
                return "Put your spare empty cup back at the cup stack with E, so you can collect the finished drink.";
            return $"The {customer.WantedDrinkName} is ready. Approach its cup (or the machine) and press E to pick it up.";
        }
        // A previous customer may have left a cup in the machine. Do not tell
        // the player to load another one into an occupied slot.
        if (machine != null && machine.HasCup)
            return "Clear the cup in the machine first. You can return an unwanted cup to the cup stack with E.";

        ShopInventory stock = ShopInventory.Instance;
        if (customer.CanApologiseForDrink &&
            (stock == null || !stock.CanBrew(wanted) || (held == null && stock.Cups <= 0)))
            return $"Not enough stock. Approach {customer.CustomerName} and press E when the prompt offers an apology. Restock on the end-of-day recap.";
        if (stock != null && !stock.CanBrew(wanted))
            return "There are not enough beans for this order. Restocking is available on the end-of-day recap.";
        if (held != null && held.IsEmpty)
            return $"Take your empty cup to the espresso machine. Press E when it offers to make {customer.WantedDrinkName}.";
        if (stock != null && stock.Cups <= 0)
            return "No cups left in stock. Look for an empty cup you set down; otherwise restock at the end of the day.";
        return "Approach the cup stack and press E to take one empty cup.";
    }

    private string RepairAction(CustomerBrain customer)
    {
        JobBase job = customer.ActiveJob;
        if (job == null) return "Finish the conversation; the customer's item will be placed on the intake shelf.";
        bool inspecting = inspector != null && inspector.FocusedItem == job;
        bool carrying = carry != null && carry.Carried == job;

        if (job.IsComplete)
        {
            if (inspecting)
                return inspector.CurrentTool != ToolType.Hand
                    ? "The repair is complete. Right-click to put your tool down, then right-click again to leave inspection."
                    : "The repair is complete. Right-click to leave inspection, then press E on the item to pick it up.";
            if (carrying)
            {
                if (interactor != null && interactor.IsAtStation)
                    return "Press F to step back, then carry the repaired item to its customer and press E at Hand it back.";
                return $"Take the repaired item to {customer.CustomerName}. Press E when the prompt says Hand it back.";
            }
            return "Pick up the repaired item with E. It must be in your hands when you return it to its customer.";
        }

        if (inspector != null && inspector.IsHoldingItem && !inspecting)
            return "You are inspecting a different item. Right-click to put down any tool, then right-click to leave inspection.";
        if (carrying)
        {
            if (interactor != null && interactor.IsAtStation)
                return "Press F to step back first. Then approach the repair bench and press E at Set down to place the item.";
            return "Carry the item to the repair bench. Press E when the prompt says Set down.";
        }
        if (carry != null && carry.IsCarrying)
            return "Free your hands first: set your item on a free surface, or return your cup to the cup stack.";
        if (inspecting)
        {
            if (job.Quality >= 0.999f && job.HasDetachedParts)
                return "The faults are fixed. Use the pry tool to refit the cover, then the screwdriver to refit the screws from the tray.";
            if (!string.IsNullOrEmpty(inspector.HoverAction) && inspector.HoverAction != "Not yet")
                return $"{inspector.HoverName}: {inspector.HoverAction}. Left-click to act; with the brush, hold left-click and move over grime.";
            return "Hover over a screw, cover, or faulty part to see its needed tool. Click the tool, then the part. Right-click puts the tool down.";
        }

        bool onBench = false;
        if (drops != null)
            foreach (DropSpot drop in drops)
                if (drop != null && drop.Kind == DropSpot.SpotKind.Bench && drop.Holds(job))
                    onBench = true;
        if (!onBench)
            return "Pick up this customer's item from the intake shelf (or where you set it down) with E.";
        if (interactor != null && interactor.CurrentStation != null && !interactor.CurrentStation.IsWorkSurface)
            return "Press F to step back from this station. Walk to the repair bench, then press F to work there.";
        if (interactor == null || interactor.CurrentStation == null || !interactor.CurrentStation.IsWorkSurface)
            return "The item is on the bench. Approach the repair bench and press F to work there.";
        return "Aim the centre crosshair at your item on the bench, then left-click to inspect it.";
    }

    private bool CreatePanel()
    {
        Canvas canvas = sourceText.GetComponentInParent<Canvas>();
        if (canvas == null || canvas.rootCanvas.renderMode == RenderMode.WorldSpace) return false;
        canvas = canvas.rootCanvas;
        panel = new GameObject("Day 1 next action (runtime)", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.SetParent(canvas.transform, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -24f);
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(Mathf.Min(620f, canvasRect.rect.width * 0.46f), 120f);
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

    private void OnDisable() => Show(false);
    private void OnDestroy()
    {
        if (panel != null) Destroy(panel);
    }
}
