using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;

// ---------------------------------------------------------------------------
// A SLIMMER ACE AND A REAL CROUCH: A PLAY CHECK (playtest 3, 7 Oct 2026; claude/playtest-3-sessions-2-6-plan.md §2.4)
//
// Fixit Fidget > Playtest > Slimmer Ace - capsule and crouch check (lab, drives itself). A fresh Day 1 in the lab:
//
//   1. Ace's capsule as the game runs it: 0.35 m round with a 3.5 cm skin (0.77 m across; it was 1.16), 2.0 m tall,
//      the feet where they were; the first-person eye 1.65 m up;
//   2. crouched (the sneak, switched on by day for the check): the capsule is 1.0 m tall with the feet kept, and the
//      eye 0.92 m up, below its top;
//   3. a bar 1.2 m up across his way (made for the check, in the open floor): standing, he can't walk under it;
//      crouched, he can; let go of the sneak beneath it and he stays crouched ("too low to stand"); out from under it,
//      he stands up;
//   4. upstairs at Grace's (her rooms made solid for the check, as at night): walking at her stairwell from the
//      bedroom, he stops on the solid floor (the invisible edge, GraceHouse), and doesn't drop through the strip of
//      landing that's only drawn.
//
// Everything made is taken away and every switch put back. Photos and report.txt: Logs/Capsule/capsule-<time>/.
// ---------------------------------------------------------------------------
public sealed class CapsuleCheck : PlayLab
{
    public const string PendingKey = "FixitFidget.Capsule.Check";

    protected override string Title => "A slimmer Ace and a real crouch - play check";
    protected override string Tag => "[Capsule check]";

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartFromTheEditor()
    {
        if (PlayerPrefs.GetInt(PendingKey, 0) == 0) return;
        PlayerPrefs.DeleteKey(PendingKey);
        PlayerPrefs.Save();
        if (!CafeLab.Active)
        {
            Debug.LogWarning("[Capsule check] The check was asked for outside a lab session; it only runs in the lab.");
            return;
        }
        var go = new GameObject("Capsule check (this Play session only)");
        go.AddComponent<CapsuleCheck>().folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Capsule",
            $"capsule-{DateTime.Now:yyyy-MM-dd_HHmmss}"));
    }
#endif

    const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    PlayerMovement movement;
    CafeViewMode view;
    CharacterController cc;
    GameObject bar;
    bool sneakByDayWas, houseTouched;
    Vector3 startAt;

    protected override IEnumerator Run()
    {
        yield return Until(() => DayClock.Instance != null && !DayClock.Instance.DayOver && Camera.main != null, 30f, "the lab's day is on");
        if (!lastWait) yield break;
        movement = FindAnyObjectByType<PlayerMovement>();
        view = movement != null ? movement.GetComponent<CafeViewMode>() : null;
        cc = movement != null ? movement.GetComponent<CharacterController>() : null;
        Check(movement != null && view != null && cc != null, "Ace, his view and his capsule are in the scene");
        if (cc == null) yield break;
        startAt = movement.transform.position;

        // ---------- 1. the capsule ----------
        float feet0 = view.AceFeet.y;
        Check(Mathf.Abs(cc.radius - .35f) < .001f && Mathf.Abs(cc.skinWidth - .035f) < .001f,
            $"the capsule is {cc.radius:0.000} m round with a {cc.skinWidth:0.000} m skin: {2f * (cc.radius + cc.skinWidth):0.00} m across (1.16 before)");
        Check(Mathf.Abs(cc.height - 2f) < .001f, $"standing, it is {cc.height:0.00} m tall");
        Check(Mathf.Abs(feet0 - (movement.transform.position.y - 1f)) < .01f, $"the feet are a metre under his middle, on the floor ({feet0:0.00})");
        view.SetFirstPerson(true);
        yield return Seconds(1f);
        float eyeUp = view.firstPersonCamera.transform.position.y - view.AceFeet.y;
        Check(Mathf.Abs(eyeUp - 1.65f) < .02f, $"in first person the eye is {eyeUp:0.00} m up standing");

        // ---------- 2. crouched ----------
        FieldInfo byDay = typeof(PlayerMovement).GetField("sneakByDay", Any);
        sneakByDayWas = (bool)byDay.GetValue(movement);
        byDay.SetValue(movement, true);
        movement.ScriptedSneak = true;
        yield return Seconds(.5f);
        float eyeCrouched = view.firstPersonCamera.transform.position.y - view.AceFeet.y;
        // Within the skin: the controller settles onto the floor by up to its skin width (3.5 cm) when its shape changes.
        Check(Mathf.Abs(cc.height - 1f) < .01f && Mathf.Abs(view.AceFeet.y - feet0) < PlayerMovement.CapsuleSkin + .01f,
            $"crouched, the capsule is {cc.height:0.00} m tall with the feet where they were ({view.AceFeet.y - feet0:+0.000;-0.000} m, within the skin)");
        Check(Mathf.Abs(eyeCrouched - .92f) < .03f && eyeCrouched < cc.height,
            $"crouched, the eye is {eyeCrouched:0.00} m up, below the capsule's top ({cc.height:0.00})");
        yield return Photo("01-crouched-first-person");
        movement.ScriptedSneak = false;
        yield return Seconds(.5f);
        Check(Mathf.Abs(cc.height - 2f) < .01f && !movement.TooLowToStand, "let go in the open, he stands up again (2.0 m)");

        // ---------- 3. the bar ----------
        // An open stretch of floor, three metres long, in front of where he is (the café's middle aisle at the start).
        Vector3 from = OpenStretch(out Vector3 along);
        Check(from != Vector3.zero, "an open stretch of the café floor, 3 m long, for the bar");
        if (from == Vector3.zero) yield break;
        float floor = from.y;
        Vector3 barAt = from + along * 1.5f;
        bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bar.name = "Capsule check - a bar 1.2 m up (made for the check)";
        bar.transform.position = new Vector3(barAt.x, floor + 1.2f + .075f, barAt.z);
        bar.transform.rotation = Quaternion.LookRotation(along);
        bar.transform.localScale = new Vector3(2.4f, .15f, .3f);
        Physics.SyncTransforms();
        Note($"the bar: across the way at {barAt.x:0.00}, {barAt.z:0.00}, its underside {1.2f:0.0} m up");

        // Standing: blocked.
        PutFeet(from - along * .5f, along);
        yield return Frames(3);
        yield return Walk(along, 1.2f);
        float reached = Vector3.Dot(movement.transform.position - (from - along * .5f), along);
        Check(reached < 2f - .1f, $"standing, he can't walk under it (stopped {reached:0.00} m along, the bar at 2.0)");
        yield return Photo("02-standing-stopped-at-the-bar");

        // Crouched: under it.
        movement.ScriptedSneak = true;
        yield return Seconds(.4f);
        PutFeet(from - along * .5f, along);
        yield return Frames(3);
        // Crouched he sneaks (well under a metre a second, eased in), so he's given four seconds for the two metres.
        yield return WalkUntil(along, 4f, () => Vector3.Dot(movement.transform.position - (from - along * .5f), along) >= 2f);
        float under = Vector3.Dot(movement.transform.position - (from - along * .5f), along);
        Check(under >= 2f - .05f, $"crouched, he walks under it ({under:0.00} m along)");
        yield return Photo("03-crouched-under-the-bar");

        // Let go beneath it: he stays crouched.
        movement.ScriptedSneak = false;
        yield return Seconds(.6f);
        Check(movement.TooLowToStand && cc.height < 1.05f && movement.Crouch > .99f,
            $"let go of the sneak beneath it, he stays crouched (too low to stand; {cc.height:0.00} m)");
        // Out from under it: he stands up.
        yield return WalkUntil(along, 4f, () => !movement.TooLowToStand && Vector3.Dot(movement.transform.position - barAt, along) > .8f);
        yield return Seconds(.6f);
        Check(!movement.TooLowToStand && Mathf.Abs(cc.height - 2f) < .01f, $"out from under it, he stands ({cc.height:0.00} m)");
        Destroy(bar);
        bar = null;
        byDay.SetValue(movement, sneakByDayWas);
        movement.ScriptedSneak = null;

        // ---------- 4. Grace's stairwell, upstairs ----------
        GraceHouse house = GraceHouse.Instance;
        Check(house != null, "Grace's house is in the scene");
        if (house == null) yield break;
        Transform edge = house.transform.Find("Stairwell edge (made while playing: keeps Ace on the solid floor upstairs)");
        Check(edge != null && edge.GetComponent<BoxCollider>() != null && edge.GetComponent<BoxCollider>().enabled,
            "the invisible edge at her stairwell is there and solid");
        // Her house is only solid at night (GraceHouse turns its colliders off by day, every frame). With the component
        // off it stands solid and leaves them alone (its OnDisable), so the walk is done with it off, and it's put back after.
        house.enabled = false;
        houseTouched = true;
        Physics.SyncTransforms();
        // From the bedroom, at the wardrobe and the dressing table (a place Ace stands: GraceHouseCheck), walk at the
        // middle of the stairwell (plan X 0.73, Y 2.90: its front wall).
        Vector3 start = house.World(1.25f, 3.62f, house.firstFloorAt);
        Vector3 toward = house.World(.73f, 2.90f, house.firstFloorAt) - start;
        toward.y = 0f;
        toward.Normalize();
        view.SetFirstPerson(false);
        PutFeet(start + Vector3.up * .02f, toward);
        yield return Frames(4);
        yield return Walk(toward, 1.6f);
        Vector3 plan = house.Plan(movement.transform.position);
        float feetZ = house.Plan(view.AceFeet).z;
        Check(plan.y >= 3.40f && feetZ > house.firstFloorAt - .1f,
            $"walking at the stairwell upstairs, he stops on the solid floor (his middle at Y {plan.y:0.00}: solid from 3.40; his feet {feetZ:0.00} m up, the floor {house.firstFloorAt:0.00})");
        view.SetFirstPerson(true);
        view.LookTo(Mathf.Atan2(toward.x, toward.z) * Mathf.Rad2Deg, 35f);
        yield return Seconds(1f);
        yield return Photo("04-upstairs-at-the-stairwell");
        view.SetFirstPerson(false);
        PutCentre(startAt, Vector3.forward);   // back in the café before her house goes back to its daytime self
        yield return Frames(2);
        house.enabled = true;
        houseTouched = false;
        yield return Seconds(.5f);
    }

    protected override void Restore()
    {
        if (bar != null) Destroy(bar);
        if (movement != null)
        {
            movement.ScriptedInput = null;
            movement.ScriptedSneak = null;
            typeof(PlayerMovement).GetField("sneakByDay", Any).SetValue(movement, sneakByDayWas);
            if (startAt != Vector3.zero) PutCentre(startAt, Vector3.forward);
        }
        if (houseTouched)
        {
            GraceHouse house = FindAnyObjectByType<GraceHouse>(FindObjectsInactive.Include);
            if (house != null) house.enabled = true;
        }
    }

    // ---------- moving ----------

    // Ace's middle a metre over the feet (the capsule's feet are a metre under the player's origin).
    void PutFeet(Vector3 feet, Vector3 facing) => PutCentre(feet + Vector3.up * 1f, facing);

    void PutCentre(Vector3 at, Vector3 facing)
    {
        if (cc == null) return;
        bool was = cc.enabled;
        cc.enabled = false;
        movement.transform.SetPositionAndRotation(at, Quaternion.LookRotation(facing));
        cc.enabled = was;
        if (view != null && view.FirstPersonSelected) view.LookTo(Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg, 5f);
    }

    // Walks a world direction for a while (the walk's own input path, as a stick).
    IEnumerator Walk(Vector3 direction, float seconds)
    {
        float until = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < until)
        {
            movement.ScriptedInput = Stick(direction);
            yield return null;
        }
        movement.ScriptedInput = null;
        yield return Frames(3);
    }

    IEnumerator WalkUntil(Vector3 direction, float seconds, Func<bool> done)
    {
        float until = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < until && !Safe(done))
        {
            movement.ScriptedInput = Stick(direction);
            yield return null;
        }
        movement.ScriptedInput = null;
        yield return Frames(3);
    }

    Vector2 Stick(Vector3 world)
    {
        float yaw = view.isActiveAndEnabled ? view.MovementYaw : 45f;
        Vector3 local = Quaternion.Euler(0f, -yaw, 0f) * world.normalized;
        return new Vector2(local.x, local.z);
    }

    // Three metres of open floor along a room axis, inside the café (CafeDaylight.CafeInside), with floor under every step of
    // it: the first version only asked for empty space, found it past the back wall where there's no floor, and Ace fell
    // out of the world (7 Oct). The search starts in the middle of the room and works outwards.
    Vector3 OpenStretch(out Vector3 along)
    {
        Rect room = CafeDaylight.CafeInside;
        Vector3 middle = new Vector3(room.center.x, view.AceFeet.y, room.center.y);
        Vector3[] axes = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
        for (int ring = 0; ring < 8; ring++)
            for (int k = 0; k < (ring == 0 ? 1 : 8); k++)
            {
                float a = k * 45f * Mathf.Deg2Rad;
                Vector3 p = middle + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * (ring * 1.2f);
                foreach (Vector3 axis in axes)
                {
                    bool clear = true;
                    for (float d = -.6f; d <= 3.6f && clear; d += .3f)
                    {
                        Vector3 q = p + axis * d;
                        clear = room.Contains(new Vector2(q.x, q.z)) && Floor(q, out q.y) && Clear(q);
                    }
                    if (clear && Floor(p, out float y))
                    {
                        along = axis;
                        return new Vector3(p.x, y, p.z);
                    }
                }
            }
        along = Vector3.forward;
        return Vector3.zero;
    }

    // The floor under a point, at the room's floor level (within 15 cm of the point's height, so a table top or a rug's
    // edge isn't taken for it): solid, and not Ace.
    bool Floor(Vector3 q, out float y)
    {
        y = q.y;
        foreach (RaycastHit hit in Physics.RaycastAll(q + Vector3.up * .5f, Vector3.down, 1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(movement.transform) || Mathf.Abs(hit.point.y - q.y) > .15f) continue;
            y = hit.point.y;
            return true;
        }
        return false;
    }

    // Room for Ace above a point of floor: a capsule a little wider than his, from 8 cm over the floor (so the floor itself
    // isn't counted: the first version's capsule dipped 10 cm into it, so no café floor was ever clear) to above his head.
    bool Clear(Vector3 q)
    {
        float r = PlayerMovement.BodyRadius + .1f;
        foreach (Collider c in Physics.OverlapCapsule(q + Vector3.up * (r + .08f), q + Vector3.up * (PlayerMovement.StandingHeight + .3f - r), r,
                     Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            if (!c.transform.IsChildOf(movement.transform)) return false;
        return true;
    }
}
