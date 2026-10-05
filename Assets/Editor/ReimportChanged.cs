#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// ---------------------------------------------------------------------------
// SCRIPTS CHANGED FROM OUTSIDE THE EDITOR (5 Oct 2026)
//
// Unity's Refresh prunes its scan by folder timestamps, and on Windows a folder's timestamp changes
// when a file is added, removed or renamed in it, not when a file is rewritten in place. A script
// rewritten from outside (Claude writes files straight to disk) in a folder where nothing else changed
// is therefore missed by Ctrl+R and Assets > Refresh (seen on 30 Sept and 5 Oct: "the menu item isn't
// there", a stale assembly). This finds every script newer than the last script compile and reimports
// just those.
//
//   Fixit Fidget > Checks > Pick up scripts changed from outside (reimport)
// ---------------------------------------------------------------------------
internal static class ReimportChanged
{
    [MenuItem("Fixit Fidget/Checks/Pick up scripts changed from outside (reimport)")]
    static void Run()
    {
        try
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string assembly = Path.Combine(root, "Library", "ScriptAssemblies", "Assembly-CSharp.dll");
            string editorAssembly = Path.Combine(root, "Library", "ScriptAssemblies", "Assembly-CSharp-Editor.dll");
            // Each script against its own assembly: a runtime script is stale when it is newer than Assembly-CSharp.dll,
            // an editor script when it is newer than Assembly-CSharp-Editor.dll (the two compile at different times).
            DateTime runtimeBuilt = File.Exists(assembly) ? File.GetLastWriteTimeUtc(assembly) : DateTime.MinValue;
            DateTime editorBuilt = File.Exists(editorAssembly) ? File.GetLastWriteTimeUtc(editorAssembly) : DateTime.MinValue;
            var report = new StringBuilder();
            int n = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string file in Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories))
                {
                    string full = Path.GetFullPath(file);
                    // Never the purchased or generated folders: their scripts don't change from outside.
                    string rel = "Assets" + full.Substring(Application.dataPath.Length).Replace('\\', '/');
                    if (rel.StartsWith("Assets/Synty/") || rel.StartsWith("Assets/ThirdParty/") || rel.StartsWith("Assets/TextMesh Pro/")) continue;
                    bool editor = rel.Contains("/Editor/");
                    if (File.GetLastWriteTimeUtc(full) <= (editor ? editorBuilt : runtimeBuilt)) continue;
                    AssetDatabase.ImportAsset(rel, ImportAssetOptions.ForceUpdate);
                    report.AppendLine("  " + rel);
                    n++;
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            if (n > 0) AssetDatabase.Refresh();
            Debug.Log($"[Reimport] {n} script(s) newer than their assembly (runtime built {runtimeBuilt.ToLocalTime():HH:mm:ss}, editor {editorBuilt.ToLocalTime():HH:mm:ss}) reimported" + (n > 0 ? ":\n" + report : "."));
        }
        catch (Exception e)
        {
            Debug.LogError("[Reimport] " + e.Message);
        }
    }
}
#endif
