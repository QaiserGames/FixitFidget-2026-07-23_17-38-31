#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// ---------------------------------------------------------------------------
// THE CLOTH, OUT OF THE CADDY (9 Oct 2026, night; his note: "i also see the cloth stuck inside the tray, it needs to sit
// and look correctly and be used correctly")
//
//   Fixit Fidget > Bench > Bench 2b - The cloth: folded on the mat, out of the caddy
//       Bench 2 stood the cloth in the caddy like the pointed tools: 12 mm into the block, leaning back 18°. A tool's
//       origin is its working tip, and the cloth's is its underside, so the whole cloth sat in the wood with its top
//       showing. This lays it folded on the mat in front of the caddy, flat, turned a little as a hand puts it down,
//       and spaces the four pointed tools evenly across the caddy (they were spaced for five). Nothing else moves. In
//       the hand the cloth is pressed flat on the glass (ToolPickup.PressedFlat). Photographs the bench from the
//       close-up camera and from beside the caddy, before and after. Saves the scene.
//   Fixit Fidget > Bench > Bench 2b - Undo: the cloth back in the caddy
//       The poses Bench 2 gave them (computed from the caddy, as Bench 2 does). Saves the scene.
//
// The poses live here so Bench 2 (a rebuild) and Bench 2b (the scene as it is) can't drift apart.
// Report and photos: Logs/Bench/cloth-<time>/.
// ---------------------------------------------------------------------------
static class BenchClothSteps
{
    const string Menu = "Fixit Fidget/Bench/";
    const string Tag = "[Bench 2b] ";
    const string StageName = "Stage (v2)";

    // The cloth on the mat: 10 cm in front of the caddy's middle (the caddy is 8 cm deep and the cloth 9: a centimetre of
    // mat between them), 4 mm right of it, half a millimetre over the mat so its underside never fights the mat for
    // the pixel. Turned 180° like the tools (its open edge toward the camera, its fold away: the way it is held, so
    // picking it up doesn't spin it) and 8° more, as a hand leaves it. Clear of the device's first two places on the
    // mat (the middle, and 12 cm left / 11.5 cm forward).
    internal static Vector3 ClothRest(Vector3 caddyLocal, float matTop) =>
        new Vector3(caddyLocal.x + .004f, matTop + .0005f, caddyLocal.z - .100f);
    internal static readonly Quaternion ClothRestRotation = Quaternion.AngleAxis(188f, Vector3.up);

    // A pointed tool's stand in the caddy: across its middle 9 cm, 12 mm into its top (the tip is the origin); the
    // handle leans back 18° (away from the camera at -Z) and the flat side (+Z) faces the camera.
    internal static Vector3 ToolStand(Vector3 caddyLocal, int index, int count) =>
        caddyLocal + new Vector3(Mathf.Lerp(-.045f, .045f, count > 1 ? index / (float)(count - 1) : .5f), .0175f - .012f, 0f);
    internal static readonly Quaternion ToolStandRotation = Quaternion.AngleAxis(18f, Vector3.right) * Quaternion.AngleAxis(180f, Vector3.up);

    // Bench 2's order of the tools in the caddy (its Tools list), for the undo.
    static readonly ToolType[] Bench2Order = { ToolType.Brush, ToolType.Screwdriver, ToolType.Pry, ToolType.Tweezers, ToolType.Cloth };

    [MenuItem(Menu + "Bench 2b - The cloth: folded on the mat, out of the caddy")]
    static void LayTheCloth() => Apply(true);

    [MenuItem(Menu + "Bench 2b - Undo: the cloth back in the caddy")]
    static void UndoTheCloth() => Apply(false);

    static void Apply(bool onTheMat)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError(Tag + "Stop Play first."); return; }
        if (SceneManager.GetActiveScene().path != AcesCafeLayoutSetup.ScenePath)
        {
            Debug.LogError(Tag + "Open the café scene (" + AcesCafeLayoutSetup.ScenePath + ") first.");
            return;
        }
        BenchRig rig = UnityEngine.Object.FindAnyObjectByType<BenchRig>(FindObjectsInactive.Include);
        Transform stage = rig != null ? rig.transform.Find(StageName) : null;
        Transform caddy = stage != null ? stage.Find("Tool caddy") : null;
        Transform mat = stage != null ? stage.Find("Mat") : null;
        if (caddy == null || mat == null)
        {
            Debug.LogError(Tag + "No bench stage (InspectRig/" + StageName + " with its Tool caddy and Mat): run Bench 2 - Build the bench stage first.");
            return;
        }
        float matTop = mat.localPosition.y + mat.localScale.y * .5f;    // the mat is a slab laid 3 mm under the top, 6 mm thick

        string folder = Path.Combine("Logs", "Bench", "cloth-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss"));
        Directory.CreateDirectory(folder);
        var report = new StringBuilder();
        report.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + (onTheMat ? "Bench 2b - the cloth on the mat" : "Bench 2b - Undo: the cloth back in the caddy"));
        Photos(folder, "before", rig.transform, caddy);

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName(onTheMat ? "Bench 2b - The cloth on the mat" : "Bench 2b - Undo");
        int standing = onTheMat ? Bench2Order.Count(t => t != ToolType.Cloth) : Bench2Order.Length;
        int k = 0, moved = 0;
        foreach (ToolType tool in Bench2Order)
        {
            Transform slot = stage.Find("Slot " + tool);
            if (slot == null) { report.AppendLine($"  Slot {tool}: not found (skipped)"); if (tool != ToolType.Cloth || !onTheMat) k++; continue; }
            Vector3 was = slot.localPosition;
            Undo.RecordObject(slot, "tool stand");
            if (tool == ToolType.Cloth && onTheMat)
            {
                slot.localPosition = ClothRest(caddy.localPosition, matTop);
                slot.localRotation = ClothRestRotation;
            }
            else
            {
                slot.localPosition = ToolStand(caddy.localPosition, k++, standing);
                slot.localRotation = ToolStandRotation;
            }
            moved++;
            report.AppendLine($"  Slot {tool}: {V(was)} -> {V(slot.localPosition)} (stage frame), turned {slot.localEulerAngles.y:0}°");
        }

        // What the cloth looks like now, in numbers: flat, on the mat, clear of the caddy.
        Physics.SyncTransforms();
        ToolPickup cloth = stage.GetComponentsInChildren<ToolPickup>(true).FirstOrDefault(t => t.tool == ToolType.Cloth);
        if (cloth != null)
        {
            Bounds c = RendererBounds(cloth.transform), box = RendererBounds(caddy);
            float matWorldTop = mat.position.y + mat.lossyScale.y * .5f;
            report.AppendLine($"  The cloth: its top {Vector3.Angle(cloth.transform.up, Vector3.up):0.0}° off level; underside {(c.min.y - matWorldTop) * 1000f:0.0} mm over the mat; " +
                              (c.Intersects(box) ? "TOUCHING the caddy" : $"clear of the caddy by {Gap(c, box) * 1000f:0} mm") +
                              $"; on the mat: {(InPlan(c, RendererBounds(mat)) ? "yes" : "NO")}");
        }
        else report.AppendLine("  No cloth (ToolPickup Cloth) under the stage.");

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Photos(folder, "after", rig.transform, caddy);
        report.AppendLine($"Moved {moved} stand(s). Scene saved. Photos: {folder}/before-*.png, after-*.png (the close-up camera; beside the caddy).");
        File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
        File.WriteAllText(Path.Combine("Logs", "Bench", "cloth-last.txt"), report.ToString());
        Debug.Log(Tag + report);
    }

    // The bench as the close-up camera sees it, and a closer look from beside the caddy.
    static void Photos(string folder, string when, Transform rig, Transform caddy)
    {
        Transform eye = rig.Find("CM_InspectCam");
        if (eye != null)
        {
            CinemachineCamera cm = eye.GetComponent<CinemachineCamera>();
            float fov = cm != null ? cm.Lens.FieldOfView : 40f;
            CafeSecondPassSteps.Capture(Path.Combine(folder, when + "-1-the-close-up.png"), eye.position, eye.position + eye.forward, fov, false);
        }
        Vector3 at = caddy.position + new Vector3(0f, -.01f, -.06f);
        CafeSecondPassSteps.Capture(Path.Combine(folder, when + "-2-beside-the-caddy.png"), at + new Vector3(.16f, .15f, -.20f), at, 45f, false);
    }

    static Bounds RendererBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(root.position, Vector3.zero);
        Bounds b = renderers[0].bounds;
        foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
        return b;
    }

    static bool InPlan(Bounds inner, Bounds outer) =>
        inner.min.x >= outer.min.x - .001f && inner.max.x <= outer.max.x + .001f && inner.min.z >= outer.min.z - .001f && inner.max.z <= outer.max.z + .001f;

    static float Gap(Bounds a, Bounds b)
    {
        float dx = Mathf.Max(0f, Mathf.Max(a.min.x - b.max.x, b.min.x - a.max.x));
        float dz = Mathf.Max(0f, Mathf.Max(a.min.z - b.max.z, b.min.z - a.max.z));
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    static string V(Vector3 v) => $"({v.x:0.000}, {v.y:0.000}, {v.z:0.000})";
}
#endif
