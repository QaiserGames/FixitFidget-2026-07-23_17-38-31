using UnityEngine;
using TMPro;

public class JobMarker : MonoBehaviour
{
    [SerializeField] private TMP_Text label;

    private void Awake()
    {
        Hide();
    }

    // Numbered version — used on items.
    public void Show(int number, Color color)
    {
        ShowText($"#{number}", color);
    }

    // Named version — used on people.
    public void ShowText(string text, Color color)
    {
        if (label == null) return;
        label.text = text;
        label.color = color;
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}