using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// 15 Oct: the labs that read the screen (Bench 6's words, run at 1920 x 1080 and at his Mac's 2560 x 1600) need the Game view at
// a set size. Unity has no public way to choose it, so these reach the Game view's own size list (GameViewSizes, internal),
// add the size if it isn't there, and select it. If Unity changes its insides, the step says so and the size is picked by hand
// in the Game view's size menu.
static class GameViewSizeMenu
{
    const string Menu = "Fixit Fidget/Checks/";

    [MenuItem(Menu + "Game view - 1920 x 1080 (the labs)")] static void FullHd() => Set(1920, 1080);
    [MenuItem(Menu + "Game view - 2560 x 1600 (his Mac, 16:10)")] static void Mac() => Set(2560, 1600);
    [MenuItem(Menu + "Game view - 2560 x 1440")] static void Qhd() => Set(2560, 1440);

    internal static bool Set(int w, int h)
    {
        try
        {
            Assembly editor = typeof(Editor).Assembly;
            Type sizesType = editor.GetType("UnityEditor.GameViewSizes", true);
            Type single = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            object sizes = single.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            object group = sizesType.GetProperty("currentGroup", BindingFlags.Public | BindingFlags.Instance).GetValue(sizes);
            Type groupType = group.GetType();
            MethodInfo count = groupType.GetMethod("GetTotalCount"), get = groupType.GetMethod("GetGameViewSize");
            Type sizeType = editor.GetType("UnityEditor.GameViewSize", true);
            PropertyInfo width = sizeType.GetProperty("width"), height = sizeType.GetProperty("height"), kind = sizeType.GetProperty("sizeType");
            int total = (int)count.Invoke(group, null), index = -1;
            for (int i = 0; i < total; i++)
            {
                object s = get.Invoke(group, new object[] { i });
                if ((int)width.GetValue(s) == w && (int)height.GetValue(s) == h && kind.GetValue(s).ToString() == "FixedResolution") { index = i; break; }
            }
            bool added = false;
            if (index < 0)
            {
                Type kindType = editor.GetType("UnityEditor.GameViewSizeType", true);
                object made = Activator.CreateInstance(sizeType, Enum.Parse(kindType, "FixedResolution"), w, h, $"{w} x {h} (labs)");
                groupType.GetMethod("AddCustomSize").Invoke(group, new[] { made });
                index = (int)count.Invoke(group, null) - 1;
                added = true;
            }
            Type viewType = editor.GetType("UnityEditor.GameView", true);
            EditorWindow view = EditorWindow.GetWindow(viewType);
            PropertyInfo selected = viewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            selected.SetValue(view, index);
            view.Repaint();
            Debug.Log($"[Game view] {w} x {h}{(added ? " (added to the size list)" : "")}: selected. The labs read Screen.width and height from it.");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Game view] Couldn't set {w} x {h} ({(e.InnerException ?? e).Message}): pick it in the Game view's size menu by hand.");
            return false;
        }
    }
}
