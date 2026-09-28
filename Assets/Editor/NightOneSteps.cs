using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// NIGHT 1: ONE NIGHT THAT CHANGES THE NEXT MORNING (claude/ace-after-dark.md §8 step 5; the Night 1 slice)
//
//   Fixit Fidget > Night > Night 1 - Put Grace's gnome and Ace's trophy shelf in the scene (Edit Mode)
//     Adds "22 - Night 1 slice" under the café layout:
//       * "Grace's gnome (Barnaby)": a NightTrophy by her front door at 12 West Street, beside the door
//         on a spot that is clear (nothing in the way, off the line people walk out on, away from where
//         they wait). By day it's scenery; at night Ace can take it (E).
//       * "Ace's trophy shelf": a small oak shelf on the back wall behind the counter, with Barnaby's copy
//         on it, hidden until Ace has taken him (TrophyShelf).
//     The gnome is made of simple shapes, a placeholder to swap for a real model: the prefab
//     "Night 1 - Barnaby the garden gnome.prefab", its cone and colours in "Night 1 - Barnaby.asset".
//     Running it again rebuilds the group. Photos and notes go to Logs/Night/night-one-setup-<time>/.
//     Nothing outside the group changes; save the scene yourself once the diff is read.
//   ... > Night 1 - Take the gnome and the shelf out again: removes the group (Edit > Undo puts it back).
//   ... > Night 1 - Check the scene (read-only).
//   ... > Night 1 - Play from Day 1's recap (lab): a café lab session (a test save; your playtest save is not
//         used) that opens on the recap of a made-up Day 1 on which Grace brought her camera in and
//         mentioned Barnaby. The recap's button leads into the night.
//   ... > Night 1 - Play check, keeping a straight face (lab, drives itself)
//   ... > Night 1 - Play check, cracking (lab, drives itself): the same lab, driven by NightOneCheck from the
//         recap to Grace's visit on Day 2. Report and photos: Logs/Night/night-one-check-<time>/.
//   ... > Night 1 - Play check, the morning after Day 2 (lab, drives itself): as a save from before this step
//         plays it: Grace's two visits are behind her and she never mentioned Barnaby ("Take the garden
//         gnome"); on Day 3, with no featured regular, she comes in first as the morning's visitor.
internal static class NightOneSteps
{
    const string Tag = "[Night 1] ";
    const string Menu = "Fixit Fidget/Night/";
    const string LayoutRoot = "ACE'S CAFE - layout study 02";
    const string GroupName = "22 - Night 1 slice";
    const string GnomeName = "Grace's gnome (Barnaby)";
    const string ShelfName = "Ace's trophy shelf";
    const string ShelfCopyName = "Barnaby (on the shelf)";
    const string Folder = "Assets/Playtests/AcesCafeLayout";
    const string PrefabPath = Folder + "/Night 1 - Barnaby the garden gnome.prefab";
    const string AssetPath = Folder + "/Night 1 - Barnaby.asset";
    const string CafePalette = Folder + "/Cafe palette.asset";
    const string SoundBankPath = "Assets/Data/Resources/Sound bank.asset";

    // The shelf: its top, against the back wall (its face is at z 18), over the rear storage, between the
    // sunset print (to x -0.4) and the wall lamp (from x 1.28). Behind Ace at the counter.
    static readonly Vector3 ShelfAt = new Vector3(.35f, 1.85f, 17.99f);
    const float BackWall = 18f;

    // Where by Grace's door to try, in order: (out from the door, to the side; positive is the door's
    // right, the way it faces). Her door has a stoop of three steps with an iron railing either side, so first the top step
    // beside the door, inside the railing ("twenty years on my front step"), then the pavement just
    // outside the railing. The first place that is clear wins.
    static readonly (float along, float side)[] Candidates =
    {
        (.2f, -.62f), (.2f, .62f), (.16f, -.64f), (.16f, .64f), (.24f, -.6f), (.24f, .6f),
        (.45f, -1.1f), (.45f, 1.1f), (.6f, -1.15f), (.6f, 1.15f), (.35f, -1.25f), (.35f, 1.25f),
    };
    // Barnaby's footprint: his stone is 0.22 m across; all of it must stand on one level.
    const float Footing = .09f;

    // ------------------------------------------------------------------ put it in the scene

    [MenuItem(Menu + "Night 1 - Put Grace's gnome and Ace's trophy shelf in the scene (Edit Mode)")]
    static void SetUp()
    {
        var report = new StringBuilder("Night 1 - put Grace's gnome and Ace's trophy shelf in the scene\n"
            + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "\n\n");
        try
        {
            CityPackChecks.RequireScene();
            GameObject layout = Layout();
            HomeDoor door = GraceDoor();
            NightWalk walk = layout.GetComponentInChildren<NightWalk>(true);
            report.AppendLine(walk == null
                ? "NOTE: there is no NightWalk in the scene yet (Night walk 1): the gnome and the shelf go in, but no night follows the day until there is one."
                : $"The night walk is here ({PathOf(walk.transform)}); Night Follows The Day is {(walk.followsTheDay ? "on" : "OFF")}.");

            // Where he stands, before anything changes (the old group, if any, is left out of the search).
            Transform old = layout.transform.Find(GroupName);
            Placement spot = FindGnomeSpot(door, old, report);

            Mesh cone = Cone();                                       // the asset file's main asset: first
            Dictionary<string, Material> colours = Materials();
            GameObject prefab = BuildGnomePrefab(report, colours, cone);

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Night 1 - put Grace's gnome and Ace's shelf in the scene");
            if (old != null)
            {
                Undo.DestroyObjectImmediate(old.gameObject);
                report.AppendLine("Took the old group out first (it is rebuilt from scratch).");
            }

            var group = new GameObject(GroupName);
            group.transform.SetParent(layout.transform, false);

            // Barnaby by Grace's door: the NightTrophy with a trigger to be found by (and a small solid
            // collider so Ace doesn't walk through him); both go off with him once he's taken.
            var gnome = new GameObject(GnomeName);
            gnome.transform.SetParent(group.transform, false);
            gnome.transform.SetPositionAndRotation(spot.position, Quaternion.LookRotation(spot.facing, Vector3.up));
            var reachable = gnome.AddComponent<BoxCollider>();
            reachable.isTrigger = true;
            reachable.center = new Vector3(0f, .35f, 0f);
            reachable.size = new Vector3(.9f, .7f, .9f);
            var solid = gnome.AddComponent<BoxCollider>();
            solid.center = new Vector3(0f, .26f, 0f);
            solid.size = new Vector3(.22f, .52f, .2f);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, gnome.transform);
            visual.name = "Barnaby the garden gnome";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            var trophy = gnome.AddComponent<NightTrophy>();
            trophy.thingId = NightThings.GraceGnome;
            trophy.visual = visual;

            // Ace's shelf behind the counter, with Barnaby's copy (hidden until he has been taken).
            Material oak = PaletteMaterial("InteriorOak") ?? colours["shelf oak"];
            Material iron = PaletteMaterial("InteriorInk") ?? colours["shelf iron"];
            var shelfRoot = new GameObject(ShelfName);
            shelfRoot.transform.SetParent(group.transform, false);
            shelfRoot.transform.SetPositionAndRotation(ShelfAt, Quaternion.identity);
            Part(shelfRoot.transform, "Oak board", PrimitiveType.Cube, new Vector3(0f, -.0175f, -.12f), Vector3.zero, new Vector3(.9f, .035f, .24f), oak);
            foreach (float x in new[] { -.33f, .33f })
            {
                Part(shelfRoot.transform, "Bracket", PrimitiveType.Cube, new Vector3(x, -.1f, -.008f), Vector3.zero, new Vector3(.025f, .13f, .016f), iron);
                Part(shelfRoot.transform, "Bracket arm", PrimitiveType.Cube, new Vector3(x, -.044f, -.1f), Vector3.zero, new Vector3(.025f, .018f, .18f), iron);
            }
            var copy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, shelfRoot.transform);
            copy.name = ShelfCopyName;
            copy.transform.localPosition = new Vector3(0f, 0f, -.115f);
            copy.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            copy.SetActive(false);
            var shelf = shelfRoot.AddComponent<TrophyShelf>();
            shelf.slots = new[] { new TrophyShelf.Slot { thingId = NightThings.GraceGnome, shown = copy } };

            Undo.RegisterCreatedObjectUndo(group, "Night 1 group");
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            report.AppendLine();
            report.AppendLine($"Barnaby stands at {V(spot.position)}, {F(Vector3.Distance(Flat(spot.position), Flat(door.transform.position)))} m from Grace's door " +
                              $"({V(door.transform.position)}), facing the street; on {spot.standingOn}.");
            report.AppendLine("The spot is clear: all of him on one level, nothing in the way, off the line people walk out on, away from where they wait.");
            report.AppendLine("Places tried:");
            report.Append(spot.tried.ToString());
            report.AppendLine($"The shelf's top is at {V(ShelfAt)} on the back wall; Barnaby's copy on it is hidden until he has been taken.");
            report.AppendLine($"Materials: the shelf in the café's own {(PaletteMaterial("InteriorOak") != null ? "oak and ink" : "colours (the café palette was not found)")}.");

            // Photos, by day: Grace's step, and the shelf with and without Barnaby.
            string folder = LogFolder("night-one-setup");
            Vector3 forward = Flat(door.transform.forward).normalized, right = Flat(door.transform.right).normalized;
            Vector3 g = spot.position;
            NightWalkSteps.Capture(Path.Combine(folder, "1-grace-step-from-the-street.png"), g + forward * 3.4f - right * 1.6f + Vector3.up * 1.6f, g + Vector3.up * .4f, 50f, false);
            NightWalkSteps.Capture(Path.Combine(folder, "2-grace-step-close.png"), g + forward * 1.25f + right * .55f + Vector3.up * .75f, g + Vector3.up * .28f, 45f, false);
            NightWalkSteps.Capture(Path.Combine(folder, "3-grace-house-from-above.png"), g + forward * 7f + right * 2f + Vector3.up * 9f, g, 45f, false);
            Vector3 shelfLook = ShelfAt + new Vector3(0f, .2f, -.12f);
            copy.SetActive(true);
            try
            {
                NightWalkSteps.Capture(Path.Combine(folder, "4-shelf-with-barnaby.png"), new Vector3(ShelfAt.x + .25f, 1.7f, 15.3f), shelfLook, 50f, false);
                NightWalkSteps.Capture(Path.Combine(folder, "5-shelf-from-the-room.png"), new Vector3(2.4f, 1.75f, 11.2f), shelfLook, 50f, false);
            }
            finally { copy.SetActive(false); }
            NightWalkSteps.Capture(Path.Combine(folder, "6-shelf-empty.png"), new Vector3(ShelfAt.x + .25f, 1.7f, 15.3f), shelfLook, 50f, false);
            report.AppendLine();
            report.AppendLine("Photos: 1-3 Grace's step (by day), 4-5 the shelf with Barnaby (as after the night), 6 the shelf as it is until then.");
            report.AppendLine("The scene changed: read the diff, then save it (Ctrl+S). Edit > Undo takes the whole group out again.");
            File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
            Selection.activeGameObject = gnome;
            Debug.Log(Tag + "Put Grace's gnome and Ace's shelf in the scene. " + folder + "\n" + report);
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Putting the gnome and the shelf in FAILED: " + e.Message + "\n" + report + "\n" + e);
        }
    }

    [MenuItem(Menu + "Night 1 - Take the gnome and the shelf out again")]
    static void TakeOut()
    {
        try
        {
            CityPackChecks.RequireScene();
            Transform group = Layout().transform.Find(GroupName);
            if (group == null) { Debug.Log(Tag + "There is no Night 1 group to take out."); return; }
            Undo.DestroyObjectImmediate(group.gameObject);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log(Tag + "Took Grace's gnome and Ace's shelf out (Edit > Undo puts them back). The prefab and its asset stay; they are harmless.");
        }
        catch (Exception e) { Debug.LogError(Tag + "FAILED: " + e.Message); }
    }

    // ------------------------------------------------------------------ check the scene (read-only)

    [MenuItem(Menu + "Night 1 - Check the scene (read-only)")]
    static void CheckScene()
    {
        var report = new StringBuilder("Night 1 - check the scene (read-only)\n"
            + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "\n\n");
        int failures = 0;
        void Check(bool ok, string what)
        {
            if (!ok) failures++;
            report.AppendLine((ok ? "PASS  " : "FAIL  ") + what);
        }
        try
        {
            Check(SceneManager.GetActiveScene().path == AcesCafeLayoutSetup.ScenePath, "the Ace's Cafe layout scene is open");
            GameObject layout = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == LayoutRoot);
            Check(layout != null, "the café layout is in the scene");
            if (layout == null) throw new InvalidOperationException("nothing more to check");

            NightWalk walk = layout.GetComponentInChildren<NightWalk>(true);
            Check(walk != null, "there is a night walk (Night walk 1)");
            if (walk != null)
            {
                Check(walk.followsTheDay, "Night Follows The Day is on (the recap's button leads into the night)");
                Check(walk.nightEndsAt > walk.nightHour, $"the night runs from {walk.nightHour:0.#} to {walk.nightEndsAt:0.#} (dawn)");
            }

            Transform group = layout.transform.Find(GroupName);
            Check(group != null, $"\"{GroupName}\" is in the scene (Night 1 - Put Grace's gnome...)");
            NightTrophy[] trophies = CityPackChecks.InScene<NightTrophy>();
            Check(trophies.Length == NightThings.All.Count, $"one thing to take for each night thing ({trophies.Length} in the scene, {NightThings.All.Count} known)");
            HomeDoor door = CityPackChecks.InScene<HomeDoor>().FirstOrDefault(d => d.homeId == "home.grace");
            Check(door != null, "Grace's front door (home.grace) is in the scene");
            foreach (NightTrophy trophy in trophies)
            {
                NightThing thing = NightThings.Find(trophy.thingId);
                Check(thing != null, $"{trophy.name}: its id \"{trophy.thingId}\" is a known night thing");
                Check(trophy.visual != null && trophy.visual.activeSelf, $"{trophy.name}: it is there to be seen by day");
                Collider[] colliders = trophy.GetComponents<Collider>();
                Check(colliders.Any(c => c.isTrigger && c.enabled), $"{trophy.name}: it has a trigger to be found by at night");
                Check(colliders.Any(c => !c.isTrigger && c.enabled), $"{trophy.name}: it is solid (Ace doesn't walk through it)");
                Check(trophy.gameObject.layer == 0, $"{trophy.name}: it is on the Default layer (the interactor's night search sees it)");
                if (thing != null && thing.owner == GraceCameraEpisode.ProfileId && door != null)
                {
                    float away = Vector3.Distance(Flat(trophy.transform.position), Flat(door.transform.position));
                    Check(away < 1.6f, $"{trophy.name}: by Grace's door ({F(away)} m from it)");
                    Check(Mathf.Abs(trophy.transform.position.y - door.transform.position.y) < .4f, $"{trophy.name}: at the door's level (y {F(trophy.transform.position.y)}, the door {F(door.transform.position.y)})");
                    float side = Mathf.Abs(Vector3.Dot(trophy.transform.position - door.transform.position, Flat(door.transform.right).normalized));
                    Check(side >= .6f, $"{trophy.name}: off the line people walk out of the door on ({F(side)} m to the side)");
                }
            }

            TrophyShelf[] shelves = CityPackChecks.InScene<TrophyShelf>();
            Check(shelves.Length == 1, $"one trophy shelf ({shelves.Length})");
            foreach (TrophyShelf shelf in shelves)
            {
                Vector3 p = shelf.transform.position;
                Check(CafeDaylight.CafeInside.Contains(new Vector2(p.x, p.z)), $"the shelf is inside the café ({V(p)})");
                Check(Mathf.Abs(p.z - BackWall) < .1f, "the shelf is against the back wall, behind the counter");
                foreach (NightThing thing in NightThings.All)
                {
                    TrophyShelf.Slot slot = shelf.slots.FirstOrDefault(s => s != null && s.thingId == thing.id);
                    Check(slot != null && slot.shown != null, $"the shelf has a place for {thing.name}");
                    if (slot != null && slot.shown != null)
                        Check(!slot.shown.activeSelf, $"{thing.name}'s place on the shelf is empty in the scene (it fills from the night's ledger while playing)");
                }
            }

            RecapUI recap = CityPackChecks.InScene<RecapUI>().FirstOrDefault();
            Check(recap != null, "the recap is in the scene");
            if (recap != null)
            {
                var button = new SerializedObject(recap).FindProperty("nextDayButton").objectReferenceValue as Button;
                Check(button != null && button.GetComponentInChildren<TMP_Text>(true) != null,
                    "the recap's button has a text label (it reads \"Close up for the night\" before the night)");
            }

            CustomerSpawner spawner = CityPackChecks.InScene<CustomerSpawner>().FirstOrDefault();
            Check(spawner != null, "the customer spawner is in the scene");
            if (spawner != null)
            {
                SerializedProperty schedule = new SerializedObject(spawner).FindProperty("schedule");
                var featured = new List<string>();
                for (int i = 0; schedule != null && i < schedule.arraySize; i++)
                    if (schedule.GetArrayElementAtIndex(i).objectReferenceValue is DayDefinition day && day.featuredRegular != null)
                        featured.Add($"Day {day.dayNumber}: {day.featuredRegular.PersistentId}");
                Check(featured.Any(f => f.EndsWith(": " + GraceCameraEpisode.ProfileId, StringComparison.Ordinal)),
                    "Grace is in the day schedule, so she can come in the morning after (" + (featured.Count > 0 ? string.Join(", ", featured) : "no featured regulars") + ")");
            }

            SoundBank bank = AssetDatabase.LoadAssetAtPath<SoundBank>(SoundBankPath);
            string[] cues = { "night.take", "night.home", "night.dawn", "face.held", "face.cracked" };
            string[] missing = bank == null ? cues : cues.Where(c => bank.Find(c) == null).ToArray();
            Check(missing.Length == 0, missing.Length == 0 ? "the sound bank has the five Night 1 cues (silent until they have files)"
                : "the sound bank is missing " + string.Join(", ", missing) + " (Fixit Fidget > Sound > Create or update the sound bank)");

            report.AppendLine();
            report.AppendLine(failures == 0 ? "All good." : $"{failures} problem(s).");
            string file = Path.Combine(LogFolder("night-one-scene-check"), "report.txt");
            File.WriteAllText(file, report.ToString());
            if (failures == 0) Debug.Log(Tag + "The scene is ready for Night 1.\n" + report);
            else Debug.LogError(Tag + $"The scene check found {failures} problem(s).\n" + report);
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Scene check FAILED: " + e.Message + "\n" + report);
        }
    }

    // ------------------------------------------------------------------ the lab

    [MenuItem(Menu + "Night 1 - Play from Day 1's recap (lab)")]
    static void PlayFromTheRecap() => StartLab(0, 1);

    [MenuItem(Menu + "Night 1 - Play check, keeping a straight face (lab, drives itself)")]
    static void PlayCheckStraightFace() => StartLab((int)NightOneCheck.Mode.StraightFace, 1);

    [MenuItem(Menu + "Night 1 - Play check, cracking (lab, drives itself)")]
    static void PlayCheckCracking() => StartLab((int)NightOneCheck.Mode.Crack, 1);

    // As a save from before this step would play it: Grace's two visits are behind her (she never mentioned
    // Barnaby), there is no featured regular on Day 3, and she comes in first thing as the morning's visitor.
    [MenuItem(Menu + "Night 1 - Play check, the morning after Day 2 (lab, drives itself)")]
    static void PlayCheckAfterDayTwo() => StartLab((int)NightOneCheck.Mode.StraightFace, 2);

    [MenuItem(Menu + "Night 1 - Play from Day 1's recap (lab)", true)]
    [MenuItem(Menu + "Night 1 - Play check, keeping a straight face (lab, drives itself)", true)]
    [MenuItem(Menu + "Night 1 - Play check, cracking (lab, drives itself)", true)]
    [MenuItem(Menu + "Night 1 - Play check, the morning after Day 2 (lab, drives itself)", true)]
    static bool CanPlay() => !EditorApplication.isPlayingOrWillChangePlaymode;

    static void StartLab(int check, int day)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        try
        {
            CityPackChecks.RequireScene();
            if (Layout().transform.Find(GroupName) == null)
                Debug.LogWarning(Tag + "There is no Night 1 group in the scene yet (Night 1 - Put Grace's gnome and Ace's trophy shelf in the scene): " +
                                 "the night will run, but there is nothing by Grace's door to take.");
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + e.Message);
            return;
        }
        string path = Path.Combine(Application.persistentDataPath, CafeLab.SaveFileName);
        SaveCheckpointStorage.Write(path, day == 2 ? DayTwoRecap() : DayOneRecap());
        PlayerPrefs.SetInt(CafeLab.PendingKey, 1);
        PlayerPrefs.SetInt(CafeLab.AutopilotKey, 0);
        if (check > 0) PlayerPrefs.SetInt(NightOneCheck.PendingKey, check);
        else PlayerPrefs.DeleteKey(NightOneCheck.PendingKey);
        PlayerPrefs.Save();
        Debug.Log(Tag + (check > 0
            ? $"Night 1 play check ({(check == (int)NightOneCheck.Mode.Crack ? "cracking" : "keeping a straight face")}): it drives itself from Day {day}'s recap " +
              $"to Grace's visit on Day {day + 1} (two or three minutes). Keep the Game view in front and leave the mouse and keyboard alone."
            : "Night 1 lab: Day 1's recap. Its button (Close up for the night) leads into the night; Barnaby is on the front step of " +
              "Grace's saffron house on the corner (12 West Street), and E inside the café's door calls it a night.")
            + $" Test save {path}; your playtest save is not used.");
        EditorApplication.EnterPlaymode();
    }

    // A made-up Day 1 for the lab only: Grace brought her camera in (done well: a Good), told Ace about it
    // and mentioned Barnaby; the day is closed and its recap is on screen. The playtest save is never touched.
    static SaveData DayOneRecap()
    {
        var facts = new List<NotebookFactData>(GraceIntake());
        NotebookFactData barnaby = NightThings.Mentioned(NightThings.GnomeOfGrace, NotebookEntries.GraceName);
        barnaby.day = 1;
        facts.Add(barnaby);
        return Recap(1, 64, 17, 16, new RecapSaveData
        {
            day = 1, peopleServed = 3, ordersCompleted = 3, repairs = 1, drinks = 2, good = 1, tips = 4, earned = 64,
            closingTill = 64, elapsedSeconds = 360f,
        }, GraceMemory(1), facts);
    }

    // A made-up Day 2, like a save from before this step: her camera's return is behind her (the photo came
    // out, a print for the shop), and she never mentioned Barnaby. Day 3 has no featured regular.
    static SaveData DayTwoRecap()
    {
        RegularMemoryData grace = GraceMemory(2);
        grace.graceReturnAcknowledged = true;
        grace.gracePhotoClaimed = true;
        grace.gracePhotoVariant = GracePhotoOutcome.Clear.ToString();
        grace.relationship = 4;
        var facts = new List<NotebookFactData>(GraceIntake());
        NotebookFactData photo = NotebookEntries.GraceReturn(NotebookEntries.GraceName, GracePhotoOutcome.Clear);
        photo.day = 2;
        facts.Add(photo);
        return Recap(2, 131, 15, 13, new RecapSaveData
        {
            day = 2, peopleServed = 4, ordersCompleted = 4, repairs = 1, drinks = 3, perfect = 1, tips = 3, earned = 67,
            closingTill = 131, elapsedSeconds = 360f,
        }, grace, facts);
    }

    static RegularMemoryData GraceMemory(int day) => new RegularMemoryData
    {
        profileId = GraceCameraEpisode.ProfileId,
        visits = day,
        relationship = 2,
        lastSeenDay = day,
        lastVisitHappy = true,
        lastJobAccepted = true,
        lastVisitServed = true,
        lastGrade = nameof(JobGrade.Good),
        graceCameraAttempted = true,
        graceCameraReturned = true,
        graceCameraGrade = nameof(JobGrade.Good),
        graceCameraDay = 1,
    };

    static IEnumerable<NotebookFactData> GraceIntake()
    {
        foreach (NotebookFactData fact in NotebookEntries.GraceIntake(NotebookEntries.GraceName))
        {
            fact.day = 1;
            yield return fact;
        }
    }

    static SaveData Recap(int day, int money, int cups, int beans, RecapSaveData recap, RegularMemoryData grace, List<NotebookFactData> facts) => new SaveData
    {
        day = day,
        money = money,
        cups = cups,
        beans = beans,
        dayCompleted = true,
        recap = recap,
        regularMemories = new[] { grace },
        notebook = facts.ToArray(),
        night = new NightSaveData(),
    };

    // A check request that never became a Play session must not make the next lab session a check.
    [InitializeOnLoadMethod]
    static void ClearStaleCheckRequest()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode && PlayerPrefs.HasKey(NightOneCheck.PendingKey))
        {
            PlayerPrefs.DeleteKey(NightOneCheck.PendingKey);
            PlayerPrefs.Save();
        }
    }

    // ------------------------------------------------------------------ Barnaby, from simple shapes

    // Colours: a glazed garden gnome that has stood outside for twenty years.
    static readonly (string key, Color colour, float smoothness, float metallic)[] Palette =
    {
        ("hat", new Color(.72f, .19f, .15f), .42f, 0f),
        ("coat", new Color(.21f, .36f, .6f), .42f, 0f),
        ("skin", new Color(.94f, .76f, .62f), .38f, 0f),
        ("beard", new Color(.94f, .93f, .89f), .36f, 0f),
        ("boots", new Color(.3f, .19f, .11f), .34f, 0f),
        ("stone", new Color(.45f, .48f, .41f), .08f, 0f),
        ("nose", new Color(.92f, .6f, .53f), .4f, 0f),
        ("eyes", new Color(.08f, .08f, .1f), .6f, 0f),
        ("brass", new Color(.78f, .62f, .3f), .5f, .6f),
        ("shelf oak", new Color(.72f, .55f, .36f), .22f, 0f),
        ("shelf iron", new Color(.12f, .13f, .14f), .3f, .4f),
    };

    static GameObject BuildGnomePrefab(StringBuilder report, Dictionary<string, Material> m, Mesh cone)
    {
        var root = new GameObject("Barnaby the garden gnome");
        try
        {
            Transform t = root.transform;
            // About 0.53 m tall, standing on a round stone; he faces +z.
            Part(t, "Stone base", PrimitiveType.Cylinder, new Vector3(0f, .02f, 0f), Vector3.zero, new Vector3(.22f, .02f, .22f), m["stone"]);
            Part(t, "Boot left", PrimitiveType.Sphere, new Vector3(-.042f, .062f, .018f), Vector3.zero, new Vector3(.075f, .05f, .1f), m["boots"]);
            Part(t, "Boot right", PrimitiveType.Sphere, new Vector3(.042f, .062f, .018f), Vector3.zero, new Vector3(.075f, .05f, .1f), m["boots"]);
            Part(t, "Coat", PrimitiveType.Capsule, new Vector3(0f, .178f, 0f), Vector3.zero, new Vector3(.17f, .118f, .155f), m["coat"]);
            Part(t, "Belt", PrimitiveType.Cylinder, new Vector3(0f, .2f, 0f), Vector3.zero, new Vector3(.176f, .012f, .162f), m["boots"]);
            Part(t, "Buckle", PrimitiveType.Cube, new Vector3(0f, .2f, .081f), Vector3.zero, new Vector3(.036f, .028f, .01f), m["brass"]);
            Part(t, "Arm left", PrimitiveType.Capsule, new Vector3(-.092f, .222f, .012f), new Vector3(0f, 0f, -14f), new Vector3(.046f, .062f, .046f), m["coat"]);
            Part(t, "Arm right", PrimitiveType.Capsule, new Vector3(.092f, .222f, .012f), new Vector3(0f, 0f, 14f), new Vector3(.046f, .062f, .046f), m["coat"]);
            Part(t, "Hand left", PrimitiveType.Sphere, new Vector3(-.105f, .163f, .02f), Vector3.zero, Vector3.one * .038f, m["skin"]);
            Part(t, "Hand right", PrimitiveType.Sphere, new Vector3(.105f, .163f, .02f), Vector3.zero, Vector3.one * .038f, m["skin"]);
            ConePart(t, "Beard", new Vector3(0f, .318f, .045f), new Vector3(180f, 0f, 0f), new Vector3(.15f, .125f, .12f), cone, m["beard"]);
            Part(t, "Face", PrimitiveType.Sphere, new Vector3(0f, .338f, .022f), Vector3.zero, new Vector3(.1f, .096f, .09f), m["skin"]);
            Part(t, "Nose", PrimitiveType.Sphere, new Vector3(0f, .334f, .068f), Vector3.zero, Vector3.one * .036f, m["nose"]);
            Part(t, "Eye left", PrimitiveType.Sphere, new Vector3(-.022f, .352f, .062f), Vector3.zero, Vector3.one * .014f, m["eyes"]);
            Part(t, "Eye right", PrimitiveType.Sphere, new Vector3(.022f, .352f, .062f), Vector3.zero, Vector3.one * .014f, m["eyes"]);
            ConePart(t, "Hat", new Vector3(0f, .366f, -.004f), new Vector3(-9f, 0f, 0f), new Vector3(.132f, .16f, .132f), cone, m["hat"]);
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
            if (!ok || saved == null) throw new InvalidOperationException("Could not save " + PrefabPath);
            report.AppendLine($"Barnaby: {PrefabPath} (simple shapes; its cone and colours in {AssetPath}).");
            return saved;
        }
        finally { Object.DestroyImmediate(root); }
    }

    static GameObject Part(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 euler, Vector3 scale, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    static GameObject ConePart(Transform parent, string name, Vector3 position, Vector3 euler, Vector3 scale, Mesh cone, Material material)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        go.GetComponent<MeshFilter>().sharedMesh = cone;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    // The colours (sub-assets of Night 1 - Barnaby.asset, after its cone), made or brought up to date.
    static Dictionary<string, Material> Materials()
    {
        Object[] existing = AssetDatabase.LoadAllAssetsAtPath(AssetPath);
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) throw new InvalidOperationException("The URP Lit shader was not found.");
        var result = new Dictionary<string, Material>();
        foreach (var (key, colour, smoothness, metallic) in Palette)
        {
            string name = "Night 1 - " + key;
            Material material = existing.OfType<Material>().FirstOrDefault(x => x.name == name);
            if (material == null)
            {
                material = new Material(lit) { name = name, enableInstancing = true };
                AssetDatabase.AddObjectToAsset(material, AssetPath);
            }
            if (material.shader != lit) material.shader = lit;
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(material);
            result[key] = material;
        }
        AssetDatabase.SaveAssets();
        return result;
    }

    // A low-poly cone (base radius 0.5 at y 0, tip at y 1), flat-shaded like the city pack: the hat, and
    // the beard upside down. The main asset of Night 1 - Barnaby.asset.
    static Mesh Cone()
    {
        const int Sides = 16;
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();
        Vector3 tip = new Vector3(0f, 1f, 0f);
        for (int i = 0; i < Sides; i++)
        {
            float a0 = i * Mathf.PI * 2f / Sides, a1 = (i + 1) * Mathf.PI * 2f / Sides;
            Vector3 p0 = new Vector3(Mathf.Cos(a0) * .5f, 0f, Mathf.Sin(a0) * .5f);
            Vector3 p1 = new Vector3(Mathf.Cos(a1) * .5f, 0f, Mathf.Sin(a1) * .5f);
            // The side (outward: p0, tip, p1) and the base (downward: centre, p0, p1).
            AddTriangle(p0, tip, p1, new Vector2((float)i / Sides, 0f), new Vector2((i + .5f) / Sides, 1f), new Vector2((i + 1f) / Sides, 0f));
            AddTriangle(Vector3.zero, p0, p1, new Vector2(.5f, .5f), new Vector2(.5f + p0.x, .5f + p0.z), new Vector2(.5f + p1.x, .5f + p1.z));
        }
        var fresh = new Mesh { name = "Night 1 - gnome cone" };
        fresh.SetVertices(vertices);
        fresh.SetNormals(normals);
        fresh.SetUVs(0, uvs);
        fresh.SetTriangles(triangles, 0);
        fresh.RecalculateBounds();

        Mesh existing = AssetDatabase.LoadAllAssetsAtPath(AssetPath).OfType<Mesh>().FirstOrDefault(x => x.name == fresh.name);
        if (existing != null)
        {
            EditorUtility.CopySerialized(fresh, existing);
            Object.DestroyImmediate(fresh);
            EditorUtility.SetDirty(existing);
            return existing;
        }
        if (AssetDatabase.LoadMainAssetAtPath(AssetPath) == null) AssetDatabase.CreateAsset(fresh, AssetPath);
        else AssetDatabase.AddObjectToAsset(fresh, AssetPath);
        return fresh;

        void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc)
        {
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            int start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            normals.Add(n); normals.Add(n); normals.Add(n);
            uvs.Add(ua); uvs.Add(ub); uvs.Add(uc);
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
        }
    }

    static Material PaletteMaterial(string name) =>
        AssetDatabase.LoadAllAssetsAtPath(CafePalette).OfType<Material>().FirstOrDefault(m => m.name == name);

    // ------------------------------------------------------------------ where by Grace's door

    sealed class Placement
    {
        public Vector3 position;
        public Vector3 facing;
        public string standingOn = "?";
        public readonly StringBuilder tried = new StringBuilder();
    }

    // Tries the places in Candidates against a copy of everything solid around her door (its meshes made
    // solid in a scene of its own, so the real scene is never touched). A place wins when the ground there
    // is at the door's level, all of his footprint stands on that one level (not across a step's edge),
    // nothing is in the way of a gnome, and no waiting spot is close by. None: nothing is placed.
    static Placement FindGnomeSpot(HomeDoor door, Transform skip, StringBuilder report)
    {
        Vector3 origin = door.transform.position;
        Vector3 forward = Flat(door.transform.forward).normalized, right = Flat(door.transform.right).normalized;
        Transform house = door.transform.parent != null ? door.transform.parent : door.transform;
        List<Vector3> waits = house.GetComponentsInChildren<Transform>(true)
            .Where(x => x.name.StartsWith("Wait here", StringComparison.Ordinal)).Select(x => x.position).ToList();
        var result = new Placement { facing = forward };
        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var source = new Dictionary<Collider, string>();
            int solids = CopySolids(preview, new Bounds(origin + Vector3.up * 1.5f, new Vector3(8f, 5f, 8f)), skip, source);
            Physics.SyncTransforms();
            PhysicsScene physics = preview.GetPhysicsScene();
            var overlaps = new Collider[16];
            report.AppendLine($"Looked for a clear spot by Grace's door among {solids} solid things around it; {waits.Count} waiting spot(s) there.");
            foreach (var (along, side) in Candidates)
            {
                Vector3 p = origin + forward * along + right * side;
                string where = $"  {F(along)} m out, {F(Mathf.Abs(side))} m to the door's {(side > 0f ? "right" : "left")}: ";
                if (!Ground(physics, p, origin.y, out RaycastHit hit))
                {
                    result.tried.AppendLine(where + "nothing underneath");
                    continue;
                }
                string under = source.TryGetValue(hit.collider, out string s) ? s : hit.collider.name;
                float rise = hit.point.y - origin.y;
                if (rise > .22f || rise < -.45f)
                {
                    result.tried.AppendLine(where + $"the first thing underneath is at {F(hit.point.y)} m, not the ground ({under})");
                    continue;
                }
                string uneven = null;
                foreach (Vector3 corner in new[] { forward, -forward, right, -right })
                {
                    Vector3 q = p + corner * Footing;
                    if (!Ground(physics, q, origin.y, out RaycastHit h) || Mathf.Abs(h.point.y - hit.point.y) > .03f)
                    {
                        uneven = "part of him would stand off it (a step's edge)";
                        break;
                    }
                }
                if (uneven != null)
                {
                    result.tried.AppendLine(where + uneven + $" (on {under})");
                    continue;
                }
                int count = physics.OverlapBox(hit.point + Vector3.up * .3f, new Vector3(.12f, .26f, .12f), overlaps,
                    Quaternion.LookRotation(forward), ~0, QueryTriggerInteraction.Ignore);
                if (count > 0)
                {
                    string inWay = source.TryGetValue(overlaps[0], out string w) ? w : overlaps[0].name;
                    result.tried.AppendLine(where + "something is in the way (" + inWay + ")");
                    continue;
                }
                float nearestWait = waits.Count == 0 ? 99f : waits.Min(x => Vector3.Distance(Flat(x), Flat(hit.point)));
                if (nearestWait < .65f)
                {
                    result.tried.AppendLine(where + $"a waiting spot is {F(nearestWait)} m away");
                    continue;
                }
                result.tried.AppendLine(where + $"clear (on {under} at {F(hit.point.y)} m; the nearest waiting spot {F(nearestWait)} m away)");
                result.position = hit.point;
                result.standingOn = under;
                return result;
            }
            throw new InvalidOperationException("There is no clear spot by Grace's door for Barnaby; nothing was changed. Places tried:\n" + result.tried);
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    // The first thing under <at>, from a little above the door's level.
    static bool Ground(PhysicsScene physics, Vector3 at, float doorY, out RaycastHit hit) =>
        physics.Raycast(new Vector3(at.x, doorY + 1.3f, at.z), Vector3.down, out hit, 2.4f, ~0, QueryTriggerInteraction.Ignore);

    // Everything that could be in the way around <region>, as colliders in the preview scene: the meshes that
    // are drawn (made solid), and the colliders that are there already.
    static int CopySolids(Scene preview, Bounds region, Transform skip, Dictionary<Collider, string> source)
    {
        int count = 0;
        var cache = new Dictionary<Mesh, bool>();
        foreach (MeshFilter filter in CityPackChecks.InScene<MeshFilter>())
        {
            var renderer = filter.GetComponent<MeshRenderer>();
            Mesh mesh = filter.sharedMesh;
            if (renderer == null || mesh == null || !renderer.enabled || !filter.gameObject.activeInHierarchy) continue;
            if (renderer.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) continue;
            if (!renderer.bounds.Intersects(region)) continue;
            if (skip != null && filter.transform.IsChildOf(skip)) continue;
            if (filter.GetComponents<Component>().Any(c => c != null && c.GetType().Name.StartsWith("TextMeshPro", StringComparison.Ordinal))) continue;
            if (!NightCollisionList.CanCollide(mesh, cache)) continue;
            try
            {
                var copy = Solid(preview, filter.transform).AddComponent<MeshCollider>();
                copy.sharedMesh = mesh;
                source[copy] = PathOf(filter.transform);
                count++;
            }
            catch (Exception) { }
        }
        foreach (Collider c in CityPackChecks.InScene<Collider>())
        {
            if (c == null || !c.enabled || c.isTrigger || !c.gameObject.activeInHierarchy || !c.bounds.Intersects(region)) continue;
            if (skip != null && c.transform.IsChildOf(skip)) continue;
            GameObject go = Solid(preview, c.transform);
            Collider copy = null;
            switch (c)
            {
                case BoxCollider b: { var x = go.AddComponent<BoxCollider>(); x.center = b.center; x.size = b.size; copy = x; break; }
                case SphereCollider sp: { var x = go.AddComponent<SphereCollider>(); x.center = sp.center; x.radius = sp.radius; copy = x; break; }
                case CapsuleCollider cp: { var x = go.AddComponent<CapsuleCollider>(); x.center = cp.center; x.radius = cp.radius; x.height = cp.height; x.direction = cp.direction; copy = x; break; }
                case MeshCollider mc when mc.sharedMesh != null: { var x = go.AddComponent<MeshCollider>(); x.convex = mc.convex; x.sharedMesh = mc.sharedMesh; copy = x; break; }
            }
            if (copy == null) { Object.DestroyImmediate(go); continue; }
            source[copy] = PathOf(c.transform) + " (its collider)";
            count++;
        }
        return count;
    }

    static GameObject Solid(Scene preview, Transform like)
    {
        var go = new GameObject("solid") { hideFlags = HideFlags.HideAndDontSave };
        SceneManager.MoveGameObjectToScene(go, preview);
        go.transform.SetPositionAndRotation(like.position, like.rotation);
        go.transform.localScale = like.lossyScale;
        return go;
    }

    // ------------------------------------------------------------------ helpers

    static GameObject Layout()
    {
        GameObject layout = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == LayoutRoot);
        if (layout == null) throw new InvalidOperationException($"\"{LayoutRoot}\" is not in the open scene.");
        return layout;
    }

    static HomeDoor GraceDoor()
    {
        HomeDoor door = CityPackChecks.InScene<HomeDoor>().FirstOrDefault(d => d.homeId == "home.grace");
        if (door == null) throw new InvalidOperationException("Grace's front door (a HomeDoor with id home.grace) is not in the scene: night step 3 put it there.");
        return door;
    }

    static string LogFolder(string what)
    {
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Night",
            what + "-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture)));
        Directory.CreateDirectory(folder);
        return folder;
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    static string PathOf(Transform t)
    {
        var parts = new List<string>();
        for (var x = t; x != null; x = x.parent) parts.Add(x.name);
        parts.Reverse();
        return string.Join("/", parts);
    }

    static string F(float v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    static string V(Vector3 v) => $"({F(v.x)}, {F(v.y)}, {F(v.z)})";
}
