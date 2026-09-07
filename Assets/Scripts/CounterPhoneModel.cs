using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Wrap an authored Blender export with these presentation references.
// No job, scoring, input or customer logic belongs on the visual prefab.
public sealed class CounterPhoneModel : MonoBehaviour
{
    [SerializeField] private PhysicalToggle muteSwitch;
    [SerializeField] private Transform switchSlider;
    [SerializeField] private Vector3 soundOnPosition;
    [SerializeField] private TMP_Text screen;
    private Vector3 soundOffPosition;
    private bool shownState;
    private bool hasShownState;
    private readonly List<Mesh> meshes = new();
    private readonly List<Material> materials = new();
    public PhysicalToggle Switch => muteSwitch;
    public bool IsValid => muteSwitch != null && muteSwitch.HitTarget != null && switchSlider != null;

    public void Bind(HumanFault fault, CustomerBrain owner)
    {
        soundOffPosition = switchSlider.localPosition;
        muteSwitch.Bind(fault, owner);
        Show(fault.Finished, true);
    }

    public void Show(bool soundOn, bool immediate = false)
    {
        Vector3 target = soundOn ? soundOnPosition : soundOffPosition;
        switchSlider.localPosition = immediate ? target
            : Vector3.Lerp(switchSlider.localPosition, target, 1 - Mathf.Exp(-22 * Time.deltaTime));
        muteSwitch.SetHighlight(false);
        if (screen != null && (!hasShownState || shownState != soundOn))
            screen.text = soundOn ? "INCOMING CALL\n\nRinging\n\nSound on" : "INCOMING CALL\n\nSilent\n\nSound off";
        if (screen != null) screen.color = soundOn ? RepairOverlayUI.Mint : new Color(.84f, .91f, .91f);
        shownState = soundOn; hasShownState = true;
    }

    public Bounds VisualBounds()
    {
        var bounds = new Bounds(transform.position, Vector3.zero);
        bool any = false;
        foreach (var renderer in GetComponentsInChildren<Renderer>())
        {
            if (renderer.GetComponent<TMP_Text>() != null) continue;
            if (!any) { bounds = renderer.bounds; any = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        return bounds;
    }

    public static CounterPhoneModel CreatePrototype(Transform parent, Material source)
    {
        var model = new GameObject("Prototype phone").AddComponent<CounterPhoneModel>();
        model.transform.SetParent(parent, false);
        model.Part("Rounded casing", Vector3.zero, new Vector3(.21f, .37f, .026f), .020f, .003f, new Color(.23f, .36f, .39f), source);
        model.Part("Glass", new Vector3(0, 0, -.016f), new Vector3(.185f, .327f, .005f), .012f, .001f, new Color(.045f, .095f, .115f), source);
        model.Part("Speaker", new Vector3(0, .172f, -.017f), new Vector3(.045f, .004f, .003f), .001f, .0002f, new Color(.045f, .06f, .065f), source);
        model.Part("Switch recess", new Vector3(-.116f, .084f, -.004f), new Vector3(.025f, .071f, .019f), .006f, .001f, new Color(.05f, .07f, .07f), source);
        var knob = model.Part("Mute switch", new Vector3(-.118f, .068f, -.015f), new Vector3(.026f, .029f, .021f), .005f, .001f, new Color(1f, .48f, .15f), source);
        var collider = knob.AddComponent<BoxCollider>(); collider.size = new Vector3(.034f, .039f, .028f);
        model.muteSwitch = knob.AddComponent<PhysicalToggle>(); model.switchSlider = knob.transform;
        model.soundOnPosition = knob.transform.localPosition + Vector3.up * .033f;
        var text = new GameObject("Call screen"); text.transform.SetParent(model.transform, false);
        text.transform.localPosition = new Vector3(0, .012f, -.021f);
        model.screen = text.AddComponent<TextMeshPro>();
        model.screen.rectTransform.sizeDelta = new Vector2(.159f, .235f);
        model.screen.fontSize = .19f; model.screen.alignment = TextAlignmentOptions.Center;
        return model;
    }

    private GameObject Part(string name, Vector3 position, Vector3 size, float radius, float bevel, Color color, Material source)
    {
        var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        part.transform.SetParent(transform, false); part.transform.localPosition = position;
        Mesh mesh = RoundedBox(size, radius, bevel); meshes.Add(mesh); part.GetComponent<MeshFilter>().sharedMesh = mesh;
        var mat = new Material(source);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", null);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", null);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", .3f);
        mat.color = color; materials.Add(mat);
        var renderer = part.GetComponent<MeshRenderer>(); renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return part;
    }

    // Four rounded rings and two caps: a small, replaceable prototype mesh.
    private static Mesh RoundedBox(Vector3 size, float radius, float bevel)
    {
        const int steps = 4, ringSize = 4 * (steps + 1);
        var vertices = new List<Vector3>(); var triangles = new List<int>();
        float halfX = size.x / 2, halfY = size.y / 2, halfZ = size.z / 2;
        radius = Mathf.Min(radius, halfX, halfY);
        bevel = Mathf.Clamp(bevel, 0, Mathf.Min(radius * .5f, halfZ * .5f));
        for (int ring = 0; ring < 4; ring++)
        {
            float inset = ring == 0 || ring == 3 ? bevel : 0;
            float z = ring == 0 ? -halfZ : ring == 1 ? -halfZ + bevel : ring == 2 ? halfZ - bevel : halfZ;
            for (int corner = 0; corner < 4; corner++)
            {
                float cx = (corner == 0 || corner == 3 ? 1 : -1) * (halfX - radius);
                float cy = (corner < 2 ? 1 : -1) * (halfY - radius);
                for (int step = 0; step <= steps; step++)
                {
                    float angle = (corner * 90 + step * 90f / steps) * Mathf.Deg2Rad;
                    vertices.Add(new Vector3(cx + Mathf.Cos(angle) * (radius - inset), cy + Mathf.Sin(angle) * (radius - inset), z));
                }
            }
        }
        for (int ring = 0; ring < 3; ring++)
            for (int i = 0; i < ringSize; i++)
            {
                int a = ring * ringSize + i, b = ring * ringSize + (i + 1) % ringSize;
                int c = a + ringSize, d = b + ringSize;
                triangles.AddRange(new[] { a, b, c, b, d, c });
            }
        int front = vertices.Count; vertices.Add(new Vector3(0, 0, -halfZ));
        int back = vertices.Count; vertices.Add(new Vector3(0, 0, halfZ));
        for (int i = 0; i < ringSize; i++)
        {
            int next = (i + 1) % ringSize;
            triangles.AddRange(new[] { front, next, i, back, 3 * ringSize + i, 3 * ringSize + next });
        }
        var mesh = new Mesh { name = "Rounded phone part" };
        mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    private void OnDestroy()
    {
        foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
        foreach (var material in materials) if (material != null) Destroy(material);
    }
}
