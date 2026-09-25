#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// The POLYGON City pass (23 Sept): turns the neighbourhood around Ace's Cafe
// into a city, using the purchased Synty pack. Everything it adds lives under
// "15 - POLYGON City streets" (plus new StreetLife entries for the cars and
// walkers it adds), so "City - Undo" removes it completely.
//
//   Inner blocks   Brick mixed-use rows (shops below, flats above) on the
//                  empty halves of the eight blocks around the cafe, rooftop
//                  plant, signs, a corner donut shop and a parking lot.
//   Hill           POLYGON terraces replace the grey placeholder boxes on the
//                  slope behind the rear street; office towers crown it.
//   Outer ring     Offices and towers beyond the grid, then hazy background
//                  blocks out to ~140 m on a ground plane, so every street
//                  view ends in city instead of empty ground.
//   Street life    Lamps, trees, benches, bins, hydrants, a bus stop, a hot
//                  dog cart, one POLYGON car per through lane, and fourteen
//                  more pedestrians; the six existing street neighbours and
//                  every new walker wear POLYGON looks (PolygonNpcVisual).
//
// Built-in guards: a building never gets more floors than CafeDaylight's sun
// allows without shading the cafe interior (9:30-17:00) or than the orbit
// camera allows; furniture is kept off roads, crossings, the cafe's block,
// every walking route and everything already standing (the baseline map).
// Nothing is saved: check the photos, then save with Ctrl+S.
public static class PolygonCityDressing
{
    const string Menu = "Fixit Fidget/City pack/";
    const string Tag = "[City pack] ";
    public const string RootName = "15 - POLYGON City streets";
    const string CafeRootName = "ACE'S CAFE - layout study 02";
    const string Bld = "Assets/Synty/PolygonCity/Prefabs/Buildings/";
    const string Prop = "Assets/Synty/PolygonCity/Prefabs/Props/";
    const string Env = "Assets/Synty/PolygonCity/Prefabs/Environments/";
    const string Veh = "Assets/Synty/PolygonCity/Prefabs/Vehicles/";
    const string GenBld = "Assets/Synty/PolygonGeneric/Prefabs/Building/";
    const string GenEnv = "Assets/Synty/PolygonGeneric/Prefabs/Environment/";
    const string AtlasFolder = "Assets/Synty/PolygonCity/Materials/Alts/";
    const string Palette = "Assets/Playtests/AcesCafeLayout/Street palette.asset";
    const string CustomerController = "Assets/CustomerAnimator(.controller";
    const string BeachModel = "Assets/ThirdParty/Quaternius_ModularMen/Beach.fbx";
    const string SlopeHomes = "V4 - homes on the distant slope";

    static Transform root;
    static readonly StringBuilder log = new StringBuilder();
    static System.Random rng;
    static CityPackChecks.Raster before;
    static readonly List<(Vector2 centre, Vector2 half, float yaw)> taken = new List<(Vector2, Vector2, float)>();
    static readonly List<(Vector2 a, Vector2 b)> walkways = new List<(Vector2, Vector2)>();
    static int placedProps, skippedProps;

    // Carriageways (x0, z0, x1, z1): nothing but flush manholes may stand here.
    static readonly Rect[] Roads =
    {
        Rect.MinMaxRect(-400f, -10.8f, 400f, -5.2f), Rect.MinMaxRect(-400f, 20.7f, 400f, 26.3f),
        Rect.MinMaxRect(-15f, -400f, -9.4f, 400f), Rect.MinMaxRect(10.1f, -400f, 15.7f, 400f),
    };

    [MenuItem(Menu + "City 1 - Record baseline and before photos")]
    static void BaselineMenu() => Run("Baseline", CityPackChecks.RecordBaseline);

    [MenuItem(Menu + "City 2 - Build the POLYGON city")]
    static void BuildMenu() => Run("Build", Build);

    [MenuItem(Menu + "City 3 - Check the city (clearance, shadows, gameplay, photos)")]
    static void CheckMenu() => Run("Check", CityPackChecks.CheckAll);

    [MenuItem(Menu + "City - Photograph city views")]
    static void PhotoMenu() => Run("Photos", () => CityPackChecks.Photograph("now"));

    [MenuItem(Menu + "City - Undo: remove the POLYGON city")]
    static void RemoveMenu() => Run("Remove", Remove);

    public static Transform FindRoot()
    {
        var cafe = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(r => r.name == CafeRootName);
        return cafe != null ? cafe.transform.Find(RootName) : null;
    }

    // ------------------------------------------------------------------ build

    public static string Build()
    {
        CityPackChecks.RequireScene();
        CityPackChecks.Require(FindRoot() == null, "The POLYGON city is already built. Use City - Undo first to rebuild it.");
        CityPackChecks.Require(AssetDatabase.IsValidFolder("Assets/Synty/PolygonCity"), "Import POLYGON City first.");
        before = CityPackChecks.BeforeRaster();
        CityPackChecks.Require(before != null, "Record the baseline first (City 1): the builder keeps new furniture off everything on that map.");
        var looks = PolygonNpcSetup.LoadLooks(PolygonNpcSetup.AllLooks);
        var lives = CityPackChecks.InScene<StreetLife>();
        CityPackChecks.Require(lives.Length == 1, "Expected one StreetLife.");
        var life = lives[0];
        var cafe = SceneManager.GetActiveScene().GetRootGameObjects().First(r => r.name == CafeRootName);
        log.Clear(); taken.Clear(); walkways.Clear(); placedProps = skippedProps = 0;
        rng = new System.Random(2309);

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Build the POLYGON city");
        root = new GameObject(RootName).transform;
        root.SetParent(cafe.transform, false);
        root.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        try
        {
            foreach (var actor in life.actors)
                if (IsWalker(actor)) AddRoute(actor.waypoints.Where(t => t != null).Select(t => new Vector2(t.position.x, t.position.z)).ToArray(), !actor.openRoute);
            var routes = PlanPedestrianRoutes();

            OuterGround(Group("Outer city ground"));
            CityPavement(Group("City pavement"));
            InnerBlocks(Group("Inner blocks"));
            Hill(Group("Hill"));
            OuterRing(Group("Outer ring and skyline"));
            ParkingLot(Group("Parking lot"));
            StreetFurniture(Group("Street furniture"));

            Undo.RecordObject(life, "Build the POLYGON city");
            string bodies = SwapCarBodies(life);
            string traffic = Traffic(Group("POLYGON traffic"), life);
            string people = Pedestrians(Group("Pedestrians"), life, looks, routes);
            var homes = cafe.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == SlopeHomes);
            if (homes != null && homes.gameObject.activeSelf)
            {
                Undo.RecordObject(homes.gameObject, "Build the POLYGON city");
                homes.gameObject.SetActive(false);
                log.AppendLine("The grey placeholder homes on the distant slope are hidden (not deleted); POLYGON terraces replace them.");
            }
            // Positions, sizes and settings changed on prefab instances must be
            // recorded as overrides, or they would be lost when the scene reloads.
            int recorded = RecordInstanceChanges(root);
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Build the POLYGON city");
            life.RebuildRoutes();
            EditorUtility.SetDirty(life);
            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Undo.CollapseUndoOperations(group);
            int buildings = root.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith("Building - ", StringComparison.Ordinal));
            return "POLYGON city built: " + buildings + " buildings, " + placedProps + " street and rooftop details (" + skippedProps
                + " candidate spots left empty because something was already there); " + recorded + " prefab-instance transforms recorded.\n" + bodies + "\n" + traffic + "\n" + people + "\n" + log
                + "Nothing is saved yet: run City 3, look at the photos, then save the scene.";
        }
        catch
        {
            Undo.RevertAllDownToGroup(group);
            if (root != null) Object.DestroyImmediate(root.gameObject);
            throw;
        }
    }

    public static string Remove()
    {
        CityPackChecks.RequireScene();
        var found = FindRoot();
        CityPackChecks.Require(found != null, "There is no POLYGON city to remove.");
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Remove the POLYGON city");
        int actors = 0, looks = 0, cars = 0;
        var lives = CityPackChecks.InScene<StreetLife>();
        foreach (var life in lives) Undo.RecordObject(life, "Remove the POLYGON city");
        foreach (var car in CityPackChecks.InScene<CityCarBody>())
        {
            foreach (var life in lives)
                foreach (var actor in life.actors)
                    if (actor != null && actor.actor == car.transform)
                    {
                        actor.wheels = car.OriginalWheels;
                        actor.wheelRadius = car.OriginalWheelRadius;
                        actor.vehicleLength = car.OriginalVehicleLength;
                        actor.wheelAxis = car.OriginalWheelAxis;
                    }
            if (car.OriginalBody != null) { Undo.RecordObject(car.OriginalBody, "Remove the POLYGON city"); car.OriginalBody.SetActive(true); }
            if (car.PolygonBody != null) Undo.DestroyObjectImmediate(car.PolygonBody);
            Undo.DestroyObjectImmediate(car);
            cars++;
        }
        foreach (var life in lives)
        {
            actors += life.actors.RemoveAll(a => a != null && a.actor != null && a.actor.IsChildOf(found));
            foreach (var actor in life.actors)
                if (actor != null && actor.actor != null)
                    foreach (var visual in actor.actor.GetComponents<PolygonNpcVisual>()) { Undo.DestroyObjectImmediate(visual); looks++; }
            life.RebuildRoutes();
            EditorUtility.SetDirty(life);
        }
        var homes = found.parent.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == SlopeHomes);
        if (homes != null) { Undo.RecordObject(homes.gameObject, "Remove the POLYGON city"); homes.gameObject.SetActive(true); }
        Undo.DestroyObjectImmediate(found.gameObject);
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return "Removed the POLYGON city, " + actors + " added street actors, " + looks + " street-neighbour looks and " + cars + " POLYGON car bodies (the original cars are back); the slope homes are back. Nothing saved.";
    }

    // ----------------------------------------------------------- inner blocks

    enum Street { Shops, Homes }

    static void InnerBlocks(Transform g)
    {
        // East block, behind the courtyard shops: shops to the front street,
        // flats to the rear street, a service yard between them.
        Building(g, "east block shops", new Vector2(25f, -1.7f), 0f, 180f, 2, 4, 2, Street.Shops, false, true,
            sign: ("SM_Prop_Sign_Barber_01", 0, 1.35f), billboard: (4, 270f));
        Building(g, "east block flats", new Vector2(35f, 17.2f), 0f, 0f, 2, 3, 1, Street.Homes, true, false);
        // West block, behind the bay-window houses.
        Building(g, "west block shops", new Vector2(-37.5f, -1.7f), 0f, 180f, 3, 4, 3, Street.Shops, true, false,
            sign: ("SM_Prop_Sign_Chinese_Noodles_01", 1, 3.35f));
        Building(g, "west block flats", new Vector2(-22.5f, 17.2f), 0f, 0f, 3, 4, 2, Street.Homes, false, true);
        // North corners, facing the rear street.
        Building(g, "north-west corner", new Vector2(-37.5f, 30.2f), 0f, 180f, 3, 6, 1, Street.Shops, true, false,
            sign: ("SM_Prop_Sign_Pizza_01", 1, 3.3f), billboard: (6, 180f), waterTower: true);
        Building(g, "north-east corner", new Vector2(29f, 30.8f), 0f, 180f, 2, 5, 3, Street.Shops, false, true,
            sign: ("SM_Prop_Sign_DeliPizza_01", 0, 4.6f), waterTower: true);
        // South-east, beside the two front shops; the hotel sign reads from the junction.
        Building(g, "south-east hotel", new Vector2(39f, -16.55f), 0f, 0f, 2, 6, 1, Street.Shops, true, false,
            sign: ("SM_Prop_Sign_Hotel_01", 0, 5.4f));
        // South-west corner shops by the parking lot, with the donut on the roof.
        var donutRow = Building(g, "south-west corner shops", new Vector2(-18f, -14.8f), 0f, 0f, 3, 2, 2, Street.Shops, true, true);
        if (donutRow != null)
        {
            var donut = Put(donutRow, Prop + "SM_Prop_LargeSign_Donut_01.prefab", Vector3.zero, 0f);
            donut.transform.localPosition = new Vector3(-2.6f, RoofTop(donutRow) + 1.95f, -2.6f);
            donut.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }
        // Behind the front shops: a terrace facing the open south.
        Building(g, "south block terrace", new Vector2(-8.5f, -27f), 0f, 180f, 3, 5, 2, Street.Homes, true, true);
        // Service yards: bins, skips and pallets between the rows.
        Yard(g, new Vector2(30f, 7.7f), 90f);
        Yard(g, new Vector2(-30f, 7.7f), 90f);
    }

    static void Yard(Transform g, Vector2 centre, float yaw)
    {
        var yard = Group(g, "Service yard");
        TryProp(yard, "SM_Prop_Skip_01", centre + new Vector2(-2.4f, 1.2f), yaw, new Vector2(1.25f, .7f), check: false);
        TryProp(yard, "SM_Prop_Skip_02", centre + new Vector2(-2.4f, -1.4f), yaw, new Vector2(1.25f, .7f), check: false);
        TryProp(yard, "SM_Prop_Pallet_01", centre + new Vector2(2.2f, 1.6f), yaw + 8f, new Vector2(1.1f, 1.1f), check: false);
        TryProp(yard, "SM_Prop_TrashBag_01", centre + new Vector2(-.6f, 2.1f), 30f, new Vector2(.4f, .4f), check: false);
        TryProp(yard, "SM_Prop_TrashBag_02", centre + new Vector2(-.1f, 2.4f), 80f, new Vector2(.4f, .4f), check: false);
        TryProp(yard, "SM_Prop_CardboardBox_03", centre + new Vector2(2.6f, -1.2f), 20f, new Vector2(.5f, .5f), check: false);
        TryProp(yard, "SM_Prop_PowerBox_01", centre + new Vector2(4.4f, 3.5f), yaw, new Vector2(1f, .45f), check: false);
        // A little green between the back walls: a full-size tree and shrubs.
        var green = Group(g, "Yard greenery");
        TryProp(green, "SM_Gen_Env_Tree_0" + (1 + rng.Next(3)), centre + new Vector2(-4.6f, -.4f), (float)(rng.NextDouble() * 360), new Vector2(.6f, .6f), Vector2.up, folder: GenEnv);
        TryProp(green, "SM_Gen_Env_Bush_0" + (1 + rng.Next(4)), centre + new Vector2(.6f, -2.9f), (float)(rng.NextDouble() * 360), new Vector2(1.3f, 1.2f), Vector2.right, folder: GenEnv);
        TryProp(green, "SM_Gen_Env_Shrub_0" + (1 + rng.Next(3)), centre + new Vector2(3.9f, -.6f), (float)(rng.NextDouble() * 360), new Vector2(.7f, .5f), Vector2.up, folder: GenEnv);
    }

    // A street-front building of 5 m bays. The pivot is the front corner of
    // bay 0; bays run along the building's local -X and fronts face local +Z.
    static Transform Building(Transform parent, string name, Vector2 pivot, float baseY, float yaw, int bays, int floors,
        int style, Street street, bool cornerAt0, bool cornerAtEnd,
        (string prefab, int bay, float height)? sign = null, (int floors, float yaw)? billboard = null, bool waterTower = false)
    {
        Quaternion r = Quaternion.Euler(0f, yaw, 0f);
        Vector2 World(float lx, float lz) { var p = r * new Vector3(lx, 0f, lz); return new Vector2(pivot.x + p.x, pivot.y + p.z); }
        var footprint = new[] { World(.25f, .95f), World(-5f * bays - .25f, .95f), World(-5f * bays - .25f, -5.3f), World(.25f, -5.3f) };
        int allowed = floors;
        while (allowed > 2 && (CityPackChecks.ShadowIntrusion(footprint, baseY + 3f * allowed + .9f) != null
            || baseY + 3f * allowed + .9f > CityPackChecks.CameraClearanceHeight(footprint)))
            allowed--;
        if (CityPackChecks.ShadowIntrusion(footprint, baseY + 3f * allowed + .9f) != null)
        {
            log.AppendLine(name + ": skipped, even two floors would shade the cafe.");
            return null;
        }
        if (allowed != floors) log.AppendLine(name + ": " + floors + " floors planned, " + allowed + " built (keeps the cafe out of its shadow and below the orbit camera).");
        floors = allowed;

        var g = new GameObject("Building - " + name).transform;
        g.SetParent(parent, false);
        g.SetPositionAndRotation(new Vector3(pivot.x, baseY, pivot.y), r);
        string s = style.ToString(CultureInfo.InvariantCulture);
        int door = bays / 2;
        for (int f = 0; f < floors; f++)
            for (int i = 0; i < bays; i++)
            {
                bool c0 = i == 0 && cornerAt0, cN = i == bays - 1 && cornerAtEnd;
                string module;
                if (f == 0 && street == Street.Shops)
                    module = c0 || cN ? "SM_Bld_Shop_Corner_0" + (1 + rng.Next(2)) : "SM_Bld_Shop_0" + new[] { 1, 2, 4, 5, 6 }[rng.Next(5)];
                else if (f == 0 && i == door && !c0 && !cN)
                    module = "SM_Bld_Apartment_Door_0" + (style == 2 ? 2 : 1);
                else
                    module = (c0 || cN ? "SM_Bld_Apartment_Corner_0" : "SM_Bld_Apartment_0") + s;
                Module(g, module, i, f * 3f, cN);
            }
        for (int i = 0; i < bays; i++)
        {
            bool c0 = i == 0 && cornerAt0, cN = i == bays - 1 && cornerAtEnd;
            Module(g, (c0 || cN ? "SM_Bld_Apartment_Roof_Corner_0" : "SM_Bld_Apartment_Roof_0") + s, i, floors * 3f, cN);
        }
        float roof = RoofTop(g);
        Rooftop(g, bays, roof, floors >= 5 && waterTower);
        if (billboard.HasValue && floors >= billboard.Value.floors - 2)
        {
            var frame = Put(g, Prop + "SM_Prop_Billboard_Roof_01.prefab", Vector3.zero, 0f);
            frame.transform.localPosition = new Vector3(-2.5f * bays, roof, -2.6f);
            frame.transform.rotation = Quaternion.Euler(0f, billboard.Value.yaw, 0f);
            var art = Put(frame.transform, Prop + "SM_Prop_Billboard_Sign_0" + new[] { 1, 2, 4, 6, 7 }[rng.Next(5)] + ".prefab", Vector3.zero, 0f);
            art.transform.localPosition = Vector3.zero;
            art.transform.localRotation = Quaternion.identity;
        }
        if (sign.HasValue)
        {
            var mounted = Put(g, Prop + sign.Value.prefab + ".prefab", Vector3.zero, 0f);
            mounted.transform.localPosition = new Vector3(-5f * sign.Value.bay - 1.3f, sign.Value.height, .32f);
            mounted.transform.localRotation = Quaternion.identity;
        }
        var bounds = BoundsOf(g.gameObject);
        taken.Add((new Vector2(bounds.center.x, bounds.center.z), new Vector2(bounds.extents.x + .1f, bounds.extents.z + .1f), 0f));
        return g;
    }

    static GameObject Module(Transform building, string module, int bay, float y, bool rotatedCorner)
    {
        var go = Put(building, Bld + module + ".prefab", Vector3.zero, 0f);
        // The far-end corner turns a quarter so its second face looks outward.
        go.transform.localPosition = new Vector3(rotatedCorner ? -5f * (bay + 1) : -5f * bay, y, 0f);
        go.transform.localRotation = Quaternion.Euler(0f, rotatedCorner ? -90f : 0f, 0f);
        return go;
    }

    // Height of the flat roof surface (building-local), from the roof modules' meshes.
    static float RoofTop(Transform building)
    {
        float top = 0f;
        foreach (var filter in building.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null || !(filter.name.Contains("Apartment_Roof") || filter.transform.parent.name.Contains("Apartment_Roof"))) continue;
            var mesh = filter.sharedMesh;
            var b = mesh.bounds;
            foreach (var v in mesh.vertices)
            {
                // Centre third of the module only: parapets sit at the edges.
                if (Mathf.Abs(v.x - b.center.x) > b.extents.x * .34f || Mathf.Abs(v.z - b.center.z) > b.extents.z * .34f) continue;
                top = Mathf.Max(top, building.InverseTransformPoint(filter.transform.TransformPoint(v)).y);
            }
        }
        return top;
    }

    static void Rooftop(Transform building, int bays, float roofY, bool waterTower)
    {
        string[] plant = { "SM_Prop_Roof_Aircon_01", "SM_Prop_Roof_Aircon_02", "SM_Prop_Roof_Aircon_03", "SM_Prop_SatDish_01",
                           "SM_Prop_Vents_Straight_01", "SM_Prop_Skylight_01", "SM_Prop_Roof_Aircon_02" };
        var slots = new List<Vector3>();
        for (int i = 0; i < bays; i++)
            foreach (float x in new[] { -1.6f, -3.6f })
                foreach (float z in new[] { -1.9f, -3.7f })
                    slots.Add(new Vector3(-5f * i + x, roofY, z));
        slots = slots.OrderBy(_ => rng.Next()).ToList();
        if (rng.Next(3) > 0)
        {
            // A stair hut on the far bay; nothing else within its reach.
            Vector3 hut = new Vector3(-5f * bays + 1.8f, roofY, -3.8f);
            var access = Put(building, Bld + "SM_Bld_Roof_Access_01.prefab", Vector3.zero, 0f);
            access.transform.localPosition = hut;
            access.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            slots.RemoveAll(v => Vector3.Distance(v, hut) < 2.3f);
        }
        if (waterTower && slots.Count > 1)
        {
            var tower = Put(building, Bld + "SM_Prop_Water_Tower_01.prefab", Vector3.zero, 0f);
            Vector3 spot = slots[0];
            tower.transform.localPosition = spot;
            tower.transform.localRotation = Quaternion.identity;
            slots.RemoveAll(v => Vector3.Distance(v, spot) < 1.9f);
            placedProps++;
        }
        int count = Mathf.Min(slots.Count, 1 + bays + rng.Next(2));
        for (int k = 0; k < count; k++)
        {
            var go = Put(building, Prop + plant[rng.Next(plant.Length)] + ".prefab", Vector3.zero, 0f);
            go.transform.localPosition = slots[k];
            go.transform.localRotation = Quaternion.Euler(0f, 90f * rng.Next(4), 0f);
            placedProps++;
        }
    }

    // ------------------------------------------------------------------- hill

    static float SlopeY(float z) => z <= 37f ? -.25f : .2f * (z - 37f) - .23f;

    // The pavement laid over the old distant grass (up to where it meets the
    // hill) is the ground for everything built on the flat.
    const float PavementY = -.012f, PavedTop = 38.2f;
    static float GroundY(float z) => z <= PavedTop ? PavementY : SlopeY(z);

    static void Hill(Transform g)
    {
        // Terraces facing the rear street; the two hill streets stay open.
        (float x, int bays, int floors, int style)[] lower = { (-70f, 5, 6, 1), (-37.5f, 3, 5, 2), (-5f, 2, 4, 1), (19.5f, 3, 5, 3), (45f, 5, 6, 2) };
        foreach (var t in lower)
            Building(g, "hill terrace " + t.x.ToString("0", CultureInfo.InvariantCulture), new Vector2(t.x, 41f), SlopeY(41f) - .15f, 180f,
                t.bays, t.floors, t.style, Street.Homes, true, true, waterTower: t.floors >= 5);
        (float x, int bays, int floors, int style)[] upper = { (-70f, 5, 8, 3), (-37.5f, 3, 7, 1), (-5f, 2, 6, 3), (19.5f, 3, 8, 2), (45f, 5, 7, 1) };
        foreach (var t in upper)
            Building(g, "upper terrace " + t.x.ToString("0", CultureInfo.InvariantCulture), new Vector2(t.x, 53f), SlopeY(53f) - .15f, 180f,
                t.bays, t.floors, t.style, Street.Homes, true, true, waterTower: true);
        // Towers crown the hill (sunk into the slope at their downhill edge).
        GlassTower(g, "hill office west", false, 1, new Vector2(-32f, 74f), 0f);
        Tower(g, "hill office", "SM_Bld_OfficeOld_Small_01", new Vector2(0f, 70f), 180f);
        GlassTower(g, "hill tower east", true, 1, new Vector2(42f, 80f), 0f);
        Tower(g, "hill offices far west", GenBld + "SM_Gen_Bld_Background_08", new Vector2(-62f, 70f), 180f);
        Tower(g, "hill offices far east", GenBld + "SM_Gen_Bld_Background_07", new Vector2(62f, 68f), 180f);
    }

    // ------------------------------------------------------ outer ring + band

    static void OuterRing(Transform g)
    {
        // Just beyond the grid: offices facing the city centre.
        Tower(g, "west offices", "SM_Bld_OfficeOld_Large_02", new Vector2(-52f, 8f), 90f);
        Tower(g, "west tower", "SM_Bld_OfficeOld_Small_02", new Vector2(-68f, 8f), 90f);
        Tower(g, "west annex", GenBld + "SM_Gen_Bld_Background_02", new Vector2(-43.5f, 5f), 90f);
        Tower(g, "north-west offices", GenBld + "SM_Gen_Bld_Background_07", new Vector2(-50f, 35.5f), 90f);
        Tower(g, "north-west tower", GenBld + "SM_Gen_Bld_Background_08", new Vector2(-68f, 36f), 90f);
        GlassTower(g, "south-west glass tower", true, 3, new Vector2(-66f, -40f), 90f);
        Tower(g, "south-west offices", GenBld + "SM_Gen_Bld_Background_05", new Vector2(-43.5f, -20f), 90f);
        Tower(g, "east offices", GenBld + "SM_Gen_Bld_Background_07", new Vector2(50f, 8f), 270f);
        GlassTower(g, "east tower", false, 3, new Vector2(68f, 6f), 270f);
        Tower(g, "north-east offices", GenBld + "SM_Gen_Bld_Background_09", new Vector2(50f, 36f), 270f);
        Tower(g, "north-east tower", "SM_Bld_OfficeOld_Large_01", new Vector2(70f, 36f), 270f);
        GlassTower(g, "south-east glass tower", true, 1, new Vector2(62f, -32f), 270f);
        Tower(g, "south-east annex", GenBld + "SM_Gen_Bld_Background_04", new Vector2(43f, -20f), 270f);
        GlassTower(g, "south offices west", false, 1, new Vector2(-28f, -50f), 0f);
        Tower(g, "south offices", GenBld + "SM_Gen_Bld_Background_08", new Vector2(0f, -48f), 0f);
        Tower(g, "south offices east", "SM_Bld_OfficeOld_Large_02", new Vector2(28f, -50f), 0f);
        Tower(g, "south-west block", GenBld + "SM_Gen_Bld_Background_06", new Vector2(-45f, -45f), 0f);
        Tower(g, "south-east block", GenBld + "SM_Gen_Bld_Background_10", new Vector2(45f, -45f), 0f);
        Tower(g, "south-east offices", "SM_Bld_OfficeOld_Small_01", new Vector2(31f, -28.5f), 0f);

        // The hazy band, 80-140 m out: the streets run on into more city.
        var band = Group(g, "Hazy skyline band");
        int n = 0;
        foreach (float x in new[] { -86f, -106f, -128f, 86f, 106f, 128f })
            for (float z = -64f; z <= 84f; z += 16f + (float)rng.NextDouble() * 6f)
            {
                if (z > -15f && z < -1f || z > 16.5f && z < 30.5f) continue;   // cross streets
                Tower(band, "skyline " + (++n), GenBld + "SM_Gen_Bld_Background_" + (1 + rng.Next(11)).ToString("00", CultureInfo.InvariantCulture),
                    new Vector2(x + (float)rng.NextDouble() * 6f - 3f, z), z < PavedTop - 6f ? PavementY - .03f : -.28f, x < 0 ? 90f : 270f);
            }
        foreach (float z in new[] { -88f, -108f, -130f })
            for (float x = -120f; x <= 120f; x += 15f + (float)rng.NextDouble() * 6f)
            {
                if (x > -19.5f && x < -5f || x > 5.5f && x < 20.5f) continue;   // south streets
                Tower(band, "skyline " + (++n), GenBld + "SM_Gen_Bld_Background_" + (1 + rng.Next(11)).ToString("00", CultureInfo.InvariantCulture),
                    new Vector2(x, z + (float)rng.NextDouble() * 6f - 3f), PavementY - .03f, 0f);
            }
    }

    // A single-prefab building placed by its footprint centre; skipped when
    // it would shade the cafe or meet the orbit camera.
    static GameObject Tower(Transform parent, string label, string prefab, Vector2 centre, float yaw) =>
        Tower(parent, label, prefab.StartsWith("Assets/", StringComparison.Ordinal) ? prefab : Bld + prefab, centre, float.NaN, yaw);

    static GameObject Tower(Transform parent, string label, string path, Vector2 centre, float baseY, float yaw)
    {
        if (!path.EndsWith(".prefab", StringComparison.Ordinal)) path += ".prefab";
        var go = Put(parent, path, Vector3.zero, yaw);
        go.name = "Building - " + label;
        return PlaceTower(go, label, centre, baseY);
    }

    // The glass offices come as open shells (no roof, no lobby): stack the
    // square ones on their lobby and cap both kinds with their roof, so the
    // overhead camera never looks into an empty tube.
    static GameObject GlassTower(Transform parent, string label, bool round, int variant, Vector2 centre, float yaw)
    {
        var g = new GameObject("Building - " + label).transform;
        g.SetParent(parent, false);
        g.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, yaw, 0f));
        string family = round ? "SM_Bld_OfficeRound" : "SM_Bld_OfficeSquare";
        var parts = round
            ? new[] { (family + "_0" + variant, 0f), (family + "_Roof_01", 38.356f) }
            : new[] { (family + "_Base_01", 0f), (family + "_0" + variant, 3.526f), (family + "_Roof_01", 33.526f) };
        foreach (var part in parts)
        {
            var go = Put(g, Bld + part.Item1 + ".prefab", Vector3.zero, 0f);
            go.transform.localPosition = new Vector3(0f, part.Item2, 0f);
            go.transform.localRotation = Quaternion.identity;
        }
        return PlaceTower(g.gameObject, label, centre, float.NaN);
    }

    // Moves a building so its footprint centre is at 'centre' and its base on
    // the ground (the slope's downhill edge when baseY is NaN); removes it if
    // it would shade the cafe or meet the orbit camera.
    static GameObject PlaceTower(GameObject go, string label, Vector2 centre, float baseY)
    {
        var b = BoundsOf(go);
        float front = b.min.z - b.center.z + centre.y;
        float ground = float.IsNaN(baseY) ? GroundY(front) - (front <= PavedTop ? .03f : .12f) : baseY;
        go.transform.position += new Vector3(centre.x - b.center.x, ground - b.min.y, centre.y - b.center.z);
        b = BoundsOf(go);
        var footprint = CityPackChecks.Footprint(b);
        string shade = CityPackChecks.ShadowIntrusion(footprint, b.max.y);
        float ceiling = CityPackChecks.CameraClearanceHeight(footprint);
        if (shade != null || b.max.y > ceiling)
        {
            log.AppendLine(label + ": not placed (" + (shade != null ? "would shade the cafe at " + shade : "the orbit camera comes down to " + ceiling.ToString("F0") + " m there") + ").");
            Object.DestroyImmediate(go);
            return null;
        }
        taken.Add((new Vector2(b.center.x, b.center.z), new Vector2(b.extents.x + .1f, b.extents.z + .1f), 0f));
        return go;
    }

    static void OuterGround(Transform g)
    {
        var material = AssetDatabase.LoadAllAssetsAtPath(Palette).OfType<Material>().FirstOrDefault(m => m.name == "Distant ground");
        CityPackChecks.Require(material != null, "Missing the street palette's Distant ground material.");
        var plane = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(plane.GetComponent<Collider>());
        plane.name = "Outer city ground (under the existing ground, reaches the hazy band)";
        plane.transform.SetParent(g, false);
        plane.transform.localPosition = new Vector3(0f, -.33f, 0f);
        plane.transform.localScale = new Vector3(320f, .1f, 300f);
        plane.GetComponent<MeshRenderer>().sharedMaterial = material;
        GameObjectUtility.SetStaticEditorFlags(plane, StaticEditorFlags.BatchingStatic);
    }

    // Paved ground over the old distant grass between the new blocks, so the
    // outer buildings stand on city ground, not on a lawn. Roads (and the
    // inner blocks, already paved) are left exactly as they are. World-space
    // UVs like the existing street geometry, with its own sidewalk material.
    static void CityPavement(Transform g)
    {
        var material = AssetDatabase.LoadAllAssetsAtPath(Palette).OfType<Material>().FirstOrDefault(m => m.name == "Sidewalk stone");
        CityPackChecks.Require(material != null, "Missing the street palette's Sidewalk stone material.");
        var rects = new[]
        {
            Rect.MinMaxRect(-160f, -150f, -35f, -10.8f), Rect.MinMaxRect(-35f, -150f, -15f, -32.5f),
            Rect.MinMaxRect(-160f, -5.2f, -35f, 20.7f), Rect.MinMaxRect(-160f, 26.3f, -35f, PavedTop),
            Rect.MinMaxRect(-9.4f, -150f, 10.1f, -32.5f),
            Rect.MinMaxRect(37.5f, -150f, 160f, -10.8f), Rect.MinMaxRect(15.7f, -150f, 37.5f, -32.5f),
            Rect.MinMaxRect(37.5f, -5.2f, 160f, 20.7f), Rect.MinMaxRect(37.5f, 26.3f, 160f, PavedTop),
        };
        var go = new GameObject("Paved city ground (over the distant grass)");
        go.transform.SetParent(g, false);
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();
        foreach (var r in rects)
        {
            int first = vertices.Count;
            foreach (var c in new[] { new Vector2(r.xMin, r.yMin), new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax), new Vector2(r.xMax, r.yMin) })
            {
                vertices.Add(go.transform.InverseTransformPoint(new Vector3(c.x, PavementY, c.y)));
                uvs.Add(c);
            }
            triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
        }
        var mesh = new Mesh { name = "City pavement" };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
    }

    // ---------------------------------------------------------- parking lot

    static void ParkingLot(Transform g)
    {
        // Behind the corner shops, reached from the west avenue.
        for (int i = 0; i < 2; i++)
        {
            var stalls = Put(g, Env + "SM_Env_Road_ParkingLines_01.prefab", new Vector3(-18f - 10f * i, -.005f, -27.5f), 0f);
            stalls.name = "Parking stalls";
            for (int k = 0; k < 2; k++)
                Put(g, Env + "SM_Env_Road_Bare_01.prefab", new Vector3(-18f - 10f * i - 5f * k, -.005f, -22.5f), 0f).name = "Parking aisle";
        }
        string[] cars = { "SM_Veh_Car_Sedan_01", "SM_Veh_Car_Small_01", "SM_Veh_Car_Taxi_01", "SM_Veh_Car_Van_01", "SM_Veh_Car_Medium_01" };
        float[] xs = { -35.8f, -30.4f, -26.3f, -23.4f, -19.2f };
        for (int i = 0; i < cars.Length; i++)
        {
            var car = Put(g, Veh + cars[i] + ".prefab", Vector3.zero, 180f + (float)(rng.NextDouble() * 6 - 3));
            if (!cars[i].Contains("Taxi")) Recolor(car, CarAtlas());
            var b = BoundsOf(car);
            car.transform.position += new Vector3(xs[i] - b.center.x, -.01f - b.min.y, -29.6f - b.center.z);
            car.name = "Parked " + cars[i].Replace("SM_Veh_Car_", "").Replace("_01", "").ToLowerInvariant();
            placedProps++;
        }
        taken.Add((new Vector2(-27.5f, -26.8f), new Vector2(10f, 5.4f), 0f));
        TryProp(g, "SM_Prop_Sign_Parking_01", new Vector2(-18.6f, -20.9f), 90f, new Vector2(.25f, .25f));
        TryProp(g, "SM_Prop_Skip_01", new Vector2(-36.6f, -20.4f), 90f, new Vector2(.7f, 1.25f));
    }

    // --------------------------------------------------------- street details

    static void StreetFurniture(Transform g)
    {
        // Bus stop outside the two front shops, facing the front street.
        TryProp(g, "SM_Prop_BusStop_01", new Vector2(24.6f, -11.95f), 0f, new Vector2(2.3f, .8f));
        TryProp(g, "SM_Prop_Sign_Bustop_01", new Vector2(28.4f, -11.35f), 0f, new Vector2(.3f, .1f));
        TryProp(g, "SM_Prop_Trashbin_01", new Vector2(21.4f, -11.45f), 0f, new Vector2(.35f, .35f));
        TryProp(g, "SM_Prop_Newspaper_02", new Vector2(20.4f, -11.5f), 0f, new Vector2(.32f, .26f));
        // A hot dog cart on the corner-shop sidewalk.
        TryProp(g, "SM_Prop_HotdogStand_01", new Vector2(-27f, -11.95f), 180f, new Vector2(1.2f, 1f));

        // Curb lines: (start, end, unit vector from the curb into the sidewalk).
        var curbs = new List<(Vector2 a, Vector2 b, Vector2 inward, string name)>
        {
            (new Vector2(-37.5f, -10.8f), new Vector2(-16f, -10.8f), new Vector2(0f, -1f), "front street, south-west"),
            (new Vector2(-8.8f, -10.8f), new Vector2(9.5f, -10.8f), new Vector2(0f, -1f), "front street, south"),
            (new Vector2(16.4f, -10.8f), new Vector2(38.5f, -10.8f), new Vector2(0f, -1f), "front street, south-east"),
            (new Vector2(-37.5f, 26.3f), new Vector2(-16f, 26.3f), new Vector2(0f, 1f), "rear street, north-west"),
            (new Vector2(-8.8f, 26.3f), new Vector2(9.5f, 26.3f), new Vector2(0f, 1f), "rear street, north"),
            (new Vector2(16.4f, 26.3f), new Vector2(38.5f, 26.3f), new Vector2(0f, 1f), "rear street, north-east"),
            (new Vector2(-15f, -33.5f), new Vector2(-15f, -11.6f), new Vector2(-1f, 0f), "west avenue, south"),
            (new Vector2(-15f, 26.9f), new Vector2(-15f, 36.5f), new Vector2(-1f, 0f), "west avenue, north"),
            (new Vector2(15.7f, -33.5f), new Vector2(15.7f, -11.6f), new Vector2(1f, 0f), "east street, south"),
            (new Vector2(15.7f, 26.9f), new Vector2(15.7f, 36.5f), new Vector2(1f, 0f), "east street, north"),
        };
        foreach (var c in curbs) Furnish(Group(g, "Curb - " + c.name), c.a, c.b, c.inward, .62f, 17f);
        // The streets beyond the grid: lamps and trees along the back of the sidewalks.
        var outer = new List<(Vector2 a, Vector2 b, Vector2 inward)>
        {
            (new Vector2(-75f, -10.8f), new Vector2(-39f, -10.8f), new Vector2(0f, -1f)), (new Vector2(39f, -10.8f), new Vector2(75f, -10.8f), new Vector2(0f, -1f)),
            (new Vector2(-75f, -5.2f), new Vector2(-39f, -5.2f), new Vector2(0f, 1f)), (new Vector2(39f, -5.2f), new Vector2(75f, -5.2f), new Vector2(0f, 1f)),
            (new Vector2(-75f, 20.7f), new Vector2(-39f, 20.7f), new Vector2(0f, -1f)), (new Vector2(39f, 20.7f), new Vector2(75f, 20.7f), new Vector2(0f, -1f)),
            (new Vector2(-75f, 26.3f), new Vector2(-39f, 26.3f), new Vector2(0f, 1f)), (new Vector2(39f, 26.3f), new Vector2(75f, 26.3f), new Vector2(0f, 1f)),
            (new Vector2(-15f, -70f), new Vector2(-15f, -36f), new Vector2(-1f, 0f)), (new Vector2(-9.4f, -70f), new Vector2(-9.4f, -36f), new Vector2(1f, 0f)),
            (new Vector2(10.1f, -70f), new Vector2(10.1f, -36f), new Vector2(-1f, 0f)), (new Vector2(15.7f, -70f), new Vector2(15.7f, -36f), new Vector2(1f, 0f)),
        };
        var avenue = Group(g, "Beyond the grid");
        foreach (var o in outer) Furnish(avenue, o.a, o.b, o.inward, 1.95f, 20f, outerStreet: true);
        // Manholes in the lanes (flush with the asphalt; cars drive over them).
        var manholes = Group(g, "Manholes");
        foreach (var p in new[] { new Vector2(-24f, -9.3f), new Vector2(31f, -6.7f), new Vector2(-30f, 24.9f), new Vector2(26f, 22.1f),
                                  new Vector2(-13.7f, -24f), new Vector2(14.2f, -26f), new Vector2(-10.9f, 31f), new Vector2(11.6f, 33f) })
            Put(manholes, Prop + "SM_Prop_Manhole_0" + (1 + rng.Next(2)) + ".prefab", new Vector3(p.x, -.178f, p.y), 90f * rng.Next(4));
    }

    // Lamps every spacing metres; trees, benches, bins, hydrants, mailboxes
    // and parking meters between them. Each spot is tried at small shifts
    // along the curb before it is left empty.
    static void Furnish(Transform g, Vector2 a, Vector2 b, Vector2 inward, float offset, float spacing, bool outerStreet = false)
    {
        Vector2 along = (b - a).normalized;
        float length = Vector2.Distance(a, b);
        float faceRoad = Mathf.Atan2(-inward.x, -inward.y) * Mathf.Rad2Deg;   // yaw whose +Z points at the road
        int slot = 0;
        for (float d = 2.2f; d <= length - 1.8f; d += spacing / 4f, slot++)
        {
            Vector2 p = a + along * d + inward * offset;
            switch (slot % 4)
            {
                case 0:
                    TryProp(g, "SM_Prop_LightPole_Base_01", p, faceRoad, new Vector2(.25f, .25f), along);
                    break;
                case 2:
                    // Only the two tall-trunk street trees; scaled up, their canopies
                    // start above 2.8 m, clear of heads and of every car roof.
                    var tree = TryProp(g, "SM_Env_Tree_0" + (1 + rng.Next(2)), p + inward * (outerStreet ? 0f : .15f), (float)(rng.NextDouble() * 360), new Vector2(.35f, .35f), along, environment: true);
                    if (tree != null) tree.transform.localScale = Vector3.one * (1.4f + (float)rng.NextDouble() * .25f);
                    break;
                default:
                    if (outerStreet) break;
                    string[] small = { "SM_Prop_Trashbin_01", "SM_Prop_Hydrant_01", "SM_Prop_ParkBench_01", "SM_Prop_Mailbox_01",
                                       "SM_Prop_ParkingMeter_01", "SM_Prop_Newspaper_02", "SM_Prop_Planter_01", "SM_Prop_ParkingMeter_01" };
                    string item = small[rng.Next(small.Length)];
                    bool bench = item.Contains("Bench") || item.Contains("Planter");
                    TryProp(g, item, p + inward * (bench ? .35f : 0f), bench ? faceRoad + 180f : faceRoad,
                        bench ? new Vector2(1.1f, .4f) : new Vector2(.35f, .35f), along);
                    break;
            }
        }
    }

    // Places a prop only where the baseline map, the new buildings and props,
    // the roads and every walking route leave room.
    static GameObject TryProp(Transform parent, string prefab, Vector2 at, float yaw, Vector2 half, Vector2? slide = null,
        bool environment = false, bool check = true, string folder = null)
    {
        string path = (folder ?? (environment ? Env : Prop)) + prefab + ".prefab";
        foreach (float shift in new[] { 0f, .5f, -.5f, 1f, -1f, 1.6f, -1.6f })
        {
            Vector2 p = at + (slide ?? Vector2.zero) * shift;
            if (check && !Free(p, half, yaw)) { if (slide == null) break; continue; }
            var go = Put(parent, path, new Vector3(p.x, StreetY(p), p.y), yaw);
            taken.Add((p, half, yaw));
            placedProps++;
            return go;
        }
        skippedProps++;
        return null;
    }

    static bool Free(Vector2 p, Vector2 half, float yaw)
    {
        Quaternion r = Quaternion.Euler(0f, yaw, 0f);
        var corners = new[] { new Vector3(-half.x, 0, -half.y), new Vector3(half.x, 0, -half.y), new Vector3(half.x, 0, half.y), new Vector3(-half.x, 0, half.y) }
            .Select(c => { var w = r * c; return new Vector2(p.x + w.x, p.y + w.z); }).ToArray();
        float xMin = corners.Min(c => c.x), xMax = corners.Max(c => c.x), zMin = corners.Min(c => c.y), zMax = corners.Max(c => c.y);
        var box = Rect.MinMaxRect(xMin, zMin, xMax, zMax);
        foreach (var road in Roads) if (road.Overlaps(box)) return false;
        if (CityPackChecks.CafeIsland.Overlaps(Expand(box, .3f))) return false;
        if (before.BoxBlocked(p, half + new Vector2(.25f, .25f), yaw)) return false;
        foreach (var t in taken)
        {
            var other = Rect.MinMaxRect(t.centre.x - t.half.x, t.centre.y - t.half.y, t.centre.x + t.half.x, t.centre.y + t.half.y);
            if (Mathf.Abs(t.yaw % 180f) > 1f && Mathf.Abs(t.yaw % 180f) < 179f)
                other = Rect.MinMaxRect(t.centre.x - t.half.y, t.centre.y - t.half.x, t.centre.x + t.half.y, t.centre.y + t.half.x);
            if (Expand(other, .2f).Overlaps(box)) return false;
        }
        // A walker's shoulders are 0.35 m from the route line; keep 0.15 m more.
        foreach (var w in walkways)
            if (SegmentToBox(w.a, w.b, p, half, yaw) < .5f) return false;
        return true;
    }

    static float StreetY(Vector2 p) => -.02f;

    static float SegmentToBox(Vector2 a, Vector2 b, Vector2 centre, Vector2 half, float yaw)
    {
        Quaternion inverse = Quaternion.Euler(0f, -yaw, 0f);
        float best = float.MaxValue;
        int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / .2f));
        for (int i = 0; i <= steps; i++)
        {
            Vector2 q = Vector2.Lerp(a, b, i / (float)steps) - centre;
            Vector3 local = inverse * new Vector3(q.x, 0f, q.y);
            float dx = Mathf.Max(0f, Mathf.Abs(local.x) - half.x), dz = Mathf.Max(0f, Mathf.Abs(local.z) - half.y);
            best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dz * dz));
        }
        return best;
    }

    // ---------------------------------------------------------------- traffic

    static string Traffic(Transform g, StreetLife life)
    {
        string[] models = { "SM_Veh_Car_Taxi_01", "SM_Veh_Car_Police_01", "SM_Veh_Car_Sedan_01", "SM_Veh_Car_Small_01",
                            "SM_Veh_Car_Van_01", "SM_Veh_Car_Medium_01", "SM_Veh_Car_Muscle_01", "SM_Veh_Car_Ambo_01" };
        var lanes = life.actors.Where(a => a != null && a.actor != null && a.wheels != null && a.wheels.Length > 0 && a.openRoute
            && !string.IsNullOrEmpty(a.trafficGroup)).GroupBy(a => a.trafficGroup).OrderBy(l => l.Key, StringComparer.Ordinal).ToArray();
        int added = 0;
        foreach (var lane in lanes)
        {
            var template = lane.First();
            var car = Put(g, Veh + models[added % models.Length] + ".prefab", Vector3.zero, 0f, fixedInPlace: false);
            if (!Special(models[added % models.Length])) Recolor(car, CarAtlas());
            car.name = "Passing POLYGON " + models[added % models.Length].Replace("SM_Veh_Car_", "").Replace("_01", "").ToLowerInvariant() + " - " + lane.Key;
            var wheels = car.GetComponentsInChildren<Transform>(true).Where(t => t.name.Contains("_Wheel_")).ToArray();
            float radius = wheels.Length > 0 ? wheels.Select(w => w.GetComponent<Renderer>()).Where(r => r != null).Select(r => r.bounds.extents.y).DefaultIfEmpty(.36f).Max() : .36f;
            var b = BoundsOf(car);
            // The widest gap between the lane's existing cars.
            var phases = lane.Select(a => a.startPhase).OrderBy(p => p).ToList();
            float best = 0f, phase = .5f;
            for (int i = 0; i < phases.Count; i++)
            {
                float next = i + 1 < phases.Count ? phases[i + 1] : phases[0] + 1f;
                if (next - phases[i] > best) { best = next - phases[i]; phase = Mathf.Repeat(phases[i] + best * .5f, 1f); }
            }
            PlaceOnRoute(car.transform, template.waypoints, true, phase);
            life.actors.Add(new StreetLife.Actor
            {
                actor = car.transform, waypoints = template.waypoints, openRoute = true,
                respawnDelay = template.respawnDelay + 1.5f, respawnDelayVariation = template.respawnDelayVariation,
                speed = template.speed * (.94f + (float)rng.NextDouble() * .1f), startPhase = phase,
                trafficGroup = template.trafficGroup, vehicleLength = Mathf.Max(4.6f, b.size.z + .3f), minimumGap = template.minimumGap,
                stopWaypoints = template.stopWaypoints, stopDuration = template.stopDuration, junctionSignalPhase = template.junctionSignalPhase,
                smoothRoute = template.smoothRoute, alignToSlope = template.alignToSlope, turnSpeed = template.turnSpeed,
                wheels = wheels, wheelRadius = radius, wheelAxis = Vector3.right,
            });
            added++;
        }
        return "Traffic: " + added + " POLYGON cars joined the " + lanes.Length + " through lanes (taxi, police car, ambulance, van, sedans...).";
    }

    // ------------------------------------------------------------ car bodies

    // Kenney car kind (from "Passing <kind> <lane>-<n>") -> POLYGON model.
    static readonly Dictionary<string, string> CarModels = new Dictionary<string, string>
    {
        { "sedan", "SM_Veh_Car_Sedan_01" }, { "hatchback-sports", "SM_Veh_Car_Small_01" }, { "taxi", "SM_Veh_Car_Taxi_01" },
        { "suv-luxury", "SM_Veh_Car_Medium_01" }, { "van", "SM_Veh_Car_Van_01" }, { "sedan-sports", "SM_Veh_Car_Muscle_01" },
        { "delivery", "SM_Veh_Car_Van_01" }, { "suv", "SM_Veh_Car_Medium_01" },
    };

    // Every existing traffic car keeps its StreetLife entry (route, lane, stops,
    // spacing); only its body changes. The Kenney body is switched off, not
    // deleted, and CityCarBody switches it back on if the purchased art is
    // missing (a clone of the public repository).
    static string SwapCarBodies(StreetLife life)
    {
        int swapped = 0;
        var skipped = new List<string>();
        foreach (var actor in life.actors)
        {
            if (actor == null || actor.actor == null || actor.wheels == null || actor.wheels.Length == 0) continue;
            var t = actor.actor;
            if (t.IsChildOf(root) || t.GetComponent<CityCarBody>() != null) continue;
            var bodies = t.Cast<Transform>().Where(c => c.gameObject.activeSelf && c.GetComponentInChildren<Renderer>(true) != null).ToArray();
            if (bodies.Length != 1) { skipped.Add(t.name); continue; }
            string[] words = t.name.Split(' ');
            string kind = words.Length >= 3 ? words[1] : "";
            string model = CarModels.TryGetValue(kind, out var m) ? m : "SM_Veh_Car_Sedan_01";
            var body = Put(t, Veh + model + ".prefab", t.position, t.eulerAngles.y, fixedInPlace: false);
            Undo.RegisterCreatedObjectUndo(body, "Build the POLYGON city");
            body.name = "POLYGON body - " + model.Replace("SM_Veh_Car_", "").Replace("_01", "").ToLowerInvariant();
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;
            body.transform.localScale = Vector3.one / Mathf.Max(1e-4f, t.lossyScale.x);
            if (!Special(model)) Recolor(body, CarAtlas());
            var wheels = body.GetComponentsInChildren<Transform>(true).Where(w => w.name.Contains("_Wheel_")).ToArray();
            float radius = wheels.Select(w => w.GetComponent<Renderer>()).Where(r => r != null).Select(r => r.bounds.extents.y).DefaultIfEmpty(.36f).Max();
            float length = LocalBounds(body.transform, t).size.z * t.lossyScale.z;
            var marker = Undo.AddComponent<CityCarBody>(t.gameObject);
            marker.Configure(body, bodies[0].gameObject, actor.wheels, actor.wheelRadius, actor.vehicleLength, actor.wheelAxis);
            Undo.RecordObject(bodies[0].gameObject, "Build the POLYGON city");
            bodies[0].gameObject.SetActive(false);
            actor.wheels = wheels;
            actor.wheelRadius = radius;
            actor.wheelAxis = Vector3.right;
            actor.vehicleLength = Mathf.Max(actor.vehicleLength, length + .2f);
            RecordInstanceChanges(body.transform);
            swapped++;
        }
        return "Car bodies: " + swapped + " Kenney traffic cars now drive as POLYGON cars (same routes, lanes and signals)"
            + (skipped.Count > 0 ? "; left as they were: " + string.Join(", ", skipped) : "") + ".";
    }

    static bool Special(string model) => model.Contains("Taxi") || model.Contains("Police") || model.Contains("Ambo");

    // The pack's alternative colour atlases recolour a whole car at once.
    static readonly string[] CarAtlases = { "PolygonCity_01_A", "PolygonCity_01_B", "PolygonCity_01_C", "PolygonCity_02_A",
                                            "PolygonCity_02_B", "PolygonCity_03_A", "PolygonCity_04_A", "PolygonCity_04_C" };

    static Material CarAtlas() => AssetDatabase.LoadAssetAtPath<Material>(AtlasFolder + CarAtlases[rng.Next(CarAtlases.Length)] + ".mat");

    static void Recolor(GameObject go, Material variant)
    {
        if (go == null || variant == null) return;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] != null && mats[i].name == "PolygonCity_01_A" && mats[i] != variant) { mats[i] = variant; changed = true; }
            if (!changed) continue;
            r.sharedMaterials = mats;
            Record(r);
        }
    }

    // Mesh bounds of everything under 'go', in 'space' (local, rotation-free).
    static Bounds LocalBounds(Transform go, Transform space)
    {
        bool any = false;
        var result = new Bounds();
        foreach (var filter in go.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            Bounds b = filter.sharedMesh.bounds;
            Matrix4x4 m = space.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                if (!any) { result = new Bounds(corner, Vector3.zero); any = true; }
                else result.Encapsulate(corner);
            }
        }
        return result;
    }

    // ----------------------------------------------------------- pedestrians

    // Sidewalk lanes: out along a -> b, back along the parallel line shifted by
    // 'back'. Lines were chosen between the curb furniture and shopfronts.
    static readonly (Vector2 a, Vector2 b, Vector2 back, string name)[] PedestrianLanes =
    {
        (new Vector2(-19.5f, -12.6f), new Vector2(-35f, -12.6f), new Vector2(0f, -.55f), "front street west"),
        (new Vector2(37.5f, -13.6f), new Vector2(17.5f, -13.6f), new Vector2(0f, -.55f), "bus stop"),
        (new Vector2(37f, -14.95f), new Vector2(18f, -14.95f), new Vector2(0f, -.55f), "bus stop, second"),
        (new Vector2(-36f, 27.95f), new Vector2(-22.8f, 27.95f), new Vector2(0f, .55f), "rear street west"),
        (new Vector2(17.5f, 28.1f), new Vector2(37.5f, 28.1f), new Vector2(0f, .55f), "rear street east"),
        (new Vector2(-16.45f, -33f), new Vector2(-16.45f, -14.8f), new Vector2(-.55f, 0f), "west avenue south"),
        (new Vector2(9f, -12.35f), new Vector2(-8f, -12.35f), new Vector2(0f, -.55f), "front shops"),
        (new Vector2(-8f, 29.45f), new Vector2(9f, 29.45f), new Vector2(0f, .55f), "rear shops"),
        (new Vector2(-41f, -11.35f), new Vector2(-72f, -11.35f), new Vector2(0f, -.4f), "front street far west"),
        (new Vector2(41f, -4.6f), new Vector2(72f, -4.6f), new Vector2(0f, .4f), "front street far east"),
        (new Vector2(41f, 26.9f), new Vector2(72f, 26.9f), new Vector2(0f, .4f), "rear street far east"),
        (new Vector2(-41f, 20.1f), new Vector2(-72f, 20.1f), new Vector2(0f, -.4f), "rear street far west"),
        (new Vector2(-15.6f, -68f), new Vector2(-15.6f, -37f), new Vector2(-.4f, 0f), "south avenue west"),
        (new Vector2(16.3f, -37f), new Vector2(16.3f, -68f), new Vector2(.4f, 0f), "south avenue east"),
    };

    static List<(Vector2[] points, string name)> PlanPedestrianRoutes()
    {
        var result = new List<(Vector2[], string)>();
        foreach (var lane in PedestrianLanes)
        {
            Vector2 back = lane.back, across = back.normalized;
            Vector2[] points = null;
            foreach (float nudge in new[] { 0f, .25f, -.25f })
            {
                Vector2 n = across * nudge;
                var candidate = new[] { lane.a + n, lane.b + n, lane.b + back + n, lane.a + back + n };
                bool clear = true;
                for (int i = 0; i < 4 && clear; i++)
                    if (before.SegmentBlocked(candidate[i], candidate[(i + 1) % 4], .38f)) clear = false;
                if (clear) { points = candidate; break; }
            }
            if (points == null) { log.AppendLine("Pedestrian lane '" + lane.name + "' left out: something already stands on it."); continue; }
            AddRoute(points, true);
            result.Add((points, lane.name));
        }
        return result;
    }

    static string Pedestrians(Transform g, StreetLife life, GameObject[] looks, List<(Vector2[] points, string name)> routes)
    {
        var template = life.actors.FirstOrDefault(IsWalker);
        CityPackChecks.Require(template != null, "No existing street walker to copy settings from.");
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CustomerController);
        var beach = AssetDatabase.LoadAssetAtPath<GameObject>(BeachModel);
        CityPackChecks.Require(controller != null && beach != null, "Missing the customer animator or the Quaternius Beach model.");
        float scale = template.actor.lossyScale.x;
        var names = PolygonNpcSetup.AllLooks.ToList();
        // The six existing street neighbours get fixed looks first.
        string[] existingLooks = { "Character_BusinessWoman", "SM_Gen_Chr_Street_Male_02", "Character_Female_Coat",
                                   "SM_Gen_Chr_Business_Male_01", "SM_Gen_Chr_Street_Female_03", "Character_Male_Hoodie" };
        int existing = 0;
        foreach (var actor in life.actors.Where(IsWalker).ToArray())
        {
            if (actor.actor.GetComponent<PolygonNpcVisual>() != null) continue;
            var visual = Undo.AddComponent<PolygonNpcVisual>(actor.actor.gameObject);
            visual.Configure(looks, 0f, names.IndexOf(existingLooks[existing % existingLooks.Length]));
            existing++;
        }
        var remaining = names.Except(existingLooks).ToList();
        int added = 0;
        var routeRoot = Group(g, "Routes");
        foreach (var route in routes)
        {
            string look = remaining[added % remaining.Count];
            var body = (GameObject)PrefabUtility.InstantiatePrefab(beach, g);
            body.name = "City pedestrian - " + route.name;
            body.transform.localScale = Vector3.one * scale;
            var animator = body.GetComponent<Animator>();
            if (animator == null) animator = body.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var visual = body.AddComponent<PolygonNpcVisual>();
            visual.Configure(looks, 0f, names.IndexOf(look));
            var markers = new Transform[route.points.Length];
            var holder = Group(routeRoot, body.name + " route");
            for (int i = 0; i < route.points.Length; i++)
            {
                markers[i] = new GameObject("Point " + (i + 1)).transform;
                markers[i].SetParent(holder, false);
                markers[i].position = new Vector3(route.points[i].x, 0f, route.points[i].y);
            }
            float phase = (float)rng.NextDouble();
            PlaceOnRoute(body.transform, markers, false, phase);
            life.actors.Add(new StreetLife.Actor
            {
                actor = body.transform, waypoints = markers, animator = animator,
                speed = template.speed * (.85f + (float)rng.NextDouble() * .3f), startPhase = phase,
                smoothRoute = false, alignToSlope = false, turnSpeed = template.turnSpeed,
            });
            added++;
        }
        return "Pedestrians: the " + existing + " existing street neighbours now wear POLYGON looks; " + added
            + " new walkers (both police officers among them) walk the new blocks' sidewalks.";
    }

    // Edit-mode pose matching the route position the actor starts from.
    static void PlaceOnRoute(Transform actor, Transform[] waypoints, bool open, float phase)
    {
        var points = waypoints.Where(t => t != null).Select(t => t.position).ToList();
        if (!open) points.Add(points[0]);
        float total = 0f;
        for (int i = 1; i < points.Count; i++) total += Vector3.Distance(points[i - 1], points[i]);
        float target = Mathf.Repeat(phase, 1f) * total;
        for (int i = 1; i < points.Count; i++)
        {
            float length = Vector3.Distance(points[i - 1], points[i]);
            if (target <= length || i == points.Count - 1)
            {
                Vector3 dir = (points[i] - points[i - 1]).normalized;
                actor.position = Vector3.Lerp(points[i - 1], points[i], length > 0f ? Mathf.Clamp01(target / length) : 0f);
                if (dir.sqrMagnitude > 0f) actor.rotation = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.z));
                return;
            }
            target -= length;
        }
    }

    static bool IsWalker(StreetLife.Actor actor) => actor != null && actor.actor != null && actor.waypoints != null
        && actor.waypoints.Length >= 2 && (actor.wheels == null || actor.wheels.Length == 0)
        && !actor.actor.name.StartsWith("Passing bird", StringComparison.Ordinal);

    static void AddRoute(Vector2[] points, bool closed)
    {
        int segments = closed ? points.Length : points.Length - 1;
        for (int i = 0; i < segments; i++)
            walkways.Add((points[i], points[(i + 1) % points.Length]));
    }

    // ----------------------------------------------------------------- utils

    static GameObject Put(Transform parent, string path, Vector3 position, float yaw, bool fixedInPlace = true)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) throw new InvalidOperationException("Missing purchased prefab " + path);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        // Decorative only: no physics, lights, particles or scripts; static for batching.
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) { c.enabled = false; Record(c); }
        foreach (var l in go.GetComponentsInChildren<Light>(true)) { l.enabled = false; Record(l); }
        foreach (var p in go.GetComponentsInChildren<ParticleSystem>(true)) { p.gameObject.SetActive(false); Record(p.gameObject); }
        foreach (var m in go.GetComponentsInChildren<MonoBehaviour>(true)) if (m != null) { m.enabled = false; Record(m); }
        if (fixedInPlace)
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
                Record(t.gameObject);
            }
        return go;
    }

    static void Record(Object o)
    {
        if (PrefabUtility.IsPartOfPrefabInstance(o)) PrefabUtility.RecordPrefabInstancePropertyModifications(o);
    }

    // Every transform, animator, collider and light under root that belongs to
    // a prefab instance: the builder moves, sizes and switches these directly.
    static int RecordInstanceChanges(Transform under)
    {
        int count = 0;
        foreach (var t in under.GetComponentsInChildren<Transform>(true))
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(t)) continue;
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
            foreach (var c in t.GetComponents<Component>())
                if ((c is Animator || c is Collider || c is Light) && PrefabUtility.IsPartOfPrefabInstance(c) && !PrefabUtility.IsAddedComponentOverride(c))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(c);
            count++;
        }
        return count;
    }

    static Bounds BoundsOf(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && (r is MeshRenderer || r is SkinnedMeshRenderer)).ToArray();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers.Skip(1)) b.Encapsulate(r.bounds);
        return b;
    }

    static Transform Group(string name) => Group(root, name);

    static Transform Group(Transform parent, string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    static Rect Expand(Rect r, float m) => Rect.MinMaxRect(r.xMin - m, r.yMin - m, r.xMax + m, r.yMax + m);

    static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = ab.sqrMagnitude < 1e-8f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }

    static void Run(string label, Func<string> action)
    {
        try { Debug.Log(Tag + label + ": " + action()); }
        catch (Exception e) { Debug.LogError(Tag + label + " FAILED: " + e.Message + "\n" + e); }
    }
}
#endif
