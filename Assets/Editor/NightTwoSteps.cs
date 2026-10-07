using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// NIGHT 2 AND THE OFFICER: GRACE'S CUPS, HIS CRATE, HIS PROFILE (session 3; claude/session-3-favours-stalling-officer.md)
//
//   Fixit Fidget > Night > Night 2 1 - Put Grace's cups and his crate in the scene (Edit Mode)
//     Adds "25 - Night 2 (Grace's cups)" under the café layout:
//       * Grace's cups: a loose sleeve (GH_CupSleeve) lying on top of the reunion cups' box (GH_CupBox) on her kitchen
//         worktop, where Break-ins 1 put the box. A NightTrophy for "grace.cups" that needs sight (never taken through a
//         wall), with a trigger to be found by and a small solid box. By day it's scenery; taken, it's gone for good;
//       * his crate: a cardboard box on the pavement in front of the trash can by the bins (a box by the bins by day), and
//         on it four paper cups, switched off (NightZeroSet.cornerCups: he sets them out the night Ace brings them). The
//         cup is a mesh of its own: Assets/Art/NightZero/Paper cup.asset;
//       * Ace's shelf: a place for the cups (a sleeve standing on end, hidden until the night's ledger puts them there).
//     Running it again rebuilds all three. Run it again after Bins 1 or Night 1 is run again (they rebuild the bins'
//     set and the shelf from scratch). Photos and notes go to Logs/Night/night-two-setup-<time>/. Save the scene
//     yourself once the diff is read.
//   ... > Night 2 1 - Take them out again
//   ... > The officer 1 - Make his profile and add his visits (Days 1 and 3)
//       * Assets/Data/Regulars/Regular_Officer.asset, made once and never overwritten (his sister rewrites it):
//         "Officer" (placeholder), a coffee, black; his look Character_Male_Police;
//       * that look added to the café customer's looks (Customer.prefab). It's his alone: walk-ins skip it, and so does
//         the street's policeman, who walks in another look from now on (City pack > NPC looks 3 would take it off the
//         prefab again: run this again after it);
//       * his story visits, on the days the open scene plays (its customer spawner's schedule: in the playtest scene,
//         Day 1 is Grace's camera day, Assets/GraceShowcase/Day_01_GraceCamera.asset): Day 1 at 0.35 (once Grace has
//         come in with her camera), Day 3 at 0.25 (his question). Needs the café layout scene open.
//   ... > Night 2 - Play from Day 2's recap (lab)
//   ... > Night 2 - Play check (lab, drives itself): NightTwoCheck, Day 2's recap through Night 2 (the bins, his
//         verdict and ask, Grace's kitchen, the cups in hand, the return, the cups on the crate) and Day 3 (Grace's
//         cups and the officer's question). Logs/Night/night-two-check-<time>/.
//   ... > Night 2 - Play check, stalling: his visit (lab, drives itself): the cups asked for and not brought; on Day 3
//         he sits in the café and says his line.
//   ... > Night 2 - Play check, stalling: his note (lab, drives itself): the third skip; on Day 3 his note, no visit.
internal static class NightTwoSteps
{
    const string Tag = "[Night 2] ";
    const string Menu = "Fixit Fidget/Night/";
    const string LayoutRoot = "ACE'S CAFE - layout study 02";
    const string GroupName = "25 - Night 2 (Grace's cups)";
    const string GraceRoot = "Grace's house - inside (break-ins)";
    const string ModelFolder = "Assets/Art/Models/GraceHouse/";
    const string City = "Assets/Synty/PolygonCity/Prefabs/Props/";
    const string CupAssetPath = "Assets/Art/NightZero/Paper cup.asset";
    const string ShelfCopyName = "The reunion cups (on the shelf)";
    const string OfficerPath = "Assets/Data/Regulars/Regular_Officer.asset";
    const string CustomerPrefab = "Assets/AssetsPrefabs/Customer.prefab";
    const string PoliceLook = "Character_Male_Police";
    const string CoffeePath = "Assets/AssetsPrefabs/Drinks/Drink_Coffee.asset";
    const float OfficerDayOne = .35f, OfficerDayThree = .25f;

    // The crate: its footprint's longer side, and how far in front of the trash can it stands.
    const float CrateSize = .5f, CrateGap = .06f;
    // A paper cup: radius at the foot and the rim, height.
    const float CupFoot = .027f, CupRim = .037f, CupHeight = .092f;

    // ================================================================== put it in the scene

    [MenuItem(Menu + "Night 2 1 - Put Grace's cups and his crate in the scene (Edit Mode)")]
    static void SetUp()
    {
        var report = new StringBuilder("Night 2 1 - put Grace's cups and his crate in the scene\n"
            + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "\n\n");
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode only.");
            CityPackChecks.RequireScene();
            GameObject layout = Layout();
            NightZeroSet set = CityPackChecks.InScene<NightZeroSet>().FirstOrDefault();
            if (set == null) throw new InvalidOperationException("The bins aren't in the scene (Bins 1 - Put the back door and the bins behind the café).");
            TrophyShelf shelf = CityPackChecks.InScene<TrophyShelf>().FirstOrDefault();
            if (shelf == null) throw new InvalidOperationException("Ace's trophy shelf isn't in the scene (Night 1 - Put Grace's gnome and Ace's trophy shelf in the scene).");
            GraceHouse house = CityPackChecks.InScene<GraceHouse>().FirstOrDefault();
            Transform rooms = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == GraceRoot);
            if (rooms == null || house == null) throw new InvalidOperationException("Grace's house has no inside yet (Break-ins 1 - Build Grace's house inside).");
            Transform box = rooms.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "GH_CupBox");
            if (box == null) throw new InvalidOperationException("Grace's reunion cups' box (GH_CupBox) isn't in her kitchen (Break-ins 1 puts it on the worktop).");
            Transform can = set.cornerGnome != null ? set.cornerGnome.transform : null;
            if (can == null) throw new InvalidOperationException("His corner on the trash can isn't on the bins' set (Bins 1).");
            GameObject sleeveModel = Model("GH_CupSleeve");
            GameObject cratePrefab = Crate(report);
            (Mesh cupMesh, Material cupMaterial) = CupAsset();

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Night 2 1 - Grace's cups and his crate");
            TakeOutOld(layout, set, shelf, report);

            var group = new GameObject(GroupName);
            Undo.RegisterCreatedObjectUndo(group, "Night 2 group");
            group.transform.SetParent(layout.transform, false);
            group.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            // ---------- Grace's cups: a sleeve on top of the box, facing into her kitchen ----------
            Bounds boxB = BoundsOf(box.gameObject);
            Vector3 kitchen = house.World(3.20f, 1.30f, 0f);
            Vector3 into = Flat(kitchen - boxB.center);
            Quaternion facing = into.sqrMagnitude > .01f ? Quaternion.LookRotation(into.normalized, Vector3.up) : Quaternion.identity;
            var cups = new GameObject("Grace's cups (a sleeve on the box)");
            cups.transform.SetParent(group.transform, false);
            cups.transform.SetPositionAndRotation(new Vector3(boxB.center.x, boxB.max.y, boxB.center.z), facing);
            GameObject sleeve = Piece(cups.transform, "A sleeve of reunion cups", sleeveModel);
            // Lying across the box's top, its long side along the box's longer side.
            sleeve.transform.rotation = Quaternion.identity;
            Bounds plain = BoundsOf(sleeve);
            bool sleeveLongX = plain.size.x >= plain.size.z, boxLongX = boxB.size.x >= boxB.size.z;
            sleeve.transform.rotation = Quaternion.Euler(0f, sleeveLongX == boxLongX ? 0f : 90f, 0f);
            SitOn(sleeve, cups.transform.position, boxB.center);
            Bounds sleeveB = BoundsOf(sleeve);
            var reach = cups.AddComponent<BoxCollider>();
            reach.isTrigger = true;
            reach.center = cups.transform.InverseTransformPoint(sleeveB.center);
            reach.size = new Vector3(.8f, .6f, .8f);
            var solid = cups.AddComponent<BoxCollider>();
            solid.center = cups.transform.InverseTransformPoint(sleeveB.center);
            solid.size = Rotated(sleeveB.size, cups.transform.rotation);
            var trophy = cups.AddComponent<NightTrophy>();
            trophy.thingId = NightThings.GraceCups;
            trophy.visual = sleeve;
            trophy.needsSight = true;
            report.AppendLine($"Grace's cups: a sleeve ({F(sleeveB.size.x)} x {F(sleeveB.size.y)} x {F(sleeveB.size.z)} m) lying on her reunion cups' box at {V(sleeveB.center)}, " +
                              $"the box's top {F(boxB.max.y)} m up; the house's floor is at {F(house.World(0f, 0f, 0f).y)} m. Taken only in sight (never through a wall).");

            // ---------- his crate in front of the trash can, the cups on it (off) ----------
            Bounds canB = TrashCanBounds(set, can.position);
            var crate = (GameObject)PrefabUtility.InstantiatePrefab(cratePrefab, group.transform);
            crate.name = "His crate (by the bins)";
            crate.transform.rotation = Quaternion.identity;
            Bounds raw = BoundsOf(crate);
            float scale = CrateSize / Mathf.Max(.01f, Mathf.Max(raw.size.x, raw.size.z));
            crate.transform.localScale = Vector3.one * scale;
            Bounds sized = BoundsOf(crate);
            Vector3 crateAt = new Vector3(canB.center.x, 0f, canB.max.z + CrateGap + sized.extents.z);
            crate.transform.position += new Vector3(crateAt.x - sized.center.x, crateAt.y - sized.min.y, crateAt.z - sized.center.z);
            Bounds crateB = BoundsOf(crate);
            // One plain box to bump into, whatever the prop came with.
            foreach (Collider c in crate.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            var crateSolid = crate.AddComponent<BoxCollider>();
            crateSolid.center = crate.transform.InverseTransformPoint(crateB.center);
            crateSolid.size = new Vector3(crateB.size.x / scale, crateB.size.y / scale, crateB.size.z / scale);
            float top = crateB.max.y;
            var cornerCups = new GameObject("His cups (what Ace has brought him)");
            cornerCups.transform.SetParent(group.transform, false);
            cornerCups.transform.SetPositionAndRotation(new Vector3(crateB.center.x, top, crateB.center.z), Quaternion.identity);
            for (int i = 0; i < 4; i++)
            {
                var cup = new GameObject($"Cup {i + 1}");
                cup.transform.SetParent(cornerCups.transform, false);
                // A row of four along the crate, facing the street.
                cup.transform.localPosition = new Vector3((i - 1.5f) * .095f, 0f, 0f);
                cup.transform.localRotation = Quaternion.Euler(0f, i * 37f, 0f);
                cup.AddComponent<MeshFilter>().sharedMesh = cupMesh;
                var r = cup.AddComponent<MeshRenderer>();
                r.sharedMaterial = cupMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            cornerCups.SetActive(false);
            Undo.RecordObject(set, "Night 2 1 - his cups");
            set.cornerCups = cornerCups;
            set.crate = crate;
            EditorUtility.SetDirty(set);
            report.AppendLine($"His crate: {cratePrefab.name} at {F(scale)}x, x {F(crateB.min.x)} to {F(crateB.max.x)}, z {F(crateB.min.z)} to {F(crateB.max.z)}, " +
                              $"{F(crateB.size.y)} m tall, in front of the trash can (x {F(canB.min.x)} to {F(canB.max.x)}, its front at z {F(canB.max.z)}); solid. " +
                              $"Four paper cups on its top at {F(top)} m, switched off until he sets them out.");
            report.AppendLine(ClearOfTheWalks(crateB, set));

            // ---------- Ace's shelf: a place for the cups ----------
            GameObject copy = Piece(shelf.transform, ShelfCopyName, sleeveModel);
            Undo.RegisterCreatedObjectUndo(copy, "Night 2 1 - the cups' place on the shelf");
            copy.transform.rotation = sleeveLongX ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.Euler(90f, 0f, 0f);   // standing on end
            Vector3 place = shelf.transform.TransformPoint(new Vector3(.3f, 0f, -.11f));
            SitOn(copy, new Vector3(place.x, shelf.transform.position.y, place.z), place);
            copy.SetActive(false);
            Undo.RecordObject(shelf, "Night 2 1 - the shelf's slots");
            shelf.slots = shelf.slots.Where(s => s != null && s.thingId != NightThings.GraceCups)
                .Append(new TrophyShelf.Slot { thingId = NightThings.GraceCups, shown = copy }).ToArray();
            EditorUtility.SetDirty(shelf);
            report.AppendLine($"Ace's shelf: a place for the cups at {V(BoundsOf(copy).center)} (a sleeve on end, beside Barnaby's place), hidden until the ledger puts them there. " +
                              $"The shelf now has {shelf.slots.Length} places.");

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            // ---------- photos, by day ----------
            string folder = LogFolder("night-two-setup");
            Vector3 c0 = sleeveB.center;
            NightWalkSteps.Capture(Path.Combine(folder, "1-grace-kitchen-cups.png"), c0 + Flat(into).normalized * 1.3f + Vector3.up * .55f, c0, 50f, false);
            NightWalkSteps.Capture(Path.Combine(folder, "2-grace-kitchen-from-the-room.png"), house.World(2.45f, 2.75f, 1.7f), c0, 55f, false);
            cornerCups.SetActive(true);
            copy.SetActive(true);
            try
            {
                Vector3 k = crateB.center;
                NightWalkSteps.Capture(Path.Combine(folder, "3-his-crate-with-the-cups.png"), k + new Vector3(.9f, 1.1f, 2.1f), k + Vector3.up * .3f, 50f, false);
                NightWalkSteps.Capture(Path.Combine(folder, "4-his-corner-from-the-street.png"), new Vector3(3.6f, 6.5f, 26f), new Vector3(3.4f, .6f, 19f), 45f, false);
                Vector3 s0 = shelf.transform.position + new Vector3(0f, .2f, -.12f);
                NightWalkSteps.Capture(Path.Combine(folder, "5-shelf-with-the-cups.png"), new Vector3(s0.x + .25f, 1.7f, 15.3f), s0, 50f, false);
            }
            finally
            {
                cornerCups.SetActive(false);
                copy.SetActive(false);
            }
            report.AppendLine();
            report.AppendLine("Photos: 1-2 Grace's kitchen (the sleeve on the box), 3-4 his crate with the cups (as after Night 2), 5 Ace's shelf with the cups.");
            report.AppendLine("The scene changed: read the diff, then save it (Ctrl+S). Edit > Undo takes it all out again.");
            File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
            Selection.activeGameObject = cups;
            Debug.Log(Tag + "Put Grace's cups and his crate in the scene. " + folder + "\n" + report);
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Putting Grace's cups and his crate in FAILED: " + e.Message + "\n" + report + "\n" + e);
        }
    }

    [MenuItem(Menu + "Night 2 1 - Take them out again")]
    static void TakeOut()
    {
        var report = new StringBuilder();
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode only.");
            CityPackChecks.RequireScene();
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Night 2 1 - take them out");
            TakeOutOld(Layout(), CityPackChecks.InScene<NightZeroSet>().FirstOrDefault(), CityPackChecks.InScene<TrophyShelf>().FirstOrDefault(), report);
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log(Tag + "Took Grace's cups, his crate and the cups' place on the shelf out (Edit > Undo puts them back).\n" + report);
        }
        catch (Exception e) { Debug.LogError(Tag + "FAILED: " + e.Message); }
    }

    static void TakeOutOld(GameObject layout, NightZeroSet set, TrophyShelf shelf, StringBuilder report)
    {
        Transform old = layout.transform.Find(GroupName);
        if (old != null)
        {
            Undo.DestroyObjectImmediate(old.gameObject);
            report.AppendLine("Took the old group out first (it is rebuilt from scratch).");
        }
        if (set != null && (set.cornerCups != null || set.crate != null))
        {
            Undo.RecordObject(set, "Night 2 1 - his cups");
            set.cornerCups = null;
            set.crate = null;
            EditorUtility.SetDirty(set);
        }
        if (shelf != null)
        {
            Transform copy = shelf.transform.Find(ShelfCopyName);
            if (copy != null) Undo.DestroyObjectImmediate(copy.gameObject);
            if (shelf.slots.Any(s => s != null && s.thingId == NightThings.GraceCups))
            {
                Undo.RecordObject(shelf, "Night 2 1 - the shelf's slots");
                shelf.slots = shelf.slots.Where(s => s != null && s.thingId != NightThings.GraceCups).ToArray();
                EditorUtility.SetDirty(shelf);
            }
        }
    }

    // ================================================================== the officer

    [MenuItem(Menu + "The officer 1 - Make his profile and add his visits (Days 1 and 3)")]
    static void MakeOfficer()
    {
        var report = new StringBuilder("The officer 1 - his profile, his look and his visits\n"
            + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "\n\n");
        try
        {
            CityPackChecks.RequireScene();
            // His visits go on the days this scene plays: its customer spawner's own schedule (the playtest scene's Days 1
            // and 2 are Grace's showcase copies), never a fixed path to a day asset the scene may not use.
            CustomerSpawner spawner = CityPackChecks.InScene<CustomerSpawner>().FirstOrDefault();
            if (spawner == null) throw new InvalidOperationException("The café's customer spawner isn't in the scene.");
            SerializedProperty schedule = new SerializedObject(spawner).FindProperty("schedule");
            DayDefinition Scheduled(int number) => schedule == null ? null : Enumerable.Range(0, schedule.arraySize)
                .Select(i => schedule.GetArrayElementAtIndex(i).objectReferenceValue as DayDefinition)
                .FirstOrDefault(d => d != null && d.dayNumber == number);
            var coffee = AssetDatabase.LoadAssetAtPath<DrinkDefinition>(CoffeePath);
            if (coffee == null) throw new InvalidOperationException($"The coffee isn't there ({CoffeePath}).");

            // ---------- his profile (made once; never overwritten) ----------
            var officer = AssetDatabase.LoadAssetAtPath<CustomerProfile>(OfficerPath);
            if (officer == null)
            {
                officer = ScriptableObject.CreateInstance<CustomerProfile>();
                FillOfficer(officer, coffee);
                AssetDatabase.CreateAsset(officer, OfficerPath);
                var so = new SerializedObject(officer);
                so.FindProperty("persistentId").stringValue = OfficerStory.ProfileId;
                so.FindProperty("standInLook").stringValue = PoliceLook;
                so.FindProperty("homeId").stringValue = "";
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(officer);
                report.AppendLine($"Made {OfficerPath}: \"{officer.characterName}\" (placeholder), id \"{officer.PersistentId}\", a coffee, black; his look {officer.StandInLook}.");
            }
            else report.AppendLine($"{OfficerPath} is already there, left as it is: \"{officer.characterName}\", id \"{officer.PersistentId}\", look \"{officer.StandInLook}\", " +
                                   $"{officer.primaryVisitKind}, {(officer.preferredDrink != null ? officer.preferredDrink.drinkName : "no drink")}.");
            if (officer.PersistentId != OfficerStory.ProfileId)
                report.AppendLine($"NOTE: his id is \"{officer.PersistentId}\", not \"{OfficerStory.ProfileId}\": his question (OfficerStory) is asked by \"{OfficerStory.ProfileId}\" only.");

            // ---------- his look on the café's customers ----------
            string lookPath = PolygonNpcSetup.LookPath(officer.StandInLook.Length > 0 ? officer.StandInLook : PoliceLook);
            var look = AssetDatabase.LoadAssetAtPath<GameObject>(lookPath);
            if (look == null) report.AppendLine($"NOTE: his look isn't in the project ({lookPath}): he wears the customer's own body.");
            else
            {
                GameObject root = PrefabUtility.LoadPrefabContents(CustomerPrefab);
                try
                {
                    var visual = root.GetComponent<PolygonNpcVisual>();
                    if (visual == null) report.AppendLine("NOTE: the customer prefab has no city looks (City pack > NPC looks 3): he wears the customer's own body.");
                    else
                    {
                        var so = new SerializedObject(visual);
                        SerializedProperty looks = so.FindProperty("appearancePrefabs");
                        bool there = Enumerable.Range(0, looks.arraySize).Any(i => looks.GetArrayElementAtIndex(i).objectReferenceValue == look);
                        if (!there)
                        {
                            looks.arraySize++;
                            looks.GetArrayElementAtIndex(looks.arraySize - 1).objectReferenceValue = look;
                            so.ApplyModifiedPropertiesWithoutUndo();
                            PrefabUtility.SaveAsPrefabAsset(root, CustomerPrefab);
                            report.AppendLine($"Added {look.name} to the café customer's looks ({looks.arraySize} now). It's his alone: walk-ins skip it, " +
                                              "and so does the street's policeman, who walks in another look from now on.");
                        }
                        else report.AppendLine($"{look.name} is already one of the café customer's looks ({looks.arraySize}).");
                    }
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            // ---------- his visits: Day 1 (the coffee, the joke) and Day 3 (the question), on the scene's own days ----------
            foreach ((int number, float at, string why) in new[]
                     {
                         (1, OfficerDayOne, "a coffee and the joke about the second batch, once Grace has come in with her camera"),
                         (3, OfficerDayThree, "his question, after his order"),
                     })
            {
                DayDefinition day = Scheduled(number);
                if (day == null) { report.AppendLine($"NOTE: the café's schedule has no Day {number}: no visit that day."); continue; }
                var visits = (day.storyVisits ?? Array.Empty<StoryVisit>()).Where(v => v != null).ToList();
                StoryVisit his = visits.FirstOrDefault(v => v.who == officer);
                if (his == null)
                {
                    Undo.RecordObject(day, "The officer's visits");
                    visits.Add(new StoryVisit { who = officer, arrivesAt = at });
                    day.storyVisits = visits.ToArray();
                    EditorUtility.SetDirty(day);
                    report.AppendLine($"Day {day.dayNumber} ({AssetDatabase.GetAssetPath(day)}): he comes in at {F(at)} of the day: {why}.");
                }
                else report.AppendLine($"Day {day.dayNumber} ({AssetDatabase.GetAssetPath(day)}): his visit is already there, at {F(his.arrivesAt)}.");
            }
            AssetDatabase.SaveAssets();
            report.AppendLine();
            report.AppendLine("Next: Fixit Fidget > Checks > Night 1 rules (his question), and Night 2 - Play check (Day 3).");
            Selection.activeObject = officer;
            Debug.Log(Tag + "The officer: done.\n" + report);
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "The officer FAILED: " + e.Message + "\n" + report + "\n" + e);
        }
    }

    // PLACEHOLDER COPY: Mansoor and his sister write him. A coffee, black; no repair, no topic (claude/foundation-pass-
    // build-plan.md §7, call 2: Day 1 is the tutorial, so he gets a coffee and one line).
    static void FillOfficer(CustomerProfile p, DrinkDefinition coffee)
    {
        p.characterName = OfficerStory.DefaultName;
        p.bio = "Walks the beat round the blocks. Drinks his coffee black and notices everything. (Placeholder: Mansoor and his sister write him.)";
        p.themeColor = new Color(.50f, .64f, .85f, 1f);
        p.patienceMultiplier = 1.1f;
        p.tipMultiplier = 1f;
        p.preferredWaitKind = WaitingSpot.SpotKind.Seat;
        p.drinkWishChance = 0f;
        p.primaryVisitKind = RegularVisitKind.DrinkOnly;
        p.preferredDrink = coffee;
        p.preferredDevice = null;
        p.preferredDeviceChance = 0f;
        p.storyteller = false;
        p.topics = Array.Empty<ConversationTopic>();
        p.lines = new DialogueSet
        {
            intake = new[] { "Coffee, black. That's all, Ace." },
            orderedDrink = new[] { "Coffee, black. First batch; the second batch is always burnt." },   // said in the room: one line
            accepted = new[] { "Thanks. I'll take it by the window." },
            completed = new[] { "Good coffee. Don't tell the second batch." },
            declined = new[] { "Fair enough. Another time." },
            reassured = new[] { "No rush. I'm on a break." },
            stormedOut = new[] { "I'll get one on the beat." },
        };
        p.returnLines = new DialogueSet
        {
            intake = new[] { "Coffee, black. That's all." },
            orderedDrink = new[] { "Morning, Ace. Black. First batch." },
            accepted = new[] { "Thanks." },
            completed = new[] { "Still good." },
            declined = new[] { "Another time, then." },
            reassured = new[] { "I've got a minute." },
            stormedOut = new[] { "I'll be back." },
        };
        p.warmLines = new DialogueSet
        {
            intake = new[] { "The usual, Ace." },
            orderedDrink = new[] { "Morning, Ace. The usual. First batch." },
            accepted = new[] { "Thanks, Ace." },
            completed = new[] { "First batch. You can tell." },
            declined = new[] { "Busy one. Another time." },
            reassured = new[] { "Take your time. I'm off the clock." },
            stormedOut = new[] { "I'll catch you tomorrow." },
        };
        p.drinkCompletedLines = new[] { "That's the first batch. You can tell." };
        p.passableRepairLines = Array.Empty<string>();
        p.rejectedRepairLines = Array.Empty<string>();
    }

    // ================================================================== the labs

    [MenuItem(Menu + "Night 2 - Play from Day 2's recap (lab)")]
    static void PlayFromTheRecap() => StartLab(0);

    [MenuItem(Menu + "Night 2 - Play check (lab, drives itself)")]
    static void PlayCheck() => StartLab((int)NightTwoCheck.Mode.Cups);

    [MenuItem(Menu + "Night 2 - Play check, stalling: one skip, his table (lab, drives itself)")]
    static void PlayCheckVisit() => StartLab((int)NightTwoCheck.Mode.Visit);

    [MenuItem(Menu + "Night 2 - Play check, stalling: the third skip (lab, drives itself)")]
    static void PlayCheckNote() => StartLab((int)NightTwoCheck.Mode.Note);

    [MenuItem(Menu + "Night 2 - Play from Day 2's recap (lab)", true)]
    [MenuItem(Menu + "Night 2 - Play check (lab, drives itself)", true)]
    [MenuItem(Menu + "Night 2 - Play check, stalling: one skip, his table (lab, drives itself)", true)]
    [MenuItem(Menu + "Night 2 - Play check, stalling: the third skip (lab, drives itself)", true)]
    static bool CanPlay() => !EditorApplication.isPlayingOrWillChangePlaymode;

    static void StartLab(int check)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        try
        {
            CityPackChecks.RequireScene();
            if (Layout().transform.Find(GroupName) == null)
                Debug.LogWarning(Tag + "Grace's cups aren't in the scene yet (Night 2 1): he'll have nothing to ask for, and the night is quiet.");
            if (AssetDatabase.LoadAssetAtPath<CustomerProfile>(OfficerPath) == null)
                Debug.LogWarning(Tag + "The officer isn't made yet (The officer 1): Day 3 has no question.");
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + e.Message);
            return;
        }
        string path = Path.Combine(Application.persistentDataPath, CafeLab.SaveFileName);
        SaveCheckpointStorage.Write(path, DayTwoRecap(check == (int)NightTwoCheck.Mode.Note ? 2 : 0));
        PlayerPrefs.SetInt(CafeLab.PendingKey, 1);
        PlayerPrefs.SetInt(CafeLab.AutopilotKey, 0);
        if (check > 0) PlayerPrefs.SetInt(NightTwoCheck.PendingKey, check);
        else PlayerPrefs.DeleteKey(NightTwoCheck.PendingKey);
        PlayerPrefs.DeleteKey(NightZeroCheck.PendingKey);
        PlayerPrefs.DeleteKey(NightOneCheck.PendingKey);
        PlayerPrefs.Save();
        Debug.Log(Tag + (check > 0
            ? "Night 2 play check: it drives itself from Day 2's recap (a few minutes). Keep the Game view in front and leave the mouse and keyboard alone."
            : "Night 2 lab: Day 2's recap, after Night 1 (the man met, Barnaby given, Nerve, Grace's morning held). Its button leads into Night 2: " +
              "the bins, his verdict, and the cups. Grace's house is the saffron house on the corner; the cups are on her kitchen worktop.")
            + $" Test save {path}; your playtest save is not used.");
        EditorApplication.EnterPlaymode();
    }

    // A made-up Day 2, after Night 0 and Night 1: the deal made, Barnaby given to him (Nerve, his Thursdays page), Grace's
    // complaint about Barnaby held this morning, the officer in for a coffee on Day 1. He asks for the cups tonight;
    // <paramref name="skips"/> > 0: he asked before and the nights ended without them (the third skip comes tonight).
    internal static SaveData DayTwoRecap(int skips)
    {
        SaveData data = NightOneSteps.DayOneRecap();
        data.day = 2;
        data.money = 131;
        data.recap = new RecapSaveData
        {
            day = 2, peopleServed = 4, ordersCompleted = 4, repairs = 1, drinks = 3, perfect = 1, tips = 3, earned = 67,
            closingTill = 131, elapsedSeconds = 360f,
        };
        RegularMemoryData grace = data.regularMemories[0];
        grace.visits = 2;
        grace.lastSeenDay = 2;
        grace.relationship = 3;
        grace.graceReturnAcknowledged = true;
        grace.gracePhotoClaimed = true;
        grace.gracePhotoVariant = GracePhotoOutcome.Clear.ToString();
        var officer = new RegularMemoryData
        {
            profileId = OfficerStory.ProfileId, visits = 1, relationship = 1, lastSeenDay = 1,
            lastVisitHappy = true, lastJobAccepted = true, lastVisitServed = true, lastGrade = nameof(JobGrade.Good),
        };
        data.regularMemories = new[] { grace, officer };
        var facts = data.notebook.ToList();
        foreach (NotebookFactData page in LodgerStory.Pages(id => id != LodgerStory.Cones)) { page.day = 1; facts.Add(page); }
        NotebookFactData taken = NightThings.Taken(NightThings.GnomeOfGrace, NotebookEntries.GraceName, forHim: true);
        taken.day = 1;
        facts.Add(taken);
        NotebookFactData thursdays = LodgerStory.PageFor(NightThings.GraceGnome);
        thursdays.day = 1;
        facts.Add(thursdays);
        NotebookFactData photo = NotebookEntries.GraceReturn(NotebookEntries.GraceName, GracePhotoOutcome.Clear);
        photo.day = 2;
        facts.Add(photo);
        data.notebook = facts.ToArray();
        data.night = new NightSaveData
        {
            nights = 1,
            trophies = new[] { NightThings.GraceGnome },
            deeds = new[] { new NightDeedData { thing = NightThings.GraceGnome, owner = GraceCameraEpisode.ProfileId, night = 1, faced = true, cracked = false, facedDay = 2 } },
            metHim = true,
            warmth = 2,
            lessons = new[] { LodgerStory.Nerve },
            given = new[] { NightThings.GraceGnome },
            favour = NightThings.GraceCups,
            askedOn = skips > 0 ? 1 : 0,
            lastAsked = skips > 0 ? 1 : 0,
            skips = skips,
        };
        return data;
    }

    // A check request that never became a Play session must not make the next lab session a check.
    [InitializeOnLoadMethod]
    static void ClearStaleCheckRequest()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode && PlayerPrefs.HasKey(NightTwoCheck.PendingKey))
        {
            PlayerPrefs.DeleteKey(NightTwoCheck.PendingKey);
            PlayerPrefs.Save();
        }
    }

    // ================================================================== helpers

    // One of Grace's furniture models (Assets/Art/Models/GraceHouse), as imported.
    static GameObject Model(string name)
    {
        string path = ModelFolder + name + ".fbx";
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) throw new InvalidOperationException($"{path} isn't there (Break-ins - Grace's furniture: import and check).");
        if (asset.GetComponentsInChildren<MeshFilter>(true).All(f => f.sharedMesh == null)) throw new InvalidOperationException($"{path} has no mesh.");
        return asset;
    }

    // The model as it imports (its own inner turn and size kept), under <paramref name="parent"/>, with nothing to collide.
    static GameObject Piece(Transform parent, string name, GameObject model)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
        go.name = name;
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        return go;
    }

    // Moves <paramref name="go"/> so the bottom of what's drawn sits at <paramref name="at"/>'s height, centred over <paramref name="centre"/>.
    static void SitOn(GameObject go, Vector3 at, Vector3 centre)
    {
        Bounds b = BoundsOf(go);
        go.transform.position += new Vector3(centre.x - b.center.x, at.y + .002f - b.min.y, centre.z - b.center.z);
    }

    // The crate: the smallest of POLYGON City's cardboard boxes that stands taller than a hand's width.
    static GameObject Crate(StringBuilder report)
    {
        var boxes = Enumerable.Range(1, 4).Select(i => AssetDatabase.LoadAssetAtPath<GameObject>(City + $"SM_Prop_CardboardBox_0{i}.prefab"))
            .Where(p => p != null).ToList();
        if (boxes.Count == 0) throw new InvalidOperationException($"POLYGON City's cardboard boxes aren't in the project ({City}).");
        GameObject best = null;
        float bestScore = float.MaxValue;
        foreach (GameObject prefab in boxes)
        {
            var probe = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                Bounds b = BoundsOf(probe);
                float wide = Mathf.Max(b.size.x, b.size.z), tall = b.size.y;
                // A crate is about as tall as it is wide.
                float score = Mathf.Abs(tall / Mathf.Max(.01f, wide) - .8f);
                report.AppendLine($"  {prefab.name}: {F(b.size.x)} x {F(b.size.y)} x {F(b.size.z)} m");
                if (score < bestScore) { bestScore = score; best = prefab; }
            }
            finally { Object.DestroyImmediate(probe); }
        }
        return best;
    }

    // The trash can's bounds: the renderers near his corner (the can and its lid), on the bins' set.
    static Bounds TrashCanBounds(NightZeroSet set, Vector3 corner)
    {
        Renderer[] near = set.GetComponentsInChildren<Renderer>(true)
            .Where(r => !r.transform.IsChildOf(set.cornerGnome.transform) && Vector2.Distance(new Vector2(r.bounds.center.x, r.bounds.center.z), new Vector2(corner.x, corner.z)) < .6f
                        && r.bounds.size.y > .1f).ToArray();
        if (near.Length == 0) return new Bounds(new Vector3(corner.x, .5f, corner.z), new Vector3(.74f, 1f, .74f));
        Bounds b = near[0].bounds;
        foreach (Renderer r in near) b.Encapsulate(r.bounds);
        return b;
    }

    // People walking past by day (the rear lane's walker turns back at x 1.6) and Ace at the bins: is the crate clear?
    static string ClearOfTheWalks(Bounds crate, NightZeroSet set)
    {
        float Away(Vector3 p) => Mathf.Max(0f, Mathf.Max(Mathf.Abs(p.x - crate.center.x) - crate.extents.x, Mathf.Abs(p.z - crate.center.z) - crate.extents.z));
        string give = set.giveSpot != null ? F(Away(set.giveSpot.position)) : "?", bin = set.binSpot != null ? F(Away(set.binSpot.position)) : "?";
        return $"Clear of: the rear lane walker's turn {F(Away(new Vector3(1.6f, 0f, 19f)))} m, where Ace hands him things {give} m, " +
               $"where Ace bins the bag {bin} m (Ace is 0.5 m round; a walker about 0.3).";
    }

    // The paper cup: a tapered cylinder with a rolled lip and a closed top a little below the rim, flat-shaded like the
    // city pack, and its material: kept in one asset.
    static (Mesh, Material) CupAsset()
    {
        Object[] existing = AssetDatabase.LoadAllAssetsAtPath(CupAssetPath);
        Mesh mesh = existing.OfType<Mesh>().FirstOrDefault();
        Material material = existing.OfType<Material>().FirstOrDefault();
        bool made = mesh == null;
        if (made)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Art/NightZero")) AssetDatabase.CreateFolder("Assets/Art", "NightZero");
            mesh = CupMesh();
            AssetDatabase.CreateAsset(mesh, CupAssetPath);
        }
        if (material == null)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) throw new InvalidOperationException("The URP Lit shader was not found.");
            material = new Material(lit) { name = "Paper cup", enableInstancing = true };
            AssetDatabase.AddObjectToAsset(material, CupAssetPath);
        }
        material.SetColor("_BaseColor", new Color(.94f, .92f, .87f, 1f));
        material.SetFloat("_Smoothness", .25f);
        material.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return (mesh, material);
    }

    static Mesh CupMesh()
    {
        const int Sides = 12;
        var v = new List<Vector3>();
        var n = new List<Vector3>();
        var t = new List<int>();
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            int i = v.Count;
            v.AddRange(new[] { a, b, c, d });
            n.AddRange(new[] { normal, normal, normal, normal });
            t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }
        void Disc(float y, float r, bool up)
        {
            int centre = v.Count;
            v.Add(new Vector3(0f, y, 0f));
            n.Add(up ? Vector3.up : Vector3.down);
            for (int s = 0; s <= Sides; s++)
            {
                float a = s * Mathf.PI * 2f / Sides;
                v.Add(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r));
                n.Add(up ? Vector3.up : Vector3.down);
            }
            for (int s = 0; s < Sides; s++)
                if (up) t.AddRange(new[] { centre, centre + s + 2, centre + s + 1 });
                else t.AddRange(new[] { centre, centre + s + 1, centre + s + 2 });
        }
        Vector3 Ring(float a, float r, float y) => new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
        float lip = CupHeight - .006f;
        for (int s = 0; s < Sides; s++)
        {
            float a0 = s * Mathf.PI * 2f / Sides, a1 = (s + 1) * Mathf.PI * 2f / Sides;
            // The side, foot to the lip (outward faces), and the lip, a little proud.
            Quad(Ring(a0, CupFoot, 0f), Ring(a0, CupRim, lip), Ring(a1, CupRim, lip), Ring(a1, CupFoot, 0f));
            Quad(Ring(a0, CupRim, lip), Ring(a0, CupRim + .0025f, CupHeight), Ring(a1, CupRim + .0025f, CupHeight), Ring(a1, CupRim, lip));
        }
        Disc(0f, CupFoot, false);
        Disc(CupHeight - .004f, CupRim + .0025f, true);
        var mesh = new Mesh { name = "Paper cup" };
        mesh.SetVertices(v);
        mesh.SetNormals(n);
        mesh.SetTriangles(t, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    static GameObject Layout()
    {
        GameObject layout = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == LayoutRoot);
        if (layout == null) throw new InvalidOperationException($"\"{LayoutRoot}\" is not in the open scene.");
        return layout;
    }

    static Bounds BoundsOf(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToArray();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    // A world size seen from a turned object (a box collider's size), its axes swapped back as needed.
    static Vector3 Rotated(Vector3 worldSize, Quaternion turn)
    {
        Vector3 x = turn * Vector3.right, z = turn * Vector3.forward;
        bool swapped = Mathf.Abs(x.z) > Mathf.Abs(x.x);
        return swapped ? new Vector3(worldSize.z, worldSize.y, worldSize.x) : worldSize;
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    static string LogFolder(string what)
    {
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Night",
            what + "-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture)));
        Directory.CreateDirectory(folder);
        return folder;
    }

    static string F(float v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    static string V(Vector3 v) => $"({F(v.x)}, {F(v.y)}, {F(v.z)})";
}
