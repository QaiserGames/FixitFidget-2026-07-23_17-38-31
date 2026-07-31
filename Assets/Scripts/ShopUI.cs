using UnityEngine;
using TMPro;

public class ShopUI : MonoBehaviour
{
    [SerializeField] private TMP_Text moneyText;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private GameObject crosshair;
    [SerializeField] private PlayerInteractor interactor;
    [SerializeField] private bool showDebug = true;
    [SerializeField] private ItemInspector inspector;

    private void Update()
    {
        bool showCrosshair = interactor.IsAtStation && (inspector == null || !inspector.IsHoldingItem);
        if (crosshair != null) crosshair.SetActive(showCrosshair);

        if (ShopEconomy.Instance != null)
            moneyText.text = $"${ShopEconomy.Instance.Money}";

        string prompt = interactor.CurrentPrompt;
        string line = string.IsNullOrEmpty(prompt) ? "" : $"[E]  {prompt}";

        if (interactor.IsAtStation)
            line += (line.Length > 0 ? "        " : "") + "[Esc]  Step back";

        if (showDebug)
            line += "\n" + interactor.DebugInfo;

        promptText.text = line;
    }
}