using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

// ---------------------------------------------------------------------------
// THE RECAP PHONE CHECK (a lab session only: Fixit Fidget > Recap phone > Play check (lab, drives itself))
//
// From the recap of a made-up Day 3 (RecapPhoneSteps writes the lab save), the phone is checked the way a
// player uses it:
//   1. it is up and the scene's three-column recap isn't; the HUD hides behind it; its button is the
//      recap's button (what the Night 1 checks press) and reads "Close up for the night";
//   2. Reviews: a card for every review, newest first, the summary, no average; Details opens and closes;
//   3. the badges: Franchise's dot (a star was earned today), Shop's "!" (beans are low), Notes' count;
//   4. switching apps by key (E, Q, 3, the arrows) and by clicking a tab; Franchise, Shop and Notes read right;
//   5. Shop: a restock and an upgrade bought with clicks, saved at once (in the lab save), the badge gone
//      and the prices moved on;
//   6. W/S scroll, and a pad (a virtual one, added for the check and removed after): LB/RB, the right
//      stick, the D-pad and A, and the gold ring that shows the pad's selection;
//   7. Close up puts the phone away, and the night begins.
// A key that never reaches the game (the Game view didn't have the keyboard) is noted and done directly,
// so the phone is still checked. A photo of every app and report.txt go to the check's folder. Nothing is
// saved in the scene; the lab save is the only file written (by the game), and the playtest save is checked
// to be untouched.
// ---------------------------------------------------------------------------
public sealed class RecapPhoneCheck : MonoBehaviour
{
    public const string PendingKey = "FixitFidget.RecapPhone.Check";

    public string folder;

    const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    readonly StringBuilder report = new StringBuilder();
    int checks, failures, photos;
    float started;
    bool lastWait;
    RecapPhone phone;
    RecapUI recap;
    Gamepad pad;
    static readonly Vector3[] corners = new Vector3[4];

#if UNITY_EDITOR
    // Asked for by the editor (RecapPhoneSteps) for this lab Play session: start once the scene is up.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartFromTheEditor()
    {
        if (PlayerPrefs.GetInt(PendingKey, 0) == 0) return;
        PlayerPrefs.DeleteKey(PendingKey);
        PlayerPrefs.Save();
        if (!CafeLab.Active)
        {
            Debug.LogWarning("[Recap phone check] The check was asked for outside a lab session; it only runs in the lab.");
            return;
        }
        var go = new GameObject("Recap phone check (this Play session only)");
        go.AddComponent<RecapPhoneCheck>().folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Recap",
            $"recap-phone-check-{DateTime.Now:yyyy-MM-dd_HHmmss}"));
    }
#endif

    // Runs the steps below; nested steps run in place, and an exception ends the check with a failure
    // rather than leaving it hanging.
    IEnumerator Start()
    {
        started = Time.realtimeSinceStartup;
        if (string.IsNullOrEmpty(folder))
            folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Recap", $"recap-phone-check-{DateTime.Now:yyyy-MM-dd_HHmmss}"));
        Directory.CreateDirectory(folder);
        report.AppendLine("The recap as Ace's phone - play check, lab session");
        report.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        report.AppendLine();
        Debug.Log($"[Recap phone check] Running: about half a minute. Hands off the mouse, keyboard and pad until it reports. {folder}");

        string playtestSave = Path.Combine(Application.persistentDataPath, "playtest-aces-cafe.json");
        DateTime playtestBefore = File.Exists(playtestSave) ? File.GetLastWriteTimeUtc(playtestSave) : DateTime.MinValue;

        var steps = new Stack<IEnumerator>();
        steps.Push(Run());
        while (steps.Count > 0)
        {
            IEnumerator step = steps.Peek();
            bool more;
            try { more = step.MoveNext(); }
            catch (Exception e)
            {
                Check(false, "the check ran to its end (it stopped: " + e.Message + ")");
                Debug.LogException(e);
                break;
            }
            if (!more) { steps.Pop(); continue; }
            if (step.Current is IEnumerator nested) { steps.Push(nested); continue; }
            yield return step.Current;
        }

        DateTime playtestAfter = File.Exists(playtestSave) ? File.GetLastWriteTimeUtc(playtestSave) : DateTime.MinValue;
        Check(playtestAfter == playtestBefore, "the playtest save was never written");
        Finish();
    }

    IEnumerator Run()
    {
        // ---------- the phone is up ----------
        yield return Until(() => DayClock.Instance != null && DayClock.Instance.DayOver && RecapUI.Showing, 15f,
            "the lab opened on a closed day with its recap on screen");
        if (!lastWait) yield break;
        yield return null;   // the phone builds its app before the frame is drawn
        recap = FindAnyObjectByType<RecapUI>();
        phone = recap != null ? recap.Phone : null;
        Check(phone != null, "the recap is Ace's phone (Use Phone is on)");
        SaveManager saves = SaveManager.Instance;
        ShopInventory stock = ShopInventory.Instance;
        ShopEconomy till = ShopEconomy.Instance;
        UpgradeManager upgrades = UpgradeManager.Instance;
        Check(saves != null && stock != null && till != null && upgrades != null, "the scene has its save manager, stock, till and upgrades");
        if (phone == null || saves == null || stock == null || till == null || upgrades == null) yield break;
        ReputationLedger rep = saves.Reputation;
        int day = DayClock.Instance.Day;
        Note($"Day {day}: reputation {rep.Reputation}, {rep.StarsEarned} stars ({rep.StarsBefore} this morning), {rep.ReviewCount} reviews, " +
             $"{rep.Cards.Count} cards; ${till.Money} in the till; cups {stock.Cups}, beans {stock.Beans}; {saves.Notebook.Count} notebook facts.");

        Check(phone.Root.activeInHierarchy, "the phone is on screen");
        GameObject scenePanel = recap.ScenePanel;
        Check(scenePanel != null && scenePanel != phone.Root && !scenePanel.activeInHierarchy, "the scene's three-column recap stays closed");
        Check(Field<GameObject>(recap, "panel") == phone.Root && Field<Button>(recap, "nextDayButton") == phone.CloseButton,
            "the recap's panel and button are the phone's (what the Night 1 checks press)");
        Check(phone.CloseLabel.text == RecapUI.NightLabel, $"its button reads \"{RecapUI.NightLabel}\" (it reads \"{phone.CloseLabel.text}\")");
        Check(HudHidden(), "the HUD hides behind it (money, clock, stock, prompt)");
        Check(Time.timeScale == 0f, "the world holds still");
        string over = DrawnOverThePhone();
        Check(over.Length == 0, "nothing else draws over the phone" + (over.Length == 0 ? "" : $" ({over} does)"));
        Check(phone.Current == RecapPhone.App.Reviews, "it opens on Reviews");
        Check(!PadInput.UsingPad || Note("a pad was the last device used before the check"), "the keyboard and mouse are in use");
        Check(phone.HintText.Contains("Q / E"), $"the hint under it is the keyboard's (\"{phone.HintText}\")");

        // ---------- Reviews ----------
        string reviews = phone.ContentText();
        Check(rep.Cards.Count == 5 && phone.ReviewCardsShown == rep.Cards.Count, $"a card for every review ({phone.ReviewCardsShown} of {rep.Cards.Count})");
        Check(reviews.Contains($"Today: {rep.ReviewCount} new reviews") && reviews.Contains(ReputationRecap.Signed(rep.TodayChange) + " reputation"),
            "the summary: how many reviews, and the reputation they made");
        int newest = rep.Cards.Count > 0 ? reviews.IndexOf(rep.Cards[rep.Cards.Count - 1].line, StringComparison.Ordinal) : -1;
        int oldest = rep.Cards.Count > 0 ? reviews.IndexOf(rep.Cards[0].line, StringComparison.Ordinal) : -1;
        Check(newest >= 0 && oldest > newest, "newest first");
        Check(reviews.Contains("regular") && reviews.Contains("a walk-in") && !reviews.Contains("Walk-in 2"),
            "a regular is tagged, and a walk-in signs \"a walk-in\"");
        Check(reviews.IndexOf("average", StringComparison.OrdinalIgnoreCase) < 0, "no average anywhere");
        Check(reviews.Contains(Weekdays.Label(day).ToUpperInvariant()) && reviews.Contains("$" + DayClock.Instance.Earned), "the takings card: the day (its weekday too), and what it earned");
        Check(Fits(), "every line fits the phone's width");
        yield return Photo("01-reviews");

        Button details = phone.ContentButton("details");
        Check(details != null && Clickable(details), "Details can be clicked (nothing covers it)");
        Click(details);
        yield return null;
        string opened = phone.ContentText();
        Check(phone.DetailsOpen && opened.Contains("People served") && opened.Contains("Closing till") && opened.Contains("Passable"),
            "Details shows the day's full numbers");
        yield return Photo("02-reviews-details");
        Click(phone.ContentButton("details"));
        yield return null;
        Check(!phone.DetailsOpen && !phone.ContentText().Contains("Closing till"), "…and a second click hides them");

        // ---------- the badges ----------
        int fresh = saves.Notebook.LearnedOn(day).Count;
        Check(rep.EarnedStarToday && phone.BadgeShowing(RecapPhone.App.Franchise) && phone.BadgeText(RecapPhone.App.Franchise) == "",
            "Franchise has a dot: a star was earned today");
        Check(stock.BeansLow && phone.BadgeShowing(RecapPhone.App.Shop) && phone.BadgeText(RecapPhone.App.Shop) == "!",
            "Shop has a \"!\": the beans are low");
        Check(fresh > 0 && phone.BadgeText(RecapPhone.App.Notes) == fresh.ToString(), $"Notes shows how many facts are new today ({fresh})");
        Check(!phone.BadgeShowing(RecapPhone.App.Reviews), "Reviews has no badge");

        // ---------- switching apps by key ----------
        yield return PressKey(Key.E, RecapPhone.App.Franchise, "E opens the next app (Franchise)");
        string franchise = phone.ContentText();
        Check(franchise.Contains(ReputationRules.NameOf(rep.StarsEarned)) && franchise.Contains($"{rep.Reputation} / {rep.NextThreshold} reputation")
              && franchise.Contains("Next: " + ReputationRules.NameOf(rep.StarsEarned + 1)), "Franchise: the café's level, and the way to the next star");
        Check(franchise.Contains("New star!"), "…\"New star!\" on the day one is earned");
        Check(franchise.Contains("REQUESTS FROM HQ") && franchise.Contains($"{rep.NextThreshold} reputation</b> for your third star")
              && franchise.Contains("no scandal") && franchise.Contains("franchise offer"), "HQ's requests: the next star, no scandal, the offer at five");
        string lesson = ReputationRecap.Lesson(rep.Cards);
        Check(franchise.Contains("WHAT CHANGED TODAY") && lesson.Length > 0 && franchise.Contains(lesson), $"what changed today, and its line (\"{lesson}\")");
        Check(Fits(), "every line fits the phone's width");
        yield return Photo("03-franchise");

        yield return PressKey(Key.Q, RecapPhone.App.Reviews, "Q goes back (Reviews)");
        yield return PressKey(Key.Digit3, RecapPhone.App.Shop, "3 jumps to the third app (Shop)");
        string shop = phone.ContentText();
        Check(shop.Contains("In the till: $" + till.Money), "Shop: what's in the till");
        Check(shop.Contains("Paper cups") && shop.Contains("Coffee beans") && shop.Contains($"+{stock.RestockAdds} each"),
            "cups, beans and the restock");
        Check(shop.Contains("Low on beans"), "the low beans are called out");
        Check(shop.IndexOf("parts", StringComparison.OrdinalIgnoreCase) < 0 && shop.IndexOf("tonight", StringComparison.OrdinalIgnoreCase) < 0,
            "no repair parts and no \"find some tonight\" yet (they come with the break-ins)");
        int rows = phone.ContentButtons.Count(b => b != null && phone.ContentButton("upgrade:" + UpgradeName(b)) == b);
        int catalogue = upgrades.Catalogue.Count(d => d != null);
        Check(rows == catalogue, $"a row for every upgrade ({rows} of {catalogue})");
        Check(Fits(), "every line fits the phone's width");
        yield return Photo("04-shop");

        // ---------- clicking a tab ----------
        Check(Clickable(phone.Tab(RecapPhone.App.Notes)), "the Notes tab can be clicked");
        Click(phone.Tab(RecapPhone.App.Notes));
        yield return null;
        Check(phone.Current == RecapPhone.App.Notes, "clicking a tab opens its app (Notes)");
        string notes = phone.ContentText();
        List<NotebookPerson> people = NotebookRecap.People(saves.Notebook);
        Check(people.Count > 0 && notes.Contains(people[0].name), $"Notes, person by person ({people.Count})");
        if (people.Count > 0 && people[0].facts.Count > 1)
        {
            string first = NotebookRecap.Sentence(people[0].facts[0].text), second = NotebookRecap.Sentence(people[0].facts[1].text);
            Check(people[0].facts[0].kind == Notebook.Kinds.Address && notes.IndexOf(first, StringComparison.Ordinal) >= 0
                  && notes.IndexOf(first, StringComparison.Ordinal) < notes.IndexOf(second, StringComparison.Ordinal),
                $"where they live comes first (\"{first}\")");
        }
        Check(notes.Contains("(hunch)"), "a guess says so (\"hunch\")");
        Check(Count(notes, ">NEW<") == fresh, $"today's facts are marked NEW ({Count(notes, ">NEW<")} of {fresh})");
        Check(Fits(), "every line fits the phone's width");
        yield return Photo("05-notes");

        yield return PressKey(Key.RightArrow, RecapPhone.App.Reviews, "the right arrow wraps round to Reviews");
        yield return PressKey(Key.LeftArrow, RecapPhone.App.Notes, "the left arrow goes back to Notes");
        yield return PressKey(Key.Digit3, RecapPhone.App.Shop, "3 goes back to Shop");

        // ---------- buying: a restock, then an upgrade ----------
        string labSave = Path.Combine(Application.persistentDataPath, CafeLab.SaveFileName);
        DateTime labBefore = File.Exists(labSave) ? File.GetLastWriteTimeUtc(labSave) : DateTime.MinValue;
        int cups = stock.Cups, beans = stock.Beans, money = till.Money, adds = stock.RestockAdds, restockCost = stock.RestockCost;
        Button restock = phone.ContentButton("restock");
        Check(restock != null && restock.interactable && Clickable(restock), $"the restock can be bought (${restockCost}, and nothing covers its button)");
        Click(restock);
        yield return null;
        Check(stock.Cups == cups + adds && stock.Beans == beans + adds && till.Money == money - restockCost,
            $"a restock adds {adds} cups and {adds} beans for ${restockCost} (cups {stock.Cups}, beans {stock.Beans}, ${till.Money} left)");
        DateTime labAfter = File.Exists(labSave) ? File.GetLastWriteTimeUtc(labSave) : DateTime.MinValue;
        Check(labAfter > labBefore, "…saved at once (the lab save)");
        Check(!stock.CupsLow && !stock.BeansLow && !phone.BadgeShowing(RecapPhone.App.Shop) && !phone.ContentText().Contains("Low on"),
            "nothing is low now: Shop's \"!\" and the warning are gone");

        UpgradeDefinition cheapest = upgrades.Catalogue.Where(d => d != null && upgrades.CanAfford(d)).OrderBy(d => d.CostAt(upgrades.LevelOf(d))).FirstOrDefault();
        Check(cheapest != null, "an upgrade is affordable (the sample day has the money for one)");
        if (cheapest != null)
        {
            int level = upgrades.LevelOf(cheapest), cost = cheapest.CostAt(level), before = till.Money;
            Button buy = phone.ContentButton("upgrade:" + cheapest.name);
            Check(buy != null && buy.interactable && Clickable(buy), $"{cheapest.upgradeName}'s button can be pressed (${cost})");
            labBefore = labAfter;
            Click(buy);
            yield return null;
            Check(upgrades.LevelOf(cheapest) == level + 1 && till.Money == before - cost, $"buying {cheapest.upgradeName} takes it to level {level + 1} for ${cost}");
            Check((File.Exists(labSave) ? File.GetLastWriteTimeUtc(labSave) : DateTime.MinValue) > labBefore, "…saved at once");
            Button after = phone.ContentButton("upgrade:" + cheapest.name);
            string label = after != null ? after.GetComponentInChildren<TMP_Text>(true).text : "";
            string expected = upgrades.IsMaxed(cheapest) ? "MAX" : "$" + cheapest.CostAt(level + 1);
            Check(phone.ContentText().Contains("Lv." + (level + 1)) && label == expected && after.interactable == upgrades.CanAfford(cheapest),
                $"its row shows the level, and the button the next price, greyed out of reach (\"{label}\")");
            Check(phone.ContentText().Contains("In the till: $" + till.Money), "the till on the phone follows");
        }
        yield return Photo("06-shop-after-buying");

        // ---------- scrolling with W and S, on Reviews (five cards: longer than the window) ----------
        yield return PressKey(Key.Digit1, RecapPhone.App.Reviews, "1 jumps to Reviews");
        Check(phone.ScrollRange > 1f, $"Reviews is longer than the phone's window ({phone.ScrollRange:0} to scroll)");
        float top = phone.ScrollOffset;
        yield return Hold(Key.S, .35f);
        float down = phone.ScrollOffset;
        if (down > top + 1f)
        {
            Check(true, $"S scrolls down ({top:0} to {down:0})");
            yield return Photo("07-reviews-scrolled");
            yield return Hold(Key.W, .6f);
            Check(phone.ScrollOffset < down && phone.ScrollOffset <= .5f, $"W scrolls back up to the top ({phone.ScrollOffset:0})");
        }
        else Note("S didn't reach the game (the Game view didn't have the keyboard?): scrolling by key not checked.");
        yield return PressKey(Key.Digit3, RecapPhone.App.Shop, "3 goes back to Shop");

        // ---------- a pad ----------
        yield return PadSteps(stock, till);

        // ---------- Close up ----------
        yield return PressKey(Key.Digit4, RecapPhone.App.Notes, "4 jumps to Notes");
        Check(Clickable(phone.CloseButton), "Close up can be clicked (nothing covers it)");
        Click(phone.CloseButton);
        yield return Until(() => !RecapUI.Showing, 5f, "Close up for the night puts the phone away");
        yield return Until(() => NightCycle.Instance != null && NightCycle.Instance.Now == NightCycle.Phase.Night, 12f, "…and the night begins");
        yield return Seconds(1.6f);
        Check(!HudHidden(), "the HUD is back");
        yield return Photo("10-night-begins");
        report.AppendLine();
        report.AppendLine(phone.Describe());
    }

    // A virtual pad, added for these steps and removed after: LB/RB switch apps, the pad's hint, the gold
    // ring, the D-pad down and up the app, A to buy, the right stick to scroll.
    IEnumerator PadSteps(ShopInventory stock, ShopEconomy till)
    {
        pad = InputSystem.AddDevice<Gamepad>("Recap phone check pad");
        yield return PadPress(GamepadButton.RightShoulder);
        Check(PadInput.UsingPad && phone.Current == RecapPhone.App.Notes, $"a pad's RB opens the next app (Notes; it's on {phone.Current})");
        Check(phone.HintText.Contains("RB") || phone.HintText.Contains("R1") || phone.HintText.Contains("R "),
            $"the hint under the phone becomes the pad's (\"{phone.HintText}\")");
        yield return PadPress(GamepadButton.LeftShoulder);
        Check(phone.Current == RecapPhone.App.Shop, "LB goes back (Shop)");
        yield return Frames(2);
        GameObject selected = Selected();
        Check(selected == phone.CloseButton.gameObject, $"a pad starts on Close up (on {Name(selected)})");
        Check(phone.FocusRingShowing, "a gold ring shows where the pad is");
        yield return Photo("08-pad-close-up");

        // Up from Close up: the app's last button that can be pressed. With the upgrades out of reach now, that's the restock.
        yield return PadPress(GamepadButton.DpadUp);
        yield return Frames(2);
        Button restock = phone.ContentButton("restock");
        Check(restock != null && Selected() == restock.gameObject, $"the D-pad goes up to the restock (on {Name(Selected())})");
        Check(restock != null && phone.IsFullyVisible((RectTransform)restock.transform) && phone.FocusRingShowing, "…scrolled into view, with the ring on it");
        yield return Photo("09-pad-on-the-restock");
        int cups = stock.Cups, money = till.Money;
        yield return PadPress(GamepadButton.South);
        yield return Frames(2);
        Check(stock.Cups == cups + stock.RestockAdds && till.Money == money - stock.RestockCost, "A buys it");
        Button again = phone.ContentButton("restock");
        Check(again != null && Selected() == again.gameObject, "…and the pad stays on the restock after the phone rebuilds");

        yield return PadPress(GamepadButton.DpadDown);
        yield return PadPress(GamepadButton.DpadDown);
        yield return Frames(2);
        Check(Selected() == phone.Tab(RecapPhone.App.Shop).gameObject, $"down past Close up to the tab bar (on {Name(Selected())})");
        yield return PadPress(GamepadButton.DpadLeft);
        yield return PadPress(GamepadButton.South);
        yield return Frames(2);
        Check(phone.Current == RecapPhone.App.Franchise, "left along the tabs and A opens Franchise");

        yield return PadPress(GamepadButton.LeftShoulder);   // to Reviews, longer than the window
        float before = phone.ScrollOffset;
        InputSystem.QueueStateEvent(pad, new GamepadState { rightStick = new Vector2(0f, -1f) });
        yield return Seconds(.3f);
        InputSystem.QueueStateEvent(pad, new GamepadState());
        yield return Frames(2);
        Check(phone.Current == RecapPhone.App.Reviews && phone.ScrollOffset > before + 1f,
            $"LB to Reviews, and the right stick scrolls it (from {before:0} to {phone.ScrollOffset:0})");

        InputSystem.RemoveDevice(pad);
        pad = null;
        yield return Frames(2);
        if (Gamepad.all.Count == 0) Check(!PadInput.UsingPad && !phone.FocusRingShowing, "with the pad gone, the ring goes too");
        else Note("another pad is connected, so the phone still reads as on a pad");
    }

    // ---------- driving it ----------

    // A key pressed and let go through the Input System, as the player's keyboard does. If it never reaches
    // the game (the Game view didn't have the keyboard), the app is opened directly and that's noted.
    IEnumerator PressKey(Key key, RecapPhone.App expected, string what)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }
        if (phone.Current == expected)
        {
            Check(true, what);
            yield break;
        }
        Note($"{key} didn't reach the game (the Game view didn't have the keyboard?); opening {expected} directly.");
        phone.Open(expected);
        yield return null;
    }

    IEnumerator Hold(Key key, float seconds)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) yield break;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
        yield return Seconds(seconds);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return null;
    }

    IEnumerator PadPress(GamepadButton button)
    {
        if (pad == null) yield break;
        InputSystem.QueueStateEvent(pad, new GamepadState(button));
        yield return null;
        InputSystem.QueueStateEvent(pad, new GamepadState());
        yield return null;
    }

    // A click as the mouse makes one: the pointer's events on the button itself.
    static void Click(Selectable target)
    {
        if (target == null) return;
        var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, position = Centre(target) };
        GameObject go = target.gameObject;
        ExecuteEvents.Execute(go, data, ExecuteEvents.pointerEnterHandler);
        ExecuteEvents.Execute(go, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(go, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(go, data, ExecuteEvents.pointerClickHandler);
        ExecuteEvents.Execute(go, data, ExecuteEvents.pointerExitHandler);
    }

    // True when a click at the middle of target would land on it: the topmost thing there is it (or its label).
    static bool Clickable(Selectable target)
    {
        EventSystem events = EventSystem.current;
        if (target == null || events == null) return false;
        var hits = new List<RaycastResult>();
        events.RaycastAll(new PointerEventData(events) { position = Centre(target) }, hits);
        return hits.Count > 0 && hits[0].gameObject != null && hits[0].gameObject.transform.IsChildOf(target.transform);
    }

    // On an overlay canvas, a point's world position is where it is on the screen.
    static Vector2 Centre(Component target)
    {
        ((RectTransform)target.transform).GetWorldCorners(corners);
        return (corners[0] + corners[2]) / 2f;
    }

    static GameObject Selected() => EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
    static string Name(GameObject go) => go != null ? go.name : "nothing";

    // The upgrade a content button buys, from its name ("Button: upgrade:Upgrade_WideBrush").
    static string UpgradeName(Button button)
    {
        const string prefix = "Button: upgrade:";
        return button.name.StartsWith(prefix, StringComparison.Ordinal) ? button.name.Substring(prefix.Length) : "";
    }

    // ---------- what's on screen ----------

    // The HUD's money, clock, stock and prompt, all hidden behind the recap.
    bool HudHidden()
    {
        ShopUI hud = FindAnyObjectByType<ShopUI>();
        if (hud == null) return false;
        foreach (string field in new[] { "moneyText", "clockText", "stockText", "promptText" })
        {
            TMP_Text text = Field<TMP_Text>(hud, field);
            if (text != null && text.gameObject.activeInHierarchy) return false;
        }
        return true;
    }

    // Anything on a canvas above the phone's that shows over the phone: a graphic that is on, not see-through,
    // and not an empty text. Resources.FindObjectsOfTypeAll also finds what FindObjectsByType can't (objects
    // marked DontSave, left over from an earlier Play session), which is what this is for.
    string DrawnOverThePhone()
    {
        Rect phoneRect = ScreenRect(phone.PhoneRect);
        foreach (Graphic graphic in Resources.FindObjectsOfTypeAll<Graphic>())
        {
            if (graphic == null || !graphic.isActiveAndEnabled || graphic.transform.IsChildOf(phone.transform)) continue;
            Canvas canvas = graphic.canvas;
            if (canvas == null || !canvas.isActiveAndEnabled || canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                || canvas.rootCanvas.sortingOrder <= RecapPhone.SortingOrder) continue;
            if (graphic.color.a * graphic.canvasRenderer.GetInheritedAlpha() < .02f) continue;
            if (graphic is TMP_Text text && string.IsNullOrWhiteSpace(text.text)) continue;
            if (ScreenRect(graphic.rectTransform).Overlaps(phoneRect))
                return $"\"{graphic.name}\" on \"{canvas.rootCanvas.name}\" (sorting order {canvas.rootCanvas.sortingOrder})";
        }
        return "";
    }

    // On an overlay canvas, where a box is on the screen.
    static Rect ScreenRect(RectTransform rect)
    {
        rect.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
    }

    // Every line of the current app sits inside the phone's window, side to side.
    bool Fits()
    {
        RectTransform window = phone.Viewport;
        float width = window.rect.width;
        foreach (TMP_Text text in window.GetComponentsInChildren<TMP_Text>(false))
        {
            if (string.IsNullOrEmpty(text.text)) continue;
            text.rectTransform.GetWorldCorners(corners);
            float left = window.InverseTransformPoint(corners[0]).x - window.rect.xMin;
            float right = window.InverseTransformPoint(corners[2]).x - window.rect.xMin;
            if (left < -.5f || right > width + .5f)
            {
                Note($"\"{text.text}\" runs outside the phone ({left:0} to {right:0} of {width:0})");
                return false;
            }
        }
        return true;
    }

    static int Count(string text, string part)
    {
        int n = 0;
        for (int at = text.IndexOf(part, StringComparison.Ordinal); at >= 0; at = text.IndexOf(part, at + part.Length, StringComparison.Ordinal)) n++;
        return n;
    }

    static T Field<T>(object owner, string name) where T : class
    {
        if (owner == null) return null;
        for (Type type = owner.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(name, Any);
            if (field != null) return field.GetValue(owner) as T;
        }
        return null;
    }

    // ---------- waiting, photos and the report ----------

    IEnumerator Until(Func<bool> condition, float seconds, string what)
    {
        float until = Time.realtimeSinceStartup + seconds;
        while (!Safe(condition) && Time.realtimeSinceStartup < until) yield return null;
        lastWait = Safe(condition);
        Check(lastWait, what + (lastWait ? "" : $" (gave up after {seconds:0} s)"));
    }

    static bool Safe(Func<bool> condition)
    {
        try { return condition(); }
        catch (Exception) { return false; }
    }

    static IEnumerator Seconds(float seconds)
    {
        float until = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < until) yield return null;
    }

    static IEnumerator Frames(int count)
    {
        for (int i = 0; i < count; i++) yield return null;
    }

    IEnumerator Photo(string name)
    {
#if UNITY_EDITOR
        // The editor draws a shader it is still compiling in plain cyan (the first time the phone's screen is
        // drawn on a machine, say): wait for it, then one more frame drawn with the real thing.
        float until = Time.realtimeSinceStartup + 10f;
        while (UnityEditor.ShaderUtil.anythingCompiling && Time.realtimeSinceStartup < until) yield return null;
        yield return null;
#endif
        yield return new WaitForEndOfFrame();
        Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
        photos++;
        File.WriteAllBytes(Path.Combine(folder, name + ".jpg"), shot.EncodeToJPG(90));
        Destroy(shot);
    }

    void Check(bool ok, string what)
    {
        checks++;
        if (!ok) failures++;
        report.AppendLine($"{Time.realtimeSinceStartup - started,6:0.0}s  {(ok ? "PASS" : "FAIL")}  {what}");
    }

    // A note in the report; true, so it can stand in a check's condition ("fine, but worth saying").
    bool Note(string what)
    {
        report.AppendLine($"{Time.realtimeSinceStartup - started,6:0.0}s  note  {what}");
        return true;
    }

    void Finish()
    {
        if (pad != null)
        {
            InputSystem.RemoveDevice(pad);
            pad = null;
        }
        report.AppendLine();
        report.AppendLine($"{checks} checks, {failures} failed; {photos} photos; {Time.realtimeSinceStartup - started:0} s.");
        string text = report.ToString();
        try { File.WriteAllText(Path.Combine(folder, "report.txt"), text); }
        catch (Exception e) { Debug.LogWarning("[Recap phone check] Could not write the report: " + e.Message); }
        if (failures == 0) Debug.Log($"[Recap phone check] All {checks} checks passed. {folder}\n{text}");
        else Debug.LogError($"[Recap phone check] {failures} of {checks} checks FAILED. {folder}\n{text}");
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (pad != null) InputSystem.RemoveDevice(pad);
    }
}
