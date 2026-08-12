using UnityEngine;

[CreateAssetMenu(fileName = "Regular_", menuName = "FixitFiasco/Customer Profile")]
public class CustomerProfile : ScriptableObject
{
    [Header("Identity")]
    public string characterName = "Alex";
    [TextArea(2, 4)] public string bio;
    public Color themeColor = Color.white;

    [Header("Portraits (regulars only — wired later)")]
    public Sprite portraitNeutral;
    public Sprite portraitHappy;
    public Sprite portraitAnnoyed;
    public Sprite portraitSad;

    [Header("Behaviour")]
    public float patienceMultiplier = 1f;
    public float tipMultiplier = 1f;

    [Header("What they usually bring")]
    [Tooltip("Their signature device. Left empty = fully random.")]
    public GameObject preferredDevice;

    [Range(0f, 1f)]
    [Tooltip("Chance they bring their signature device. The rest of the time it's a surprise.")]
    public float preferredDeviceChance = 0.7f;

    [Header("Dialogue")]
    public DialogueSet lines;

    [Header("Once we know each other (relationship 2+, wired with memory later)")]
    public DialogueSet warmLines;
}