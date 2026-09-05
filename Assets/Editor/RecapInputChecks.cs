#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Unity.Cinemachine;

// Runs Update methods explicitly on inactive temporary objects. Awake/Start,
// real input devices, customer cleanup, and disk writes are never invoked.
public static class RecapInputChecks
{
    [MenuItem("Fixit Fidget/Checks/Recap input isolation")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before running recap-input checks.");

        DayClock previousClock = DayClock.Instance;
        SaveManager previousSave = SaveManager.Instance;
        float previousScale = Time.timeScale;
        CursorLockMode previousLock = Cursor.lockState;
        bool previousCursor = Cursor.visible;
        var root = new GameObject("RecapInputCheck") { hideFlags = HideFlags.HideAndDontSave };
        root.SetActive(false);
        try
        {
            var clock = root.AddComponent<DayClock>();
            SetStatic(typeof(DayClock), "Instance", clock);
            clock.SetDay(5);
            int endedEvents = 0;
            clock.OnDayEnded += () => endedEvents++;
            clock.RestoreRecap(new RecapSaveData { day = 5, earned = 120, closingTill = 300 });
            Require(clock.DayOver && !clock.IsOpen && clock.TimeRemaining == 0f && Time.timeScale == 0f && endedEvents == 0,
                "Restoring a recap freezes the day without replaying EndDay.");

            CheckFrozenOverlays(root.transform);
            CheckRepairAndMovement(root.transform);
            CheckCameraAndFailedContinue(root.transform, clock);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            SetStatic(typeof(DayClock), "Instance", previousClock);
            SetStatic(typeof(SaveManager), "Instance", previousSave);
            Time.timeScale = previousScale;
            Cursor.lockState = previousLock;
            Cursor.visible = previousCursor;
        }

        Debug.Log("[Recap input] PASS: restored-day pause, frozen-overlay cleanup, repair release, " +
                  "movement reset, camera suspension/restoration, and blocked Continue. " +
                  "Still test mouse/camera behaviour in Play Mode using recap-input-checklist.md.");
    }

    private static void CheckFrozenOverlays(Transform parent)
    {
        GameObject tooltipObject = Child(parent, "Tooltip", typeof(RectTransform), typeof(CanvasGroup));
        var tooltip = tooltipObject.AddComponent<HoverTooltipUI>();
        CanvasGroup tooltipGroup = tooltipObject.GetComponent<CanvasGroup>();
        var rect = tooltipObject.GetComponent<RectTransform>();
        Set(tooltip, "panel", rect);
        Set(tooltip, "group", tooltipGroup);
        Set(tooltip, "lastTitle", "Brush");
        Set(tooltip, "hoverTime", 10f);
        tooltipGroup.alpha = 1f;
        tooltipGroup.blocksRaycasts = true;
        rect.position = new Vector3(100f, 200f, 0f);
        Vector3 oldPosition = rect.position;
        Invoke(tooltip, "Update");
        Require(tooltipGroup.alpha == 0f && !tooltipGroup.blocksRaycasts && rect.position == oldPosition,
            "A visible tooltip clears immediately while paused and stops following the cursor.");
        Require((string)Get(tooltip, "lastTitle") == "" && (float)Get(tooltip, "hoverTime") == 0f,
            "Tomorrow cannot inherit yesterday's hover delay or target.");

        GameObject dialogueObject = Child(parent, "Conversation", typeof(RectTransform), typeof(CanvasGroup));
        var ui = dialogueObject.AddComponent<ConversationUI>();
        CanvasGroup dialogueGroup = dialogueObject.GetComponent<CanvasGroup>();
        Set(ui, "group", dialogueGroup);
        Set(ui, "visible", true);
        dialogueGroup.alpha = 1f;
        dialogueGroup.blocksRaycasts = true;
        Invoke(ui, "Update");
        Require(dialogueGroup.alpha == 0f && !dialogueGroup.blocksRaycasts && ui.LineFinished,
            "Conversation UI also clears without waiting on a scaled fade.");

        var controller = Child(parent, "Conversation controller").AddComponent<ConversationController>();
        Set(controller, "ui", ui);
        Set(controller, "closing", true);
        Invoke(controller, "Update");
        Require(!(bool)Get(controller, "closing") && !controller.InConversation,
            "Recap cancels conversation input before reading any decision keys.");
    }

    private static void CheckRepairAndMovement(Transform parent)
    {
        var inspector = Child(parent, "Inspector").AddComponent<ItemInspector>();
        var item = Child(parent, "Repair").AddComponent<RepairJob>();
        var camera = Child(parent, "Inspect camera").AddComponent<CinemachineCamera>();
        Set(inspector, "focusedItem", item);
        Set(inspector, "restPosition", new Vector3(3f, 2f, 1f));
        Set(inspector, "restRotation", Quaternion.identity);
        Set(inspector, "inspectCam", camera);
        Set(inspector, "currentTool", ToolType.Brush);
        item.transform.position = new Vector3(20f, 20f, 20f);
        camera.Priority = 30;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Invoke(inspector, "Update");
        Require(!inspector.IsHoldingItem && inspector.CurrentTool == ToolType.Hand && camera.Priority == 0
                && item.transform.position == new Vector3(3f, 2f, 1f),
            "Paused inspection restores the item and clears its camera/tool before any mouse handling.");
        Require(Cursor.visible && Cursor.lockState == CursorLockMode.None, "Cancelling inspection preserves the recap cursor.");

        var movement = Child(parent, "Movement").AddComponent<PlayerMovement>();
        Set(movement, "moveInput", Vector2.one);
        Invoke(movement, "Update");
        Require((Vector2)Get(movement, "moveInput") == Vector2.zero, "Paused movement consumes no cached movement from gameplay.");
    }

    private static void CheckCameraAndFailedContinue(Transform parent, DayClock clock)
    {
        var recap = Child(parent, "Recap").AddComponent<RecapUI>();
        var enabledReader = Child(parent, "Enabled camera reader").AddComponent<CinemachineInputAxisController>();
        var disabledReader = Child(parent, "Disabled camera reader").AddComponent<CinemachineInputAxisController>();
        disabledReader.enabled = false;
        var readers = new[] { enabledReader, disabledReader };
        Invoke(recap, "SuspendCameraInput", (object)readers);
        Invoke(recap, "SuspendCameraInput", (object)readers);
        Require(!enabledReader.enabled && !disabledReader.enabled, "Repeated recap opening keeps camera readers suspended.");

        // Reject before the storage call; this fixture has no permission to
        // write the player's save and never attempts a successful transition.
        var save = Child(parent, "Blocked save").AddComponent<SaveManager>();
        Set(save, "writesBlocked", true);
        SetStatic(typeof(SaveManager), "Instance", save);
        GameObject panel = Child(parent, "Recap panel");
        panel.SetActive(true);
        Set(recap, "panel", panel);
        Invoke(recap, "OnNextDay");
        Require(clock.DayOver && clock.Day == 5 && Time.timeScale == 0f && panel.activeSelf && !enabledReader.enabled,
            "A failed Continue cannot close the recap, advance the day, or restore gameplay input.");

        Invoke(recap, "ResumeCameraInput");
        Require(enabledReader.enabled && !disabledReader.enabled, "Resuming restores only camera readers suspended by the recap.");
    }

    private static GameObject Child(Transform parent, string name, params Type[] components)
    {
        var child = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
        child.SetActive(false);
        child.transform.SetParent(parent, false);
        foreach (Type type in components) child.AddComponent(type);
        return child;
    }

    private static object Get(object target, string field) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void SetStatic(Type type, string property, object value) =>
        type.GetProperty(property, BindingFlags.Public | BindingFlags.Static).GetSetMethod(true).Invoke(null, new[] { value });
    private static void Invoke(object target, string method, params object[] arguments) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("[Recap input] FAIL: " + message);
    }
}
#endif
