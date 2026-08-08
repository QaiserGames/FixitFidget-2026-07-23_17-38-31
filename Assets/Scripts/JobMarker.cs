using UnityEngine;
using TMPro;

public class JobMarker : MonoBehaviour
{
    [SerializeField] private TMP_Text label;

    private void Awake()
    {
        Hide();
    }

    public void Show(int number, Color color)
    {
        if (label == null) return;
        label.text = $"#{number}";
        label.color = color;
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}