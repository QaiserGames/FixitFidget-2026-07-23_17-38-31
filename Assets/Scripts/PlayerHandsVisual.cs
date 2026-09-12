using System.Collections.Generic;
using UnityEngine;

// A compact anatomical prototype: cloth forearms, a palm, four curled fingers
// and an opposing thumb. The same grasp is attached to the body on the floor.
[DisallowMultipleComponent]
public sealed class PlayerHandsVisual : MonoBehaviour
{
    private sealed class Hand
    {
        public Transform root, upper, forearm, cuff, wrist, palm;
        public readonly Transform[,] fingers = new Transform[4, 3];
        public readonly Transform[] thumb = new Transform[2];
    }
    private readonly Hand[] hands = new Hand[2];
    private Transform visualRoot;
    private Mesh roundMesh, sleeveMesh;
    private Material skin, cloth, cuffMaterial;
    private int capacity = 2;
    private bool visible;

    private void Awake() => Initialize();
    private void Initialize()
    {
        if (visualRoot != null) return;
        visualRoot = new GameObject("Physical hands and sleeves").transform;
        visualRoot.SetParent(transform, false);
        roundMesh = RoundedMesh(); sleeveMesh = SleeveMesh();
        skin = Material("Warm hand skin", new Color(.58f, .34f, .23f), .24f);
        cloth = Material("Work shirt teal", new Color(.12f, .30f, .29f), .04f);
        cuffMaterial = Material("Rolled shirt cuffs", new Color(.21f, .43f, .39f), .06f);
        for (int side = 0; side < 2; side++)
        {
            var hand = new Hand(); hands[side] = hand;
            hand.root = new GameObject(side == 0 ? "Left hand" : "Right hand").transform;
            hand.root.SetParent(visualRoot, false);
            hand.upper = Part("Upper sleeve", hand.root, sleeveMesh, cloth);
            hand.forearm = Part("Forearm sleeve", hand.root, sleeveMesh, cloth);
            hand.cuff = Part("Rolled cuff", hand.root, sleeveMesh, cuffMaterial);
            hand.wrist = Part("Wrist", hand.root, roundMesh, skin);
            hand.palm = Part("Palm", hand.root, roundMesh, skin);
            for (int finger = 0; finger < 4; finger++)
                for (int bone = 0; bone < 3; bone++)
                    hand.fingers[finger, bone] = Part("Finger " + (finger + 1) + " joint " + (bone + 1), hand.root, roundMesh, skin);
            for (int i = 0; i < 2; i++) hand.thumb[i] = Part("Opposing thumb " + (i + 1), hand.root, roundMesh, skin);
        }
        SetVisible(false);
    }
    public void SetCapacity(int count)
    {
        capacity = count;
        if (visualRoot == null) return;
        for (int i = 0; i < 2; i++) if (i >= capacity) hands[i].root.gameObject.SetActive(false);
    }
    public void SetVisible(bool show)
    {
        visible = show;
        if (visualRoot != null) visualRoot.gameObject.SetActive(show);
    }
    public void Pose(int side, Vector3 centre, Quaternion facing, bool occupied, bool cup,
        Vector3 itemSize, Camera firstPerson, Transform body)
    {
        Initialize();
        Hand hand = hands[side]; float sign = side == 0 ? -1 : 1;
        if (!occupied && firstPerson == null) { hand.root.gameObject.SetActive(false); return; }
        Vector3 right = facing * Vector3.right, forward = facing * Vector3.forward;
        float radius = cup ? Mathf.Clamp(itemSize.x * .5f, .03f, .055f) : .035f;
        Vector3 grip = centre;
        if (occupied && !cup)
            grip += Vector3.down * (itemSize.y * .5f + .016f);
        Vector3 palm = grip + right * (sign * (radius + .020f)) - Vector3.up * .010f;
        Vector3 wrist = palm - forward * .021f - Vector3.up * .043f + right * (sign * .008f);
        Vector3 elbow, shoulder;
        if (firstPerson != null)
        {
            shoulder = firstPerson.ViewportToWorldPoint(new Vector3(side == 0 ? .02f : .98f, -.40f, .33f));
            elbow = firstPerson.ViewportToWorldPoint(new Vector3(side == 0 ? .12f : .88f, -.045f, .48f));
        }
        else
        {
            shoulder = body.TransformPoint(new Vector3(sign * .44f, .28f, .02f));
            elbow = body.TransformPoint(new Vector3(sign * .56f, -.13f, .16f));
        }
        Segment(hand.upper, shoulder, elbow, .092f, false);
        Segment(hand.forearm, elbow, wrist, .073f, false);
        Vector3 cuffStart = Vector3.Lerp(wrist, elbow, .10f);
        Segment(hand.cuff, cuffStart, wrist + (wrist - elbow).normalized * .018f, .082f, false);
        Segment(hand.wrist, wrist, palm - Vector3.up * .024f, .043f, true);
        hand.palm.position = palm; hand.palm.rotation = facing;
        hand.palm.localScale = new Vector3(.040f, .077f, .052f);
        for (int digit = 0; digit < 4; digit++)
        {
            float y = .023f - digit * .0175f;
            float curl = occupied ? 1 : .64f;
            Vector3 a = grip + facing * new Vector3(sign * (radius + .016f), y, -.012f);
            Vector3 b = grip + facing * new Vector3(sign * (radius + .008f), y - .002f, -.035f);
            Vector3 c = grip + facing * new Vector3(sign * radius * .44f, y - .003f, -.043f * curl);
            Vector3 d = grip + facing * new Vector3(sign * radius * -.07f, y - .003f, -.032f * curl);
            float width = digit == 3 ? .013f : .015f;
            Segment(hand.fingers[digit, 0], a, b, width, true);
            Segment(hand.fingers[digit, 1], b, c, width * .96f, true);
            Segment(hand.fingers[digit, 2], c, d, width * .85f, true);
        }
        Vector3 ta = palm + Vector3.up * .029f + forward * .015f;
        Vector3 tb = grip + facing * new Vector3(sign * (radius + .005f), .032f, .030f);
        Vector3 tc = grip + facing * new Vector3(sign * radius * .34f, .026f, .034f);
        Segment(hand.thumb[0], ta, tb, .024f, true);
        Segment(hand.thumb[1], tb, tc, .021f, true);
        hand.root.gameObject.SetActive(side < capacity);
        visualRoot.gameObject.SetActive(visible);
    }
    private static void Segment(Transform part, Vector3 start, Vector3 end, float width, bool rounded)
    {
        Vector3 direction = end - start;
        part.position = (start + end) * .5f;
        part.rotation = direction.sqrMagnitude > .000001f ? Quaternion.FromToRotation(Vector3.up, direction) : Quaternion.identity;
        part.localScale = new Vector3(width, direction.magnitude + (rounded ? width * .5f : 0), width);
    }
    private static Transform Part(string name, Transform parent, Mesh mesh, Material material)
    {
        var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        part.transform.SetParent(parent, false);
        part.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = part.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = true;
        return part.transform;
    }
    private static Material Material(string name, Color color, float smoothness)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var material = new Material(shader) { name = name, color = color };
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        return material;
    }
    private static Mesh RoundedMesh()
    {
        const int around = 16, rows = 10;
        var vertices = new List<Vector3>(); var triangles = new List<int>();
        for (int row = 0; row <= rows; row++)
        {
            float phi = row * Mathf.PI / rows;
            for (int i = 0; i <= around; i++)
            {
                float theta = i * Mathf.PI * 2 / around;
                vertices.Add(new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta)) * .5f);
            }
        }
        for (int row = 0; row < rows; row++)
            for (int i = 0; i < around; i++)
            {
                int a = row * (around + 1) + i, b = a + around + 1;
                triangles.Add(a); triangles.Add(a + 1); triangles.Add(b);
                triangles.Add(a + 1); triangles.Add(b + 1); triangles.Add(b);
            }
        var mesh = new Mesh { name = "Soft stylized hand geometry" };
        mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
    }
    private static Mesh SleeveMesh()
    {
        const int sides = 16;
        var vertices = new List<Vector3>(); var triangles = new List<int>();
        for (int row = 0; row < 2; row++)
            for (int i = 0; i <= sides; i++)
            {
                float angle = i * Mathf.PI * 2 / sides, radius = row == 0 ? .5f : .43f;
                vertices.Add(new Vector3(Mathf.Cos(angle) * radius, row - .5f, Mathf.Sin(angle) * radius));
            }
        for (int i = 0; i < sides; i++)
        {
            int b = i + sides + 1;
            triangles.Add(i); triangles.Add(b); triangles.Add(i + 1);
            triangles.Add(i + 1); triangles.Add(b); triangles.Add(b + 1);
        }
        int bottom = vertices.Count; vertices.Add(new Vector3(0, -.5f, 0));
        int top = vertices.Count; vertices.Add(new Vector3(0, .5f, 0));
        for (int i = 0; i < sides; i++)
        {
            triangles.Add(bottom); triangles.Add(i); triangles.Add(i + 1);
            triangles.Add(top); triangles.Add(i + sides + 2); triangles.Add(i + sides + 1);
        }
        var mesh = new Mesh { name = "Tapered cloth sleeves" };
        mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
    }
    private static void Release(Object item)
    {
        if (item == null) return;
        if (Application.isPlaying) Destroy(item); else DestroyImmediate(item);
    }
    private void OnDestroy()
    {
        Release(roundMesh); Release(sleeveMesh); Release(skin); Release(cloth); Release(cuffMaterial);
        if (visualRoot != null) Release(visualRoot.gameObject);
    }
}
