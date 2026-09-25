using UnityEngine;

/// <summary>
/// Runs PadInput's once-per-frame device tracking before any gameplay script
/// reads input, so every script agrees on whether a controller is in use.
/// Added at runtime by PadInput; never saved into a scene.
/// </summary>
[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public sealed class PadInputDriver : MonoBehaviour
{
    private void Update() => PadInput.Track();
}
