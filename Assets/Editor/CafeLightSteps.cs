#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using S = CafeRedesignShared;

// ---------------------------------------------------------------------------
// THE BENCH LAMP'S LIGHT, TUNED TO THE CLOSE-UP (the café redesign, 10 Oct 2026; Claude, after Repair bar 1's photos)
//
//   Fixit Fidget > Cafe redesign > Light 0 - Photograph the bench lamp at six strengths (read-only)
//   Fixit Fidget > Cafe redesign > Light 1 - Tune the bench lamp to the close-up (scene)
//   Fixit Fidget > Cafe redesign > Light 1 - Undo (scene)
//
// Bar 1 lit the kit's magnifier lamp with cafe_bar.json's spot: #FFE4C2, 1.6, range 1.5, 50°, its source 0.39 m over the
// mat. This project's lights are not in physical units: URP's spot and point lights fall off with the square of the
// distance, so 1.6 at 0.39 m lands about ten times the room's own light on the mat. In Repair bar 1's close-up the middle
// of the mat burned white, which is exactly where the device lies.
//
// Light 0 photographs the close-up (CM_InspectCam's own pose and lens) and Ace's view with a phone lying on the mat, at
// six strengths, then puts the lamp and the scene back as they were (the phone is a temporary copy of HD_Phone.fbx, never
// saved). Light 1 sets the strength chosen from those photos (BenchLampIntensity) and a softer edge, through the
// redesign's record (CafeRedesignShared.ChangeLight), so Light 1 - Undo puts 1.6 back. The routes and the guards are not
// touched by a light, so this step neither re-bakes nor runs them.
// ---------------------------------------------------------------------------
internal static class CafeLightSteps
{
    const string Title = "Light 1 - The bench lamp, tuned to the close-up", Label = "light-1";
    const string PhotosLabel = "light-0";
    const string MenuPhotos = S.Menu + "Light 0 - Photograph the bench lamp at six strengths (read-only)";
    const string MenuDo = S.Menu + "Light 1 - Tune the bench lamp to the close-up (scene)";
    const string MenuUndo = S.Menu + "Light 1 - Undo (scene)";

    const string LightName = "Bench lamp light (spot, cafe_bar.json)";
    const string WorkAreaPath = "03 - repair work area";
    const string PhoneModel = "Assets/Art/Models/Devices/HD_Phone.fbx";

    // What Light 0 photographs (the last is Bar 1's, for comparison).
    static readonly float[] Strengths = { .06f, .10f, .14f, .20f, .30f, 1.6f };

    // Chosen from Light 0's photos (10 Oct): at 0.06-0.10 there is no pool to see; 0.14 a faint one; 0.20 a clear warm pool
    // with the phone's cracks and the parts still crisp; 0.30 starts to wash the parts out; 1.6 burns them white. The
    // edge: a 30° inner cone inside the 50° outer one, so the pool fades out over the mat's edges instead of ending in a ring.
    // cafe_bar.json and CafeBarSteps.LampIntensity carry 0.2 too, so a fresh Bar 1 lights the lamp this way.
    const float BenchLampIntensity = .20f;
    const float BenchLampInnerAngle = 30f;

    [MenuItem(MenuPhotos)]
    static void PhotographStrengths() => S.Check("Light 0 - The bench lamp at six strengths", PhotosLabel, run =>
    {
        Plan p = Survey(run, false);
        run.Folder = Folder(PhotosLabel);
        float was = p.light.intensity, wasInner = p.light.innerSpotAngle;
        GameObject phone = null;
        try
        {
            phone = TemporaryPhone(run, p);
            foreach (float k in Strengths)
            {
                p.light.intensity = k;
                p.light.innerSpotAngle = Mathf.Min(BenchLampInnerAngle, p.light.spotAngle);
                run.Phase = "strength-" + k.ToString("0.00", CultureInfo.InvariantCulture);
                Shots(run, p);
            }
        }
        finally
        {
            p.light.intensity = was;
            p.light.innerSpotAngle = wasInner;
            if (phone != null) Object.DestroyImmediate(phone);
            run.Phase = "0-check";
        }
        run.Check(Mathf.Approximately(p.light.intensity, was) && Mathf.Approximately(p.light.innerSpotAngle, wasInner),
                  $"the lamp is back as it was: intensity {S.F(was, "0.00")}, inner cone {S.F(wasInner, "0.0")}°");
        run.Check(GameObject.Find(PhoneCopyName) == null, "the phone's temporary copy is gone");
        run.Info($"Photos: {run.Folder} (strength-<k>-1-the-close-up, strength-<k>-2-where-ace-stands, for k = {string.Join(", ", Strengths.Select(k => S.F(k, "0.00")))}).");
        run.Info($"Light 1 sets {S.F(BenchLampIntensity, "0.00")} with a {S.F(BenchLampInnerAngle, "0")}° inner cone.");
    });

    [MenuItem(MenuDo)]
    static void Tune()
    {
        Plan plan = null;
        S.Step(Title, Label, run => plan = Survey(run, true), run => Change(run, plan), run => ShotsWithPhone(run, plan ?? Survey(run, false)),
               guards: false, rebake: false, roomPhotos: false);
    }

    [MenuItem(MenuUndo)]
    static void UndoTune() => S.UndoStep(Title, Label, rebake: false);

    [MenuItem(MenuPhotos, true)]
    [MenuItem(MenuDo, true)]
    [MenuItem(MenuUndo, true)]
    static bool NotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // ================================================================== the survey

    sealed class Plan
    {
        public Light light;
        public Transform inspectCam, inspectPoint, standPoint;
        public float closeFov = 40f;
    }

    static Plan Survey(S.Run run, bool forTheStep)
    {
        var p = new Plan();
        if (forTheStep) run.Require(!S.Done(run, Title), $"{Title} has already run in this scene (Light 1 - Undo puts the lamp back).");
        run.Require(S.Done(run, "Bar 1 - The front counter"), "Bar 1 hasn't run in this scene: there is no bench lamp yet.");
        run.Require(S.RepairBarDone(run), "Repair bar 1 hasn't run: the close-up is still at the back wall, away from the lamp.");

        Light[] lights = S.InScene<Light>(run).Where(l => l.name == LightName).ToArray();
        run.Require(lights.Length == 1, $"Expected one light named \"{LightName}\" (Bar 1's), found {lights.Length}.");
        p.light = lights[0];
        run.Line("The bench lamp's light:");
        run.Info($"{S.PathOf(p.light.transform)} at {S.V(p.light.transform.position)}: {p.light.type}, #{ColorUtility.ToHtmlStringRGB(p.light.color)}, " +
                 $"intensity {S.F(p.light.intensity, "0.00")}, range {S.F(p.light.range, "0.0")}, {S.F(p.light.spotAngle, "0")}° (inner {S.F(p.light.innerSpotAngle, "0.0")}°), shadows {p.light.shadows}");

        Transform work = S.Need(run, WorkAreaPath);
        p.inspectCam = Child(work, "CM_InspectCam");
        p.inspectPoint = Child(work, "InspectPoint");
        p.standPoint = Child(work, "StandPoint");
        run.Require(p.inspectCam != null && p.inspectPoint != null && p.standPoint != null, "The work area's CM_InspectCam, InspectPoint or StandPoint is missing.");
        CinemachineCamera close = p.inspectCam.GetComponent<CinemachineCamera>();
        if (close != null) p.closeFov = close.Lens.FieldOfView;

        float height = p.light.transform.position.y - p.inspectPoint.position.y;
        float flat = new Vector2(p.light.transform.position.x - p.inspectPoint.position.x, p.light.transform.position.z - p.inspectPoint.position.z).magnitude;
        run.Info($"Its source is {S.F(height, "0.000")} m over the inspect point and {S.F(flat, "0.000")} m off it sideways; the close-up looks from {S.V(p.inspectCam.position)} with a {S.F(p.closeFov, "0")}° lens.");
        run.Info($"Inverse square: at {S.F(height, "0.00")} m the mat gets about {S.F(p.light.intensity / (height * height), "0.0")} (intensity over distance squared) from it now; " +
                 $"at {S.F(BenchLampIntensity, "0.00")} about {S.F(BenchLampIntensity / (height * height), "0.00")}. The counter's pendants put about " +
                 $"{S.F(1.2f / (1.3f * 1.3f), "0.00")} on the counter's top (1.2 at about 1.3 m).");
        run.Check(p.light.type == LightType.Spot && Vector3.Dot(p.light.transform.forward, Vector3.down) > .999f, "it is a spot pointing straight down at the mat");
        run.Check(flat < .05f, "it stands over the inspect point (within 5 cm sideways)");
        return p;
    }

    static Transform Child(Transform root, string name) =>
        root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

    // ================================================================== the change

    static void Change(S.Run run, Plan p)
    {
        S.ChangeLight(run, p.light, l =>
        {
            l.intensity = BenchLampIntensity;
            l.innerSpotAngle = Mathf.Min(BenchLampInnerAngle, l.spotAngle);
        }, $"the bench lamp at {S.F(BenchLampIntensity, "0.00")} with a {S.F(BenchLampInnerAngle, "0")}° inner cone: 1.6 at 0.39 m burned the close-up's mat white (inverse square, no physical units)");

        run.Line("Checked after:");
        run.Check(Mathf.Abs(p.light.intensity - BenchLampIntensity) < 1e-4f, $"the lamp's intensity is {S.F(p.light.intensity, "0.00")}");
        run.Check(Mathf.Abs(p.light.innerSpotAngle - Mathf.Min(BenchLampInnerAngle, p.light.spotAngle)) < .01f && p.light.spotAngle > p.light.innerSpotAngle,
                  $"its cone: {S.F(p.light.innerSpotAngle, "0")}° inside {S.F(p.light.spotAngle, "0")}°");
        run.Check(ColorUtility.ToHtmlStringRGB(p.light.color) == "FFE4C2" && Mathf.Abs(p.light.range - 1.5f) < .001f && p.light.shadows == LightShadows.None,
                  "its colour, range and shadows are Bar 1's (#FFE4C2, 1.5, none)");
        CafeDaylight daylight = S.InScene<CafeDaylight>(run).FirstOrDefault();
        run.Check(daylight == null || daylight.warmLights == null || !daylight.warmLights.Contains(p.light), "it is still a task light, not one of CafeDaylight's evening lamps");
    }

    // ================================================================== the photos

    const string PhoneCopyName = "Light 0 - temporary phone (never saved)";

    // A copy of the hero phone lying flat on the inspect point, so the photos show the light on a device, not only the mat.
    static GameObject TemporaryPhone(S.Run run, Plan p)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(PhoneModel);
        if (model == null) { run.Info($"{PhoneModel} isn't in the project: the photos show the bare mat."); return null; }
        GameObject phone = Object.Instantiate(model);
        phone.name = PhoneCopyName;
        foreach (Transform t in phone.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.DontSave;
        foreach (Collider c in phone.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        // Lie it flat: of three turns, the one with the thinnest height.
        Quaternion best = Quaternion.identity;
        float thinnest = float.MaxValue;
        foreach (Quaternion q in new[] { Quaternion.identity, Quaternion.Euler(90f, 0f, 0f), Quaternion.Euler(-90f, 0f, 0f), Quaternion.Euler(0f, 0f, 90f) })
        {
            phone.transform.SetPositionAndRotation(Vector3.zero, q * Quaternion.Euler(0f, 180f, 0f));
            float h = S.RendererBounds(phone.transform, true).size.y;
            if (h < thinnest - 1e-4f) { thinnest = h; best = q; }
        }
        phone.transform.SetPositionAndRotation(Vector3.zero, best * Quaternion.Euler(0f, 180f, 0f));
        Bounds b = S.RendererBounds(phone.transform, true);
        Vector3 at = p.inspectPoint.position;
        float mat = at.y;
        phone.transform.position += new Vector3(at.x - b.center.x, mat + .001f - b.min.y, at.z - b.center.z);
        run.Info($"A copy of HD_Phone.fbx lies flat on the inspect point for the photos: {S.BoundsText(S.RendererBounds(phone.transform, true))} (removed after).");
        return phone;
    }

    static void Shots(S.Run run, Plan p)
    {
        S.Photo(run, "1-the-close-up", p.inspectCam.position, p.inspectCam.position + p.inspectCam.forward, p.closeFov);
        Vector3 eye = p.standPoint.position + Vector3.up * 1.65f;
        S.Photo(run, "2-where-ace-stands", eye, p.inspectPoint.position, 60f);
        S.Photo(run, "3-from-a-stool", new Vector3(-4.20f, 1.25f, 12.85f), new Vector3(-4.60f, 1.00f, 14.00f), 62f);
    }

    // The step's before and after: the same three photos with the phone's temporary copy on the mat.
    static void ShotsWithPhone(S.Run run, Plan p)
    {
        GameObject phone = null;
        try { phone = TemporaryPhone(run, p); Shots(run, p); }
        finally { if (phone != null) Object.DestroyImmediate(phone); }
    }

    static string Folder(string label)
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "CafeRedesign"));
        string folder = Path.Combine(root, label + "-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(folder);
        return folder;
    }
}
#endif
