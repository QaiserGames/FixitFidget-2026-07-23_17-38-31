#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using Debug = UnityEngine.Debug;

// ---------------------------------------------------------------------------
// THE COMPILER'S OWN WORDS, ON DISK (30 Sept 2026)
//
// Every script compile writes its errors and warnings, per assembly, to Logs/Compile/last.txt, so they can be
// read without the Console (Claude works on this project from outside the editor, and the Console only shows a
// count). Written as the compile ends, before Unity reloads the scripts.
//
//   Fixit Fidget > Checks > Copy the editor's log into Logs (read-only)
//       Copies the editor's own log (Editor.log: everything the Console has said this session, and more) to
//       Logs/Compile/Editor-<time>.log.
// ---------------------------------------------------------------------------
[InitializeOnLoad]
internal static class CompileLog
{
    static readonly StringBuilder pending = new StringBuilder();
    static int errors, warnings, assemblies;
    static DateTime started = DateTime.Now;

    static CompileLog()
    {
        CompilationPipeline.compilationStarted -= Started;
        CompilationPipeline.compilationStarted += Started;
        CompilationPipeline.assemblyCompilationFinished -= AssemblyDone;
        CompilationPipeline.assemblyCompilationFinished += AssemblyDone;
        CompilationPipeline.compilationFinished -= AllDone;
        CompilationPipeline.compilationFinished += AllDone;
    }

    static string Folder => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Compile"));

    static void Started(object context)
    {
        pending.Clear();
        errors = warnings = assemblies = 0;
        started = DateTime.Now;
    }

    static void AssemblyDone(string assembly, CompilerMessage[] messages)
    {
        int e = 0, w = 0;
        foreach (CompilerMessage m in messages)
        {
            if (m.type == CompilerMessageType.Error) e++;
            else if (m.type == CompilerMessageType.Warning) w++;
        }
        assemblies++;
        errors += e;
        warnings += w;
        pending.AppendLine($"{Path.GetFileName(assembly)}: {e} error(s), {w} warning(s)");
        // Errors first, then warnings, each as the compiler wrote it (file(line,column): code: text).
        foreach (CompilerMessage m in messages) if (m.type == CompilerMessageType.Error) pending.AppendLine("  " + m.message);
        foreach (CompilerMessage m in messages) if (m.type == CompilerMessageType.Warning) pending.AppendLine("  " + m.message);
    }

    static void AllDone(object context)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            string text = string.Format(CultureInfo.InvariantCulture, "Compile {0:yyyy-MM-dd HH:mm:ss} to {1:HH:mm:ss}: {2} assemblies, {3} error(s), {4} warning(s)\n",
                started, DateTime.Now, assemblies, errors, warnings) + pending;
            File.WriteAllText(Path.Combine(Folder, "last.txt"), text);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Compile log] Couldn't write Logs/Compile/last.txt: " + e.Message);
        }
    }

    [MenuItem("Fixit Fidget/Checks/Copy the editor's log into Logs (read-only)")]
    static void CopyEditorLog()
    {
        try
        {
            string source = Application.consoleLogPath;
            if (string.IsNullOrEmpty(source) || !File.Exists(source)) throw new FileNotFoundException("The editor's log isn't where Unity says it is: " + source);
            Directory.CreateDirectory(Folder);
            string target = Path.Combine(Folder, "Editor-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture) + ".log");
            // The editor is still writing it: read it shared.
            using (var from = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var to = new FileStream(target, FileMode.CreateNew, FileAccess.Write))
                from.CopyTo(to);
            Debug.Log("[Compile log] The editor's log is copied: " + target);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Compile log] Couldn't copy the editor's log: " + e.Message);
        }
    }
}
#endif
