#if UNITY_EDITOR
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// ---------------------------------------------------------------------------
// Ace's notebook in the end-of-day recap (claude/night-notebook-spec.md §4).
//
//   Add the notebook to the recap (open scene): one text block, "Notebook", in
//   the recap panel's middle column under the reputation block. RecapUI only
//   keeps the reference and hides the block on days nothing was learned. Run it
//   once per scene; running it again re-wires, and a block you have moved or
//   restyled yourself is left where it is. A block still in the first layout
//   (230 tall, fixed 22pt: a long day's last lines were cut off) is updated.
//
//   Preview a notebook day in the recap (Play mode, recap open): fills the
//   block with a made-up day - Grace's four facts plus two other regulars with
//   the longest device names in the game - so the layout can be judged without
//   playing to Grace's visit. Nothing is saved; leaving Play mode clears it.
// ---------------------------------------------------------------------------
public static class NotebookSetup
{
    const string BlockName = "Notebook";

    // Recap canvas reference units (1920x1080), the same column as the
    // reputation block (ReputationSetup: centre -154, top 300, 640x380), from
    // 20 under it down to 20 above the Open Tomorrow button (anchored to the
    // bottom at y 90, 80 tall, so its top edge is 410 below the centre).
    static readonly Vector2 BlockPosition = new(-154f, -100f);
    static readonly Vector2 BlockSize = new(640f, 290f);
    // The reviews' size; on a long day (five facts that all wrap) the text
    // shrinks to fit, down to MinFontSize, instead of losing its last lines.
    const float FontSize = 24f, MinFontSize = 16f;

    [MenuItem("Fixit Fidget/Night/Add the notebook to the recap (open scene)")]
    public static void AddToRecap()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Notebook", "Stop Play Mode first.", "OK");
            return;
        }

        var log = new StringBuilder("[Notebook setup]\n");
        RecapUI recap = Object.FindAnyObjectByType<RecapUI>(FindObjectsInactive.Include);
        if (recap == null)
        {
            Debug.LogError(log + "No RecapUI in the open scene. Nothing changed in the scene.");
            return;
        }
        var recapSo = new SerializedObject(recap);
        var panel = recapSo.FindProperty("panel").objectReferenceValue as GameObject;
        var mainText = recapSo.FindProperty("text").objectReferenceValue as TMP_Text;
        if (panel == null)
        {
            Debug.LogError(log + "RecapUI has no panel assigned. Nothing changed in the scene.");
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Add the notebook to the recap");

        Transform existing = panel.transform.Find(BlockName);
        TextMeshProUGUI text;
        if (existing != null)
        {
            text = existing.GetComponent<TextMeshProUGUI>();
            if (text != null && IsFirstVersionLayout((RectTransform)existing, text))
            {
                Undo.RecordObject(existing, "Lay out the notebook block");
                Undo.RecordObject(text, "Lay out the notebook block");
                LayOut((RectTransform)existing, text);
                log.AppendLine($"Found the Notebook block in its first layout; now {BlockSize.x}x{BlockSize.y}, {FontSize}pt shrinking to {MinFontSize}pt on a long day.");
            }
            else log.AppendLine("Found the existing Notebook block; re-wiring only.");
        }
        else
        {
            var go = new GameObject(BlockName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            Undo.RegisterCreatedObjectUndo(go, "Add notebook block");
            go.layer = panel.layer;
            go.transform.SetParent(panel.transform, false);
            text = go.GetComponent<TextMeshProUGUI>();
            if (mainText != null) text.font = mainText.font;
            text.color = Color.white;
            text.richText = true;
            text.raycastTarget = false;
            text.text = "Notebook";
            LayOut((RectTransform)go.transform, text);
            go.SetActive(false);   // RecapUI shows it on days something was learned
            log.AppendLine($"Created the Notebook block: {BlockSize.x}x{BlockSize.y}, under the reputation block, {FontSize}pt shrinking to {MinFontSize}pt on a long day.");
        }
        if (text == null)
        {
            Debug.LogError(log + "The Notebook object has no TextMeshProUGUI. Nothing wired.");
            return;
        }

        Undo.RecordObject(recap, "Wire the notebook");
        recapSo.Update();
        recapSo.FindProperty("notebookText").objectReferenceValue = text;
        recapSo.ApplyModifiedProperties();
        log.AppendLine("Wired RecapUI: notebook text.");

        var scene = recap.gameObject.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        log.AppendLine(saved ? $"Saved {scene.path}." : $"Could not save {scene.path}: save it with Ctrl+S.");
        Debug.Log(log.ToString());
    }

    static void LayOut(RectTransform rect, TextMeshProUGUI text)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = BlockSize;
        rect.anchoredPosition = BlockPosition;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;   // only if even the smallest size cannot fit
        text.fontSize = FontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = MinFontSize;
        text.fontSizeMax = FontSize;
    }

    // What the first version of this menu made (27 Sept, same day).
    static bool IsFirstVersionLayout(RectTransform rect, TMP_Text text) =>
        rect.anchorMin == new Vector2(0.5f, 0.5f) && rect.anchorMax == new Vector2(0.5f, 0.5f)
        && Mathf.Abs(rect.anchoredPosition.x - BlockPosition.x) < 0.5f && Mathf.Abs(rect.anchoredPosition.y - BlockPosition.y) < 0.5f
        && Mathf.Abs(rect.sizeDelta.x - 640f) < 0.5f && Mathf.Abs(rect.sizeDelta.y - 230f) < 0.5f
        && Mathf.Abs(text.fontSize - 22f) < 0.01f && !text.enableAutoSizing;

    [MenuItem("Fixit Fidget/Night/Preview a notebook day in the recap (Play mode, recap open)")]
    static void Preview()
    {
        RecapUI recap = Object.FindAnyObjectByType<RecapUI>(FindObjectsInactive.Include);
        if (recap == null) return;
        var text = new SerializedObject(recap).FindProperty("notebookText").objectReferenceValue as TMP_Text;
        if (text == null)
        {
            Debug.LogWarning("[Notebook preview] The recap has no notebook block. Run Fixit Fidget > Night > Add the notebook to the recap first.");
            return;
        }
        var day = new Notebook();
        foreach (NotebookFactData fact in NotebookEntries.GraceIntake("Grace")) day.Learn(fact, 1);
        day.Learn(NotebookEntries.RegularRepair("tomas", "Tomas", "reunion film camera", "sticky shutter, dirty lens and film path"), 1);
        day.Learn(NotebookEntries.RegularRepair("priya", "Priya", "Pocket Watch", "stopped"), 1);
        text.text = NotebookRecap.Build(day, 1);
        text.gameObject.SetActive(true);
        Debug.Log("[Notebook preview] Showing a made-up day with six new facts (five listed, \"+1 more\"). Nothing saved.");
    }

    [MenuItem("Fixit Fidget/Night/Preview a notebook day in the recap (Play mode, recap open)", true)]
    static bool CanPreview() => EditorApplication.isPlaying;
}
#endif
