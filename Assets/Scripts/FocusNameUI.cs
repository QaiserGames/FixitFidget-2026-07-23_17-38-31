using UnityEngine;
using TMPro;

public class FocusNameUI : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private PlayerInteractor interactor;
    [SerializeField] private ItemInspector inspector;
    [SerializeField] private ConversationController conversation;
    [SerializeField] private float fadeSpeed = 8f;

    private void Update()
    {
        string who = interactor != null ? interactor.FocusName : "";

        bool busy = inspector != null && inspector.IsHoldingItem;
        // The panel already shows who we're talking to — no need to say it twice.
        bool talking = conversation != null && conversation.InConversation;

        bool show = !busy && !talking && !string.IsNullOrEmpty(who);

        if (show) label.text = who;

        // Unscaled: the recap pauses the game (timeScale 0). Scaled time froze
        // the fade, so resuming a closed day left the label's placeholder
        // ("New Text") showing in the middle of the recap.
        group.alpha = Mathf.MoveTowards(group.alpha, show ? 1f : 0f, fadeSpeed * Time.unscaledDeltaTime);
    }
}