using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 15 Oct: Bench 3 found the stage 6 mm into the bench top, and the wood shows through the mat in play (the cups, the well and
// patches along its front edge). This read-only probe says what is where: the mat's drawn box, the height the devices rest
// at, every surface a ray meets going down through the mat, and every drawn thing outside the stage whose box reaches the
// mat's height under it. Nothing changes.
static class MatContactProbe
{
    [MenuItem("Fixit Fidget/Checks/Probe - what the repair mat sits on (read-only)")]
    static void Run()
    {
        var sb = new StringBuilder("[Mat probe]\n");
        BenchStage stage = Object.FindAnyObjectByType<BenchStage>(FindObjectsInactive.Include);
        if (stage == null) { Debug.LogError("[Mat probe] No BenchStage in the open scene."); return; }
        Transform matT = stage.Mat;
        Renderer[] matRs = matT != null ? matT.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
        if (matRs.Length == 0) { Debug.LogError("[Mat probe] The stage has no drawn mat."); return; }
        Bounds mat = matRs[0].bounds;
        foreach (Renderer r in matRs) mat.Encapsulate(r.bounds);
        sb.AppendLine($"the stage root '{stage.name}' at y {stage.transform.position.y:0.0000}; the mat '{matT.name}' at y {matT.position.y:0.0000}");
        sb.AppendLine($"the mat's drawn box: y {mat.min.y:0.0000} .. {mat.max.y:0.0000}, x {mat.min.x:0.000} .. {mat.max.x:0.000}, z {mat.min.z:0.000} .. {mat.max.z:0.000}");
        Vector3 rest = stage.MatToWorld(Vector2.zero, BenchStage.MatTop);
        sb.AppendLine($"MatToWorld(0, 0, MatTop {BenchStage.MatTop * 1000f:0.0} mm) = y {rest.y:0.0000} (where a device's lowest point rests)");
        if (stage.LegacyMat != null && stage.LegacyMat != matT)
            sb.AppendLine($"the v2 slab '{stage.LegacyMat.name}': active {stage.LegacyMat.gameObject.activeInHierarchy}, at y {stage.LegacyMat.position.y:0.0000}, scale y {stage.LegacyMat.lossyScale.y:0.0000}, colliders on: {stage.LegacyMat.GetComponentsInChildren<Collider>(false).Count(c => c.enabled)}");

        Physics.SyncTransforms();
        sb.AppendLine("rays straight down through the mat (every hit; [stage] = one of the stage's own):");
        for (int ix = -1; ix <= 1; ix++)
            for (int iz = -1; iz <= 1; iz++)
            {
                var from = new Vector3(mat.center.x + ix * mat.extents.x * .8f, mat.max.y + .3f, mat.center.z + iz * mat.extents.z * .8f);
                RaycastHit[] hits = Physics.RaycastAll(from, Vector3.down, .6f, ~0, QueryTriggerInteraction.Collide).OrderBy(h => h.distance).ToArray();
                sb.Append($"  at ({from.x:0.000}, {from.z:0.000}): ");
                sb.AppendLine(hits.Length == 0 ? "nothing" : string.Join(" | ", hits.Take(6).Select(h =>
                    $"{(h.collider.transform.IsChildOf(stage.transform) ? "[stage] " : "")}{h.collider.name} ({h.collider.GetType().Name}{(h.collider.isTrigger ? ", trigger" : "")}) y {h.point.y:0.0000}")));
            }

        sb.AppendLine("drawn things outside the stage whose box covers the mat's middle and reaches within 3 cm of its height:");
        var c = mat.center;
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
        {
            if (r.transform.IsChildOf(stage.transform) || !r.enabled) continue;
            Bounds b = r.bounds;
            if (c.x < b.min.x || c.x > b.max.x || c.z < b.min.z || c.z > b.max.z) continue;
            if (b.max.y < mat.min.y - .03f || b.min.y > mat.max.y + .03f) continue;
            sb.AppendLine($"  '{Path(r.transform)}' ({r.GetType().Name}): y {b.min.y:0.0000} .. {b.max.y:0.0000}");
        }
        Debug.Log(sb.ToString());
    }

    static string Path(Transform t)
    {
        string p = t.name;
        for (Transform u = t.parent; u != null; u = u.parent) p = u.name + "/" + p;
        return p.Length > 160 ? "..." + p.Substring(p.Length - 160) : p;
    }
}
