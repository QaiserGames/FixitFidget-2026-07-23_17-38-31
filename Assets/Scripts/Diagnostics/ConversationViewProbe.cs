using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Cinemachine;
using UnityEngine;

// ---------------------------------------------------------------------------
// THE CONVERSATION'S VIEW AND ITS REPLIES: A PLAY CHECK (12 Oct 2026; a diagnostic probe the night before)
//
// Mansoor's playtest (12 Oct): "when talking to the customer to the far left, lets say im right infront of this customer
// whose on the left side, even if im looking right at him and talk, the camera angle shifts to the right and downwards,
// i tried this on all three customers and it feels odd, it should feel like how it feels when talking to anyone in
// skyrim, i also noticed the dialogue choice option placement to be too far to the left". The probe measured the old
// fixed camera's jump (2.2 m right for the customer on the left). Agreed (claude/playtest-4-findings-and-proposals.md §2
// and §3), and checked here:
//
//   * three customers at the counter; Ace stands in front of each in turn (left, middle and right as he sees them), in
//     first person, looking at their face, and talks: the eye moves no more than 5 cm, the lens leans in to 45°, their face
//     sits in the middle band, a little above the middle;
//   * nothing covers their face (the replies' plates, their line, the name card, the keys' hint line), and everything sits
//     inside safe margins (5% of the screen from each edge); the replies are right of the face, the first one lit;
//   * once from the overhead view: the view comes down to just behind Ace's shoulder, Ace is drawn in the shot, his head at
//     the left and clear of their face; when it ends he stays drawn as the view rises (no flicker);
//   * the replies' column laid out for other shapes of screen (16:10, 4:3, 21:9, 32:9) stays inside the margins, right of
//     the face and above their line.
//
// Fixit Fidget > Dialogue > Conversation view - the camera and the replies (lab, a check). Photos and report.txt go to
// Logs/ConversationViewProbe/check-<time>/. Nothing is saved in the scene; the lab's own save only.
// ---------------------------------------------------------------------------
public sealed class ConversationViewProbe : PlayLab
{
    public const string PendingKey = "FixitFidget.ConversationViewProbe";

    protected override string Title => "Conversation view - the camera and the replies";
    protected override string Tag => "[Conversation view check]";

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartFromTheEditor()
    {
        if (PlayerPrefs.GetInt(PendingKey, 0) == 0) return;
        PlayerPrefs.DeleteKey(PendingKey);
        PlayerPrefs.Save();
        if (!CafeLab.Active)
        {
            Debug.LogWarning("[Conversation view check] Asked for outside a lab session; it only runs in the lab.");
            return;
        }
        var go = new GameObject("Conversation view check (this Play session only)");
        go.AddComponent<ConversationViewProbe>().folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "ConversationViewProbe",
            $"check-{DateTime.Now:yyyy-MM-dd_HHmmss}"));
    }
#endif

    const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const float Margin = .05f;

    PlayerMovement movement;
    CafeViewMode view;
    AceBody body;
    ConversationController conversation;
    ConversationUI ui;
    CustomerSpawner spawner;
    bool spawnerWas = true;
    bool shapesChecked;
    readonly List<CustomerBrain> people = new List<CustomerBrain>();

    protected override IEnumerator Run()
    {
        yield return Until(() => DayClock.Instance != null && !DayClock.Instance.DayOver && DayClock.Instance.IsOpen && Camera.main != null,
            30f, "the lab's day is open");
        if (!lastWait) yield break;
        movement = FindAnyObjectByType<PlayerMovement>();
        view = movement != null ? movement.GetComponent<CafeViewMode>() : null;
        body = movement != null ? movement.GetComponent<AceBody>() : null;
        conversation = FindAnyObjectByType<ConversationController>();
        ui = FindAnyObjectByType<ConversationUI>();
        spawner = FindAnyObjectByType<CustomerSpawner>();
        Check(movement != null && view != null && conversation != null && ui != null, "Ace, the conversation and its panel are in the scene");
        if (movement == null || view == null || conversation == null || ui == null) yield break;
        Note($"the screen {Screen.width} x {Screen.height}");

        // Three customers at the counter at once.
        if (spawner != null) { spawnerWas = spawner.enabled; spawner.enabled = false; }
        foreach (CustomerBrain c in FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude)) Destroy(c.gameObject);
        yield return Frames(3);
        int sent = CafeLabDirector.Instance != null ? CafeLabDirector.Instance.SendCustomersNow(3) : 0;
        Check(sent == 3, $"three customers sent in together ({sent})");
        FieldInfo patienceLeft = typeof(CustomerBrain).GetField("patienceLeft", Any), queueMax = typeof(CustomerBrain).GetField("queueMax", Any);
        yield return Until(() =>
        {
            people.Clear();
            foreach (CustomerBrain c in FindObjectsByType<CustomerBrain>(FindObjectsInactive.Exclude))
            {
                if (c == null || c.IsLeaving) continue;
                if (patienceLeft != null && queueMax != null) patienceLeft.SetValue(c, queueMax.GetValue(c));   // the lab's hand: they wait
                if (c.CanHearIntake && Settled(c)) people.Add(c);
            }
            return people.Count >= 3;
        }, 90f, "three customers wait at the counter");
        if (people.Count == 0) yield break;
        yield return Seconds(1.5f);
        Note($"Ace's body: {(body != null && body.Worn ? body.BodyName : "not worn (the capsule)")}");

        // Left to right as Ace sees them: he faces the room across the counter.
        float aceYaw = 180f;
        CounterStand(out Vector3 stand, ref aceYaw);
        Vector3 aceRight = Quaternion.Euler(0f, aceYaw, 0f) * Vector3.right;
        List<CustomerBrain> order = people.OrderBy(c => Vector3.Dot(c.transform.position, aceRight)).ToList();
        string[] sides = { "left", "middle", "right" };
        for (int i = 0; i < order.Count && i < 3; i++)
            yield return Talk(order[i], i + 1 + "-" + sides[i], stand, aceYaw, true);

        // Once from above.
        if (order.Count > 1) yield return Talk(order[1], "4-middle-from-above", stand, aceYaw, false);
        view.SetFirstPerson(false);
    }

    // Standing still in their counter slot (not walking up to it).
    static bool Settled(CustomerBrain c)
    {
        var agent = c.GetComponent<UnityEngine.AI.NavMeshAgent>();
        return agent == null || !agent.enabled || agent.velocity.sqrMagnitude < .01f;
    }

    // Where Ace stands to serve, and which way he faces (the counter's stand point).
    void CounterStand(out Vector3 at, ref float yaw)
    {
        at = movement.transform.position;
        foreach (StationInteractable s in StationInteractable.All)
            if (s != null && !s.IsWorkSurface && s.StandPoint != null && s.name == "Counter") { at = s.StandPoint.position; yaw = s.StandPoint.eulerAngles.y; return; }
    }

    IEnumerator Talk(CustomerBrain who, string name, Vector3 stand, float aceYaw, bool firstPerson)
    {
        // (From above it's the middle customer again: their request heard, still to be decided. Begin takes either.)
        if (who == null || !who.CanHearIntake && !who.CanDecide) { Check(false, $"{name}: they can be spoken to"); yield break; }
        Transform face = who.LookTarget != null ? who.LookTarget : who.transform;
        Note($"---------- {name}: {who.CustomerName} ----------");
        view.SetFirstPerson(firstPerson);
        // Right in front of them, at the counter's stand line.
        Vector3 forward = Quaternion.Euler(0f, aceYaw, 0f) * Vector3.forward;
        Vector3 at = stand + Vector3.ProjectOnPlane(who.transform.position - stand, forward);
        at.y = movement.transform.position.y;
        PutAce(at, aceYaw);
        yield return Frames(2);
        Camera cam = Camera.main;
        yield return AfterBlend();
        // Looking at their face.
        Vector3 eye = cam.transform.position;
        Vector3 toFace = face.position - eye;
        float yaw = Mathf.Atan2(toFace.x, toFace.z) * Mathf.Rad2Deg;
        float pitch = -Mathf.Atan2(toFace.y, new Vector2(toFace.x, toFace.z).magnitude) * Mathf.Rad2Deg;
        if (firstPerson) view.LookTo(yaw, pitch);
        yield return Frames(4);
        yield return Seconds(.3f);
        Pose before = new Pose(cam.transform.position, cam.transform.rotation);
        yield return Photo(name + "-a-before-talking");

        conversation.Begin(who);
        Check(conversation.InConversation && conversation.Partner == who, $"{name}: the conversation opens");
        yield return AfterBlend();
        yield return Await(() => conversation.RepliesShowing, 6f);
        yield return Seconds(.5f);   // the replies' fade, and the composer's last settling
        Pose during = new Pose(cam.transform.position, cam.transform.rotation);
        Vector3 moved = Quaternion.Inverse(before.rotation) * (during.position - before.position);
        Vector3 faceOn = cam.WorldToViewportPoint(face.position);
        Rect faceBox = Box(cam, face.position, .1f, .14f);
        Note($"{name}: the eye moved {moved.x * 100f:+0;-0} cm right, {moved.y * 100f:+0;-0} cm up, {moved.z * 100f:+0;-0} cm forward " +
             $"and turned {Quaternion.Angle(before.rotation, during.rotation):0}°; lens {cam.fieldOfView:0}°; their face at {P(faceOn)}");

        if (firstPerson)
            Check((during.position - before.position).magnitude <= .05f,
                $"{name}: the view stays at Ace's eyes ({(during.position - before.position).magnitude * 100f:0.0} cm moved; was 2.2 m for the left customer)");
        else
        {
            Vector3 eyes = view.EyePoint;
            Vector3 flat = Vector3.ProjectOnPlane(face.position - eyes, Vector3.up).normalized;
            float back = Vector3.Dot(during.position - eyes, flat);
            Check(conversation.SeenOverShoulder && view.TalkingOverShoulder,
                $"{name}: from above, it is seen over Ace's shoulder");
            Check(back < -.2f && (during.position - eyes).magnitude < 1f,
                $"{name}: the view came down to just behind Ace ({-back * 100f:0} cm behind his eyes, {(during.position - eyes).magnitude * 100f:0} cm from them)");
            if (body != null && body.Worn)
            {
                Check(body.Drawn, $"{name}: Ace is drawn in the shot ({body.BodyName})");
                Rect head = Box(cam, eyes + Vector3.up * .03f, .11f, .13f);
                Check(!head.Overlaps(faceBox) && head.center.x < faceBox.center.x,
                    $"{name}: his head is at the left, clear of their face (his head {Px(head)}, their face {Px(faceBox)})");
            }
            else Note($"{name}: Ace wears no body here (the capsule), so his shoulder isn't checked");
        }
        Check(Mathf.Abs(cam.fieldOfView - 45f) <= 1.5f, $"{name}: the lens leans in to 45° ({cam.fieldOfView:0.0}°)");
        Check(faceOn.z > 0f && faceOn.x >= .42f && faceOn.x <= .58f && faceOn.y >= .5f && faceOn.y <= .66f,
            $"{name}: their face sits in the middle band, a little above the middle ({P(faceOn)})");

        // What's on the screen: nothing over the face, everything inside the margins, the replies right of the face.
        List<Rect> plates = ui.ReplyPlatesOnScreen();
        Rect line = ui.LineOnScreen, card = ui.NameCardOnScreen, hint = ui.FooterOnScreen;
        Note($"{name}: replies {string.Join("; ", plates.Select(Px))}; their line {Px(line)}; the name card \"{Plain(ui.NameCard)}\" {Px(card)}; the hint {Px(hint)}");
        Check(plates.Count >= 2 && conversation.Highlighted == 0, $"{name}: the replies are up, each on its plate, the first lit ({plates.Count})");
        var everything = new List<Rect>(plates) { line, card, hint };
        everything.RemoveAll(r => r.width <= 0f || r.height <= 0f);
        Check(everything.All(r => !r.Overlaps(faceBox)), $"{name}: nothing covers their face ({Px(faceBox)})");
        Check(everything.All(Inside), $"{name}: everything is inside the safe margins (5% from each edge)");
        Check(plates.All(r => r.xMin > faceBox.xMax), $"{name}: the replies are right of their face");
        Check(Plain(ui.NameCard).Contains(who.CustomerName), $"{name}: the name card names them (\"{Plain(ui.NameCard)}\")");
        Check(ui.TypeSizes.x >= 32f && ui.TypeSizes.y >= 32f, $"{name}: the replies and their line are 32 px at 1080 ({ui.TypeSizes.x:0}, {ui.TypeSizes.y:0})");
        yield return Photo(name + "-b-talking");

        if (!shapesChecked) { shapesChecked = true; Shapes(); }

        conversation.End();
        if (!firstPerson && body != null && body.Worn)
        {
            // As the view rises from his shoulder, he stays drawn (no frame of hiding).
            bool alwaysDrawn = true;
            float until = Time.realtimeSinceStartup + 1.2f;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
                if (!body.Drawn) alwaysDrawn = false;
            }
            Check(alwaysDrawn, $"{name}: Ace stays drawn as the view rises back up");
        }
        yield return AfterBlend();
        yield return Seconds(.3f);
    }

    // The replies' column on other shapes of screen (laid out, measured and put back): inside the margins, right of the face,
    // above their line. Canvas units: the HUD's scaler keeps the screen's area near 1920 x 1080's.
    void Shapes()
    {
        var shapes = new (string name, float aspect)[] { ("16:9", 16f / 9f), ("16:10", 1.6f), ("4:3", 4f / 3f), ("21:9", 21f / 9f), ("32:9", 32f / 9f) };
        foreach (var (shape, aspect) in shapes)
        {
            var size = new Vector2(Mathf.Sqrt(aspect * 1920f * 1080f), Mathf.Sqrt(1920f * 1080f / aspect));
            List<Rect> boxes = ui.PlatesIfTheScreenWere(size);
            float faceRight = size.x * .5f + .074f * size.y + 20f;   // a face at a metre and a bit, with its hair (the 45° lens)
            bool inside = boxes.Count > 0 && boxes.All(b => b.xMin >= size.x * Margin && b.xMax <= size.x * (1f - Margin)
                                                        && b.yMin >= size.y * Margin && b.yMax <= size.y * (1f - Margin));
            bool clear = boxes.All(b => b.xMin > faceRight && b.yMin > 72f + 120f);
            Check(inside && clear, $"the replies on a {shape} screen ({size.x:0} x {size.y:0}): inside the margins, right of the face, above their line " +
                                   $"({string.Join("; ", boxes.Select(b => $"x {b.xMin:0}-{b.xMax:0}, y {b.yMin:0}-{b.yMax:0}"))})");
        }
    }

    // A box around a point, metres to each side and up and down as the camera sees it, in pixels.
    static Rect Box(Camera cam, Vector3 at, float half, float halfHeight)
    {
        Vector3 r = cam.transform.right * half, u = cam.transform.up * halfHeight;
        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
        foreach (Vector3 corner in new[] { at - r - u, at - r + u, at + r - u, at + r + u })
        {
            Vector3 p = cam.WorldToScreenPoint(corner);
            x0 = Mathf.Min(x0, p.x); y0 = Mathf.Min(y0, p.y); x1 = Mathf.Max(x1, p.x); y1 = Mathf.Max(y1, p.y);
        }
        return Rect.MinMaxRect(x0, y0, x1, y1);
    }

    static bool Inside(Rect r) => r.xMin >= Screen.width * Margin - 1f && r.xMax <= Screen.width * (1f - Margin) + 1f
                                  && r.yMin >= Screen.height * Margin - 1f && r.yMax <= Screen.height * (1f - Margin) + 1f;

    static string Px(Rect r) => r.width <= 0f ? "(none)" : $"x {r.xMin / Screen.width * 100f:0}-{r.xMax / Screen.width * 100f:0}%, y {r.yMin / Screen.height * 100f:0}-{r.yMax / Screen.height * 100f:0}%";

    static string P(Vector3 v) => v.z <= 0f ? "behind the view" : $"{v.x * 100f:0}% across, {v.y * 100f:0}% up";

    static string Plain(string rich) => System.Text.RegularExpressions.Regex.Replace(rich ?? "", "<[^>]+>", "");

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

    IEnumerator AfterBlend()
    {
        yield return Frames(3);
        CinemachineBrain brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
        yield return Await(() => brain == null || !brain.IsBlending, 4f);
        yield return Frames(3);
    }

    protected override void Restore()
    {
        if (conversation != null && conversation.InConversation) conversation.End();
        if (spawner != null) spawner.enabled = spawnerWas;
    }
}
