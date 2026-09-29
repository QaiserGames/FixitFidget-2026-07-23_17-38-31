using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// ---------------------------------------------------------------------------
// THE DIALOGUE PASS: its one scene step, and its play check (claude/dialogue-skyrim-proposal.md)
//
//   * Walk-in lines: the walk-ins' lines live on the café scene's CustomerSpawner. Five requests started a
//     sentence with {fault} or {device} ("Morning! jammed gears on my pocket watch."): they are fixed,
//     and each personality gets two drink orders of its own (until now everyone who came in only for a
//     drink used the same two placeholder lines). A line is only changed while it still reads as it did,
//     and drink orders are only added where there are none, so nothing written since is overwritten.
//     PLACEHOLDER COPY: Mansoor rewrites it.
//   * Conversation play check: a fresh Day 1 in the lab (the lab's own save; the playtest save is never
//     used) that drives Grace's conversation and a walk-in's by itself (ConversationCheck).
// ---------------------------------------------------------------------------
public static class DialoguePassSteps
{
    const string Menu = "Fixit Fidget/Dialogue/";
    const string Tag = "[Dialogue pass] ";

    static readonly (string personality, string was, string now)[] IntakeFixes =
    {
        ("Cheerful", "Morning! {fault} on my {device}. Can you look?", "Morning! My {device} — {fault}. Can you look?"),
        ("Impatient", "{device}. {fault}. How long?", "My {device}: {fault}. How long?"),
        ("Chatty", "So — my {device}. {fault}. Long story.", "So — my {device}, {fault}. Long story."),
        ("Chatty", "You'll laugh. {device}, {fault}.", "You'll laugh. My {device}: {fault}."),
        ("Rushed", "I know you're busy. {fault}. Please.", "I know you're busy. My {device}: {fault}. Please."),
    };

    static readonly (string personality, string[] orders)[] DrinkOrders =
    {
        ("Cheerful", new[] { "Hi! Could I get {a drink}?", "Ooh, {a drink}, please!" }),
        ("Impatient", new[] { "Just {a drink}. Quick.", "I'll take {a drink}. Now, ideally." }),
        ("Chatty", new[] { "Oh, {a drink} would be lovely.", "I'll have {a drink}, I think. Yes!" }),
        ("Rushed", new[] { "Could I get {a drink} to go?", "Sorry — {a drink}, quick as you can?" }),
        ("Sentimental", new[] { "Could I have {a drink}, please?", "I'd love {a drink}, if it's no trouble." }),
    };

    [MenuItem(Menu + "Walk-in lines - fix five requests and add drink orders (Edit Mode)")]
    static void ApplyWalkInLines()
    {
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != AcesCafeLayoutSetup.ScenePath)
                throw new InvalidOperationException("Open the café scene (" + AcesCafeLayoutSetup.ScenePath + ") first.");
            if (scene.isDirty)
                throw new InvalidOperationException("The café scene has unsaved changes. Save or revert them first, so this step saves only its own.");
            CustomerSpawner spawner = UnityEngine.Object.FindAnyObjectByType<CustomerSpawner>(FindObjectsInactive.Include)
                                      ?? throw new InvalidOperationException("The café scene has no CustomerSpawner.");

            var serialized = new SerializedObject(spawner);
            SerializedProperty archetypes = serialized.FindProperty("archetypes");
            if (archetypes == null || !archetypes.isArray) throw new InvalidOperationException("CustomerSpawner.archetypes is missing.");
            var report = new StringBuilder();
            int changes = 0;
            for (int i = 0; i < archetypes.arraySize; i++)
            {
                SerializedProperty archetype = archetypes.GetArrayElementAtIndex(i);
                string name = archetype.FindPropertyRelative("archetypeName").stringValue;
                SerializedProperty lines = archetype.FindPropertyRelative("lines");
                SerializedProperty intake = lines.FindPropertyRelative("intake");
                foreach (var fix in IntakeFixes.Where(f => f.personality == name))
                    for (int j = 0; j < intake.arraySize; j++)
                    {
                        SerializedProperty line = intake.GetArrayElementAtIndex(j);
                        if (line.stringValue != fix.was) continue;
                        line.stringValue = fix.now;
                        changes++;
                        report.AppendLine($"  {name}: \"{fix.was}\" -> \"{fix.now}\"");
                    }
                SerializedProperty orders = lines.FindPropertyRelative("drinkOrder");
                string[] mine = DrinkOrders.FirstOrDefault(d => d.personality == name).orders;
                if (orders == null)
                    throw new InvalidOperationException("DialogueSet.drinkOrder is missing: let Unity finish compiling, then run this again.");
                bool hasAny = false;
                for (int j = 0; j < orders.arraySize; j++) hasAny |= !string.IsNullOrWhiteSpace(orders.GetArrayElementAtIndex(j).stringValue);
                if (mine == null || hasAny)
                {
                    if (hasAny) report.AppendLine($"  {name}: already has drink orders of its own (left as they are)");
                    continue;
                }
                orders.arraySize = mine.Length;
                for (int j = 0; j < mine.Length; j++) orders.GetArrayElementAtIndex(j).stringValue = mine[j];
                changes++;
                report.AppendLine($"  {name}: drink orders \"{string.Join("\", \"", mine)}\"");
            }
            if (changes == 0)
            {
                Debug.Log(Tag + "Nothing to change: the walk-in lines are already up to date.\n" + report);
                return;
            }
            serialized.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("The scene could not be saved.");
            Debug.Log(Tag + $"Walk-in lines: {changes} change(s), and the café scene is saved.\n" + report +
                      "Next: Fixit Fidget > Checks > Dialogue rules.");
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "Walk-in lines: " + e.Message);
        }
    }

    [MenuItem(Menu + "Conversation - play check (lab, drives itself)")]
    static void PlayConversationCheck()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (SceneManager.GetActiveScene().path != AcesCafeLayoutSetup.ScenePath)
        {
            Debug.LogError(Tag + "Open the café scene (" + AcesCafeLayoutSetup.ScenePath + ") first.");
            return;
        }
        string path = Path.Combine(Application.persistentDataPath, CafeLab.SaveFileName);
        SaveCheckpointStorage.Write(path, new SaveData());   // a fresh game: Day 1's morning, in the lab's own save
        PlayerPrefs.SetInt(CafeLab.PendingKey, 1);
        PlayerPrefs.SetInt(CafeLab.AutopilotKey, 0);
        PlayerPrefs.SetInt(ConversationCheck.PendingKey, 1);
        PlayerPrefs.Save();
        Debug.Log(Tag + "Conversation play check: a fresh Day 1 in the lab. It talks to Grace (her request, Ace's replies, " +
                  "asking about her plans, taking the camera) and to the next walk-in (turning them away), by itself, in " +
                  "about two minutes. Keep the Game view in front and leave the mouse and keyboard alone. " +
                  $"Test save {path}; your playtest save is not used.");
        EditorApplication.EnterPlaymode();
    }

    [MenuItem(Menu + "Conversation - play check (lab, drives itself)", true)]
    static bool CanPlay() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // A check request that never became a Play session must not make the next lab session a check.
    [InitializeOnLoadMethod]
    static void ClearStaleCheckRequest()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode && PlayerPrefs.HasKey(ConversationCheck.PendingKey))
        {
            PlayerPrefs.DeleteKey(ConversationCheck.PendingKey);
            PlayerPrefs.Save();
        }
    }
}
