using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The cup a café guest carries (the NPC life pass, 12-13 Oct 2026). Until now a served drink flew to the customer as a
/// ghost (Juice.HandOver) and vanished in their hands: nobody ever held a cup. Now:
///  * a customer served a drink gets this cup once the hand-over has landed (0.4 s); a patron gets one on their table a
///    few seconds after they sit (the coffee the café quietly charged them for at the door);
///  * seated, the cup stands on the seat's cup spot (beside the laptop when one is open there); standing, it stays in
///    the hand;
///  * leaving, the cup comes back into the hand, and on the way out the guest stops at the three-bin station by the door
///    and drops it in (<see cref="DropInto"/>): a short arc into the bins' top, a small sparkle, a sound. The arm's throw
///    comes with batch 2's clip; until then the pause and the cup going in are the beat. Without a bin station in the
///    scene the cup goes home with them.
/// The cup is the café's own paper cup (DrinkLiquidVisual's shell and surface, as the man at the bins' used cups), made
/// in code, never saved. Presentation only: no patience, money or order changes.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(160)] // after PolygonNpcVisual (150) has posed the hand this frame
public sealed class NpcCup : MonoBehaviour
{
    public enum Where { Hidden, InHand, OnTable, Dropping, Gone }

    private static Mesh shellMesh, discMesh;
    private static Material paperMaterial;
    private static readonly Dictionary<Color, Material> liquids = new Dictionary<Color, Material>();
    private static int made, binned;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        shellMesh = discMesh = null;
        paperMaterial = null;
        liquids.Clear();
        made = binned = 0;
    }

    /// <summary>For the checks: cups made this session, and cups dropped in the bins.</summary>
    public static int Made => made;
    public static int Binned => binned;

    private GameObject cup;
    private Transform rigRoot;
    private PolygonNpcVisual visual;
    private NpcBeats beats;
    private Where where = Where.Hidden;
    private float showAt;
    private TableSeat table;
    private Vector3 tableAt;
    private float tableYaw;
    private bool left;   // the hand it is carried in this frame

    public Where State => where;
    public bool InHand => where == Where.InHand;
    public bool OnTable => where == Where.OnTable;
    /// <summary>Still theirs to carry or bin (not dropped, not gone).</summary>
    public bool Held => where == Where.InHand || where == Where.OnTable || where == Where.Hidden;
    /// <summary>The cup object (for checks), or null once it is gone.</summary>
    public GameObject Cup => cup;

    /// <summary>
    /// Gives <paramref name="owner"/> a cup of <paramref name="drink"/> (null: the house coffee), in the hand after
    /// <paramref name="delay"/> seconds (the hand-over's flight). One cup a person: a second call refreshes the drink.
    /// </summary>
    public static NpcCup Give(GameObject owner, DrinkDefinition drink, float delay = 0f)
    {
        if (owner == null) return null;
        NpcCup held = owner.GetComponent<NpcCup>();
        if (held == null) held = owner.AddComponent<NpcCup>();
        held.Build(drink);
        if (held.where == Where.Gone || held.where == Where.Dropping) held.where = Where.Hidden;
        if (held.where == Where.Hidden) held.showAt = Time.time + Mathf.Max(0f, delay);
        return held;
    }

    public static NpcCup Of(GameObject owner) => owner != null ? owner.GetComponent<NpcCup>() : null;

    private void Awake()
    {
        Animator animator = GetComponentInChildren<Animator>();
        rigRoot = animator != null ? animator.transform : transform;
        visual = GetComponent<PolygonNpcVisual>();
        beats = GetComponent<NpcBeats>();
    }

    private void Build(DrinkDefinition drink)
    {
        if (shellMesh == null) shellMesh = DrinkLiquidVisual.BuildShell();
        if (discMesh == null) discMesh = DrinkLiquidVisual.BuildDisc();
        if (paperMaterial == null) paperMaterial = DrinkLiquidVisual.MakeMaterial("Guests' cups: paper", DrinkLiquidVisual.PaperColour, .08f);
        Color colour = DrinkLiquidVisual.LiquidColor(drink);
        if (!liquids.TryGetValue(colour, out Material liquid) || liquid == null)
        {
            liquid = DrinkLiquidVisual.MakeMaterial("Guests' cups: " + (drink != null ? drink.drinkName : "coffee"), colour, .68f);
            liquids[colour] = liquid;
        }
        if (cup == null)
        {
            cup = new GameObject("Cup (" + name + ")");
            Part(cup.transform, "Paper cup", shellMesh, paperMaterial, Vector3.zero, Vector3.one);
            made++;
        }
        Transform surface = cup.transform.Find("Drink");
        if (surface == null) surface = Part(cup.transform, "Drink", discMesh, liquid, new Vector3(0f, .082f, 0f), new Vector3(.034f, 1f, .034f)).transform;
        surface.GetComponent<MeshRenderer>().sharedMaterial = liquid;
        cup.SetActive(false);
    }

    private static GameObject Part(Transform parent, string label, Mesh mesh, Material material, Vector3 at, Vector3 scale)
    {
        var part = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
        part.transform.SetParent(parent, false);
        part.transform.localPosition = at;
        part.transform.localScale = scale;
        part.GetComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer r = part.GetComponent<MeshRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return part;
    }

    /// <summary>Set the cup down on this seat's table (its cup spot; beside the laptop when one is open there).</summary>
    public void PutOnTable(TableSeat seat)
    {
        if (cup == null || seat == null || where == Where.Gone || where == Where.Dropping) return;
        table = seat;
        Vector3 at = seat.CupSpot.position;
        Transform laptop = NpcLifeProps.LaptopFor(seat, 1.2f);
        Component user = laptop != null ? NpcLifeProps.UserOf(laptop) : null;
        if (laptop != null && user != null && user.gameObject == gameObject)
        {
            // Their laptop lies where the cup would go: the cup stands to its right, as people put it.
            Vector3 facing = seat.FacingPoint - seat.SeatPose.position;
            facing.y = 0f;
            Vector3 right = facing.sqrMagnitude > 1e-4f ? Vector3.Cross(Vector3.up, facing.normalized) : Vector3.right;
            at += right * .17f;
        }
        tableAt = at;
        tableYaw = Random.Range(0f, 360f);
        where = Where.OnTable;
        cup.transform.SetPositionAndRotation(tableAt, Quaternion.Euler(0f, tableYaw, 0f));
        cup.SetActive(true);
    }

    /// <summary>Back into the hand (getting up to go).</summary>
    public void TakeInHand()
    {
        if (cup == null || where == Where.Gone || where == Where.Dropping) return;
        table = null;
        where = Where.InHand;
        showAt = Time.time;
    }

    /// <summary>
    /// Into the bins' top at <paramref name="mouth"/>: a short arc from wherever the cup is, then gone, with a small
    /// sparkle and a sound. The arm's throw is batch 2's clip.
    /// </summary>
    public void DropInto(Vector3 mouth, float seconds = .3f)
    {
        if (cup == null || where == Where.Gone || where == Where.Dropping) return;
        where = Where.Dropping;
        table = null;
        if (!cup.activeSelf) cup.transform.position = HandPoint(out _);
        cup.SetActive(true);
        StartCoroutine(Fly(mouth, Mathf.Max(.1f, seconds)));
    }

    private IEnumerator Fly(Vector3 mouth, float seconds)
    {
        Vector3 from = cup.transform.position;
        Quaternion turn = cup.transform.rotation;
        for (float t = 0f; t < 1f; t += Time.deltaTime / seconds)
        {
            if (cup == null) yield break;
            float e = 1f - (1f - t) * (1f - t);
            cup.transform.position = Vector3.Lerp(from, mouth, e) + Vector3.up * (.18f * 4f * t * (1f - t));
            cup.transform.rotation = turn * Quaternion.Euler(0f, 0f, 70f * e);
            cup.transform.localScale = Vector3.one * (t < .7f ? 1f : Mathf.Lerp(1f, .4f, (t - .7f) / .3f));
            yield return null;
        }
        if (cup != null) Destroy(cup);
        cup = null;
        where = Where.Gone;
        binned++;
        Juice.Sparkle(mouth + Vector3.up * .05f, false);
        if (!Sfx.Play("cup.bin", mouth)) Sfx.Play("cup.set", mouth, .6f);
    }

    /// <summary>The cup goes without ceremony (a guest removed, a day reset).</summary>
    public void Vanish()
    {
        if (cup != null) Destroy(cup);
        cup = null;
        where = Where.Gone;
    }

    private void LateUpdate()
    {
        if (cup == null) return;
        switch (where)
        {
            case Where.Hidden:
                if (Time.time < showAt) return;
                where = Where.InHand;
                goto case Where.InHand;
            case Where.InHand:
                Vector3 at = HandPoint(out bool found);
                if (!found) { if (cup.activeSelf) cup.SetActive(false); return; }
                cup.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
                if (!cup.activeSelf) cup.SetActive(true);
                break;
            case Where.OnTable:
                if (table == null) { TakeInHand(); return; }
                cup.transform.SetPositionAndRotation(tableAt, Quaternion.Euler(0f, tableYaw, 0f));
                break;
        }
    }

    // The rig's hand bones, looked up once per side (the rig never changes).
    private readonly Transform[] wrists = new Transform[2], middles = new Transform[2], indexes = new Transform[2], pinkies = new Transform[2];
    private readonly bool[] bonesSearched = new bool[2];

    // Where the cup's base goes in the hand: a little past the palm, between the fingers, upright. The hand that holds
    // it is the one without the phone (a phone beat showing), the right otherwise.
    private Vector3 HandPoint(out bool found)
    {
        found = false;
        NpcBeatLibrary.Hand phone = beats != null && beats.Phone != null ? beats.PhoneHand : NpcBeatLibrary.Hand.None;
        left = phone == NpcBeatLibrary.Hand.Right;
        int side = left ? 0 : 1;
        string s = left ? "L" : "R";
        if (!bonesSearched[side])
        {
            bonesSearched[side] = true;
            wrists[side] = RigBone("Wrist." + s);
            middles[side] = RigBone("Middle2." + s);
            indexes[side] = RigBone("Index2." + s);
            pinkies[side] = RigBone("Pinky2." + s);
        }
        Transform wrist = wrists[side], middle = middles[side], index = indexes[side], pinky = pinkies[side];
        if (wrist == null) return transform.position + Vector3.up * .9f;
        Transform hand = visual != null && visual.VisualInstance != null ? visual.CityHand(left) : null;
        if (hand == null) hand = wrist;
        Vector3 along = middle != null ? (middle.position - wrist.position).normalized : hand.forward;
        Vector3 palm;
        if (index != null && pinky != null)
        {
            Vector3 across = (index.position - pinky.position).normalized;
            palm = (s == "R" ? Vector3.Cross(along, across) : Vector3.Cross(across, along)).normalized;
        }
        else palm = -transform.forward;
        float reach = middle != null ? Vector3.Distance(wrist.position, middle.position) * .55f : .07f;
        found = true;
        // The cup's base sits below the fingers so the fingers wrap its middle.
        return hand.position + along * reach + palm * (.038f * transform.lossyScale.y) - Vector3.up * (.05f * transform.lossyScale.y);
    }

    private Transform RigBone(string boneName)
    {
        Transform skip = visual != null && visual.VisualInstance != null ? visual.VisualInstance.transform : null;
        return Find(rigRoot != null ? rigRoot : transform, boneName, skip);
    }

    private static Transform Find(Transform node, string boneName, Transform skip)
    {
        if (node == skip || node.name.StartsWith("City look", System.StringComparison.Ordinal)) return null;
        if (node.name == boneName) return node;
        for (int i = 0; i < node.childCount; i++)
        {
            Transform hit = Find(node.GetChild(i), boneName, skip);
            if (hit != null) return hit;
        }
        return null;
    }

    private void OnDisable()
    {
        if (cup != null && where != Where.Dropping) cup.SetActive(false);
    }

    private void OnDestroy()
    {
        if (cup != null) Destroy(cup);
    }
}
