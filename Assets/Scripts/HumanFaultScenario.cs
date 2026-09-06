using UnityEngine;

[CreateAssetMenu(menuName = "Fixit Fidget/Human fault scenario")]
public sealed class HumanFaultScenario : ScriptableObject
{
    public HumanDialogueStep[] steps;
    [TextArea(2, 5)] public string completionLine;
    public HumanConversationRun CreateRun() => new HumanConversationRun(steps, completionLine);
}
