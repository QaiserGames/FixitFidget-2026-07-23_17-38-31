using UnityEngine;

// A physical section owns its cup and pour, never an order or a customer.
public sealed class BeverageSlot : MonoBehaviour
{
    public DrinkDefinition drink;
    public Transform cupPoint;
    public LineRenderer stream;
    private DrinkJob cup;
    private float pourLeft;
    private float pourDuration;
    private bool pouring;
    private bool caughtPour;
    private BeveragePourAudio pourAudio;
    private LineRenderer focusOutline;
    private Material focusMaterial;
    public bool IsPouring => pouring;
    public float Progress => pouring ? Mathf.Clamp01(1 - pourLeft / Mathf.Max(.1f, pourDuration)) : Cup != null && !Cup.IsEmpty ? 1 : 0;
    public void Release(DrinkJob item) { if (cup == item && !pouring) cup = null; }
    public DrinkJob Cup
    {
        get
        {
            // Pick-up can happen via the cup's own ItemInteractable as well as the pad.
            if (cup != null)
            {
                var carry = FindAnyObjectByType<PlayerCarry>();
                if (carry != null && carry.Contains(cup)) cup = null;
            }
            return cup;
        }
    }
    public string PourPrompt => pouring ? $"Pouring {drink.drinkName}…"
        : drink == null ? "No drink assigned"
        : Cup != null && !Cup.IsEmpty ? "Lift the filled cup first"
        : ShopInventory.Instance == null || !ShopInventory.Instance.CanBrew(drink) ? "Out of ingredients"
        : Cup == null ? $"Dispense {drink.drinkName} — no cup; ingredients will be wasted"
        : $"Dispense {drink.drinkName}";
    public bool TryPour()
    {
        if (pouring || drink == null || (DayClock.Instance != null && DayClock.Instance.DayOver)
            || Time.timeScale <= 0 || Cup != null && !Cup.IsEmpty) return false;
        if (ShopInventory.Instance == null || !ShopInventory.Instance.ConsumeBeans(drink)) return false;
        // Debit exactly once at the button press, even if there is no cup.
        caughtPour = Cup != null;
        if (caughtPour) cup.Locked = true;
        float multiplier = UpgradeManager.Instance != null ? UpgradeManager.Instance.BrewTimeMultiplier : 1;
        pourLeft = pourDuration = Mathf.Max(.1f, drink.brewSeconds * multiplier);
        pouring = true;
        if (Application.isPlaying)
        {
            if (pourAudio == null) pourAudio = GetComponent<BeveragePourAudio>();
            if (pourAudio == null) pourAudio = gameObject.AddComponent<BeveragePourAudio>();
            pourAudio.Begin();
        }
        RefreshPourVisual();
        return true;
    }

    // The generous cup area is the everyday action. The separate paddle still
    // permits deliberate dispensing without a cup, with its explicit waste warning.
    public bool PlaceAndPour(PlayerCarry carry)
    {
        if (pouring) return false;
        if (Cup != null) return cup.IsEmpty ? TryPour() : TransferCup(carry);
        if (carry == null || !(carry.Carried is DrinkJob held) || !held.IsEmpty || held.Locked
            || ShopInventory.Instance == null || !ShopInventory.Instance.CanBrew(drink)) return false;
        return TransferCup(carry) && TryPour();
    }
    public bool TransferCup(PlayerCarry carry)
    {
        if (carry == null || pouring || cupPoint == null || Time.timeScale <= 0
            || DayClock.Instance != null && DayClock.Instance.DayOver) return false;
        if (Cup != null)
        {
            if (!carry.TryPickUp(cup)) return false;
            cup = null; return true;
        }
        if (!(carry.Carried is DrinkJob held) || !held.IsEmpty || held.Locked) return false;
        carry.PlaceAt(cupPoint);
        if (carry.Contains(held)) return false;
        cup = held; cup.SetOwner(null); return true;
    }
    private void Update()
    {
        bool running = Time.timeScale > 0 && (DayClock.Instance == null || !DayClock.Instance.DayOver);
        if (pourAudio != null) pourAudio.SetRunning(pouring && running);
        if (!pouring) return;
        if (!running) { if (stream != null) stream.enabled = false; return; }
        pourLeft -= Time.deltaTime;
        RefreshPourVisual();
        if (pourLeft > 0) return;
        pouring = false;
        // A lost/destroyed cup does not spawn a replacement or refund stock.
        if (caughtPour && cup != null) { cup.SetDrink(drink, true); cup.Locked = false; }
        if (stream != null) stream.enabled = false;
        if (pourAudio != null) pourAudio.Finish(caughtPour && cup != null);
        caughtPour = false;
    }

    private void RefreshPourVisual()
    {
        if (caughtPour && cup != null) cup.ShowPourProgress(drink, Progress, true);
        if (stream == null) return;
        stream.enabled = pouring;
        stream.positionCount = 5;
        Vector3 start = stream.transform.position;
        Vector3 end = caughtPour && cup != null && cup.LiquidVisual != null
            ? cup.LiquidVisual.SurfacePosition : cupPoint != null ? cupPoint.position : start - Vector3.up * .2f;
        Color color = DrinkLiquidVisual.LiquidColor(drink);
        // Vertex colour tints the existing unlit material without allocating per frame.
        stream.startColor = stream.endColor = Color.Lerp(color, Color.white, .30f);
        for (int i = 0; i < 5; i++)
        {
            float t = i / 4f;
            Vector3 point = Vector3.Lerp(start, end, t);
            if (i > 0 && i < 4) point += stream.transform.right * (Mathf.Sin(Time.time * 23 + i * 1.8f) * .0018f);
            stream.SetPosition(i, stream.useWorldSpace ? point : stream.transform.InverseTransformPoint(point));
        }
        stream.startWidth = .0065f;
        stream.endWidth = .009f + Mathf.Sin(Time.time * 29) * .001f;
    }

    public void SetFocused(bool focused)
    {
        if (focused && focusOutline == null && cupPoint != null)
        {
            var outlineObject = new GameObject("Selected cup pad");
            outlineObject.transform.SetParent(cupPoint, false);
            focusOutline = outlineObject.AddComponent<LineRenderer>();
            focusOutline.useWorldSpace = false; focusOutline.loop = true; focusOutline.positionCount = 4;
            focusOutline.startWidth = focusOutline.endWidth = .0022f;
            focusMaterial = new Material(Shader.Find("Sprites/Default"));
            focusOutline.sharedMaterial = focusMaterial;
            focusOutline.startColor = focusOutline.endColor = new Color(.55f, 1f, .83f, .9f);
            focusOutline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            focusOutline.SetPositions(new[] { new Vector3(-.060f,.003f,-.058f), new Vector3(-.060f,.003f,.058f),
                new Vector3(.060f,.003f,.058f), new Vector3(.060f,.003f,-.058f) });
        }
        if (focusOutline != null) focusOutline.enabled = focused;
    }
    private void OnDisable()
    {
        // Interrupting a station wastes spent ingredients, but never strands a locked cup.
        pouring = false; caughtPour = false;
        if (cup != null) { cup.Locked = false; cup.ShowPourProgress(cup.Drink, cup.IsEmpty ? 0 : 1, false); }
        if (stream != null) stream.enabled = false;
        if (pourAudio != null) pourAudio.Finish(false);
        SetFocused(false);
    }
    private void OnDestroy()
    {
        if (focusMaterial == null) return;
        if (Application.isPlaying) Destroy(focusMaterial); else DestroyImmediate(focusMaterial);
    }
}
