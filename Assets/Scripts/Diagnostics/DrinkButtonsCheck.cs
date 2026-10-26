using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

// ---------------------------------------------------------------------------
// THE DRINKS MACHINE'S BUTTONS: A PLAY CHECK, 26 Oct 2026 (Drink buttons)
//
// Fixit Fidget > Drinks > Lab - The machine's buttons (drives itself). His 26 Oct note: the paddles were "just words on a
// square in different colors". They are pictures in relief now (DrinkButtonsSteps, Tools/Blender/drink_buttons.py), no
// words, so his girlfriend (dyslexic, on a PlayStation pad) tells the drinks apart by picture first and colour second. A
// fresh Day 1 in the lab, first person at the dispenser's stand point (the café's own arrivals stopped), a virtual
// gamepad:
//
//   1. the whole machine from the counter, as a player walking up sees it (aimed at the middle of its working face);
//   2. each button in turn: the crosshair on it (the line names its drink: "No cup: pouring wastes the Latte"), a photo
//      AT REST; A pressed, so it pours onto the tray (a deliberate cup-less pour, as BeverageSlot allows: the beans are
//      spent) and the cap sinks its 9 mm, a photo PRESSED AND POURING with the "Pouring <Drink>" line and the gold ring;
//      then the lab's hand stops the pour (nothing is in a cup) and the cap comes back. The last button (Latte) is played
//      the whole way, as the Stations lab plays a drink: a cup taken (LB), set under its nozzle (A), the line "(A) Pour
//      Latte" on the button, the pour into the cup.
//
// What it checks: the cap and its picture are under every paddle and the old word is gone; the six pictures are six
// different shapes and the six caps six different colours (told apart by picture, then colour); on the screen at 1080p the
// cap is at least 80 px tall and the picture at least 42 px (the hit box's own bar is 96, DispenserFrameCheck); the press
// moves the cap at least 8 mm and it returns; the prompt names the drink; and in every photo, measured by the review
// screen, any word on the machine itself and the prompt's own word are at least 26 px (the floor; the old printed words
// ran 16 to 30 px, art finding a10). Photos and report.txt go to Logs/Drinks/drink-buttons-<time>/. Nothing is saved.
// Last line: "[Drink buttons] All N checks passed." or "[Drink buttons] N of M checks FAILED. <folder>".
// ---------------------------------------------------------------------------
public sealed class DrinkButtonsCheck : PlayLab
{
    public const string PendingKey = "FixitFidget.DrinkButtons.Check";
    const float CapPx = 80f, PicturePx = 42f, WordPx = 26f, Press = .008f;

    protected override string Title => "The machine's buttons - play check, first person";
    protected override string Tag => "[Drink buttons]";

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartFromTheEditor()
    {
        if (PlayerPrefs.GetInt(PendingKey, 0) == 0) return;
        PlayerPrefs.DeleteKey(PendingKey);
        PlayerPrefs.Save();
        if (!CafeLab.Active)
        {
            Debug.LogWarning("[Drink buttons] The check was asked for outside a lab session; it only runs in the lab.");
            return;
        }
        var go = new GameObject("Drink buttons check (this Play session only)");
        var check = go.AddComponent<DrinkButtonsCheck>();
        check.folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Drinks",
            $"drink-buttons-{DateTime.Now:yyyy-MM-dd_HHmmss}"));
    }
#endif

    PlayerMovement movement;
    PlayerInteractor interactor;
    CafeViewMode view;
    CustomerSpawner spawner;
    bool spawnerWas = true, aimWas, aimTaken;
    readonly List<BeverageSlot> pouring = new List<BeverageSlot>();

    protected override IEnumerator Run()
    {
        yield return Until(() => DayClock.Instance != null && !DayClock.Instance.DayOver && DayClock.Instance.IsOpen && Camera.main != null,
            30f, "the lab's day is open");
        if (!lastWait) yield break;
        movement = FindAnyObjectByType<PlayerMovement>();
        interactor = movement != null ? movement.GetComponent<PlayerInteractor>() : null;
        view = movement != null ? movement.GetComponent<CafeViewMode>() : null;
        spawner = FindAnyObjectByType<CustomerSpawner>();
        Check(movement != null && interactor != null && view != null, "Ace (his walking, reach and view) is in the scene");
        if (interactor == null || view == null) yield break;
        StationInteractable drinks = interactor.DrinksStation;
        Check(drinks != null && drinks.StandPoint != null, "the interactor knows the dispenser and its stand point");
        if (drinks == null || drinks.StandPoint == null) yield break;

        // The café's own arrivals stop: nobody walks into the photos or calls Ace away.
        if (spawner != null) { spawnerWas = spawner.enabled; spawner.enabled = false; }
        foreach (CustomerBrain c in FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude)) Destroy(c.gameObject);
        aimWas = AimAssist.Enabled;
        aimTaken = true;
        AimAssist.Enabled = true;
        PlugInPad("Drink buttons check pad");
        yield return Frames(4);
        if (!view.FirstPersonSelected) view.SetFirstPerson(true);
        yield return Seconds(1.2f);
        Check(view.FirstPersonSelected, "the view is first person");

        // ---------- the machine's parts ----------
        var slots = drinks.GetComponentsInChildren<BeverageSlot>().Where(s => s.drink != null).ToList();
        var motions = drinks.GetComponentsInChildren<BeveragePaddleMotion>(true).ToList();
        Check(slots.Count == 6 && motions.Count == 6, $"the dispenser has six drinks and six paddles ({slots.Count}, {motions.Count})");
        var buttons = new List<(BeverageSlot slot, BeverageControl control, BeveragePaddleMotion motion, Renderer cap, Renderer picture)>();
        foreach (BeverageSlot slot in slots)
        {
            BeverageControl control = slot.GetComponentsInChildren<BeverageControl>().FirstOrDefault(c => c.dispenseButton);
            BeveragePaddleMotion motion = motions.FirstOrDefault(m => m.slot == slot);
            Renderer cap = motion != null ? Part(motion.transform, "DB_Cap_") : null;
            Renderer picture = motion != null ? Part(motion.transform, "DB_Picture_") : null;
            Check(control != null && motion != null && cap != null && picture != null,
                $"{slot.drink.drinkName}: its hit box, its paddle, the cap and the picture are there " +
                $"({(control != null ? "hit box" : "NO hit box")}, {(motion != null ? "paddle" : "NO paddle")}, {(cap != null ? cap.name : "NO cap")}, {(picture != null ? picture.name : "NO picture")})");
            if (control == null || motion == null || cap == null || picture == null) continue;
            var words = motion.GetComponentsInChildren<TMP_Text>(false).ToList();
            Check(words.Count == 0, $"{slot.drink.drinkName}: no printed word is left on the button ({words.Count}; the picture says it)");
            var old = motion.GetComponent<MeshRenderer>();
            Check(old == null || !old.enabled, $"{slot.drink.drinkName}: the old tile is hidden");
            buttons.Add((slot, control, motion, cap, picture));
        }
        if (buttons.Count == 6)
        {
            // Told apart by picture first: six different shapes, never one mesh in six colours. Then by colour: six materials.
            var shapes = buttons.Select(b => { var f = b.picture.GetComponent<MeshFilter>(); return f != null && f.sharedMesh != null ? f.sharedMesh.name + "/" + f.sharedMesh.vertexCount : "none"; }).ToList();
            Check(shapes.Distinct().Count() == 6 && !shapes.Contains("none"), "the six pictures are six different shapes (" + string.Join(", ", shapes.Select(s => s.Split('/')[0].Replace("DB_Picture_", ""))) + ")");
            var colours = buttons.Select(b => b.cap.sharedMaterial != null ? b.cap.sharedMaterial.name : "none").ToList();
            Check(colours.Distinct().Count() == 6 && !colours.Contains("none"), "the six caps are six different colours (" + string.Join(", ", colours) + ")");
        }

        // ---------- at the machine ----------
        Transform stand = drinks.StandPoint;
        PutAce(stand.position, stand.eulerAngles.y);
        yield return Frames(4);
        Check(!interactor.IsAtStation, "in first person nothing is stepped up to at the dispenser");
        BeverageStation line = drinks.GetComponent<BeverageStation>();

        // 1. the whole machine from the counter (the Stations lab's own framing: the middle of every control)
        Bounds face = new Bounds(Point(buttons.Count > 0 ? buttons[0].control : (Component)drinks), Vector3.zero);
        foreach (BeverageControl c in drinks.GetComponentsInChildren<BeverageControl>()) face.Encapsulate(Point(c));
        yield return LookAt(face.center);
        yield return Photo("0-the-whole-machine-from-the-counter");
        yield return WordsInPhoto("0-the-whole-machine-from-the-counter", null);
        foreach (var b in buttons)
            Check(ScreenHeight(b.cap) >= CapPx * .85f, $"{b.slot.drink.drinkName}: from the counter its cap is {ScreenHeight(b.cap):0} px tall at 1080p ({CapPx * .85f:0} at least, all six in one view)");

        // 2. each button: at rest, then pressed and pouring (the last one with a cup under its nozzle, played the whole way)
        PlayerCarry carry = movement.GetComponent<PlayerCarry>();
        BeverageCupSupply stack = drinks.GetComponentsInChildren<BeverageCupSupply>().FirstOrDefault(s => !s.discard);
        int n = 0;
        foreach (var b in buttons)
        {
            n++;
            string drink = b.slot.drink.drinkName;
            string label = $"{n}-{drink.ToLowerInvariant().Replace(" ", "-")}";
            bool withCup = n == buttons.Count && carry != null && stack != null;
            if (withCup)
            {
                BeverageControl zone = b.slot.GetComponentsInChildren<BeverageControl>().FirstOrDefault(c => !c.dispenseButton);
                yield return LookAt(Point(stack));
                yield return Until(() => interactor.Focused == stack, 2f, $"{drink}, with a cup: the crosshair is on the cup stack");
                yield return PadPress(GamepadButton.LeftShoulder);
                yield return Until(() => carry.GetHandItem(0) is DrinkJob, 2f, $"{drink}, with a cup: LB takes a cup in the left hand");
                DrinkJob cup = carry.GetHandItem(0) as DrinkJob;
                if (zone != null && cup != null)
                {
                    yield return LookAt(Point(zone));
                    yield return Until(() => interactor.Focused == zone, 2f, $"{drink}, with a cup: the crosshair is under its nozzle");
                    yield return PadPress(GamepadButton.South);
                    yield return Until(() => b.slot.Cup == cup, 2f, $"{drink}, with a cup: A sets the cup under its nozzle");
                }
                withCup = b.slot.Cup != null;
            }
            yield return LookAt(Point(b.control));
            yield return Until(() => interactor.Focused == b.control, 2f, $"{drink}: the crosshair is on its button");
            yield return Frames(2);
            Check(ScreenHeight(b.cap) >= CapPx && ScreenHeight(b.picture) >= PicturePx,
                $"{drink}: aimed at, the cap is {ScreenHeight(b.cap):0} px tall and its picture {ScreenHeight(b.picture):0} px at 1080p ({CapPx:0} and {PicturePx:0} at least)");
            if (line != null)
            {
                // With a cup under the nozzle the button's line is "(A) Pour Latte"; with none it warns, and still names the drink.
                string plain = RecapPhone.Plain(line.ActionText);
                Check(withCup ? line.ActionText == ControlHints.Interact + "  Pour " + drink : plain.Contains(drink),
                    $"{drink}: the button's line names it{(withCup ? " with its button" : "")}: \"{plain}\"");
            }
            Vector3 rest = b.motion.transform.localPosition;
            yield return Photo(label + "-1-at-rest");
            yield return WordsInPhoto(label + "-1-at-rest", drink);

            int beans = ShopInventory.Instance != null ? ShopInventory.Instance.Beans : -1;
            yield return PadPress(GamepadButton.South);
            yield return Until(() => b.slot.IsPouring, 2f, $"{drink}: A on its button starts the pour (beans before: {beans})");
            if (!b.slot.IsPouring) continue;
            pouring.Add(b.slot);
            yield return Seconds(.35f);   // BeveragePaddleMotion reaches the bottom of its travel in about a ninth of a second
            float sunk = Vector3.Distance(b.motion.transform.localPosition, rest) * b.motion.transform.parent.lossyScale.x;
            Check(sunk >= Press, $"{drink}: pressed, the cap sinks {sunk * 1000:0.0} mm ({Press * 1000:0} at least; 9 built)");
            if (line != null)
                Check(line.RingShowing && line.ActionText == "Pouring " + drink, $"{drink}: pouring, the line says \"{line.ActionText}\" with the gold ring ({line.RingFill:P0})");
            yield return Photo(label + "-2-pressed-and-pouring");
            yield return WordsInPhoto(label + "-2-pressed-and-pouring", drink);
            if (withCup)
            {
                // Played the whole way: the pour ends in the cup, the cap comes back by itself.
                yield return Until(() => !b.slot.IsPouring && b.slot.Cup != null && !b.slot.Cup.IsEmpty, b.slot.drink.brewSeconds * 1.6f + 6f, $"{drink}: the drink is poured into the cup");
                pouring.Remove(b.slot);
            }
            else
            {
                // The lab's hand stops the pour (no cup: nothing to strand; the beans were spent at the press), the cap comes back.
                b.slot.CancelForDayClose();
                pouring.Remove(b.slot);
            }
            yield return Until(() => Vector3.Distance(b.motion.transform.localPosition, rest) * b.motion.transform.parent.lossyScale.x < .001f, 1.5f,
                $"{drink}: let go, the cap comes back to rest");
        }
    }

    protected override void Restore()
    {
        foreach (BeverageSlot slot in pouring) if (slot != null) slot.CancelForDayClose();
        pouring.Clear();
        if (interactor != null && interactor.IsAtStation) interactor.ExitStation();
        if (aimTaken) AimAssist.Enabled = aimWas;
        if (spawner != null) spawner.enabled = spawnerWas;
    }

    // ---------- the words in a photo ----------

    // The review screen measured every word on the screen as the photo was taken (PlayLab.Photo -> ReviewScreen.Scan ->
    // <name>.text.json). Any word on the machine itself (a world text under the dispenser) and the prompt's own word for this
    // drink must be at least 26 px at 1080p. With the review screen off there is no measure, and that is a failure to see.
    IEnumerator WordsInPhoto(string name, string drink)
    {
        yield return null;   // the file is written in the same end of frame as the photo; read it on the next
        string path = Path.Combine(folder, name + ".text.json");
        if (!File.Exists(path))
        {
            Check(false, $"{name}: the photo's words were measured (no {name}.text.json: is the review screen off?)");
            yield break;
        }
        ReviewScreen.PhotoCheck photo;
        try { photo = JsonUtility.FromJson<ReviewScreen.PhotoCheck>(File.ReadAllText(path)); }
        catch (Exception e) { Check(false, $"{name}: the photo's words were read ({e.Message})"); yield break; }
        var onMachine = photo.items.Where(t => t.kind == "world" && (t.path.Contains("Beverage dispenser") || t.path.Contains("ceramic paddle") || t.path.Contains("/Printed "))).ToList();
        var small = onMachine.Where(t => t.px1080 < WordPx).ToList();
        Check(small.Count == 0, $"{name}: any word on the machine is at least {WordPx:0} px ({onMachine.Count} on it" +
            (small.Count > 0 ? "; too small: " + string.Join(", ", small.Select(t => $"\"{t.text}\" {t.px1080:0} px")) : "") + ")");
        if (drink == null) yield break;
        var prompt = photo.items.Where(t => t.kind != "world" && t.text.Contains(drink) && t.text.IndexOf("pour", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        Check(prompt.Count > 0 && prompt.All(t => t.px1080 >= WordPx),
            $"{name}: the prompt's word for {drink} is on the screen at {WordPx:0} px or more (" +
            (prompt.Count > 0 ? string.Join(", ", prompt.Select(t => $"\"{t.text}\" {t.px1080:0} px")) : "not measured") + ")");
    }

    // ---------- measuring ----------

    // A renderer's height on a 1080-pixel-tall screen: its box's eight corners through the main camera.
    static float ScreenHeight(Renderer r)
    {
        Camera cam = Camera.main;
        if (cam == null || r == null) return 0f;
        Bounds b = r.bounds;
        float lo = float.MaxValue, hi = float.MinValue;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
            Vector3 p = cam.WorldToScreenPoint(corner);
            if (p.z <= 0f) return 0f;
            lo = Mathf.Min(lo, p.y);
            hi = Mathf.Max(hi, p.y);
        }
        return (hi - lo) * 1080f / Mathf.Max(1, Screen.height);
    }

    // The cap or the picture under a paddle, by the name the step gives it (DrinkButtonsSteps: DB_Cap_<Name>, DB_Picture_<Name>).
    static Renderer Part(Transform paddle, string prefix) =>
        paddle.GetComponentsInChildren<Renderer>(false).FirstOrDefault(r => r.name.StartsWith(prefix, StringComparison.Ordinal) && r.enabled);

    static Vector3 Point(Component c)
    {
        var col = c.GetComponentInChildren<Collider>();
        return col != null ? col.bounds.center : c.transform.position;
    }

    // ---------- moving and looking (as the Stations lab does it) ----------

    void PutAce(Vector3 at, float yaw)
    {
        var cc = movement.GetComponent<CharacterController>();
        at.y = movement.transform.position.y;
        bool was = cc != null && cc.enabled;
        if (cc != null) cc.enabled = false;
        movement.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
        if (cc != null) cc.enabled = was;
        view.LookTo(yaw, 10f);
    }

    // First person: turn the view onto a point (as a mouse would).
    IEnumerator LookAt(Vector3 point)
    {
        Vector2 a = AimAssist.Angles(Eye(), point);
        view.LookTo(a.x, a.y);
        yield return Frames(3);
    }

    // Ace's first-person eye where he stands (the camera trails a frame: worked out from the view instead).
    Vector3 Eye()
    {
        Vector3 at = movement.transform.position;
        float floor = at.y - 1f;
        return new Vector3(at.x, floor + view.eyeHeight - movement.EyeDrop, at.z);
    }
}
