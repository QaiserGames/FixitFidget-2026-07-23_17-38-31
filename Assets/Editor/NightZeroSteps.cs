using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// THE BINS BEHIND THE CAFÉ: NIGHT 0's SET (6 Oct 2026; claude/the-man-at-the-bins-story.md, all four of Mansoor's calls)
//
//   Fixit Fidget > Night > Bins 1 - Put the back door and the bins behind the café (Edit Mode)
//     Adds "24 - The bins (Night 0)" under the café layout, with NightZeroSet on it:
//       * the café's back door, at the east end of the back wall: a leaf inside (behind the counter's end) and one
//         outside on Back Street (POLYGON City's SM_Prop_Door_01, flattened against the wall);
//       * a dumpster on Back Street against the café's back wall, its back to the wall: POLYGON City's SM_Prop_Skip_02,
//         its two lids cut free of its mesh so they can lift (the near one, nearer the door, for the bag; the far one
//         for him). The cut meshes are saved in Assets/Art/NightZero/Dumpster - lids cut free.asset;
//       * a lamp on the wall beside it (lit at night), a trash can at its far end with his corner on the lid (Barnaby,
//         switched off until Ace gives him the gnome), and a camera for the moment the lid lifts;
//       * where Ace stands (inside the door, outside it, at each half of the dumpster) and where he stands (inside the
//         far half), each checked for room (a capsule Ace's size, against everything solid around);
//       * the night's own things (on with the night: NightWalk's night-only list): the lamp's light, the places to
//         stand (the back door both sides, "Bin it", "Give him ...", and "Take ... off the shelf" at Ace's shelf), and
//         the dumpster and trash can made solid.
//     To make room, three things nearby move (each only if it's still where it was): the pot plant in the back
//     corner (to the end of the drinks counter), the chalkboard menu (8 cm away from the door), the wall clock (up,
//     if it would sit on the door's frame); and the rear lane's walker turns back before the trash can.
//     Running it again rebuilds the group (the moves aren't repeated). Photos and notes go to Logs/Night/bins-setup-<time>/.
//     Save the scene yourself once the diff is read.
//   ... > Bins 1 - Take them out again: removes the group and puts the three things and the walker's turn back (and what
//     Bins 2 moved, and the whole window sill).
//   ... > Bins 2 - Give the back door room (Edit Mode): once, after Bins 1 (6 Oct 2026, Mansoor's playtest; see its section):
//     the courtyard window's sill stops where the glass does, and the door moves along the wall, 0.30 m from the corner, with
//     the menu, the cat picture, a coffee sack and the clock moved round it.
//   ... > Night 0 - Play check (lab, drives itself): NightZeroCheck from Day 1's recap through Night 0 and the errand.
internal static class NightZeroSteps
{
    const string Tag = "[Bins] ";
    const string Menu = "Fixit Fidget/Night/";
    const string LayoutRoot = "ACE'S CAFE - layout study 02";
    const string GroupName = "24 - The bins (Night 0)";
    const string NightOnlyName = "Night only (the bins)";
    const string City = "Assets/Synty/PolygonCity/Prefabs/Props/";
    const string MeshFolder = "Assets/Art/NightZero";
    const string MeshAsset = MeshFolder + "/Dumpster - lids cut free.asset";
    const string LookPath = "Assets/Art/CityNeighbors/Prefabs/Character_BusinessMan_Suit.prefab";
    const string BulbMaterialPath = "Assets/Playtests/AcesCafeLayout/Night walk - lamp bulb.mat";
    const string BodyMesh = "Dumpster body", NearLidMesh = "Dumpster lid (near)", FarLidMesh = "Dumpster lid (far)";

    // The café's back wall: its outside face (Back plaster: z 18.0 to 18.24), and the east wall's inside face.
    const float WallOutside = 18.24f, WallInside = 18f, EastWallInside = 7.4f;
    // The back door: its middle, as wide as a door (0.94 m) and as tall (2.1 m), flat against the wall.
    const float DoorX = 6.9f;
    static readonly Vector3 DoorScale = new Vector3(.66f, .8f, .25f);
    // The dumpster: its middle along the wall (it reaches 1.2 m either way), and a hand's width off the wall.
    const float DumpsterX = 4.4f, OffTheWall = .03f;
    // The trash can (his corner) at the dumpster's far end, clear of the bench further along.
    const float CanX = 2.65f;
    // The lamp on the wall, between the dumpster and the door (outside the lids' swing).
    static readonly Vector3 LampAt = new Vector3(5.95f, 2.75f, WallOutside);

    // What moves to make room: where each was (it moves only if it's still there), and where it goes.
    static readonly Vector3 PlantWas = new Vector3(6.9f, .02f, 17.42f), PlantGoes = new Vector3(4.95f, .02f, 17.55f);
    const float ChalkboardWasX = 5.97f, ChalkboardGoesX = 5.89f;
    static readonly Vector3 ClockWas = new Vector3(6.5f, 2.35f, 18f);
    const float RouteTurnWasX = 7f, RouteTurnGoesX = 1.6f;

    // Ace's body: the CharacterController (radius 0.5, 2 m tall, its middle 1 m up). Checked a touch thinner, and
    // from 0.15 m up, so the floor and a kerb's lip don't count.
    const float AceRadius = .45f, AceY = 1f;

    // ------------------------------------------------------------------ put it in the scene

    [MenuItem(Menu + "Bins 1 - Put the back door and the bins behind the café (Edit Mode)")]
    static void SetUp()
    {
        var report = new StringBuilder("Bins 1 - put the back door and the bins behind the café\n"
            + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "\n\n");
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode only.");
            CityPackChecks.RequireScene();
            GameObject layout = Layout();
            NightWalk walk = layout.GetComponentInChildren<NightWalk>(true);
            if (walk == null) throw new InvalidOperationException("There is no NightWalk in the scene (Night walk 1 puts it there).");
            TrophyShelf shelf = CityPackChecks.InScene<TrophyShelf>().FirstOrDefault();
            if (shelf == null) throw new InvalidOperationException("Ace's trophy shelf isn't in the scene (Night 1 - Put Grace's gnome and Ace's trophy shelf in the scene).");
            GameObject doorPrefab = Prefab("SM_Prop_Door_01"), skipPrefab = Prefab("SM_Prop_Skip_02"), canPrefab = Prefab("SM_Prop_TrashCan_01"),
                       canLidPrefab = Prefab("SM_Prop_TrashCan_Lid_01"), bagPrefab = Prefab("SM_Prop_TrashBag_03");
            GameObject look = AssetDatabase.LoadAssetAtPath<GameObject>(LookPath);
            GameObject barnaby = AssetDatabase.LoadAssetAtPath<GameObject>(NightOneSteps.PrefabPath);
            if (barnaby == null) throw new InvalidOperationException("Barnaby's prefab isn't there (" + NightOneSteps.PrefabPath + "): run Night 1's set-up first.");
            report.AppendLine(look != null ? $"His look: {look.name} (nobody else in the city wears it while the bins are here)."
                : $"NOTE: his look ({LookPath}) wasn't found: he wears the patron body's own look.");

            // The dumpster's lids, cut free of its mesh (before anything in the scene changes).
            Dumpster cut = CutTheLids(skipPrefab, report);

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Bins 1 - the back door and the bins");
            Transform old = layout.transform.Find(GroupName);
            if (old != null)
            {
                Undo.RecordObject(walk, "Bins 1 - the old night-only things go");
                walk.nightOnly = walk.nightOnly.Where(g => g != null && !g.transform.IsChildOf(old)).ToArray();
                Undo.DestroyObjectImmediate(old.gameObject);
                report.AppendLine("Took the old group out first (it is rebuilt from scratch).");
            }

            var group = new GameObject(GroupName);
            Undo.RegisterCreatedObjectUndo(group, "Bins 1 group");
            group.transform.SetParent(layout.transform, false);
            group.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var set = group.AddComponent<NightZeroSet>();
            set.lidOpen = cut.lidOpen;
            set.look = look;
            set.bag = bagPrefab;
            report.AppendLine();

            // ---------- the back door, both sides ----------
            GameObject doorIn = Door(doorPrefab, group.transform, "Back door (inside)", inside: true);
            GameObject doorOut = Door(doorPrefab, group.transform, "Back door (outside)", inside: false);
            Bounds inB = BoundsOf(doorIn), outB = BoundsOf(doorOut);
            report.AppendLine($"The back door: {F(inB.size.x)} m wide and {F(inB.size.y)} m tall, x {F(inB.min.x)} to {F(inB.max.x)} " +
                              $"(the east wall's inside face is at {F(EastWallInside)}); inside it stands out from the wall to z {F(inB.min.z)}, " +
                              $"outside to z {F(outB.max.z)}.");

            // ---------- the dumpster, its back to the wall ----------
            float yaw = cut.backAtMinZ ? 0f : 180f;   // its back (the hinges' side) against the café
            float centreZ = WallOutside + OffTheWall + cut.halfDepthBack;
            var dumpster = new GameObject("Dumpster");
            dumpster.transform.SetParent(group.transform, false);
            dumpster.transform.SetPositionAndRotation(new Vector3(DumpsterX, 0f, centreZ), Quaternion.Euler(0f, yaw, 0f));
            MeshPart(dumpster, cut.body, cut.material);
            Transform near = Hinge(dumpster.transform, "Near lid (the bag goes in)", cut.nearLid, cut.nearPivot, cut.material);
            Transform far = Hinge(dumpster.transform, "Far lid (his)", cut.farLid, cut.farPivot, cut.material);
            set.nearLid = near;
            set.farLid = far;
            Bounds dumpB = BoundsOf(dumpster);
            report.AppendLine($"The dumpster: x {F(dumpB.min.x)} to {F(dumpB.max.x)}, z {F(dumpB.min.z)} to {F(dumpB.max.z)}, {F(dumpB.max.y)} m at the back; " +
                              $"its back {F(dumpB.min.z - WallOutside)} m off the wall; turned {yaw:0}°. The near lid is the half nearer the door " +
                              $"(x {F(near.position.x)}), his the other (x {F(far.position.x)}); a lid opens {cut.lidOpen:0}° about its hinge.");

            // Where he stands: the middle of the far half, on the dumpster's floor, facing the street.
            Vector3 hisSpot = new Vector3(far.position.x, cut.floor, centreZ);
            set.inside = Marker(group.transform, "His spot (inside the far half)", hisSpot, 0f);

            // ---------- the trash can at the far end, his corner on its lid ----------
            var can = (GameObject)PrefabUtility.InstantiatePrefab(canPrefab, group.transform);
            can.name = "Trash can (his corner)";
            Sit(can, new Vector3(CanX, 0f, 0f), WallOutside + .1f);
            Bounds canB = BoundsOf(can);
            var canLid = (GameObject)PrefabUtility.InstantiatePrefab(canLidPrefab, can.transform);
            canLid.name = "Lid";
            Bounds lidB0 = BoundsOf(canLid);
            canLid.transform.position += new Vector3(canB.center.x - lidB0.center.x, canB.max.y - .03f - lidB0.min.y, canB.center.z - lidB0.center.z);
            Bounds lidB = BoundsOf(canLid);
            // Barnaby stands on the lid's top beside its handle, toward the street: where the lid is, under that spot.
            Vector3 onLid = new Vector3(lidB.center.x, lidB.max.y, lidB.center.z + Mathf.Min(.18f, lidB.extents.z - .13f));
            onLid.y = TopAt(canLid, onLid, lidB.max.y);
            var corner = new GameObject("His corner (what Ace has brought him)");
            corner.transform.SetParent(group.transform, false);
            corner.transform.SetPositionAndRotation(onLid, Quaternion.identity);
            var gnome = (GameObject)PrefabUtility.InstantiatePrefab(barnaby, corner.transform);
            gnome.name = "Barnaby (in his corner)";
            gnome.transform.localPosition = Vector3.zero;
            gnome.transform.localRotation = Quaternion.identity;
            corner.SetActive(false);
            set.cornerGnome = corner;
            report.AppendLine($"The trash can: x {F(canB.min.x)} to {F(canB.max.x)}, its lid's top {F(lidB.max.y)} m up (the handle); his corner " +
                              $"(Barnaby, off until given) stands on the lid at {F(onLid.y)} m, beside the handle, facing the street.");

            // ---------- the lamp on the wall ----------
            Dictionary<string, Material> palette = NightOneSteps.Materials();
            Material iron = palette["shelf iron"];
            var lamp = new GameObject("Lamp by the bins");
            lamp.transform.SetParent(group.transform, false);
            lamp.transform.SetPositionAndRotation(LampAt, Quaternion.identity);
            Part(lamp.transform, "Wall plate", PrimitiveType.Cube, new Vector3(0f, 0f, .015f), new Vector3(.16f, .22f, .03f), iron);
            Part(lamp.transform, "Arm", PrimitiveType.Cube, new Vector3(0f, .02f, .12f), new Vector3(.04f, .04f, .2f), iron);
            Part(lamp.transform, "Hood", PrimitiveType.Cube, new Vector3(0f, -.01f, .24f), new Vector3(.3f, .08f, .2f), iron);

            // ---------- the reveal camera (off: on for the lid's moment) ----------
            Vector3 camAt = new Vector3(DumpsterX + 2f, 2.2f, dumpB.max.z + 3f);
            Vector3 camLook = new Vector3(far.position.x, 1.6f, centreZ);
            var reveal = new GameObject("Reveal camera (the lid lifts)");
            reveal.transform.SetParent(group.transform, false);
            reveal.transform.SetPositionAndRotation(camAt, Quaternion.LookRotation(camLook - camAt));
            var cm = reveal.AddComponent<CinemachineCamera>();
            cm.Priority = 50;
            LensSettings cmLens = cm.Lens;
            cmLens.FieldOfView = 40f;
            cm.Lens = cmLens;
            reveal.SetActive(false);
            set.revealCamera = reveal;

            // ---------- making room (before the room for Ace is checked) ----------
            report.AppendLine();
            report.AppendLine("Making room:");
            MoveIfStill(layout, "Plants - PotPlant_02", PlantWas, PlantGoes, "the pot plant in the back corner, to the end of the drinks counter", report);
            MoveChalkboard(layout, report);
            RaiseTheClock(layout, inB, report);
            TurnTheWalkerBack(layout, report);
            report.AppendLine();

            // ---------- where Ace stands, each checked for room ----------
            var spots = new StringBuilder();
            Vector3 insideAt, outsideAt, binAt, giveAt;
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var source = new Dictionary<Collider, string>();
                var region = new Bounds(new Vector3(4.6f, 1.5f, 19f), new Vector3(9f, 4f, 8f));
                int solids = CopySolids(preview, region, group.transform, source);
                // The dumpster and the trash can, as they are at night.
                SolidBox(preview, dumpB, "the dumpster", source);
                SolidBox(preview, Union(canB, lidB), "the trash can", source);
                Physics.SyncTransforms();
                PhysicsScene physics = preview.GetPhysicsScene();
                spots.AppendLine($"Room for Ace (a capsule {F(AceRadius)} m round, from 0.15 m to 1.95 m up), among {solids} solid things around the bins:");
                // Inside, the back corner is tight (the window ledge along the east wall, the coffee sacks): the clear
                // place nearest the door, for Ace's whole width if there is one.
                insideAt = Nearest(physics, source, spots, "inside the back door", new Vector2(DoorX, inB.min.z - .45f),
                    5.3f, 6.95f, 16f, inB.min.z - .5f);
                outsideAt = Clear(physics, source, spots, "outside the back door", new[]
                {
                    new Vector2(6.9f, outB.max.z + .65f), new Vector2(6.85f, outB.max.z + .7f), new Vector2(6.95f, outB.max.z + .75f),
                });
                float front = dumpB.max.z;
                binAt = Clear(physics, source, spots, "at the near half (the bag goes in)", new[]
                {
                    new Vector2(near.position.x, front + .56f), new Vector2(near.position.x, front + .62f), new Vector2(near.position.x - .1f, front + .62f),
                });
                giveAt = Clear(physics, source, spots, "at the far half (handing him things)", new[]
                {
                    new Vector2(far.position.x, front + .56f), new Vector2(far.position.x, front + .62f), new Vector2(far.position.x + .1f, front + .62f),
                });
                // What's underfoot at the street spots (a kerb or the road would show here).
                foreach ((string what, Vector3 at) in new[] { ("outside the door", outsideAt), ("at the near half", binAt), ("at the far half", giveAt) })
                {
                    bool ground = physics.Raycast(new Vector3(at.x, 1.2f, at.z + AceRadius), Vector3.down, out RaycastHit hit, 2.5f, ~0, QueryTriggerInteraction.Ignore);
                    spots.AppendLine(ground
                        ? $"  underfoot {what}, at Ace's front edge: {(source.TryGetValue(hit.collider, out string s) ? s : hit.collider.name)} at {F(hit.point.y)} m"
                        : $"  underfoot {what}, at Ace's front edge: nothing solid (the ground has no collision by day; the night gives it some)");
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            // Facing the door (Night 0 begins here, the bag in hand).
            set.insideDoor = Marker(group.transform, "Ace - inside the back door", insideAt,
                Mathf.Atan2(DoorX - insideAt.x, inB.min.z - insideAt.z) * Mathf.Rad2Deg);
            set.outsideDoor = Marker(group.transform, "Ace - outside the back door", outsideAt, 270f);
            set.binSpot = Marker(group.transform, "Ace - at the near half", binAt, 180f);
            set.giveSpot = Marker(group.transform, "Ace - at the far half", giveAt, 180f);
            report.Append(spots);

            // ---------- the night's own things ----------
            var night = new GameObject(NightOnlyName);
            night.transform.SetParent(group.transform, false);
            var light = new GameObject("Lamp light");
            light.transform.SetParent(night.transform, false);
            light.transform.SetPositionAndRotation(LampAt + new Vector3(0f, -.06f, .24f),
                Quaternion.LookRotation(new Vector3(-.45f, -1f, .3f).normalized, Vector3.forward));
            Light bulbLight = light.AddComponent<Light>();
            bulbLight.type = LightType.Spot;
            bulbLight.spotAngle = 112f;
            bulbLight.innerSpotAngle = 50f;
            bulbLight.range = 7f;
            bulbLight.intensity = 9f;
            bulbLight.color = new Color(1f, .74f, .46f);
            bulbLight.shadows = LightShadows.None;
            bulbLight.renderMode = LightRenderMode.ForcePixel;
            set.lamp = bulbLight;
            Material bulb = AssetDatabase.LoadAssetAtPath<Material>(BulbMaterialPath);
            GameObject lens = Part(night.transform, "Lamp glow", PrimitiveType.Cube, LampAt + new Vector3(0f, -.055f, .24f) - night.transform.position,
                new Vector3(.24f, .02f, .15f), bulb != null ? bulb : iron);
            lens.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            Zone<NightBackDoor>(night.transform, "Back door (inside)", new Vector3(DoorX, AceY, inB.min.z - .45f), new Vector3(1f, 2f, .9f))
                .Also(d => { d.side = NightBackDoor.Side.Inside; d.through = set.outsideDoor; });
            Zone<NightBackDoor>(night.transform, "Back door (outside)", new Vector3(DoorX, AceY, outB.max.z + .55f), new Vector3(1.1f, 2f, 1.1f))
                .Also(d => { d.side = NightBackDoor.Side.Outside; d.through = set.insideDoor; });
            Zone<NightBins>(night.transform, "Bin it (the near half)", binAt, new Vector3(1.2f, 2f, 1f));
            Zone<NightGive>(night.transform, "Give him (the far half)", giveAt, new Vector3(1.2f, 2f, 1f));
            Vector3 shelfAt = shelf.transform.position;
            Zone<NightShelfTake>(night.transform, "Take it off the shelf (Ace's shelf)", new Vector3(shelfAt.x, AceY, WallInside - .8f), new Vector3(1.2f, 2f, 1.2f))
                .Also(t => t.shelf = shelf);
            var solid = new GameObject("Solid at night (the dumpster, the trash can)");
            solid.transform.SetParent(night.transform, false);
            var dumpSolid = solid.AddComponent<BoxCollider>();
            dumpSolid.center = dumpB.center;
            dumpSolid.size = dumpB.size;
            var canSolid = solid.AddComponent<BoxCollider>();
            Bounds canAll = Union(canB, lidB);
            canSolid.center = canAll.center;
            canSolid.size = canAll.size;
            night.SetActive(false);
            Undo.RecordObject(walk, "Bins 1 - the bins' night-only things");
            walk.nightOnly = walk.nightOnly.Where(g => g != null).Append(night).Distinct().ToArray();
            EditorUtility.SetDirty(walk);
            report.AppendLine($"At night: the lamp's light, five places to stand (the back door both sides, Bin it, Give him, the shelf) and the " +
                              $"dumpster and trash can made solid ({walk.nightOnly.Length} night-only things in all).");

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            // ---------- photos, by day ----------
            string folder = LogFolder("bins-setup");
            Photos(folder, set, dumpB, canB, report);
            report.AppendLine();
            report.AppendLine("The scene changed: read the diff, then save it (Ctrl+S). Edit > Undo takes it all out again.");
            File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
            Selection.activeGameObject = group;
            Debug.Log(Tag + "Put the back door and the bins behind the café. " + folder + "\n" + report);
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Putting the bins in FAILED: " + e.Message + "\n" + report + "\n" + e);
        }
    }

    [MenuItem(Menu + "Bins 1 - Take them out again")]
    static void TakeOut()
    {
        var report = new StringBuilder();
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode only.");
            CityPackChecks.RequireScene();
            GameObject layout = Layout();
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Bins 1 - take them out");
            Transform group = layout.transform.Find(GroupName);
            NightWalk walk = layout.GetComponentInChildren<NightWalk>(true);
            // What Bins 2 moved round the back door goes back first (its record is on the group).
            if (group != null && group.TryGetComponent(out NightZeroSet roomed)) PutTheCornerBack(roomed, walk, report);
            if (group != null)
            {
                if (walk != null)
                {
                    Undo.RecordObject(walk, "Bins 1 - without the bins' night-only things");
                    walk.nightOnly = walk.nightOnly.Where(g => g != null && !g.transform.IsChildOf(group)).ToArray();
                    EditorUtility.SetDirty(walk);
                }
                Undo.DestroyObjectImmediate(group.gameObject);
                report.AppendLine("Took the bins group out.");
            }
            else report.AppendLine("There was no bins group.");
            MoveIfStill(layout, "Plants - PotPlant_02", PlantGoes, PlantWas, "the pot plant, back to the corner", report);
            Transform board = Find(layout.transform, "Chalkboard menu");
            if (board != null && Mathf.Abs(board.position.x - ChalkboardGoesX) < .01f)
            {
                Undo.RecordObject(board, "Bins 1 - the chalkboard back");
                board.position = new Vector3(ChalkboardWasX, board.position.y, board.position.z);
                report.AppendLine("Put the chalkboard menu back.");
            }
            Transform clock = Find(layout.transform, "Walls - Clock_01");
            if (clock != null && Mathf.Abs(clock.position.x - ClockWas.x) < .01f && Mathf.Abs(clock.position.z - ClockWas.z) < .05f
                && clock.position.y > ClockWas.y + .005f)
            {
                Undo.RecordObject(clock, "Bins 1 - the clock back down");
                clock.position = ClockWas;
                report.AppendLine("Put the clock back down.");
            }
            TurnTheWalkerBack(layout, report, undo: true);
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log(Tag + "Took the bins out (Edit > Undo puts them back). The cut dumpster meshes stay in " + MeshAsset + "; they are harmless.\n" + report);
        }
        catch (Exception e) { Debug.LogError(Tag + "Taking the bins out FAILED: " + e.Message + "\n" + report); }
    }

    // ------------------------------------------------------------------ Bins 2: room for the back door (6 Oct 2026)

    // Mansoor's playtest (6 Oct): "the door inside the cafe that goes out towards the bin is off ... there's a side pillar
    // that's going from the wall to hitting the door". It was the courtyard window's sill: a cream ledge 0.26 m deep, 0.76 to
    // 0.86 m up, that ran the whole east wall (z 0 to 18) and so into the back door just under its handle; and the door's frame
    // stood 3 cm off the east wall. His call: the sill stops where the glass stops, and the door moves along the wall.
    //
    //   Fixit Fidget > Night > Bins 2 - Give the back door room (Edit Mode), once, after Bins 1:
    //     * the sill stops where the glass does, at the plaster pier by the corner (a copy of its mesh, cut short, in
    //       Assets/Art/NightZero/Courtyard sill - stops at the glass.asset; the scene's sill and the night's collision list use
    //       the copy, the street's geometry asset is not touched);
    //     * the door, both its leaves, the night's two places to stand at it and where Ace stands each side move along the
    //       wall, leaving 0.30 m of wall between its frame and the corner: as far as the coffee sacks allow (they reach x 6.07);
    //     * the chalkboard menu slides left, over the sacks; the cat picture goes on the plain pier, facing the room, far enough
    //       from the back wall not to fade with it (CutawayWall's reach); the loose coffee sack goes under the window ledge by
    //       the pier, out of the way to the door; the wall clock sits centred over the door.
    //   Photos before and after, and a report: Logs/Night/bins-door-room-<time>/. Edit > Undo takes it all out again; so does
    //   Bins 1 - Take them out again (which puts these things back too). Save the scene yourself once the diff is read.

    const string CourtyardWindows = "09 - neighborhood surrounds V3/V3 - courtyard-facing cafe windows";
    const string SillMeshAsset = MeshFolder + "/Courtyard sill - stops at the glass.asset";
    // Wall between the door's frame and the east wall: what the coffee sacks leave (the frame stops 9 cm short of them).
    const float CornerReturn = .30f;
    // CutawayWall counts anything within this of a wall's face as hung on it (its Reach): the picture on the pier stays further
    // than this from the back wall, or it would fade with the back wall.
    const float CutawayReach = .55f;
    // The skirting along the east wall stands this far out from it.
    const float Skirting = .09f;

    [MenuItem(Menu + "Bins 2 - Give the back door room (Edit Mode)")]
    static void GiveTheDoorRoom()
    {
        var report = new StringBuilder("Bins 2 - give the back door room\n"
            + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "\n\n");
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode only.");
            CityPackChecks.RequireScene();
            GameObject layout = Layout();
            Transform group = layout.transform.Find(GroupName);
            if (group == null) throw new InvalidOperationException("There are no bins in the scene yet (Bins 1 - Put the back door and the bins behind the café).");
            NightZeroSet set = group.GetComponent<NightZeroSet>();
            if (set == null) throw new InvalidOperationException("The bins group has no NightZeroSet.");
            if (set.cornerMoved != null && set.cornerMoved.Length > 0)
                throw new InvalidOperationException("Bins 2 has already run on this scene (the door has its room). Edit > Undo, or Bins 1 - Take them out again, to start over.");
            NightWalk walk = layout.GetComponentInChildren<NightWalk>(true);
            if (walk == null) throw new InvalidOperationException("There is no NightWalk in the scene.");
            Transform doorIn = group.Find("Back door (inside)"), doorOut = group.Find("Back door (outside)");
            if (doorIn == null || doorOut == null) throw new InvalidOperationException("The back door's two leaves aren't in the bins group.");
            Transform windows = layout.transform.Find(CourtyardWindows);
            MeshFilter sill = windows != null ? windows.Find("Cream trim")?.GetComponent<MeshFilter>() : null;
            MeshFilter plaster = windows != null ? windows.Find("Cloud plaster")?.GetComponent<MeshFilter>() : null;
            if (sill == null || sill.sharedMesh == null || plaster == null || plaster.sharedMesh == null)
                throw new InvalidOperationException($"The courtyard windows' sill and plaster aren't where they were ({LayoutRoot}/{CourtyardWindows}/Cream trim, Cloud plaster).");
            Transform board = Find(layout.transform, "Chalkboard menu");
            Transform picture = Find(layout.transform, "Cat portrait study");
            Transform sack = Find(layout.transform, "Coffee sack");
            Transform sacks = Find(layout.transform, "Coffee sacks");
            Transform clock = FindNear(layout.transform, "Walls - Clock_01", new Vector3(6.5f, 2.4f, WallInside), .5f);
            if (board == null || picture == null || sack == null || clock == null)
                throw new InvalidOperationException("The chalkboard menu, the cat picture, the loose coffee sack or the wall clock isn't in the scene where it was.");

            // ---------- where the glass stops: the start of the plaster pier by the corner ----------
            float pierFrom = PierStart(plaster);
            float pierTo = WallInside;
            report.AppendLine($"The east wall by the corner: the glass stops at z {F(pierFrom)}, where the plaster pier begins; the pier runs to the back wall (z {F(pierTo)}).");

            string folder = LogFolder("bins-door-room");
            Bounds inB0 = BoundsOf(doorIn.gameObject);
            CornerPhotos(folder, "before", set, inB0.center.x);

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Bins 2 - room for the back door");
            var moved = new List<NightZeroSet.Moved>();
            void Move(Transform what, Vector3 to, Quaternion turn)
            {
                moved.Add(new NightZeroSet.Moved { what = what, wasPosition = what.position, wasRotation = what.rotation, putAt = to });
                Undo.RecordObject(what, "Bins 2 - move " + what.name);
                what.SetPositionAndRotation(to, turn);
            }

            // ---------- the sill stops where the glass stops ----------
            Mesh was = sill.sharedMesh;
            Mesh cut = CutTheSill(was, sill.transform, pierFrom, report);
            Undo.RecordObject(sill, "Bins 2 - the sill cut short");
            sill.sharedMesh = cut;
            int pieces = 0;
            if (walk.nightCollision != null)
            {
                Undo.RecordObject(walk.nightCollision, "Bins 2 - the sill at night");
                for (int i = 0; i < walk.nightCollision.pieces.Length; i++)
                    if (walk.nightCollision.pieces[i].mesh == was)
                    {
                        walk.nightCollision.pieces[i].mesh = cut;
                        pieces++;
                    }
                EditorUtility.SetDirty(walk.nightCollision);
            }
            report.AppendLine($"  The night's collision list: {pieces} piece(s) now the cut sill (it was solid at night too).");

            // ---------- the door, and everything at it, along the wall ----------
            Bounds inB = BoundsOf(doorIn.gameObject), outB = BoundsOf(doorOut.gameObject);
            float doorX = EastWallInside - CornerReturn - inB.extents.x;
            float dx = doorX - inB.center.x;
            Vector3 along = new Vector3(dx, 0f, 0f);
            Move(doorIn, doorIn.position + along, doorIn.rotation);
            Move(doorOut, doorOut.position + along, doorOut.rotation);
            Transform night = group.Find(NightOnlyName);
            if (night != null)
                foreach (NightBackDoor zone in night.GetComponentsInChildren<NightBackDoor>(true))
                    Move(zone.transform, zone.transform.position + along, zone.transform.rotation);
            inB = BoundsOf(doorIn.gameObject);
            outB = BoundsOf(doorOut.gameObject);
            report.AppendLine();
            report.AppendLine($"The door: {F(-dx)} m along the wall, x {F(inB.min.x)} to {F(inB.max.x)} now ({F(EastWallInside - inB.max.x)} m of wall to the corner); " +
                              "both leaves and the night's two places to stand at it moved with it.");
            if (sacks != null)
            {
                Bounds pile = BoundsOf(sacks.gameObject);
                report.AppendLine(pile.max.x <= inB.min.x - .02f
                    ? $"  The coffee sacks reach x {F(pile.max.x)}: {F(inB.min.x - pile.max.x)} m short of the frame."
                    : $"  NOTE: the coffee sacks reach x {F(pile.max.x)}, past the frame's edge ({F(inB.min.x)}): look at photo 2.");
            }

            // ---------- the corner, tidied ----------
            report.AppendLine();
            report.AppendLine("Round it:");
            Bounds boardB = BoundsOf(board.gameObject);
            float boardShift = inB.min.x - .08f - boardB.max.x;
            if (boardShift < 0f)
            {
                Move(board, board.position + new Vector3(boardShift, 0f, 0f), board.rotation);
                boardB = BoundsOf(board.gameObject);
                report.AppendLine($"  the chalkboard menu: {F(-boardShift)} m left, x {F(boardB.min.x)} to {F(boardB.max.x)} (8 cm from the door's frame), over the sacks.");
            }
            else report.AppendLine("  the chalkboard menu: already clear of the door; left where it is.");

            Vector3 clockAt = clock.position;
            Move(clock, new Vector3(doorX, clockAt.y, clockAt.z), clock.rotation);
            Bounds clockB = BoundsOf(clock.gameObject);
            report.AppendLine($"  the wall clock: centred over the door (x {F(doorX)}), {F(clockB.min.y - inB.max.y)} m above its frame.");

            // The cat picture, onto the pier: facing the room (turned a quarter), its back on the plaster, out of the back wall's
            // fade reach, otherwise in the pier's middle.
            Quaternion facingRoom = Quaternion.Euler(0f, 90f, 0f) * picture.rotation;
            picture.rotation = facingRoom;   // measured turned (recorded below)
            Bounds pic = BoundsOf(picture.gameObject);
            picture.rotation = Quaternion.Inverse(Quaternion.Euler(0f, 90f, 0f)) * facingRoom;
            float picZ = Mathf.Min((pierFrom + pierTo) * .5f, WallInside - CutawayReach - .03f - pic.extents.z);
            Vector3 picShift = new Vector3(EastWallInside - .012f - pic.max.x, 0f, picZ - pic.center.z);
            Move(picture, picture.position + picShift, facingRoom);
            pic = BoundsOf(picture.gameObject);
            report.AppendLine($"  the cat picture: onto the plaster pier, facing the room: z {F(pic.min.z)} to {F(pic.max.z)}, {F(pic.min.y)} to {F(pic.max.y)} m up; " +
                              $"{F(WallInside - pic.max.z)} m from the back wall (it fades with a wall within {F(CutawayReach)}), {F(pic.min.z - pierFrom)} m clear of the glass.");
            if (pic.min.z < pierFrom + .03f) report.AppendLine("  NOTE: the picture reaches past the pier onto the glass.");

            // The loose sack: under the window ledge, at the foot of the last bay before the pier, its long side along the wall.
            // (By the pier itself it would narrow the way to the door to Ace's width: the sacks' pile is on the other side.)
            Quaternion sackTurn = sack.rotation, upright = Quaternion.identity;
            sack.rotation = upright;   // measured turned; put back for the record below
            Bounds sackB = BoundsOf(sack.gameObject);
            if (sackB.size.x > sackB.size.z)
            {
                upright = Quaternion.Euler(0f, 90f, 0f);
                sack.rotation = upright;
                sackB = BoundsOf(sack.gameObject);
            }
            Vector3 sackShift = new Vector3(EastWallInside - Skirting - sackB.max.x, 0f, pierFrom - .25f - sackB.max.z);
            sack.rotation = sackTurn;
            Move(sack, sack.position + sackShift, upright);
            sackB = BoundsOf(sack.gameObject);
            Bounds sillB = BoundsOf(sill.gameObject);
            report.AppendLine($"  the loose coffee sack: under the window ledge by the pier, x {F(sackB.min.x)} to {F(sackB.max.x)}, z {F(sackB.min.z)} to {F(sackB.max.z)}; " +
                              $"its top {F(sackB.max.y)} m up, the ledge's underside {F(sillB.min.y)} m.");

            // ---------- where Ace stands, each side, checked for room again ----------
            var spots = new StringBuilder();
            Vector3 insideAt, outsideAt;
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var source = new Dictionary<Collider, string>();
                var region = new Bounds(new Vector3(5.5f, 1.5f, 18f), new Vector3(6f, 4f, 7f));
                int solids = CopySolids(preview, region, group, source);
                Transform dump = group.Find("Dumpster"), can = group.Find("Trash can (his corner)");
                if (dump != null) SolidBox(preview, BoundsOf(dump.gameObject), "the dumpster", source);
                if (can != null) SolidBox(preview, BoundsOf(can.gameObject), "the trash can", source);
                Physics.SyncTransforms();
                PhysicsScene physics = preview.GetPhysicsScene();
                spots.AppendLine($"Room for Ace (a capsule {F(AceRadius)} m round, from 0.15 m to 1.95 m up), among {solids} solid things around the door:");
                insideAt = Nearest(physics, source, spots, "inside the back door", new Vector2(doorX, inB.min.z - .45f),
                    doorX - 1.4f, doorX + .35f, 16f, inB.min.z - .5f);
                outsideAt = Clear(physics, source, spots, "outside the back door", new[]
                {
                    new Vector2(doorX, outB.max.z + .65f), new Vector2(doorX - .05f, outB.max.z + .7f), new Vector2(doorX + .05f, outB.max.z + .75f),
                });
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            if (set.insideDoor != null)
                Move(set.insideDoor, insideAt, Quaternion.Euler(0f, Mathf.Atan2(doorX - insideAt.x, inB.min.z - insideAt.z) * Mathf.Rad2Deg, 0f));
            if (set.outsideDoor != null) Move(set.outsideDoor, outsideAt, set.outsideDoor.rotation);
            report.AppendLine();
            report.Append(spots);

            // ---------- the record, to put it all back ----------
            Undo.RecordObject(set, "Bins 2 - the record");
            set.cornerMoved = moved.ToArray();
            set.sill = sill;
            set.sillWas = was;
            EditorUtility.SetDirty(set);
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            CornerPhotos(folder, "after", set, doorX);
            report.AppendLine();
            report.AppendLine("Photos, before and after (the same five views): 1 the corner from behind the counter, 2 from where Ace stands inside the door, " +
                              "3 the side wall by the corner, 4 from above, 5 the door from Back Street.");
            report.AppendLine();
            report.AppendLine("The scene changed: read the diff, then save it (Ctrl+S). Edit > Undo takes it all out again; so does Bins 1 - Take them out again.");
            File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
            Selection.activeGameObject = doorIn.gameObject;
            Debug.Log(Tag + "Gave the back door room. " + folder + "\n" + report);
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Giving the back door room FAILED: " + e.Message + "\n" + report + "\n" + e);
        }
    }

    // Where the plaster pier by the corner begins: the courtyard plaster is three boxes (the piers), the last reaching the back wall.
    static float PierStart(MeshFilter plaster)
    {
        Vector3[] v = plaster.sharedMesh.vertices;
        float best = float.MaxValue;
        for (int b = 0; b + 24 <= v.Length; b += 24)
        {
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            for (int i = b; i < b + 24; i++)
            {
                Vector3 w = plaster.transform.TransformPoint(v[i]);
                min = Vector3.Min(min, w);
                max = Vector3.Max(max, w);
            }
            if (min.x >= EastWallInside - .05f && max.z >= WallInside - .1f) best = Mathf.Min(best, min.z);
        }
        if (best == float.MaxValue || best < 15.5f || best > 17.5f)
            throw new InvalidOperationException($"The plaster pier by the corner wasn't found in '{plaster.sharedMesh.name}' (expected one box from about z 16.4 to the back wall).");
        return best;
    }

    // A copy of the sill's mesh (one box along the east wall), its far end moved back to <endZ>: saved as an asset of our own.
    static Mesh CutTheSill(Mesh sill, Transform at, float endZ, StringBuilder report)
    {
        Vector3[] v = sill.vertices;
        Vector3[] world = v.Select(p => at.TransformPoint(p)).ToArray();
        float minX = world.Min(p => p.x), maxZ = world.Max(p => p.z), minZ = world.Min(p => p.z);
        if (v.Length != 24 || minX < EastWallInside - .4f || maxZ < WallInside - .1f || minZ > 1f)
            throw new InvalidOperationException($"'{sill.name}' isn't the sill it was (one box along the east wall, z 0 to 18): {v.Length} corners, x from {F(minX)}, z {F(minZ)} to {F(maxZ)}.");
        int movedCorners = 0;
        for (int i = 0; i < v.Length; i++)
            if (world[i].z > endZ + .001f)
            {
                v[i] = at.InverseTransformPoint(new Vector3(world[i].x, world[i].y, endZ));
                movedCorners++;
            }
        var copy = Object.Instantiate(sill);
        copy.name = "Courtyard sill - stops at the glass";
        copy.vertices = v;
        copy.RecalculateBounds();
        EnsureMeshFolder();
        Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(SillMeshAsset);
        if (saved != null)
        {
            EditorUtility.CopySerialized(copy, saved);
            saved.name = copy.name;
            Object.DestroyImmediate(copy);
            EditorUtility.SetDirty(saved);
        }
        else
        {
            AssetDatabase.CreateAsset(copy, SillMeshAsset);
            saved = copy;
        }
        report.AppendLine($"  The sill: {movedCorners} of its 24 corners moved from z {F(maxZ)} back to z {F(endZ)} (it is {F(endZ - minZ)} m long now, " +
                          $"was {F(maxZ - minZ)}); saved in {SillMeshAsset}. The street's geometry asset is untouched.");
        return saved;
    }

    static void EnsureMeshFolder()
    {
        if (AssetDatabase.IsValidFolder(MeshFolder)) return;
        string parent = Path.GetDirectoryName(MeshFolder).Replace('\\', '/');
        AssetDatabase.CreateFolder(parent, Path.GetFileName(MeshFolder));
    }

    // Bins 1 - Take them out again: what Bins 2 moved goes back (each only if it's still where Bins 2 put it), and the sill is
    // whole again, in the scene and in the night's collision list.
    static void PutTheCornerBack(NightZeroSet set, NightWalk walk, StringBuilder report)
    {
        int back = 0, left = 0;
        foreach (NightZeroSet.Moved m in set.cornerMoved ?? Array.Empty<NightZeroSet.Moved>())
        {
            if (m.what == null) continue;
            if (m.what.IsChildOf(set.transform)) continue;   // the door and its places go with the group
            if (Vector3.Distance(m.what.position, m.putAt) > .02f) { left++; continue; }
            Undo.RecordObject(m.what, "Bins 1 - put it back");
            m.what.SetPositionAndRotation(m.wasPosition, m.wasRotation);
            back++;
        }
        if (set.sill != null && set.sillWas != null && set.sill.sharedMesh != set.sillWas)
        {
            Mesh cut = set.sill.sharedMesh;
            Undo.RecordObject(set.sill, "Bins 1 - the sill whole again");
            set.sill.sharedMesh = set.sillWas;
            if (walk != null && walk.nightCollision != null)
            {
                Undo.RecordObject(walk.nightCollision, "Bins 1 - the sill whole at night");
                for (int i = 0; i < walk.nightCollision.pieces.Length; i++)
                    if (walk.nightCollision.pieces[i].mesh == cut) walk.nightCollision.pieces[i].mesh = set.sillWas;
                EditorUtility.SetDirty(walk.nightCollision);
            }
            report.AppendLine("The window sill runs the whole wall again (Bins 2 undone).");
        }
        if (back + left > 0)
            report.AppendLine($"Bins 2's moves: {back} put back{(left > 0 ? $", {left} left alone (moved since)" : "")}.");
    }

    // The corner in five views (Edit Mode), before or after: the same views both times.
    static void CornerPhotos(string folder, string when, NightZeroSet set, float doorX)
    {
        void Shot(string name, Vector3 from, Vector3 at, float fov) =>
            NightWalkSteps.Capture(Path.Combine(folder, when + "-" + name + ".png"), from, at, fov, false);
        Shot("1-from-behind-the-counter", new Vector3(5.1f, 1.7f, 14.6f), new Vector3(6.6f, 1.1f, WallInside), 55f);
        Vector3 ace = set.insideDoor != null ? set.insideDoor.position : new Vector3(doorX, AceY, 16.9f);
        Shot("2-from-where-ace-stands", new Vector3(ace.x - .35f, 1.65f, ace.z - .55f), new Vector3(doorX, 1.15f, WallInside), 60f);
        Shot("3-the-side-wall-by-the-corner", new Vector3(5.3f, 1.65f, 16.2f), new Vector3(EastWallInside, 1.35f, 16.9f), 55f);
        Shot("4-from-above", new Vector3(6.1f, 8.5f, 16.6f), new Vector3(6.1f, 0f, 17.15f), 50f);
        Shot("5-the-door-from-back-street", new Vector3(doorX + .4f, 2.1f, 22.4f), new Vector3(doorX - .3f, 1.2f, WallOutside), 55f);
    }

    // ------------------------------------------------------------------ the dumpster's lids, cut free

    sealed class Dumpster
    {
        public Mesh body, nearLid, farLid;
        public Material[] material;
        public Vector3 nearPivot, farPivot;   // the hinges, in the dumpster's own space
        public bool backAtMinZ;               // its back (high, hinged) edge is at its own -z
        public float halfDepthBack;           // from its middle to its back
        public float floor;                   // its floor inside, above the ground
        public float lidOpen;
    }

    // The skip's mesh is one piece of many separate parts (its body, wheels, hinge bar, the two lids...). The lids are
    // found as the two parts that are lid-shaped (over a metre deep, about half its width, high up), one each side of
    // its middle; everything else is the body. The parts are found by joining triangles that share a corner (corners
    // at the same place count as shared). Each lid's mesh is moved so its hinge (the top of its back edge) is at its
    // origin.
    static Dumpster CutTheLids(GameObject skipPrefab, StringBuilder report)
    {
        MeshFilter filter = skipPrefab.GetComponentInChildren<MeshFilter>(true);
        MeshRenderer renderer = filter != null ? filter.GetComponent<MeshRenderer>() : null;
        Mesh source = filter != null ? filter.sharedMesh : null;
        if (source == null || renderer == null) throw new InvalidOperationException("SM_Prop_Skip_02 has no mesh to cut.");
        if (filter.transform != skipPrefab.transform && (filter.transform.localPosition != Vector3.zero || filter.transform.localRotation != Quaternion.identity))
            report.AppendLine("NOTE: the skip's mesh sits on a child with its own offset; the cut parts use the mesh's own space.");
        Vector3[] v = source.vertices;
        int[] tris = source.triangles;
        if (source.subMeshCount != 1) report.AppendLine($"NOTE: the skip's mesh has {source.subMeshCount} sub-meshes; the cut parts use one (its first material).");

        // Corners at the same place are one corner.
        var weld = new int[v.Length];
        var keyOf = new Dictionary<(int, int, int), int>();
        for (int i = 0; i < v.Length; i++)
        {
            var key = (Mathf.RoundToInt(v[i].x * 10000f), Mathf.RoundToInt(v[i].y * 10000f), Mathf.RoundToInt(v[i].z * 10000f));
            if (!keyOf.TryGetValue(key, out int w)) keyOf[key] = w = keyOf.Count;
            weld[i] = w;
        }
        var parent = Enumerable.Range(0, keyOf.Count).ToArray();
        int Root(int a) { while (parent[a] != a) a = parent[a] = parent[parent[a]]; return a; }
        for (int t = 0; t < tris.Length; t += 3)
        {
            int a = Root(weld[tris[t]]);
            for (int k = 1; k < 3; k++)
            {
                int b = Root(weld[tris[t + k]]);
                if (a != b) parent[b] = a;
            }
        }
        var parts = new Dictionary<int, List<int>>();   // root -> the part's triangles (first index of each)
        for (int t = 0; t < tris.Length; t += 3)
        {
            int r = Root(weld[tris[t]]);
            if (!parts.TryGetValue(r, out List<int> list)) parts[r] = list = new List<int>();
            list.Add(t);
        }
        Bounds PartBounds(List<int> part)
        {
            var b = new Bounds(v[tris[part[0]]], Vector3.zero);
            foreach (int t in part) for (int k = 0; k < 3; k++) b.Encapsulate(v[tris[t + k]]);
            return b;
        }
        Bounds all = source.bounds;
        var lids = parts.Values.Where(p =>
        {
            Bounds b = PartBounds(p);
            return b.size.z > all.size.z * .8f && b.size.x > all.size.x * .3f && b.size.x < all.size.x * .6f && b.min.y > all.size.y * .55f;
        }).ToList();
        report.AppendLine($"The skip's mesh: {v.Length} corners, {tris.Length / 3} triangles, {parts.Count} separate parts; {all.size.x:0.00} x {all.size.y:0.00} x {all.size.z:0.00} m.");
        if (lids.Count != 2 || lids.Count(p => PartBounds(p).center.x > 0f) != 1)
        {
            var listing = new StringBuilder();
            foreach (List<int> p in parts.Values.OrderBy(p => PartBounds(p).min.y))
            {
                Bounds b = PartBounds(p);
                listing.AppendLine($"  {p.Count} triangles: x {F(b.min.x)}..{F(b.max.x)}, y {F(b.min.y)}..{F(b.max.y)}, z {F(b.min.z)}..{F(b.max.z)}");
            }
            throw new InvalidOperationException($"Couldn't tell the skip's two lids from its other parts ({lids.Count} lid-shaped); nothing was changed. Its parts:\n" + listing);
        }
        List<int> nearPart = lids.First(p => PartBounds(p).center.x > 0f), farPart = lids.First(p => PartBounds(p).center.x <= 0f);
        var lidTris = new HashSet<int>(nearPart.Concat(farPart));
        var bodyPart = Enumerable.Range(0, tris.Length / 3).Select(i => i * 3).Where(t => !lidTris.Contains(t)).ToList();

        // Which edge is its back: a lid is higher at its hinge than at its front.
        Bounds nb = PartBounds(nearPart);
        float HeightAt(List<int> part, bool minZ, Bounds b)
        {
            float best = float.MinValue;
            foreach (int t in part)
                for (int k = 0; k < 3; k++)
                {
                    Vector3 p = v[tris[t + k]];
                    if (minZ ? p.z < b.min.z + .1f : p.z > b.max.z - .1f) best = Mathf.Max(best, p.y);
                }
            return best;
        }
        var cut = new Dumpster { backAtMinZ = HeightAt(nearPart, true, nb) > HeightAt(nearPart, false, nb) };
        Vector3 Pivot(List<int> part)
        {
            Bounds b = PartBounds(part);
            return new Vector3(b.center.x, b.max.y, cut.backAtMinZ ? b.min.z : b.max.z);
        }
        cut.nearPivot = Pivot(nearPart);
        cut.farPivot = Pivot(farPart);
        // Lifting the front edge up and over: about the x axis, away from the back.
        cut.lidOpen = cut.backAtMinZ ? -104f : 104f;
        cut.halfDepthBack = cut.backAtMinZ ? -all.min.z : all.max.z;
        // The body's lowest parts are its wheels; its floor is the body proper's bottom (the biggest part), a little up from it.
        Bounds biggest = parts.Values.Where(p => !lidTris.Contains(p[0])).Select(PartBounds).OrderByDescending(b => b.size.x * b.size.z).First();
        cut.floor = biggest.min.y + .06f;

        Object[] existing = File.Exists(MeshAsset) ? AssetDatabase.LoadAllAssetsAtPath(MeshAsset) : Array.Empty<Object>();
        Mesh Existing(string name) => existing.OfType<Mesh>().FirstOrDefault(m => m.name == name);
        bool fresh = Existing(BodyMesh) == null || Existing(NearLidMesh) == null || Existing(FarLidMesh) == null;
        if (fresh && existing.Length > 0) AssetDatabase.DeleteAsset(MeshAsset);
        cut.body = Build(fresh ? null : Existing(BodyMesh), BodyMesh, source, bodyPart, Vector3.zero);
        cut.nearLid = Build(fresh ? null : Existing(NearLidMesh), NearLidMesh, source, nearPart, cut.nearPivot);
        cut.farLid = Build(fresh ? null : Existing(FarLidMesh), FarLidMesh, source, farPart, cut.farPivot);
        if (fresh)
        {
            if (!AssetDatabase.IsValidFolder(MeshFolder)) AssetDatabase.CreateFolder(Path.GetDirectoryName(MeshFolder).Replace('\\', '/'), Path.GetFileName(MeshFolder));
            AssetDatabase.CreateAsset(cut.body, MeshAsset);
            AssetDatabase.AddObjectToAsset(cut.nearLid, MeshAsset);
            AssetDatabase.AddObjectToAsset(cut.farLid, MeshAsset);
        }
        else
        {
            EditorUtility.SetDirty(cut.body);
            EditorUtility.SetDirty(cut.nearLid);
            EditorUtility.SetDirty(cut.farLid);
        }
        AssetDatabase.SaveAssets();
        cut.material = renderer.sharedMaterials.Take(1).ToArray();
        report.AppendLine($"Cut its lids free: the body {bodyPart.Count} triangles, the lids {nearPart.Count} and {farPart.Count}; its back (the hinges) is at its own " +
                          $"{(cut.backAtMinZ ? "-z" : "+z")}, {F(nb.max.y)} m up, the lids' front edge {F(HeightAt(nearPart, !cut.backAtMinZ, nb))} m. " +
                          $"Its floor inside is about {F(cut.floor)} m up. Saved in {MeshAsset}.");
        return cut;
    }

    // A mesh of some of <source>'s triangles, its corners moved by -<origin>, its unused corners left out. Into
    // <into> (an existing asset's mesh, kept so the scene's references hold) or a new one.
    static Mesh Build(Mesh into, string name, Mesh source, List<int> triangleStarts, Vector3 origin)
    {
        int[] tris = source.triangles;
        Vector3[] v = source.vertices, n = source.normals;
        Vector4[] tan = source.tangents;
        Vector2[] uv = source.uv, uv2 = source.uv2;
        Color[] col = source.colors;
        var map = new Dictionary<int, int>();
        var used = new List<int>();
        var newTris = new List<int>(triangleStarts.Count * 3);
        foreach (int t in triangleStarts)
            for (int k = 0; k < 3; k++)
            {
                int old = tris[t + k];
                if (!map.TryGetValue(old, out int now)) { map[old] = now = used.Count; used.Add(old); }
                newTris.Add(now);
            }
        Mesh mesh = into != null ? into : new Mesh();
        mesh.Clear();
        mesh.name = name;
        mesh.SetVertices(used.Select(i => v[i] - origin).ToList());
        if (n.Length == v.Length) mesh.SetNormals(used.Select(i => n[i]).ToList());
        if (tan.Length == v.Length) mesh.SetTangents(used.Select(i => tan[i]).ToList());
        if (uv.Length == v.Length) mesh.SetUVs(0, used.Select(i => uv[i]).ToList());
        if (uv2.Length == v.Length) mesh.SetUVs(1, used.Select(i => uv2[i]).ToList());
        if (col.Length == v.Length) mesh.SetColors(used.Select(i => col[i]).ToList());
        mesh.SetTriangles(newTris, 0, true);
        if (n.Length != v.Length) mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static void MeshPart(GameObject go, Mesh mesh, Material[] material)
    {
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterials = material;
    }

    static Transform Hinge(Transform dumpster, string name, Mesh lid, Vector3 pivot, Material[] material)
    {
        var hinge = new GameObject(name);
        hinge.transform.SetParent(dumpster, false);
        hinge.transform.localPosition = pivot;
        hinge.transform.localRotation = Quaternion.identity;
        MeshPart(hinge, lid, material);
        return hinge.transform;
    }

    // ------------------------------------------------------------------ the pieces

    // A door leaf, flattened against the back wall: inside, its handle side toward the room; outside, toward the street.
    static GameObject Door(GameObject prefab, Transform parent, string name, bool inside)
    {
        var door = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        door.name = name;
        door.transform.SetPositionAndRotation(new Vector3(DoorX, 0f, inside ? WallInside - .1f : WallOutside + .1f), Quaternion.Euler(0f, inside ? 180f : 0f, 0f));
        door.transform.localScale = DoorScale;
        Bounds b = BoundsOf(door);
        float shift = inside ? WallInside - .02f - b.max.z : WallOutside + .005f - b.min.z;
        door.transform.position += new Vector3(DoorX - b.center.x, -b.min.y, shift);
        return door;
    }

    // <thing> standing on the ground at <at>'s x, its back <back> (a z) off the wall.
    static void Sit(GameObject thing, Vector3 at, float back)
    {
        thing.transform.SetPositionAndRotation(new Vector3(at.x, 0f, back + 1f), Quaternion.identity);
        Bounds b = BoundsOf(thing);
        thing.transform.position += new Vector3(at.x - b.center.x, -b.min.y, back - b.min.z);
    }

    // How high <thing>'s surface is under <at> (a ray down onto its own mesh, through a collider that's there only for
    // the ray). <fallback> if the ray misses.
    static float TopAt(GameObject thing, Vector3 at, float fallback)
    {
        var made = new List<MeshCollider>();
        try
        {
            foreach (MeshFilter f in thing.GetComponentsInChildren<MeshFilter>())
                if (f.sharedMesh != null) { var c = f.gameObject.AddComponent<MeshCollider>(); c.sharedMesh = f.sharedMesh; made.Add(c); }
            Physics.SyncTransforms();
            float best = float.MinValue;
            foreach (MeshCollider c in made)
                if (c.Raycast(new Ray(new Vector3(at.x, fallback + 1f, at.z), Vector3.down), out RaycastHit hit, 3f)) best = Mathf.Max(best, hit.point.y);
            return best > float.MinValue ? best : fallback;
        }
        finally { foreach (MeshCollider c in made) if (c != null) Object.DestroyImmediate(c); }
    }

    static Transform Marker(Transform parent, string name, Vector3 at, float yaw)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(new Vector3(at.x, at.y, at.z), Quaternion.Euler(0f, yaw, 0f));
        return go.transform;
    }

    // A place to stand at night: a trigger on the Default layer (the interactor's night search sees it), the thing on it.
    static T Zone<T>(Transform parent, string name, Vector3 at, Vector3 size) where T : NightInteractable
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(new Vector3(at.x, AceY, at.z), Quaternion.identity);
        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = size;
        return go.AddComponent<T>();
    }

    static GameObject Part(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    // ------------------------------------------------------------------ making room

    static void MoveIfStill(GameObject layout, string name, Vector3 was, Vector3 goes, string what, StringBuilder report)
    {
        Transform t = FindNear(layout.transform, name, was, .3f);
        if (t == null)
        {
            Transform there = FindNear(layout.transform, name, goes, .05f);
            report.AppendLine(there != null ? $"  {what}: already there." : $"  {what}: not where it was ({V(was)}), so left alone.");
            return;
        }
        Undo.RecordObject(t, "Bins 1 - make room");
        t.position = goes;
        report.AppendLine($"  {what}: {V(was)} to {V(goes)}.");
    }

    static void MoveChalkboard(GameObject layout, StringBuilder report)
    {
        Transform board = Find(layout.transform, "Chalkboard menu");
        if (board == null) { report.AppendLine("  the chalkboard menu: not found."); return; }
        if (Mathf.Abs(board.position.x - ChalkboardGoesX) < .01f) { report.AppendLine("  the chalkboard menu: already moved."); return; }
        if (Mathf.Abs(board.position.x - ChalkboardWasX) > .01f) { report.AppendLine($"  the chalkboard menu: not where it was (x {F(board.position.x)}), so left alone."); return; }
        Undo.RecordObject(board, "Bins 1 - the chalkboard");
        board.position = new Vector3(ChalkboardGoesX, board.position.y, board.position.z);
        report.AppendLine($"  the chalkboard menu: {F(ChalkboardWasX - ChalkboardGoesX)} m away from the door (its right edge now at x {F(BoundsOf(board.gameObject).max.x)}).");
    }

    // The wall clock over the door: up, if it would sit on the door's frame (its top stays under the ceiling).
    static void RaiseTheClock(GameObject layout, Bounds door, StringBuilder report)
    {
        Transform clock = FindNear(layout.transform, "Walls - Clock_01", ClockWas, .3f);
        if (clock == null) { report.AppendLine("  the clock: not found where it was; left alone."); return; }
        Bounds b = BoundsOf(clock.gameObject);
        bool over = b.max.x > door.min.x - .02f && b.min.x < door.max.x + .02f;
        float gap = b.min.y - door.max.y;
        if (!over || gap >= .06f)
        {
            report.AppendLine($"  the clock: clear of the door ({(over ? F(gap) + " m above its frame" : "beside it")}); left where it is.");
            return;
        }
        float up = .06f - gap;
        if (b.max.y + up > 3.12f)
        {
            report.AppendLine($"  the clock: it would sit on the door's frame ({F(gap)} m above it), and there's no room above; left where it is: move it by hand.");
            return;
        }
        Undo.RecordObject(clock, "Bins 1 - the clock up");
        clock.position += Vector3.up * up;
        report.AppendLine($"  the clock: {F(up)} m up, off the door's frame (its bottom {F(b.min.y + up)} m, the frame's top {F(door.max.y)} m).");
    }

    // The rear lane's walker walked the pavement behind the café from x -5 to x 7: through where the dumpster
    // stands now. He turns back at x 1.6 instead, before the trash can.
    static void TurnTheWalkerBack(GameObject layout, StringBuilder report, bool undo = false)
    {
        float from = undo ? RouteTurnGoesX : RouteTurnWasX, to = undo ? RouteTurnWasX : RouteTurnGoesX;
        Transform route = Find(layout.transform, "Rear lane neighbor route");
        if (route == null) { report.AppendLine("  the rear lane's walker: his route wasn't found."); return; }
        int moved = 0;
        foreach (Transform point in route)
            if (Mathf.Abs(point.position.x - from) < .05f)
            {
                Undo.RecordObject(point, "Bins 1 - the walker's turn");
                point.position = new Vector3(to, point.position.y, point.position.z);
                moved++;
            }
        StreetLife life = CityPackChecks.InScene<StreetLife>().FirstOrDefault(l => l.actors.Any(a => a != null && a.waypoints != null && a.waypoints.Contains(route.GetChild(0))));
        if (moved > 0 && life != null)
        {
            Undo.RecordObjects(life.actors.Where(a => a != null && a.actor != null).Select(a => (Object)a.actor).ToArray(), "Bins 1 - the walkers' places");
            life.RebuildRoutes();   // where they stand in Edit Mode, too
        }
        report.AppendLine(moved > 0 ? $"  the rear lane's walker: turns back at x {F(to)} now, not {F(from)} ({moved} points)."
            : Mathf.Abs(route.GetChild(Mathf.Min(1, route.childCount - 1)).position.x - to) < .05f ? "  the rear lane's walker: already turns back there." : "  the rear lane's walker: his turn wasn't where it was; left alone.");
    }

    // ------------------------------------------------------------------ room for Ace

    // The first of <candidates> (x, z) where Ace fits; or the first, noted as tight.
    static Vector3 Clear(PhysicsScene physics, Dictionary<Collider, string> source, StringBuilder spots, string what, Vector2[] candidates)
    {
        var hits = new Collider[16];
        string firstBlocker = null;
        foreach (Vector2 c in candidates)
        {
            int count = physics.OverlapCapsule(new Vector3(c.x, .15f + AceRadius, c.y), new Vector3(c.x, 1.95f - AceRadius, c.y), AceRadius, hits, ~0, QueryTriggerInteraction.Ignore);
            if (count == 0)
            {
                spots.AppendLine($"  {what}: ({F(c.x)}, {F(c.y)}), clear.");
                return new Vector3(c.x, AceY, c.y);
            }
            string blocker = source.TryGetValue(hits[0], out string s) ? s : hits[0].name;
            firstBlocker ??= blocker;
            spots.AppendLine($"  {what}: ({F(c.x)}, {F(c.y)}) touches {blocker}");
        }
        spots.AppendLine($"  {what}: NO candidate is clear; using the first (Ace may be nudged by {firstBlocker}).");
        return new Vector3(candidates[0].x, AceY, candidates[0].y);
    }

    // The clear place nearest <want> within x <x0>..<x1>, z <z0>..<z1> (a 5 cm grid): first for Ace's whole width (0.5 m
    // round), then a touch thinner (0.45 m: the controller slides out of a few centimetres). None: <want> itself, noted.
    static Vector3 Nearest(PhysicsScene physics, Dictionary<Collider, string> source, StringBuilder spots, string what, Vector2 want,
        float x0, float x1, float z0, float z1)
    {
        var hits = new Collider[16];
        foreach (float radius in new[] { .5f, AceRadius })
        {
            Vector2 best = default;
            float bestDistance = float.MaxValue;
            for (float x = x0; x <= x1 + 1e-4f; x += .05f)
                for (float z = z0; z <= z1 + 1e-4f; z += .05f)
                {
                    float d = Vector2.Distance(new Vector2(x, z), want);
                    if (d >= bestDistance) continue;
                    if (physics.OverlapCapsule(new Vector3(x, .15f + radius, z), new Vector3(x, 1.95f - radius, z), radius, hits, ~0,
                            QueryTriggerInteraction.Ignore) > 0) continue;
                    best = new Vector2(x, z);
                    bestDistance = d;
                }
            if (bestDistance < float.MaxValue)
            {
                spots.AppendLine($"  {what}: ({F(best.x)}, {F(best.y)}), clear for a capsule {F(radius)} m round, {F(bestDistance)} m from the door's front.");
                return new Vector3(best.x, AceY, best.y);
            }
        }
        int count = physics.OverlapCapsule(new Vector3(want.x, .6f, want.y), new Vector3(want.x, 1.5f, want.y), AceRadius, hits, ~0, QueryTriggerInteraction.Ignore);
        spots.AppendLine($"  {what}: NO clear place near the door; using ({F(want.x)}, {F(want.y)}) (it touches " +
                         $"{(count > 0 ? source.TryGetValue(hits[0], out string s) ? s : hits[0].name : "nothing")}).");
        return new Vector3(want.x, AceY, want.y);
    }

    // Everything that could be in the way around <region>, as colliders in the preview scene: the meshes that are
    // drawn (made solid), and the colliders that are there already. (As NightOneSteps' search by Grace's door.)
    static int CopySolids(Scene preview, Bounds region, Transform skip, Dictionary<Collider, string> source)
    {
        int count = 0;
        var cache = new Dictionary<Mesh, bool>();
        var people = new List<Transform>();
        foreach (StreetLife life in CityPackChecks.InScene<StreetLife>())
            foreach (StreetLife.Actor actor in life.actors)
                if (actor != null && actor.actor != null) people.Add(actor.actor);
        foreach (PolygonNpcVisual npc in CityPackChecks.InScene<PolygonNpcVisual>()) people.Add(npc.transform);
        foreach (CharacterController cc in CityPackChecks.InScene<CharacterController>()) people.Add(cc.transform);
        bool Skip(Transform t) => skip != null && t.IsChildOf(skip) || people.Any(p => t.IsChildOf(p));
        foreach (MeshFilter filter in CityPackChecks.InScene<MeshFilter>())
        {
            var renderer = filter.GetComponent<MeshRenderer>();
            Mesh mesh = filter.sharedMesh;
            if (renderer == null || mesh == null || !renderer.enabled || !filter.gameObject.activeInHierarchy) continue;
            if (renderer.shadowCastingMode == ShadowCastingMode.ShadowsOnly || !renderer.bounds.Intersects(region) || Skip(filter.transform)) continue;
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
            if (c == null || !c.enabled || c.isTrigger || !c.gameObject.activeInHierarchy || !c.bounds.Intersects(region) || Skip(c.transform)) continue;
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

    static void SolidBox(Scene preview, Bounds b, string name, Dictionary<Collider, string> source)
    {
        var go = new GameObject("solid") { hideFlags = HideFlags.HideAndDontSave };
        SceneManager.MoveGameObjectToScene(go, preview);
        var box = go.AddComponent<BoxCollider>();
        box.center = b.center;
        box.size = b.size;
        source[box] = name;
    }

    static GameObject Solid(Scene preview, Transform like)
    {
        var go = new GameObject("solid") { hideFlags = HideFlags.HideAndDontSave };
        SceneManager.MoveGameObjectToScene(go, preview);
        go.transform.SetPositionAndRotation(like.position, like.rotation);
        go.transform.localScale = like.lossyScale;
        return go;
    }

    // ------------------------------------------------------------------ photos (by day, Edit Mode)

    static void Photos(string folder, NightZeroSet set, Bounds dump, Bounds can, StringBuilder report)
    {
        Vector3 mid = dump.center;
        void Shot(string name, Vector3 from, Vector3 at, float fov) => NightWalkSteps.Capture(Path.Combine(folder, name + ".png"), from, at, fov, false);
        Shot("1-back-street-from-the-road", new Vector3(mid.x + 3.4f, 3.4f, dump.max.z + 6.5f), new Vector3(mid.x + .6f, 1f, mid.z), 50f);
        Shot("2-the-dumpster-close", new Vector3(mid.x + 1.3f, 1.9f, dump.max.z + 2.6f), new Vector3(mid.x, 1.2f, mid.z), 55f);
        try
        {
            set.SetLid(set.nearLid, .65f);
            Shot("3-the-near-lid-up-for-the-bag", new Vector3(mid.x + 2.2f, 2.3f, dump.max.z + 2.8f), new Vector3(mid.x + .4f, 1.7f, mid.z), 55f);
            set.SetLid(set.nearLid, 0f);
            set.SetLid(set.farLid, 1f);
            Shot("4-his-lid-open", new Vector3(mid.x + 1.6f, 2.4f, dump.max.z + 3.2f), new Vector3(mid.x - .5f, 2f, mid.z), 55f);
            Shot("5-his-lid-open-from-the-side", new Vector3(dump.min.x - 2.6f, 2.2f, mid.z + 1.6f), new Vector3(mid.x - .5f, 2.1f, mid.z - .2f), 55f);
        }
        finally
        {
            set.SetLid(set.nearLid, 0f);
            set.SetLid(set.farLid, 0f);
        }
        try
        {
            set.cornerGnome.SetActive(true);
            Shot("6-his-corner", new Vector3(can.center.x - .6f, 1.6f, can.max.z + 1.9f), new Vector3(can.center.x, 1f, can.center.z), 45f);
        }
        finally { set.cornerGnome.SetActive(false); }
        Shot("7-the-back-door-inside", new Vector3(5.1f, 1.7f, 14.6f), new Vector3(DoorX, 1.1f, WallInside), 55f);
        Transform reveal = set.revealCamera.transform;
        Shot("8-the-reveal-camera", reveal.position, reveal.position + reveal.forward * 5f, 40f);
        // The bins view from the street (CafeViewMode's house view), around Ace at the near half.
        Vector3 focus = set.binSpot.position + Vector3.up * .3f;
        Quaternion angle = Quaternion.Euler(set.viewPitch, set.viewYaw, 0f);
        Shot("9-the-bins-view", focus - angle * Vector3.forward * set.viewDistance, focus, 40f);
        Shot("10-from-above", new Vector3(mid.x, 15f, mid.z + 4f), new Vector3(mid.x, 0f, mid.z), 55f);
        report.AppendLine();
        report.AppendLine("Photos (by day): 1 Back Street from the road, 2 the dumpster close, 3 the near lid up for the bag, 4-5 his lid open, " +
                          "6 his corner with Barnaby, 7 the back door from inside, 8 the reveal camera's view, 9 the bins view, 10 from above.");
    }

    // ------------------------------------------------------------------ the lab

    [MenuItem(Menu + "Night 0 - Play check (lab, drives itself)")]
    static void PlayCheck()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        try
        {
            CityPackChecks.RequireScene();
            if (Layout().transform.Find(GroupName) == null)
                throw new InvalidOperationException("There are no bins in the scene yet (Bins 1 - Put the back door and the bins behind the café).");
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + e.Message);
            return;
        }
        string path = Path.Combine(Application.persistentDataPath, CafeLab.SaveFileName);
        SaveCheckpointStorage.Write(path, NightOneSteps.DayOneRecap());
        PlayerPrefs.SetInt(CafeLab.PendingKey, 1);
        PlayerPrefs.SetInt(CafeLab.AutopilotKey, 0);
        PlayerPrefs.SetInt(NightZeroCheck.PendingKey, 1);
        PlayerPrefs.DeleteKey(NightOneCheck.PendingKey);
        PlayerPrefs.Save();
        Debug.Log(Tag + "Night 0 play check: it drives itself from Day 1's recap through the bins, the deal, Grace's gnome and the return, " +
                  "to Grace's visit on Day 2 (three or four minutes). Keep the Game view in front and leave the mouse and keyboard alone. " +
                  $"Test save {path}; your playtest save is not used.");
        EditorApplication.EnterPlaymode();
    }

    [MenuItem(Menu + "Night 0 - Play check (lab, drives itself)", true)]
    static bool CanPlay() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // A check request that never became a Play session must not make the next lab session a check.
    [InitializeOnLoadMethod]
    static void ClearStaleCheckRequest()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode && PlayerPrefs.HasKey(NightZeroCheck.PendingKey))
        {
            PlayerPrefs.DeleteKey(NightZeroCheck.PendingKey);
            PlayerPrefs.Save();
        }
    }

    // ------------------------------------------------------------------ helpers

    static GameObject Prefab(string name)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(City + name + ".prefab");
        if (prefab == null) throw new InvalidOperationException($"POLYGON City's {name} isn't in the project ({City}).");
        return prefab;
    }

    static GameObject Layout()
    {
        GameObject layout = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == LayoutRoot);
        if (layout == null) throw new InvalidOperationException($"\"{LayoutRoot}\" is not in the open scene.");
        return layout;
    }

    static Transform Find(Transform root, string name) =>
        root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

    static Transform FindNear(Transform root, string name, Vector3 at, float within) =>
        root.GetComponentsInChildren<Transform>(true).Where(t => t.name == name && Vector3.Distance(t.position, at) <= within)
            .OrderBy(t => Vector3.Distance(t.position, at)).FirstOrDefault();

    static Bounds BoundsOf(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToArray();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static Bounds Union(Bounds a, Bounds b)
    {
        a.Encapsulate(b);
        return a;
    }

    static string LogFolder(string what)
    {
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Night",
            what + "-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture)));
        Directory.CreateDirectory(folder);
        return folder;
    }

    static string PathOf(Transform t)
    {
        var parts = new List<string>();
        for (var x = t; x != null; x = x.parent) parts.Add(x.name);
        parts.Reverse();
        return string.Join("/", parts);
    }

    static string F(float v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    static string V(Vector3 v) => $"({F(v.x)}, {F(v.y)}, {F(v.z)})";

    static T Also<T>(this T thing, Action<T> then)
    {
        then(thing);
        return thing;
    }
}
