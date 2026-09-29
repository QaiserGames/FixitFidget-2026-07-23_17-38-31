using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// ACE'S STAND-IN LOOK (break-ins, call 6 in claude/break-ins-spec.md section 11)
//
//   Fixit Fidget > Night > Break-ins - Photograph Ace's stand-in candidates (read-only)
//
// Until Ace has a model of Ace's own, Ace gets a stand-in body at night: one of the POLYGON looks the
// walk-ins already wear (Assets/Art/CityNeighbors/Prefabs, built by City pack > NPC looks 1). This step
// photographs every look Ace could take, standing and walking with the café's own clips, so Mansoor can
// pick one. It uses the looks' own line-up (PolygonNpcSetup.Lineup) with bigger pictures.
//
// Left out: any look a regular already has as a stand-in (Grace's), and the officers, who only walk the
// beat. Whichever look Ace takes later leaves the walk-ins' pool, as Grace's did, so Ace never meets a
// double. This step is read-only: it renders in a preview scene and changes nothing in the project or
// the scene. Photos and a list: Logs/Night/ace-stand-ins-<time>/.
internal static class AceStandInPhotos
{
    const string Tag = "[Break-ins] ";

    [MenuItem("Fixit Fidget/Night/Break-ins - Photograph Ace's stand-in candidates (read-only)")]
    static void Photograph()
    {
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            var taken = new HashSet<string>(AssetDatabase.FindAssets("t:CustomerProfile")
                .Select(g => AssetDatabase.LoadAssetAtPath<CustomerProfile>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(p => p != null && p.StandInLook.Length > 0)
                .Select(p => p.StandInLook), StringComparer.Ordinal);
            string[] free = PolygonNpcSetup.WalkInLooks.Where(n => !taken.Contains(n)).ToArray();
            // Two sheets, so each picture stays a readable size: the first half of the pool, then the rest.
            int half = (free.Length + 1) / 2;
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Night",
                "ace-stand-ins-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture)));
            Directory.CreateDirectory(folder);
            var poses = new[] { ("CharacterArmature|Idle", .35f), ("CharacterArmature|Walk", .15f) };
            string a = PolygonNpcSetup.Lineup(Path.Combine(folder, "candidates-1.png"), free.Take(half).ToArray(), poses, 300, 520, 5.4f);
            string b = PolygonNpcSetup.Lineup(Path.Combine(folder, "candidates-2.png"), free.Skip(half).ToArray(), poses, 300, 520, 5.4f);
            string report = "Ace's stand-in candidates, " + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "\n"
                + "Left out (already a regular's stand-in): " + (taken.Count == 0 ? "none" : string.Join(", ", taken.OrderBy(n => n, StringComparer.Ordinal))) + "\n"
                + "Left out (officers, street only): " + string.Join(", ", PolygonNpcSetup.StreetOnlyLooks) + "\n"
                + "Candidates: " + free.Length + "\n\n" + a + "\n\n" + b + "\n";
            File.WriteAllText(Path.Combine(folder, "report.txt"), report);
            Debug.Log(Tag + "Ace's stand-in candidates: " + free.Length + " looks photographed in " + folder);
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Ace's stand-in candidates FAILED: " + e.Message + "\n" + e);
        }
    }
}
