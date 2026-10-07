using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Small non-interactive HUD primitives. These overlays never intercept input.
// Their colours and lettering are the UI skin's (playtest 3, session 3; UiSkin): the band, its faint writing, the brand's
// light green, gold.
public static class RepairOverlayUI
{
    public static readonly Color Background = UiSkin.Band;
    public static readonly Color Muted = UiSkin.BandFaint;
    public static readonly Color Mint = UiSkin.BrandBright;
    public static readonly Color Amber = UiSkin.Gold;

    public static Canvas Canvas(string name, Transform parent, int order)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        obj.transform.SetParent(parent, false);
        var canvas = obj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = order;
        var scaler = obj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        var group = obj.GetComponent<CanvasGroup>();
        group.interactable = false; group.blocksRaycasts = false;
        return canvas;
    }

    public static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot; rect.anchoredPosition = position; rect.sizeDelta = size;
        return rect;
    }

    public static Image Panel(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        var rect = Rect(name, parent, new Vector2(0, 1), new Vector2(0, 1), position, size);
        var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false;
        return image;
    }

    public static TMP_Text Text(string name, Transform parent, Vector2 position, Vector2 size, float fontSize, Color color)
    {
        var rect = Rect(name, parent, new Vector2(0, 1), new Vector2(0, 1), position, size);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        UiSkin.UseFont(text);
        text.fontSize = fontSize; text.color = color; text.raycastTarget = false;
        text.richText = false; text.overflowMode = TextOverflowModes.Ellipsis;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        return text;
    }
}
