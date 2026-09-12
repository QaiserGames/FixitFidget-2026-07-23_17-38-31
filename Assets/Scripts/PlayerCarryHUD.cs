using UnityEngine;

// Kept as a migration target for scenes that serialized the previous overlay.
// Carrying is communicated by the physical left and right hands now.
[DisallowMultipleComponent]
public sealed class PlayerCarryHUD : MonoBehaviour
{
    private void Awake() => Retire();
    private void OnEnable() => Retire();
    public void Retire()
    {
        Transform old = transform.Find("Carried items");
        if (old != null)
        {
            old.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(old.gameObject);
        }
        enabled = false;
    }
}
