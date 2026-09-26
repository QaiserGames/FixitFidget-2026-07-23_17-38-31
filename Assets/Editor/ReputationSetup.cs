#if UNITY_EDITOR
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// ---------------------------------------------------------------------------
// Adds the café's stars and today's reviews to the end-of-day recap in the open
// scene (claude/reputation-spec.md), and wires the review lines into the
// SaveManager. Run it once per scene. Running it again re-wires, and moves a
// block that is still where an earlier version of this tool put it; a block
// you have moved or restyled yourself is left where it is.
//
//   - Placeholder star sprites, drawn here: a solid star for earned stars
//     (Assets/Art/UI/ReputationStar.png, tinted gold) and its outline for the
//     empty places (ReputationStarEmpty.png), so a café with no stars never
//     reads as five. Swap either for real art any time: both are plain white.
//   - The review lines asset (Assets/Data/Reputation/ReviewLines.asset), made
//     from the draft lines in ReviewLines.cs. The writers edit that asset.
//   - "Reputation" in the recap panel: five star images and one text block, in
//     the middle column between today's numbers (left) and the shop (right),
//     level with the top of the numbers. RecapUI only keeps the references.
//
// Why the middle: the upgrade list grows downward past its own rectangle as
// upgrades are added (six rows already reach the bottom of the screen), so
// nothing can sit under it.
// ---------------------------------------------------------------------------
public static class ReputationSetup
{
    const string StarPath = "Assets/Art/UI/ReputationStar.png";
    const string EmptyStarPath = "Assets/Art/UI/ReputationStarEmpty.png";
    const string LinesPath = "Assets/Data/Reputation/ReviewLines.asset";
    const string BlockName = "Reputation";

    // In the recap canvas's 1920x1080 reference units. Today's numbers hang off
    // the panel's left edge and the shop off its right, so the gap between them
    // is always centred 154 units left of the panel's centre: anchored there,
    // the block stays in the gap at 16:9 and at 16:10.
    static readonly Vector2 BlockPosition = new(-154f, 300f);   // top edge 240 below the top of the screen
    static readonly Vector2 BlockSize = new(640f, 380f);
    const float StarSize = 40f, StarGap = 8f, TextTop = StarSize + 10f, FontSize = 24f;

    // Empty places were a faint solid star at first, which read as five stars
    // on screen. They're an outline now; the old tint is replaced if unchanged.
    static readonly Color FirstEmptyColor = new(1f, 1f, 1f, 0.16f);
    static readonly Color EmptyColor = new(1f, 1f, 1f, 0.35f);

    [MenuItem("Fixit Fidget/Reputation/Add stars and reviews to the recap (open scene)")]
    public static void AddToRecap()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Reputation", "Stop Play Mode first.", "OK");
            return;
        }

        var log = new StringBuilder("[Reputation setup]\n");
        Sprite star = EnsureStarSprite(StarPath, false, log);
        Sprite emptyStar = EnsureStarSprite(EmptyStarPath, true, log);
        ReviewLines lines = EnsureReviewLines(log);

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
        Undo.SetCurrentGroupName("Add reputation to the recap");

        // ---- the block ----
        // Anything this run creates is laid out. So is everything in a block
        // still where the first version put it. Anything else keeps its layout.
        Transform existing = panel.transform.Find(BlockName);
        RectTransform block;
        bool layOutAll;
        if (existing != null)
        {
            block = (RectTransform)existing;
            layOutAll = IsFirstVersionLayout(block);
            log.AppendLine(layOutAll
                ? "Found the Reputation block where the first version put it (under the upgrade list); moving it to the middle column."
                : "Found the existing Reputation block; re-wiring only.");
        }
        else
        {
            var go = new GameObject(BlockName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Add reputation block");
            go.layer = panel.layer;
            block = (RectTransform)go.transform;
            block.SetParent(panel.transform, false);
            layOutAll = true;
            log.AppendLine("Created the Reputation block.");
        }
        if (layOutAll) LayOutBlock(block);

        // ---- five stars in a row ----
        var row = block.Find("Stars") as RectTransform;
        bool layOutRow = layOutAll;
        if (row == null)
        {
            var go = new GameObject("Stars", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Add stars");
            go.layer = panel.layer;
            row = (RectTransform)go.transform;
            row.SetParent(block, false);
            layOutRow = true;
        }
        if (layOutRow) LayOutRow(row);
        var stars = new Image[5];
        for (int i = 0; i < 5; i++)
        {
            Transform child = row.Find("Star " + (i + 1));
            Image image;
            if (child == null)
            {
                var go = new GameObject("Star " + (i + 1), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                Undo.RegisterCreatedObjectUndo(go, "Add star");
                go.layer = panel.layer;
                go.transform.SetParent(row, false);
                image = go.GetComponent<Image>();
                image.color = new Color(1f, 1f, 1f, 0.16f);
                image.raycastTarget = false;
                image.preserveAspect = true;
                LayOutStar((RectTransform)go.transform, i);
            }
            else
            {
                image = child.GetComponent<Image>();
                if (layOutRow) LayOutStar((RectTransform)child, i);
            }
            if (image != null && star != null) image.sprite = star;
            stars[i] = image;
        }

        // ---- the text below them ----
        Transform textChild = block.Find("Text");
        TextMeshProUGUI text;
        if (textChild == null)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            Undo.RegisterCreatedObjectUndo(go, "Add reputation text");
            go.layer = panel.layer;
            go.transform.SetParent(block, false);
            text = go.GetComponent<TextMeshProUGUI>();
            if (mainText != null) text.font = mainText.font;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.richText = true;
            text.raycastTarget = false;
            text.text = "Reputation";
            LayOutText(text);
        }
        else
        {
            text = textChild.GetComponent<TextMeshProUGUI>();
            if (layOutAll && text != null) LayOutText(text);
        }

        if (layOutAll)
            log.AppendLine($"Laid out: {BlockSize.x}x{BlockSize.y} in the middle column, top edge level with today's numbers; stars {StarSize}px; text {FontSize}pt.");

        // ---- wiring ----
        Undo.RecordObject(recap, "Wire reputation");
        recapSo.Update();
        recapSo.FindProperty("reputationText").objectReferenceValue = text;
        SerializedProperty starList = recapSo.FindProperty("reputationStars");
        starList.arraySize = stars.Length;
        for (int i = 0; i < stars.Length; i++) starList.GetArrayElementAtIndex(i).objectReferenceValue = stars[i];
        recapSo.FindProperty("starEarnedSprite").objectReferenceValue = star;
        recapSo.FindProperty("starEmptySprite").objectReferenceValue = emptyStar;
        SerializedProperty emptyColor = recapSo.FindProperty("starEmptyColor");
        if (emptyColor.colorValue == FirstEmptyColor) emptyColor.colorValue = EmptyColor;
        recapSo.ApplyModifiedProperties();
        log.AppendLine("Wired RecapUI: reputation text, five stars, and the earned and empty star sprites.");

        SaveManager save = Object.FindAnyObjectByType<SaveManager>(FindObjectsInactive.Include);
        if (save != null && lines != null)
        {
            var saveSo = new SerializedObject(save);
            saveSo.FindProperty("reviewLines").objectReferenceValue = lines;
            saveSo.ApplyModifiedProperties();
            log.AppendLine("Wired SaveManager: review lines.");
        }
        else log.AppendLine("No SaveManager in this scene (or no review lines): quotes use the built-in draft lines.");

        var scene = recap.gameObject.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        log.AppendLine(saved ? $"Saved {scene.path}." : $"Could not save {scene.path}: save it with Ctrl+S.");
        Debug.Log(log.ToString());
    }

    static void LayOutBlock(RectTransform block)
    {
        Undo.RecordObject(block, "Lay out reputation");
        block.anchorMin = block.anchorMax = new Vector2(0.5f, 0.5f);
        block.pivot = new Vector2(0.5f, 1f);
        block.sizeDelta = BlockSize;
        block.anchoredPosition = BlockPosition;
    }

    static void LayOutRow(RectTransform row)
    {
        Undo.RecordObject(row, "Lay out stars");
        row.anchorMin = row.anchorMax = new Vector2(0f, 1f);
        row.pivot = new Vector2(0f, 1f);
        row.sizeDelta = new Vector2(5 * StarSize + 4 * StarGap, StarSize);
        row.anchoredPosition = Vector2.zero;
    }

    static void LayOutStar(RectTransform star, int index)
    {
        Undo.RecordObject(star, "Lay out star");
        star.anchorMin = star.anchorMax = new Vector2(0f, 0.5f);
        star.pivot = new Vector2(0f, 0.5f);
        star.sizeDelta = new Vector2(StarSize, StarSize);
        star.anchoredPosition = new Vector2(index * (StarSize + StarGap), 0f);
    }

    static void LayOutText(TextMeshProUGUI text)
    {
        var rect = (RectTransform)text.transform;
        Undo.RecordObject(rect, "Lay out reputation text");
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = new Vector2(0f, -TextTop);
        Undo.RecordObject(text, "Lay out reputation text");
        text.fontSize = FontSize;
    }

    // The first version (26 Sept) anchored the block to the recap's right edge,
    // under the upgrade list, which overlaps it once the list is built.
    static bool IsFirstVersionLayout(RectTransform block) =>
        block.anchorMin == new Vector2(1f, 0.5f) && block.anchorMax == new Vector2(1f, 0.5f)
        && Mathf.Abs(block.anchoredPosition.x + 420f) < 0.5f
        && Mathf.Abs(block.sizeDelta.x - 640f) < 0.5f && Mathf.Abs(block.sizeDelta.y - 270f) < 0.5f;

    // A plain white five-pointed star, or its outline, anti-aliased by
    // supersampling. Placeholder art: RecapUI tints earned stars gold.
    static Sprite EnsureStarSprite(string path, bool outline, StringBuilder log)
    {
        if (!File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            const int size = 128, samples = 4;
            const float stroke = 10f;   // outline width in texture pixels (about 3 on screen)
            var points = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float radius = i % 2 == 0 ? 60f : 60f * 0.3819660f;
                float angle = Mathf.PI / 2f + i * Mathf.PI / 5f;
                points[i] = new Vector2(64f + radius * Mathf.Cos(angle), 61f + radius * Mathf.Sin(angle));
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int inside = 0;
                for (int sy = 0; sy < samples; sy++)
                for (int sx = 0; sx < samples; sx++)
                {
                    var p = new Vector2(x + (sx + 0.5f) / samples, y + (sy + 0.5f) / samples);
                    if (Inside(points, p) && (!outline || DistanceToEdge(points, p) <= stroke)) inside++;
                }
                byte alpha = (byte)Mathf.RoundToInt(255f * inside / (samples * samples));
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
            texture.SetPixels32(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            log.AppendLine((outline ? "Drew the placeholder empty star: " : "Drew the placeholder star: ") + path);
        }

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer != null && (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled))
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static float DistanceToEdge(Vector2[] polygon, Vector2 p)
    {
        float best = float.MaxValue;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            Vector2 a = polygon[j], ab = polygon[i] - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            best = Mathf.Min(best, Vector2.Distance(p, a + t * ab));
        }
        return best;
    }

    static bool Inside(Vector2[] polygon, Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            if ((polygon[i].y > p.y) != (polygon[j].y > p.y)
                && p.x < (polygon[j].x - polygon[i].x) * (p.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x)
                inside = !inside;
        return inside;
    }

    static ReviewLines EnsureReviewLines(StringBuilder log)
    {
        var lines = AssetDatabase.LoadAssetAtPath<ReviewLines>(LinesPath);
        if (lines != null) return lines;
        if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
        if (!AssetDatabase.IsValidFolder("Assets/Data/Reputation")) AssetDatabase.CreateFolder("Assets/Data", "Reputation");
        lines = ScriptableObject.CreateInstance<ReviewLines>();
        AssetDatabase.CreateAsset(lines, LinesPath);
        AssetDatabase.SaveAssets();
        log.AppendLine("Created the review lines (draft text): " + LinesPath);
        return lines;
    }
}
#endif
