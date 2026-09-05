using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class HoverTooltipUI : MonoBehaviour
{
    [SerializeField] private RectTransform panel;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text actionText;
    [SerializeField] private ItemInspector inspector;
    [SerializeField] private PlayerInteractor interactor;
    [SerializeField] private Vector2 cursorOffset = new Vector2(24f, -12f);

    [Tooltip("Hover must settle this long before the tooltip appears — stops strobing.")]
    [SerializeField] private float appearDelay = 0.12f;
    [SerializeField] private float fadeSpeed = 12f;

    private string lastTitle = "";
    private float hoverTime;

    private void Update()
    {
        // Update and raw mouse input still run at timeScale 0. A scaled fade
        // cannot clear yesterday's tooltip once the recap has paused the day.
        if (DayClock.Instance != null && DayClock.Instance.DayOver)
        {
            HideImmediately();
            return;
        }

        string title = "";
        string action = "";

        if (inspector != null && inspector.IsHoldingItem)
        {
            title = inspector.HoverName;
            action = inspector.HoverAction;
        }
        else if (interactor != null && interactor.IsAtStation)
        {
            action = interactor.CurrentPrompt;
            title = string.IsNullOrEmpty(action) ? "" : "Customer";
        }

        bool wantsShow = !string.IsNullOrEmpty(title) || !string.IsNullOrEmpty(action);

        // Reset the settle timer whenever the target changes.
        if (title != lastTitle)
        {
            lastTitle = title;
            hoverTime = 0f;
        }

        hoverTime += Time.deltaTime;
        bool show = wantsShow && hoverTime >= appearDelay;

        // Fade rather than snap.
        float target = show ? 1f : 0f;
        group.alpha = Mathf.MoveTowards(group.alpha, target, fadeSpeed * Time.deltaTime);
        group.blocksRaycasts = false;   // never intercept clicks

        if (group.alpha <= 0.01f) return;

        if (show)
        {
            nameText.text = title;
            actionText.text = action;
        }

        Vector2 screenPos = Cursor.visible && Mouse.current != null
            ? Mouse.current.position.ReadValue()
            : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f - 60f);

        panel.position = screenPos + cursorOffset;
    }

    public void HideImmediately()
    {
        lastTitle = "";
        hoverTime = 0f;
        if (group != null)
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
        }
        if (nameText != null) nameText.text = "";
        if (actionText != null) actionText.text = "";
    }
}
