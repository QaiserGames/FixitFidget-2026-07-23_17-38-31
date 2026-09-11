using System.Collections.Generic;
using UnityEngine;

// Only cups taken from the new dispenser opt in. Geometry is in metres, with an
// open rim and an independent liquid surface: the cup itself never changes colour.
[DisallowMultipleComponent]
public sealed class DrinkLiquidVisual : MonoBehaviour
{
    public const float Height = .112f;
    private Transform visualRoot, liquid;
    private LineRenderer ripple;
    private Material shellMaterial, liquidMaterial, rippleMaterial;
    private Mesh shellMesh, liquidMesh;
    private float amount;
    private bool agitated;
    private float rippleTime;
    public float FillAmount => amount;
    public float SurfaceHeight => Mathf.Lerp(.012f, .098f, amount);
    public Vector3 SurfacePosition => visualRoot != null
        ? visualRoot.TransformPoint(new Vector3(0, SurfaceHeight, 0)) : transform.position;

    public void Initialize()
    {
        if (visualRoot != null) return;
        foreach (Renderer old in GetComponentsInChildren<Renderer>(true)) old.enabled = false;
        var root = new GameObject("Open paper cup");
        root.transform.SetParent(transform, false); visualRoot = root.transform;
        Vector3 scale = transform.lossyScale;
        visualRoot.localScale = new Vector3(1 / Mathf.Max(.001f, Mathf.Abs(scale.x)),
            1 / Mathf.Max(.001f, Mathf.Abs(scale.y)), 1 / Mathf.Max(.001f, Mathf.Abs(scale.z)));
        shellMaterial = MakeMaterial("Warm ivory paper", new Color(.91f, .85f, .70f), .08f);
        liquidMaterial = MakeMaterial("Drink surface", new Color(.19f, .075f, .025f), .68f);
        shellMesh = BuildShell();
        MeshObject("Tapered open shell and rolled rim", visualRoot, shellMesh, shellMaterial);
        liquidMesh = BuildDisc();
        liquid = MeshObject("Rising liquid", visualRoot, liquidMesh, liquidMaterial).transform;
        // Renderer.enabled stays true, even while the empty surface is inactive.
        // PlayerCarry can snapshot it before pickup without freezing fill visibility.
        var rippleObject = new GameObject("Small pour ripples"); rippleObject.transform.SetParent(liquid, false);
        ripple = rippleObject.AddComponent<LineRenderer>(); ripple.useWorldSpace = false;
        ripple.loop = true; ripple.positionCount = 33; ripple.startWidth = ripple.endWidth = .0013f;
        rippleMaterial = new Material(Shader.Find("Sprites/Default")); ripple.sharedMaterial = rippleMaterial;
        ripple.startColor = ripple.endColor = new Color(1, .86f, .61f, .5f);
        ripple.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ripple.receiveShadows = false;
        SetFill(0, null, false);
    }

    public void SetFill(float fraction, DrinkDefinition recipe, bool isPouring)
    {
        Initialize(); amount = Mathf.Clamp01(fraction); agitated = isPouring;
        if (recipe != null) liquidMaterial.color = LiquidColor(recipe);
        liquid.localPosition = new Vector3(0, SurfaceHeight, 0);
        float radius = Mathf.Lerp(.028f, .036f, amount);
        liquid.localScale = new Vector3(radius, 1, radius);
        liquid.gameObject.SetActive(amount > .002f);
        ripple.gameObject.SetActive(agitated && amount > .002f);
    }

    private void Update()
    {
        if (!agitated || liquid == null || Time.timeScale <= 0) return;
        rippleTime += Time.deltaTime;
        float radius = Mathf.Lerp(.10f, .85f, Mathf.Repeat(rippleTime * 2.7f, 1));
        for (int i = 0; i < ripple.positionCount; i++)
        {
            float angle = i * Mathf.PI * 2 / ripple.positionCount;
            ripple.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, .0008f, Mathf.Sin(angle) * radius));
        }
    }

    public static Color LiquidColor(DrinkDefinition recipe)
    {
        string label = recipe != null ? recipe.drinkName.ToLowerInvariant() : "";
        if (label.Contains("latte")) return new Color(.65f, .40f, .18f);
        if (label.Contains("tea")) return new Color(.47f, .19f, .035f);
        if (label.Contains("chocolate")) return new Color(.23f, .085f, .032f);
        if (label.Contains("espresso")) return new Color(.27f, .105f, .025f);
        return new Color(.12f, .045f, .012f);
    }

    private static Material MakeMaterial(string label, Color color, float smoothness)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var material = new Material(shader) { name = label, color = color };
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        return material;
    }
    private static GameObject MeshObject(string label, Transform parent, Mesh mesh, Material material)
    {
        var item = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
        item.transform.SetParent(parent, false);
        item.GetComponent<MeshFilter>().sharedMesh = mesh; item.GetComponent<MeshRenderer>().sharedMaterial = material;
        return item;
    }
    private static Mesh BuildShell()
    {
        const int sides = 40;
        // Cross section goes up the outer wall, over the rolled lip, then down
        // the inner wall and across the floor. There is deliberately no top cap.
        Vector2[] profile = { new Vector2(.030f, 0), new Vector2(.042f, .109f),
            new Vector2(.0425f, .112f), new Vector2(.0385f, .112f),
            new Vector2(.0375f, .108f), new Vector2(.028f, .008f), new Vector2(0, .008f) };
        var vertices = new List<Vector3>(); var triangles = new List<int>();
        for (int ring = 0; ring < profile.Length; ring++)
            for (int i = 0; i <= sides; i++)
            {
                float angle = i * Mathf.PI * 2 / sides;
                vertices.Add(new Vector3(Mathf.Cos(angle) * profile[ring].x, profile[ring].y,
                    Mathf.Sin(angle) * profile[ring].x));
            }
        for (int ring = 0; ring < profile.Length - 1; ring++)
            for (int i = 0; i < sides; i++)
            {
                int a = ring * (sides + 1) + i, b = a + sides + 1;
                triangles.Add(a); triangles.Add(b); triangles.Add(a + 1);
                triangles.Add(a + 1); triangles.Add(b); triangles.Add(b + 1);
            }
        var mesh = new Mesh { name = "Hollow tapered cup" };
        mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }
    private static Mesh BuildDisc()
    {
        const int sides = 40;
        var vertices = new Vector3[sides + 1]; var triangles = new int[sides * 3];
        for (int i = 0; i < sides; i++)
        {
            float angle = i * Mathf.PI * 2 / sides;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            triangles[i * 3] = 0; triangles[i * 3 + 1] = (i + 1) % sides + 1; triangles[i * 3 + 2] = i + 1;
        }
        var mesh = new Mesh { name = "Liquid surface", vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
    }
    private static void Release(Object item)
    {
        if (item == null) return;
        if (Application.isPlaying) Destroy(item); else DestroyImmediate(item);
    }
    private void OnDestroy()
    {
        Release(shellMaterial); Release(liquidMaterial); Release(rippleMaterial); Release(shellMesh); Release(liquidMesh);
    }
}
