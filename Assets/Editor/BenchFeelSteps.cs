#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// ---------------------------------------------------------------------------
// THE BENCH, V2, PART 4b: DIRT, GLASS AND THE WAKE CUES, IN THE PREFABS (9 Oct 2026; claude/bench-spec-v2.md §2.4-2.6)
//
//   Fixit Fidget > Bench > Bench 4 - Dirt, glass and the wake cues (the prefabs)
//       PhoneRepair: the three grime spots of the "gunked up" fault sized to the glass (36 mm, inside its edge; the
//       stand-in's 50 mm hung over it) and one of them made a SMUDGE (the cloth's); the working screen (Screen/Glass,
//       made by Devices 2) switched on by every fault but the cracked one; the wake: the screen lights.
//       PocketWatch: a thumbprint on the crystal (Smudge_Crystal, at the crystal's plane over the dial) for the "debris
//       inside" fault; the wake: the crown is wound and the watch ticks.
//       GraceReunionCamera: the lens's dirt is a SMUDGE now (a lens is wiped, never brushed), lying on the front
//       element's dome; the wake: the advance is wound and the new shutter fires.
//       Run after Devices 2 (it needs the Glass piece). Safe to run again: it finds what it made by name.
// ---------------------------------------------------------------------------
static class BenchFeelSteps
{
    const string Tag = "[Bench 4] ";

    [MenuItem("Fixit Fidget/Bench/Bench 4 - Dirt, glass and the wake cues (the prefabs)")]
    static void Wire()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError(Tag + "This one is for Edit Mode: stop Play first."); return; }
        var report = new StringBuilder();
        int failures = 0;
        failures += Phone(report);
        failures += Watch(report);
        failures += Camera(report);
        AssetDatabase.SaveAssets();
        string folder = Path.Combine("Logs", "Bench", "bench-4-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
        string line = Tag + (failures == 0 ? "Dirt, glass and the wake cues are in the prefabs. " : failures + " step(s) FAILED. ") + folder + "\n" + report;
        if (failures == 0) Debug.Log(line); else Debug.LogError(line);
    }

    // ---------- the phone ----------

    static int Phone(StringBuilder report)
    {
        const string path = "Assets/AssetsPrefabs/PhoneRepair.prefab";
        report.AppendLine(path);
        int failures = 0;
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Transform screen = Find(root.transform, "Screen");
            Transform glass = Find(root.transform, "Glass");
            Transform fresh = Find(root.transform, "Fresh");
            if (screen == null) { report.AppendLine("FAIL  no Screen"); return 1; }
            if (glass == null) { report.AppendLine("FAIL  no Screen/Glass: run Devices 2 first (it makes the working screen)"); return 1; }

            // the grime spots, sized to the glass; the middle one a smudge
            var spots = new (string name, Vector3 at, GrimeSpot.Kind kind)[]
            {
                ("Grime_Extra_Phone_1", new Vector3(.015f, .002f, .020f), GrimeSpot.Kind.Dirt),
                ("Grime_Extra_Phone_2", new Vector3(-.014f, .002f, .002f), GrimeSpot.Kind.Smudge),
                ("Grime_Extra_Phone_3", new Vector3(.002f, .002f, -.024f), GrimeSpot.Kind.Dirt),
            };
            foreach (var (name, at, kind) in spots)
            {
                Transform t = Find(root.transform, name);
                GrimeSpot spot = t != null ? t.GetComponent<GrimeSpot>() : null;
                if (spot == null) { failures++; report.AppendLine($"FAIL  no grime spot '{name}'"); continue; }
                t.localPosition = at;
                t.localScale = new Vector3(.036f, .003f, .036f);
                spot.Configure(kind);
                report.AppendLine($"PASS  {name}: {kind}, 36 mm at {V(at)} on the glass");
            }

            // the working screen on every fault but the cracked one
            var definition = root.GetComponent<DeviceDefinition>();
            if (definition == null || definition.faults == null) { failures++; report.AppendLine("FAIL  no DeviceDefinition with faults"); }
            else
            {
                for (int i = 0; i < definition.faults.Length; i++)
                {
                    DeviceFault fault = definition.faults[i];
                    bool cracked = fault.enableObjects != null && fault.enableObjects.Any(g => g != null && g.GetComponent<ReplaceablePart>() != null);
                    if (cracked) { Without(fault, glass.gameObject); report.AppendLine($"PASS  fault {i} \"{fault.description}\": the broken screen (the working one off)"); }
                    else { With(fault, glass.gameObject); report.AppendLine($"PASS  fault {i} \"{fault.description}\": the working screen on"); }
                }
                glass.gameObject.SetActive(false);   // as authored: a fault switches it on
            }

            // the wake: whichever screen is on lights up
            DeviceWake wake = root.GetComponent<DeviceWake>() ?? root.AddComponent<DeviceWake>();
            wake.Configure(DeviceWake.Kind.ScreenLights, fresh, glass);
            report.AppendLine("PASS  DeviceWake: the screen lights (Fresh or Glass, whichever is on)");

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        catch (Exception e) { failures++; report.AppendLine("FAIL  " + e.Message); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        report.AppendLine();
        return failures;
    }

    // ---------- the watch ----------

    static int Watch(StringBuilder report)
    {
        const string path = "Assets/AssetsPrefabs/PocketWatch.prefab";
        report.AppendLine(path);
        int failures = 0;
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            // a thumbprint on the crystal, for the "debris inside" fault: at the crystal's plane, the bezel's top (+10 mm),
            // over the dial recessed 3.5 mm under it. The crystal itself isn't modelled, and the case's collider is one
            // solid up to the bezel top (nothing was to be done on the dial side until now), so the print lies a hair over
            // that plane: where the glass would be, and in the brush's and the cloth's reach
            Transform smudge = Find(root.transform, "Smudge_Crystal");
            if (smudge == null)
            {
                var go = new GameObject("Smudge_Crystal", typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider), typeof(GrimeSpot));
                go.transform.SetParent(root.transform, false);
                go.layer = root.layer;
                smudge = go.transform;
                report.AppendLine("PASS  Smudge_Crystal: made");
            }
            smudge.localPosition = new Vector3(.012f, .0115f, -.010f);   // its underside (the surface it lies on) at the bezel top
            smudge.localRotation = Quaternion.identity;
            smudge.localScale = new Vector3(.045f, .003f, .045f);
            smudge.GetComponent<GrimeSpot>().Configure(GrimeSpot.Kind.Smudge);
            smudge.gameObject.SetActive(false);
            var definition = root.GetComponent<DeviceDefinition>();
            DeviceFault debris = definition != null && definition.faults != null
                ? definition.faults.FirstOrDefault(f => f.type == FaultType.Cleaning) : null;
            if (debris == null) { failures++; report.AppendLine("FAIL  no cleaning fault on the watch"); }
            else { With(debris, smudge.gameObject); report.AppendLine($"PASS  fault \"{debris.description}\": the thumbprint on the crystal (the cloth's)"); }

            Transform crown = Find(root.transform, "Crown");
            if (crown == null) { failures++; report.AppendLine("FAIL  no Crown"); }
            else
            {
                DeviceWake wake = root.GetComponent<DeviceWake>() ?? root.AddComponent<DeviceWake>();
                wake.Configure(DeviceWake.Kind.CrownWinds, crown);
                report.AppendLine("PASS  DeviceWake: the crown is wound and the watch ticks");
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        catch (Exception e) { failures++; report.AppendLine("FAIL  " + e.Message); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        report.AppendLine();
        return failures;
    }

    // ---------- Grace's camera ----------

    static int Camera(StringBuilder report)
    {
        const string path = "Assets/GraceShowcase/GraceReunionCamera.prefab";
        report.AppendLine(path);
        int failures = 0;
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Transform lens = Find(root.transform, "Lens grime");
            GrimeSpot spot = lens != null ? lens.GetComponent<GrimeSpot>() : null;
            if (spot == null) { failures++; report.AppendLine("FAIL  no 'Lens grime'"); }
            else
            {
                // the front element is a dome: sagitta 2.8 mm over a 21.8 mm radius (hero_camera.lens_glass), a sphere of 86.2 mm
                spot.Configure(GrimeSpot.Kind.Smudge, .0862f);
                report.AppendLine("PASS  Lens grime: a smudge (the cloth's), on the front element's dome");
            }
            Transform blade = Find(root.transform, "Working shutter blade");
            if (blade == null) { failures++; report.AppendLine("FAIL  no 'Working shutter blade'"); }
            else
            {
                DeviceWake wake = root.GetComponent<DeviceWake>() ?? root.AddComponent<DeviceWake>();
                wake.Configure(DeviceWake.Kind.ShutterFires, blade);
                report.AppendLine("PASS  DeviceWake: the advance is wound and the new shutter fires");
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        catch (Exception e) { failures++; report.AppendLine("FAIL  " + e.Message); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        report.AppendLine();
        return failures;
    }

    // ---------- hands ----------

    static void With(DeviceFault fault, GameObject g)
    {
        var list = (fault.enableObjects ?? Array.Empty<GameObject>()).Where(x => x != null).ToList();
        if (!list.Contains(g)) list.Add(g);
        fault.enableObjects = list.ToArray();
    }

    static void Without(DeviceFault fault, GameObject g)
    {
        if (fault.enableObjects == null) return;
        fault.enableObjects = fault.enableObjects.Where(x => x != null && x != g).ToArray();
    }

    static Transform Find(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        return null;
    }

    static string V(Vector3 v) => string.Format(CultureInfo.InvariantCulture, "({0:0.0000}, {1:0.0000}, {2:0.0000})", v.x, v.y, v.z);
}
#endif
