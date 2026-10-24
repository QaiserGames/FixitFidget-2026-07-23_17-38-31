#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// ---------------------------------------------------------------------------
// THE NOTEBOOK'S PICTURES, PART 3: THE PHOTOS (23 Oct 2026)
//
//   Fixit Fidget > Night > Notebook pictures 1 - Photograph the notebook's subjects (Edit Mode)
//
// His note (22 Oct): "let's add like pictures that make it also easier for players to know what's happening ... it's
// word vomit." Every subject the night's notebook can be about (NoteSubjects) gets a photo of the game's own model, taken
// here in a small preview studio (BenchScale.Studio, the one the model sheets and the bench steps use; the open scene is
// never changed: what's photographed is a copy in the studio's own scene), as a 256 px PNG under
// Assets/Data/Resources/UI/NotePhotos/<key>.png, with index.json beside them (each key's file, how it was taken, and where
// a place is, for its street map). So every picture matches the game, and a new subject gets its picture by one line in
// Shots below (and one in NoteSubjects for its words).
//
//   * People wear their look on the café people's rig, standing in its idle, photographed from the chest up (the way
//     AceSidekickSteps photographs a café person: PolygonNpcVisual, the idle sampled, the skin baked to a still mesh for
//     the render). Grace and the Officer wear their profiles' stand-in looks, the man at the bins the bins' look. The game
//     has no face cards (every profile's portrait is empty), so a person this can't pose (the purchased looks are copied by
//     hand and may be missing) is drawn as a head and shoulders in their colour, and the report says so.
//   * Places and things are copies of the scene's own: Grace's house from her street, the back door, the bins and his
//     corner from the street (the bins' view), Barnaby and the cups (the night's trophies), his crate, the road works'
//     cones; the café is its sign. Each on a soft backdrop by kind (people warm, places sky, things sage: NotePhotos.Backdrop),
//     lit from the camera's upper left.
//   * His girlfriend can replace any PNG with a drawing of the same name: the index keeps the fingerprint of every PNG this
//     step wrote, and a PNG that isn't one of them is left alone (reported "kept"). Delete it to get the photo back.
//   * A subject it couldn't photograph keeps the photo it had (if this step made one), else the page draws it (NotePhotos).
//
// The report lists every subject: photographed, kept, or couldn't and why. Logs/Night/notebook-pictures-<time>/: the
// report and a contact sheet of every subject's picture as the page will show it (an amber edge: drawn in code; a green
// edge: drawn by hand). Run it with the café scene open, in Edit Mode, after any change to a model it photographs; then
// commit the PNGs and index.json.
// ---------------------------------------------------------------------------
internal static class NotePhotoSteps
{
    const string Tag = "[Notebook pictures] ";
    const string Title = "Notebook pictures";
    const string MenuName = "Fixit Fidget/Night/Notebook pictures 1 - Photograph the notebook's subjects (Edit Mode)";
    const string GraceProfile = "Assets/Data/Regulars/Regular_Grace.asset";
    const string OfficerProfile = "Assets/Data/Regulars/Regular_Officer.asset";
    const string LodgerLookName = "Character_BusinessMan_Suit";   // the bins' look when the scene has none (Bins 1)
    const float Fov = 26f;

    sealed class Skip : Exception
    {
        public Skip(string why) : base(why) { }
    }

    sealed class Shot
    {
        public string key = "", what = "";
        public Func<Studio, Texture2D> take;
    }

    // ===================================================================== THE LIST: one line a subject

    static readonly Shot[] Shots =
    {
        // People: their look on the café people's rig, from the chest up, turned a little to the camera's left or right.
        new Shot { key = NoteSubjects.Grace, what = "Grace: her stand-in look (Regular_Grace) on the café people's rig",
                   take = s => s.Person(Look(GraceProfile, "Character_BusinessWoman"), 14f) },
        new Shot { key = NoteSubjects.Lodger, what = "the man at the bins: the bins' look (NightZeroSet.look) on the rig",
                   take = s => s.Person(LodgerLook(), -14f) },
        new Shot { key = NoteSubjects.Officer, what = "the Officer: his stand-in look (Regular_Officer) on the rig",
                   take = s => s.Person(Look(OfficerProfile, "Character_Male_Police"), 14f) },
        // The neighbours are nobody in particular; what the notebook knows of them is their windows: their house.
        new Shot { key = NoteSubjects.Neighbours, what = "the neighbours: the first night neighbour's house (NightWalk), from its street",
                   take = s => s.House(NeighbourHouse(), null, "the night neighbours' houses aren't in the scene (NightWalk's neighbours)") },
        // Places.
        new Shot { key = NoteSubjects.GraceHouse, what = "12 West Street: Grace's house (her HomeDoor's house), from her street",
                   take = s => s.House(GraceDoor() != null ? GraceDoor().transform.parent : null, GraceDoor(),
                                       "Grace's house has no door in the scene (Fixit Fidget > Night > Give Grace her home)") },
        new Shot { key = NoteSubjects.BackDoor, what = "the back door, from Back Street (the bins' set)",
                   take = s => s.BackDoor() },
        new Shot { key = NoteSubjects.Alley, what = "the bins: the dumpster and the trash can, from the street (the bins' view)",
                   take = s => s.AtTheBins(false) },
        new Shot { key = NoteSubjects.Corner, what = "his corner: the trash can with Barnaby on its lid, and his crate",
                   take = s => s.AtTheBins(true) },
        new Shot { key = NoteSubjects.Cafe, what = "Ace's café: its sign (SignAcesCafe), from the front",
                   take = s => s.Model("SignAcesCafe", 0f, 4f) },   // from +Z, stood up (Model): on its back, from +Z and then from +X, it was an edge (23 Oct 2026)
        // Things.
        new Shot { key = NoteSubjects.Barnaby, what = "Barnaby: the gnome on Grace's step (the night's trophy grace.gnome)",
                   take = s => s.Thing(Trophy(NightThings.GraceGnome), 22f, 14f) },
        new Shot { key = NoteSubjects.Cups, what = "the reunion cups: the sleeve on her worktop (the night's trophy grace.cups)",
                   take = s => s.Thing(Trophy(NightThings.GraceCups), 30f, 38f) },
        new Shot { key = NoteSubjects.Crate, what = "his crate: the box by the bins (NightZeroSet.crate)",
                   take = s => s.Crate() },
        new Shot { key = NoteSubjects.Cones, what = "the cones: the road works' cones on West Street (scene objects named *cone*)",
                   take = s => s.Cones() },
    };

    /// <summary>The subjects with a line in Shots (the notebook rules check every subject either has one or is drawn).</summary>
    internal static IEnumerable<string> Photographed => Shots.Select(s => s.key);

    // ===================================================================== the menu

    [MenuItem(MenuName, false, 60)]
    static void Photograph()
    {
        try { CityPackChecks.RequireScene(); }
        catch (Exception e)
        {
            Debug.LogError(Tag + e.Message + " Nothing was photographed.");
            return;
        }
        string stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture);
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Night", "notebook-pictures-" + stamp));
        Directory.CreateDirectory(folder);
        var report = new StringBuilder();
        report.AppendLine("Notebook pictures 1 - photograph the notebook's subjects   (" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + ")");
        report.AppendLine("Each subject the night's notebook can be about, photographed from the game's own model in a preview studio; the scene is not changed.");
        report.AppendLine("Pictures: " + NotePhotos.AssetFolder + "/<key>.png, with index.json. A PNG this step didn't make is kept (a drawing).");
        report.AppendLine();

        EnsureFolder();
        NotePhotos.Index before = ReadIndex();
        var index = new NotePhotos.Index { made = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) };
        var entries = new List<NotePhotos.Entry>();
        var tiles = new List<(string key, Color32[] pixels, char how)>();
        var written = new List<string>();
        int took = 0, kept = 0, failed = 0;
        bool stopped = false;
        try
        {
            for (int i = 0; i < Shots.Length; i++)
            {
                Shot shot = Shots[i];
                if (EditorUtility.DisplayCancelableProgressBar(Title, $"{i + 1}/{Shots.Length}: {shot.key}", i / (float)Shots.Length))
                {
                    stopped = true;
                    report.AppendLine("STOPPED by hand before " + shot.key + ": the subjects after it keep what they had.");
                    break;
                }
                NoteSubject subject = NoteSubjects.Find(shot.key);
                string asset = NotePhotos.AssetFolder + "/" + shot.key + ".png", file = FullPath(asset);
                NotePhotos.Entry was = before != null ? before.Find(shot.key) : null;
                var entry = new NotePhotos.Entry { key = shot.key, made = was != null ? was.made ?? "" : "" };
                entries.Add(entry);
                Spot(shot.key, entry);

                // A PNG that isn't the one this step last wrote is someone's drawing: left alone.
                bool exists = File.Exists(file);
                string onDisk = exists ? Fingerprint(File.ReadAllBytes(file)) : "";
                if (exists && (entry.made.Length == 0 || onDisk != entry.made))
                {
                    kept++;
                    entry.file = shot.key;
                    entry.kept = true;
                    entry.how = "kept: not the picture this step made (a drawing?); delete it to get the photo back";
                    report.AppendLine($"KEPT      {shot.key,-11} {asset}: not the PNG this step last wrote, so it's left as it is.");
                    tiles.Add((shot.key, Load(file), 'k'));
                    continue;
                }

                using (var studio = new Studio(subject != null ? subject.kind : NoteKind.Thing))
                {
                    try
                    {
                        Texture2D photo = shot.take(studio);
                        byte[] bytes;
                        Color32[] pixels;
                        try
                        {
                            bytes = photo.EncodeToPNG();
                            pixels = photo.GetPixels32();
                        }
                        finally { Object.DestroyImmediate(photo); }
                        File.WriteAllBytes(file, bytes);
                        written.Add(asset);
                        entry.file = shot.key;
                        entry.made = Fingerprint(bytes);
                        entry.how = shot.what + (studio.Notes.Length > 0 ? " (" + studio.Notes + ")" : "");
                        took++;
                        report.AppendLine($"PHOTO     {shot.key,-11} {shot.what}{(studio.Notes.Length > 0 ? " (" + studio.Notes + ")" : "")} -> {asset}");
                        tiles.Add((shot.key, pixels, 'p'));
                    }
                    catch (Exception e)
                    {
                        failed++;
                        string why = e is Skip ? e.Message : e.GetType().Name + ": " + e.Message;
                        if (!(e is Skip)) Debug.LogException(e);
                        if (exists)
                        {
                            entry.file = shot.key;
                            entry.how = "couldn't photograph it this time (" + why + "): the photo from before stays";
                            report.AppendLine($"COULDN'T  {shot.key,-11} {why}. The photo from before stays ({asset}).");
                            tiles.Add((shot.key, Load(file), 'p'));
                        }
                        else
                        {
                            entry.file = "";
                            entry.how = "couldn't: " + why + "; the page draws it";
                            report.AppendLine($"COULDN'T  {shot.key,-11} {why}. The page draws it instead ({DrawingName(shot.key)}).");
                            tiles.Add((shot.key, null, 'd'));
                        }
                    }
                }
            }
        }
        finally { EditorUtility.ClearProgressBar(); }

        // The rest: a subject a stop by hand never reached keeps what it had; a subject with no line in the list is drawn
        // (declared: Someone, any regular with no subject of their own), unless someone put a PNG there.
        foreach (NoteSubject s in NoteSubjects.All)
        {
            if (entries.Any(e => e.key == s.key)) continue;
            NotePhotos.Entry was = before != null ? before.Find(s.key) : null;
            string file = FullPath(NotePhotos.AssetFolder + "/" + s.key + ".png");
            bool exists = File.Exists(file);
            bool listed = Shots.Any(shot => shot.key == s.key);
            if (stopped && listed)
            {
                NotePhotos.Entry left = was ?? new NotePhotos.Entry { key = s.key, file = exists ? s.key : "", how = "not photographed: stopped by hand" };
                entries.Add(left);
                report.AppendLine($"LEFT      {s.key,-11} not reached (stopped by hand): {(exists ? "its PNG stays" : "the page draws it")}.");
                tiles.Add((s.key, exists ? Load(file) : null, exists ? 'p' : 'd'));
                continue;
            }
            var entry = new NotePhotos.Entry { key = s.key, file = exists ? s.key : "", kept = exists, made = was != null ? was.made ?? "" : "",
                how = exists ? "kept: a drawing put in its place" : "drawn: no photo for it (" + DrawingName(s.key) + ")" };
            Spot(s.key, entry);
            entries.Add(entry);
            report.AppendLine(exists ? $"KEPT      {s.key,-11} a PNG someone put there (no photo is taken of it)."
                                     : $"DRAWN     {s.key,-11} no line in the photo list: the page draws it ({DrawingName(s.key)}).");
            tiles.Add((s.key, exists ? Load(file) : null, exists ? 'k' : 'd'));
        }
        index.photos = entries.ToArray();

        // The index, then the import (each new PNG imported for the UI: clamped, mipmapped, at most 256 px).
        string indexAsset = NotePhotos.AssetFolder + "/" + NotePhotos.IndexName + ".json";
        File.WriteAllText(FullPath(indexAsset), JsonUtility.ToJson(index, true));
        AssetDatabase.ImportAsset(indexAsset, ImportAssetOptions.ForceSynchronousImport);
        foreach (string asset in written)
        {
            AssetDatabase.ImportAsset(asset, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(asset) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Trilinear;
                importer.maxTextureSize = NotePhotos.PhotoSize;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
        }
        NotePhotos.Forget();

        string sheet = Path.Combine(folder, "contact-sheet.png");
        try { ContactSheet(tiles, sheet); }
        catch (Exception e) { report.AppendLine("The contact sheet couldn't be made: " + e.Message); sheet = ""; }

        report.AppendLine();
        report.AppendLine("The contact sheet, left to right, top to bottom: " + string.Join(", ", tiles.Select(t => t.key)) + ".");
        report.AppendLine("  no edge: a photo; amber edge: drawn in code (no photo); green edge: a PNG drawn by hand, kept.");
        report.AppendLine();
        string verdict = $"{took} photographed, {kept} kept (not this step's), {failed} couldn't{(failed > 0 ? " (see above; the page draws those or keeps the photo from before)" : "")}" +
                         $"{(stopped ? "; stopped by hand" : "")}.";
        report.AppendLine(verdict);
        string reportPath = Path.Combine(folder, "report.txt");
        File.WriteAllText(reportPath, report.ToString());
        string line = Tag + verdict + " Report: " + reportPath + (sheet.Length > 0 ? "  Contact sheet: " + sheet : "") + "\n" + report;
        if (failed > 0 || stopped) Debug.LogWarning(line);
        else Debug.Log(line);
    }

    [MenuItem(MenuName, true)]
    static bool CanPhotograph() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // ===================================================================== the scene's subjects

    static NightZeroSet Set() => CityPackChecks.InScene<NightZeroSet>().FirstOrDefault();

    static HomeDoor GraceDoor() => CityPackChecks.InScene<HomeDoor>().FirstOrDefault(d => d.homeId == "home.grace");

    static NightTrophy Trophy(string id) => CityPackChecks.InScene<NightTrophy>().FirstOrDefault(t => t.thingId == id);

    static Transform NeighbourHouse()
    {
        NightWalk walk = CityPackChecks.InScene<NightWalk>().FirstOrDefault();
        string name = walk != null && walk.neighbours != null
            ? walk.neighbours.Where(n => n != null && !string.IsNullOrWhiteSpace(n.house)).Select(n => n.house.Trim()).FirstOrDefault() : null;
        return string.IsNullOrEmpty(name) ? null : CityPackChecks.InScene<Transform>().FirstOrDefault(t => t.name == name);
    }

    static GameObject Look(string profilePath, string fallback)
    {
        var profile = AssetDatabase.LoadAssetAtPath<CustomerProfile>(profilePath);
        string name = profile != null && profile.StandInLook.Length > 0 ? profile.StandInLook : fallback;
        return string.IsNullOrEmpty(name) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(PolygonNpcSetup.LookPath(name));
    }

    static GameObject LodgerLook()
    {
        NightZeroSet set = Set();
        return set != null && set.look != null ? set.look : AssetDatabase.LoadAssetAtPath<GameObject>(PolygonNpcSetup.LookPath(LodgerLookName));
    }

    // Where a place is (for its map): Grace's door, the back door, the bins, his corner, the cones. The café's is its floor (NotePhotos).
    static void Spot(string key, NotePhotos.Entry entry)
    {
        NoteSubject s = NoteSubjects.Find(key);
        if (s == null || s.kind != NoteKind.Place) return;
        NightZeroSet set = Set();
        Vector3? at = null;
        switch (key)
        {
            case NoteSubjects.GraceHouse: at = GraceDoor() != null ? GraceDoor().DoorPoint : (Vector3?)null; break;
            case NoteSubjects.BackDoor: at = set != null && set.outsideDoor != null ? set.outsideDoor.position : (Vector3?)null; break;
            case NoteSubjects.Alley: at = set != null ? set.Bins : (Vector3?)null; break;
            case NoteSubjects.Corner: at = set != null && set.cornerGnome != null ? set.cornerGnome.transform.position : (Vector3?)null; break;
        }
        if (at == null) return;
        entry.hasSpot = true;
        entry.x = Mathf.Round(at.Value.x * 100f) / 100f;
        entry.z = Mathf.Round(at.Value.z * 100f) / 100f;
    }

    // ===================================================================== the studio

    sealed class Studio : IDisposable
    {
        readonly BenchScale.Studio studio = new BenchScale.Studio();
        readonly Scene preview;
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<string> notes = new List<string>();

        public string Notes => string.Join("; ", notes);

        public Studio(NoteKind kind)
        {
            Camera cam = studio.Utility.camera;
            cam.cullingMask = -1;   // every layer: a part is a part whatever layer it was put on
            cam.backgroundColor = NotePhotos.Backdrop(kind);
            studio.Utility.ambientColor = new Color(.46f, .45f, .47f);
            preview = studio.Utility.lights[0].gameObject.scene;   // the studio's lamp lives in its preview scene
        }

        public void Dispose()
        {
            studio.Dispose();
            foreach (Mesh m in meshes) if (m != null) Object.DestroyImmediate(m);
        }

        // ---- people ----

        public Texture2D Person(GameObject look, float yaw)
        {
            if (look == null) throw new Skip("their look isn't in the project (the purchased looks in Assets/Art/CityNeighbors are copied by hand)");
            GameObject rig = AssetDatabase.LoadAssetAtPath<GameObject>(AceBodySteps.RigModel);
            if (rig == null) throw new Skip("the café people's rig isn't in the project (" + AceBodySteps.RigModel + ")");
            AnimationClip idle = AssetDatabase.LoadAllAssetsAtPath(AceBodySteps.RigModel).OfType<AnimationClip>().FirstOrDefault(c => c.name == AceBodySteps.IdleName);
            GameObject actor = studio.Place(rig);
            if (actor == null) throw new Skip("the rig couldn't be put in the studio");
            actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            actor.transform.localScale = Vector3.one * AceBodySteps.CafePeopleScale();
            foreach (Animator a in actor.GetComponentsInChildren<Animator>(true)) a.enabled = false;
            var visual = actor.AddComponent<PolygonNpcVisual>();
            visual.Configure(new[] { look }, 0f, 0, 0f);
            if (!visual.ApplyAppearance(0)) throw new Skip(look.name + " didn't fit the rig (its bones couldn't be bound)");
            if (idle != null) idle.SampleAnimation(actor, idle.length * .35f);
            else notes.Add("no idle clip on the rig: in its bind pose");
            visual.Follow();
            List<Renderer> body = Bake(actor);
            if (body.Count == 0) throw new Skip(look.name + " has no skinned mesh to photograph");
            Bounds all = Union(body);
            // From the chest up: the top half of the body, a little room over the head (the café people face +Z).
            float height = all.size.y;
            var bust = new Bounds(new Vector3(all.center.x, all.max.y - height * .23f, all.center.z), new Vector3(height * .26f, height * .46f, height * .16f));
            notes.Add(look.name);
            return Take(bust, Quaternion.Euler(0f, yaw, 0f) * Vector3.forward, 3f, 1.02f);
        }

        // Several renders happen in one editor frame and a skinned mesh re-skins once a frame: each is drawn as a still copy
        // of its pose (PolygonNpcSetup.SkinToWorld), made in the studio's own scene (BenchScale.Studio.Proxy).
        List<Renderer> Bake(GameObject root)
        {
            var result = new List<Renderer>();
            foreach (SkinnedMeshRenderer skin in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!skin.enabled || skin.forceRenderingOff || skin.sharedMesh == null) continue;
                Mesh mesh = PolygonNpcSetup.SkinToWorld(skin);
                meshes.Add(mesh);
                GameObject still = studio.Proxy("Posed " + skin.name, mesh, skin.sharedMaterials, Vector3.zero, Quaternion.identity, Vector3.one);
                skin.forceRenderingOff = true;
                result.Add(still.GetComponent<MeshRenderer>());
            }
            return result;
        }

        // ---- places and things ----

        public Texture2D House(Transform house, HomeDoor door, string missing)
        {
            if (house == null) throw new Skip(missing);
            GameObject copy = CopyIn(house.gameObject);
            List<Renderer> parts = Drawn(copy, null);
            if (parts.Count == 0) throw new Skip(house.name + " has nothing drawn");
            Bounds b = Union(parts);
            Vector3 toward = door != null ? door.transform.forward : house.forward;
            Vector3 street = TowardStreet(b.center);
            if (door == null && Vector3.Dot(Flat(toward), street) < 0f) { toward = street; notes.Add("faced from its street, not its forward"); }
            notes.Add(house.name);
            return Take(b, toward, 6f, 1.04f);
        }

        public Texture2D BackDoor()
        {
            NightZeroSet set = Set();
            if (set == null || set.outsideDoor == null) throw new Skip("the bins aren't in the scene (Fixit Fidget > Night > Bins 1)");
            Transform door = set.transform.Find("Back door (outside)");
            // Only the door goes into the studio when it can be found, seen from Back Street the way the bins' view looks
            // (23 Oct 2026, the first photo on his Mac: with the whole set copied and the door's own forward, the dumpster's
            // lid filled the picture and the door was a few posts behind it).
            GameObject copy = CopyIn(door != null ? door.gameObject : set.gameObject);
            Vector3 at = set.outsideDoor.position;
            List<Renderer> parts = door != null ? Drawn(copy, null) : Drawn(copy, r => Near(r.bounds.center, at, 1.4f));
            if (parts.Count == 0) throw new Skip("nothing is drawn at the back door");
            return Take(Union(parts), BinsStreet(set), 8f, 1.2f);
        }

        // The bins (the dumpster and the trash can), or his corner (the trash can with Barnaby on its lid, and his crate),
        // from the street the way the bins' view looks at them (NightZeroSet.viewYaw).
        public Texture2D AtTheBins(bool corner)
        {
            NightZeroSet set = Set();
            if (set == null) throw new Skip("the bins aren't in the scene (Fixit Fidget > Night > Bins 1)");
            if (corner && set.cornerGnome == null) throw new Skip("his corner has no gnome in the scene (Bins 1)");
            GameObject copy = CopyIn(set.gameObject);
            Vector3 at = corner ? set.cornerGnome.transform.position : set.Bins;
            if (corner)
            {
                // Barnaby on the trash can's lid is switched off until he's been given: on, in the copy, for the photo.
                Transform gnome = copy.transform.Find(Relative(set.transform, set.cornerGnome.transform));
                if (gnome != null) gnome.gameObject.SetActive(true);
                else notes.Add("the corner's gnome couldn't be found in the copy");
            }
            List<Renderer> parts = Drawn(copy, r => Near(r.bounds.center, at, corner ? 1.1f : 2.6f));
            if (parts.Count == 0) throw new Skip("nothing is drawn there");
            return Take(Union(parts), BinsStreet(set), corner ? 18f : 22f, 1.06f);
        }

        public Texture2D Crate()
        {
            NightZeroSet set = Set();
            if (set == null || set.crate == null) throw new Skip("his crate isn't in the scene (Fixit Fidget > Night > Night 2 1)");
            GameObject copy = CopyIn(set.crate);
            List<Renderer> parts = Drawn(copy, null);
            if (parts.Count == 0) throw new Skip("the crate has nothing drawn");
            return Take(Union(parts), BinsStreet(set), 26f, 1.1f);
        }

        public Texture2D Thing(NightTrophy trophy, float yaw, float pitch)
        {
            if (trophy == null) throw new Skip("it isn't in the scene (the night's trophies: Night 1, Night 2 1)");
            GameObject what = trophy.visual != null ? trophy.visual : trophy.gameObject;
            GameObject copy = CopyIn(what);
            List<Renderer> parts = Drawn(copy, null);
            if (parts.Count == 0) throw new Skip(what.name + " has nothing drawn");
            notes.Add(what.name);
            return Take(Union(parts), Quaternion.Euler(0f, yaw, 0f) * what.transform.forward, pitch, 1.12f);
        }

        public Texture2D Model(string name, float yaw, float pitch)
        {
            string path = AssetDatabase.FindAssets(name + " t:GameObject").Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .OrderBy(p => Path.GetFileNameWithoutExtension(p) == name ? 0 : 1).ThenBy(p => p, StringComparer.Ordinal).FirstOrDefault();
            GameObject asset = path != null ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : null;
            if (asset == null) throw new Skip("no model or prefab named " + name + " in the project");
            GameObject copy = studio.Place(asset);
            if (copy == null) throw new Skip(path + " couldn't be put in the studio");
            copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Quiet(copy);
            List<Renderer> parts = Drawn(copy, null);
            if (parts.Count == 0) throw new Skip(path + " has nothing drawn");
            // Our Blender exports lie on their backs (Z-up): stood up the way the café stands them (CafeFurnishing.Spawn,
            // CafeParkingLot.CafeSign), and their face then looks +z. Without it the sign was photographed lying down, an
            // edge from +Z and from +X (23 Oct 2026, the first two photos on his Mac).
            Bounds raw = Union(parts);
            if (CafeFurnishingCatalog.IsZUp(new Bounds(raw.center - copy.transform.position, raw.size)))
            {
                copy.transform.rotation = CafeFurnishingCatalog.Upright;
                notes.Add("stood up: a Z-up export");
            }
            notes.Add(path + " from +Z" + (yaw != 0f ? " turned " + yaw.ToString("0", CultureInfo.InvariantCulture) + "°" : "") +
                      " (if it shows its back, give its line in Shots a yaw of 180)");
            return Take(Union(parts), Quaternion.Euler(0f, yaw, 0f) * Vector3.forward, pitch, 1.08f);
        }

        // The road works' cones (his third favour, not in the game yet): any drawn object named like a cone near West Street.
        public Texture2D Cones()
        {
            List<Renderer> cones = CityPackChecks.InScene<Renderer>()
                .Where(r => r.enabled && r.gameObject.activeInHierarchy && (r is MeshRenderer || r is SkinnedMeshRenderer) && IsCone(r.gameObject.name))
                .OrderBy(r => Mathf.Abs(r.bounds.center.x - WestStreetX)).ToList();
            if (cones.Count == 0) throw new Skip("no cones in the scene (no drawn object named like a cone)");
            Vector3 first = cones[0].bounds.center;
            var parts = new List<Renderer>();
            foreach (Renderer r in cones.Where(r => Near(r.bounds.center, first, 3f)).Take(5))
                parts.AddRange(Drawn(CopyIn(r.gameObject), null));
            if (parts.Count == 0) throw new Skip("the cones have nothing drawn");
            notes.Add(cones[0].gameObject.name);
            return Take(Union(parts), Vector3.right, 24f, 1.2f);
        }

        // ---- the copy ----

        // A copy of a scene object in the studio's own scene, never the open one (as ReviewModelSheets copies a scene
        // group): its world pose kept, switched on, and nothing on it left to act (scripts, cameras, lights, canvases).
        GameObject CopyIn(GameObject original)
        {
            GameObject copy = null;
#if UNITY_6000_0_OR_NEWER
            try { copy = Object.Instantiate(original, new InstantiateParameters { scene = preview, worldSpace = true }); }
            catch (Exception e) { notes.Add("copied, then moved into the studio (" + e.Message + ")"); }
#endif
            if (copy == null) copy = Object.Instantiate(original);
            if (copy.scene != preview) SceneManager.MoveGameObjectToScene(copy, preview);
            Transform o = original.transform, t = copy.transform;
            t.SetPositionAndRotation(o.position, o.rotation);
            t.localScale = o.lossyScale;
            copy.name = original.name;
            copy.SetActive(true);
            Quiet(copy);
            return copy;
        }

        static void Quiet(GameObject copy)
        {
            foreach (MonoBehaviour b in copy.GetComponentsInChildren<MonoBehaviour>(true))
                if (b != null && !(b is TMPro.TMP_Text)) b.enabled = false;   // a house number's text still draws
            foreach (Camera c in copy.GetComponentsInChildren<Camera>(true)) c.enabled = false;
            foreach (AudioListener a in copy.GetComponentsInChildren<AudioListener>(true)) a.enabled = false;
            foreach (Canvas c in copy.GetComponentsInChildren<Canvas>(true)) c.enabled = false;
            foreach (Light l in copy.GetComponentsInChildren<Light>(true)) l.enabled = false;   // the studio's light, the same for every photo
            foreach (LODGroup g in copy.GetComponentsInChildren<LODGroup>(true)) g.ForceLOD(0);
        }

        // ---- the photo ----

        // The camera on the line toward the camera (raised by pitch), far enough that the box fits with the margin to spare;
        // lit from the camera's upper left with a cool fill from its right; then the render fitted to 256 px square.
        Texture2D Take(Bounds focus, Vector3 toward, float pitch, float margin)
        {
            Vector3 flat = Flat(toward);
            float p = pitch * Mathf.Deg2Rad;
            Vector3 dir = (flat * Mathf.Cos(p) + Vector3.up * Mathf.Sin(p)).normalized;
            Quaternion look = Quaternion.LookRotation(-dir, Vector3.up);
            Vector3 right = look * Vector3.right, up = look * Vector3.up;
            float tan = Mathf.Tan(Fov * .5f * Mathf.Deg2Rad);
            Vector3 c = focus.center, e = focus.extents;
            float distance = .05f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 rel = Corner(e, i);
                float across = Mathf.Max(Mathf.Abs(Vector3.Dot(rel, right)), Mathf.Abs(Vector3.Dot(rel, up)));
                distance = Mathf.Max(distance, across * margin / tan + Vector3.Dot(rel, dir));
            }
            float nearest = float.MaxValue;
            for (int i = 0; i < 8; i++) nearest = Mathf.Min(nearest, distance - Vector3.Dot(Corner(e, i), dir));
            Camera cam = studio.Utility.camera;
            cam.nearClipPlane = Mathf.Max(.005f, nearest * .5f);
            cam.farClipPlane = distance + e.magnitude * 4f + 40f;

            Light key = studio.Utility.lights[0], fill = studio.Utility.lights[1];
            key.transform.rotation = Quaternion.LookRotation(-(dir * .8f - right * .6f + Vector3.up * .9f).normalized);
            key.intensity = 1.15f;
            key.color = new Color(1f, .95f, .88f);
            fill.transform.rotation = Quaternion.LookRotation(-(dir * .6f + right * .7f + Vector3.up * .15f).normalized);
            fill.intensity = .55f;
            fill.color = new Color(.85f, .9f, 1f);

            Texture2D shot = studio.Shoot(c + dir * distance, look, Fov, NotePhotos.PhotoSize, NotePhotos.PhotoSize);
            try { return Fit(shot, NotePhotos.PhotoSize); }
            finally { Object.DestroyImmediate(shot); }
        }

        static Vector3 Corner(Vector3 e, int i) =>
            new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
    }

    // ===================================================================== helpers

    // The streets' middle lines (NotePhotos' map: West Street x -12.0, East Street x 12.6, Front Street z -7.7, Back Street z 23.2).
    const float WestStreetX = -12.0f;
    static readonly (bool alongZ, float at)[] StreetLines = { (true, -12.0f), (true, 12.6f), (false, -7.7f), (false, 23.2f) };

    // From a point toward the nearest street, flat.
    static Vector3 TowardStreet(Vector3 p)
    {
        float best = float.MaxValue;
        Vector3 toward = Vector3.forward;
        foreach (var s in StreetLines)
        {
            float d = (s.alongZ ? s.at - p.x : s.at - p.z);
            if (Mathf.Abs(d) >= best) continue;
            best = Mathf.Abs(d);
            toward = s.alongZ ? new Vector3(Mathf.Sign(d), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(d));
        }
        return toward;
    }

    // From the bins toward the street, the way the bins' view looks at them (its camera looks along viewYaw).
    static Vector3 BinsStreet(NightZeroSet set) => Quaternion.Euler(0f, set.viewYaw, 0f) * Vector3.back;

    static Vector3 Flat(Vector3 v)
    {
        var f = new Vector3(v.x, 0f, v.z);
        return f.sqrMagnitude < 1e-6f ? Vector3.forward : f.normalized;
    }

    static bool Near(Vector3 a, Vector3 b, float metres) => new Vector2(a.x - b.x, a.z - b.z).magnitude <= metres;

    static bool IsCone(string name)
    {
        string n = (name ?? "").ToLowerInvariant();
        return n.Contains("cone") && !n.Contains("icon") && !n.Contains("scone");
    }

    // What of a copy is drawn: switched-on mesh renderers (skinned too), optionally only those a test keeps.
    static List<Renderer> Drawn(GameObject root, Func<Renderer, bool> keep) => root.GetComponentsInChildren<Renderer>()
        .Where(r => r.enabled && r.gameObject.activeInHierarchy && (r is MeshRenderer || r is SkinnedMeshRenderer) && (keep == null || keep(r))).ToList();

    static Bounds Union(List<Renderer> parts)
    {
        Bounds b = parts[0].bounds;
        for (int i = 1; i < parts.Count; i++) b.Encapsulate(parts[i].bounds);
        return b;
    }

    static string Relative(Transform root, Transform t)
    {
        var names = new List<string>();
        for (Transform x = t; x != null && x != root; x = x.parent) names.Add(x.name);
        names.Reverse();
        return string.Join("/", names);
    }

    static string DrawingName(string key) => "a drawing of " + NotePhotos.DrawingOf(key);

    static string FullPath(string asset) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", asset));

    static void EnsureFolder()
    {
        if (AssetDatabase.IsValidFolder(NotePhotos.AssetFolder)) return;
        string parent = Path.GetDirectoryName(NotePhotos.AssetFolder).Replace('\\', '/');   // Assets/Data/Resources/UI (the button icons' folder)
        if (AssetDatabase.IsValidFolder(parent))
        {
            AssetDatabase.CreateFolder(parent, Path.GetFileName(NotePhotos.AssetFolder));
            return;
        }
        Directory.CreateDirectory(FullPath(NotePhotos.AssetFolder));
        AssetDatabase.Refresh();
    }

    // The index on disk as the step last wrote it (not the game's cached copy).
    static NotePhotos.Index ReadIndex()
    {
        string file = FullPath(NotePhotos.AssetFolder + "/" + NotePhotos.IndexName + ".json");
        if (!File.Exists(file)) return null;
        try { return JsonUtility.FromJson<NotePhotos.Index>(File.ReadAllText(file)); }
        catch (Exception) { return null; }
    }

    // A fingerprint of a file's bytes (FNV-1a, 64 bits): tells the step's own PNGs from anyone else's.
    static string Fingerprint(byte[] bytes)
    {
        ulong hash = 14695981039346656037UL;
        foreach (byte b in bytes) hash = (hash ^ b) * 1099511628211UL;
        return hash.ToString("x16", CultureInfo.InvariantCulture);
    }

    // A PNG on disk, as 256 px pixels (null if it can't be read).
    static Color32[] Load(string file)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!texture.LoadImage(File.ReadAllBytes(file))) return null;
            Texture2D fitted = Fit(texture, NotePhotos.PhotoSize);
            try { return fitted.GetPixels32(); }
            finally { Object.DestroyImmediate(fitted); }
        }
        catch (Exception) { return null; }
        finally { Object.DestroyImmediate(texture); }
    }

    /// <summary>A texture at size × size (the preview may come back larger on a Retina screen): a box average per pixel.</summary>
    static Texture2D Fit(Texture2D src, int size)
    {
        Color32[] s = src.GetPixels32();
        int sw = src.width, sh = src.height;
        var d = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            int y0 = y * sh / size, y1 = Mathf.Max(y0 + 1, (y + 1) * sh / size);
            for (int x = 0; x < size; x++)
            {
                int x0 = x * sw / size, x1 = Mathf.Max(x0 + 1, (x + 1) * sw / size);
                int r = 0, g = 0, b = 0, n = 0;
                for (int yy = y0; yy < y1; yy++)
                    for (int xx = x0; xx < x1; xx++)
                    {
                        Color32 p = s[yy * sw + xx];
                        r += p.r; g += p.g; b += p.b; n++;
                    }
                d[y * size + x] = new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), 255);
            }
        }
        var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
        texture.SetPixels32(d);
        texture.Apply(false, false);
        return texture;
    }

    // Every subject's picture as the page shows it, five to a row, in the order of the report.
    static void ContactSheet(List<(string key, Color32[] pixels, char how)> tiles, string path)
    {
        const int size = NotePhotos.PhotoSize, gap = 8, columns = 5;
        int rows = Mathf.Max(1, (tiles.Count + columns - 1) / columns);
        int w = columns * size + (columns + 1) * gap, h = rows * size + (rows + 1) * gap;
        var px = new Color32[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(31, 27, 23, 255);   // the page's ink
        for (int i = 0; i < tiles.Count; i++)
        {
            Color32[] tile = tiles[i].pixels;
            if (tile == null || tile.Length != size * size)
            {
                Texture2D drawing = NotePhotos.Drawing(tiles[i].key).texture;
                Texture2D fitted = Fit(drawing, size);
                tile = fitted.GetPixels32();
                Object.DestroyImmediate(fitted);
            }
            int col = i % columns, row = i / columns;
            int x0 = gap + col * (size + gap), y0 = h - (gap + (row + 1) * size + row * gap);   // rows from the top
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[(y0 + y) * w + x0 + x] = tile[y * size + x];
            if (tiles[i].how == 'd') BenchScale.Outline(px, w, h, x0, y0, x0 + size - 1, y0 + size - 1, new Color32(255, 178, 56, 255), 4);
            else if (tiles[i].how == 'k') BenchScale.Outline(px, w, h, x0, y0, x0 + size - 1, y0 + size - 1, new Color32(46, 125, 91, 255), 4);
        }
        var sheet = new Texture2D(w, h, TextureFormat.RGBA32, false);
        try
        {
            sheet.SetPixels32(px);
            sheet.Apply(false, false);
            BenchScale.SavePng(sheet, path);
        }
        finally { Object.DestroyImmediate(sheet); }
    }
}
#endif
